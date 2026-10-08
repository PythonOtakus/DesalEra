# UnityCLI 使用指南

## 为什么用它

它是目前唯一能让 agent **在运行中的 Unity 编辑器里执行任意 C# 且不需重编译**的工具。

替代方案（批处理 `-executeMethod`）的局限：

- 每次改代码都要重启编辑器，20-40 秒
- 不能交互式地"改一个数，立刻看结果"
- 只能跑事先写好的固定方法

对"调玩法参数、找 bug"这类工作，这个差别是决定性的。

## 安装

**位置**：`C:\Users\Anantian\tools\unity-cli\unity-cli.exe`（v0.4.1，单二进制，无依赖）

Unity 侧插件已加进 `Packages/manifest.json`：

```json
"com.youngwoocho02.unity-cli-connector": "https://github.com/youngwoocho02/unity-cli.git?path=unity-connector#v0.4.1"
```

## 四个必须知道的限制

### 1. 窗口失焦会让编辑器停止响应

Unity 窗口不在前台时，编辑器会限流（throttling），Unity API 派发在主线程上被推迟。结果是 CLI 报：

```
Error: timed out sending command to Unity: cannot reach Unity health endpoint
```

端口仍在监听、实例文件仍在更新，但 HTTP 请求超时。

**两个解决办法**：

- 定期把窗口拉到前台（下面有脚本）
- 或者在 `Edit → Preferences → General → Interaction Mode` 设成 **No Throttling**

本项目用的是第一种，因为它不需要 GUI 操作。

**前台锁的坑**：Windows 不允许"最近没有用户输入"的进程抢前台，此时 `SetForegroundWindow`
会静默失败，只让任务栏闪烁，Unity 依旧被限流。解法是先用 `keybd_event` 模拟按放一次 Alt
（`0x12`，按下后 flags=2 抬起），再调用 `SetForegroundWindow`，之后用 `GetForegroundWindow()`
核对是否真的到了前台。下面的脚本已包含这一步。

### 2. 进/出 Play 模式会触发域重载

此刻 HTTP listener 会短暂消失，`exec` 会报 `no Unity instances running`。等 10-20 秒后恢复。

`status` 读的是磁盘上的心跳文件，所以它可能显示 `playing` 而 `exec` 仍失败——两者不一致就说明 listener 还没起来。

### 3. 场景必须在 Build Settings 里

Play 模式加载的是 Build Settings 的首个场景，不是"最近打开的"。用 `-executeMethod` 构建 exe 时可以手动传场景数组绕过，但编辑器里 Play 不行。

本项目已把 `Main.unity` 设为唯一构建场景。

### 4. 测试中途夭折会锁死整个 connector（只有域重载能解）

**现象**：`status` 正常（读的是心跳文件），但 `exec` / `console` / `editor stop` / `test` 全部超时，等多久都不恢复。2026-10-08 多次"Unity 断连"都是它，不是失焦。

**根因**（connector 0.4.1 的 bug）：

- `CommandRouter.Dispatch` 用一个全局 `SemaphoreSlim(1,1)` 串行执行所有命令。
- `test --mode EditMode` 的处理函数只在 `RunFinished` 回调里结束。测试框架夭折时走的是 `RunFailed`（`IErrorCallbacks.OnError`），connector 没实现，于是这条命令永远不返回，锁永远不释放。
- 最常见的触发：**编辑器处于 Play 模式时跑 EditMode 测试**。测试框架在 `SaveModiedSceneTask` 抛 `InvalidOperationException: This cannot be used during play mode`。录完一段会话忘了停 Play，再让 agent 跑测试，就会中招。
- 退出 Play 模式不会释放锁，要靠域重载重建静态字段。

**本项目的防护**（`Assets/Scripts/Editor/Cli/TestRunGuard.cs`）：

- 自动：注册 `IErrorCallbacks`，测试夭折时自动退出 Play 并 `RequestScriptReload()`。CLI 在连接断开后会重发请求，测试会在编辑模式下正常跑完。已实测：Play 中发 `test`，约 30 秒后返回 12/12 通过。
- 手动：如果测试不报错只是卡住（例如编辑器失焦被限流），把 Unity 拉到前台后按 **Ctrl+Alt+R**（菜单 DesalEra → Reload Scripts）：先退出 Play，再重载脚本，connector 随即恢复，不必重启 Unity。

**操作习惯**：跑 `test` 前先 `status`，如果是 `playing` 就先 `editor stop`。

## 标准操作流程

```powershell
$cli = "C:\Users\Anantian\tools\unity-cli\unity-cli.exe"

# 0. 启动编辑器并保持前台
$unity = "C:\Program Files\Unity\Hub\Editor\2022.3.62f3c1\Editor\Unity.exe"
$proj  = "C:\Users\Anantian\source\repos\DesalEra"
$p = Start-Process $unity -ArgumentList "-projectPath","`"$proj`"","-logFile","`"$env:TEMP\editor.log`"" -PassThru
Start-Sleep -Seconds 60

# 1. 把窗口拉到前台（否则下面全部超时）
Add-Type @"
using System; using System.Runtime.InteropServices;
public class U {
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int n);
  [DllImport("user32.dll")] public static extern bool BringWindowToTop(IntPtr h);
  [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
}
"@
$h = (Get-Process -Id $p.Id).MainWindowHandle
[U]::ShowWindow($h,9)|Out-Null
[U]::keybd_event(0x12,0,0,[UIntPtr]::Zero); [U]::keybd_event(0x12,0,2,[UIntPtr]::Zero)
[U]::BringWindowToTop($h)|Out-Null; [U]::SetForegroundWindow($h)|Out-Null
[U]::GetForegroundWindow() -eq $h   # 应为 True

# 2. 确认连接
& $cli status

# 3. 打开正确场景
$code = 'UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/Main.unity"); return "opened";'
$code | & $cli exec

# 4. 进 Play
& $cli editor play
Start-Sleep -Seconds 20
[U]::SetForegroundWindow($h)|Out-Null

# 5. 读状态
$code = @'
var b = UnityEngine.Object.FindObjectOfType<DesalEra.Unity.GameBootstrap>();
var buoy = b.Raft.SolveBuoyancy();
return $"pieces={b.Raft.MemberCount} float={buoy.IsFloating} spare={buoy.NetVerticalForceKn:F1}kN";
'@
$code | & $cli exec

# 6. 改参数，立刻观察（这是关键能力，无需重编译）
$code = @'
var b = UnityEngine.Object.FindObjectOfType<DesalEra.Unity.GameBootstrap>();
b.Raft.WindLoadKnPerM = 40f;
b.Reanalyse();
var w = b.Raft.SolveWind();
return $"wind=40 topDisp={w.TopDisplacementM:F3}m failed={b.Raft.CountFailedPieces()}";
'@
$code | & $cli exec

# 7. 截图
& $cli screenshot

# 8. 退出
& $cli editor stop
Get-Process -Id $p.Id | Stop-Process -Force
```

## 常用命令

| 命令 | 作用 |
|---|---|
| `exec "<code>"` | 执行 C#，`return` 返回值 |
| `editor play/stop/pause/refresh` | 播放控制 |
| `console --type error` | 读错误日志 |
| `test --mode PlayMode` | 跑 PlayMode 测试 |
| `screenshot` | 场景/游戏视图截图 |
| `menu "路径"` | 执行菜单项 |
| `list` | 列出所有可用工具 |

**用 stdin 管道传代码**，避免 PowerShell 对 `$"` 字符串的解析冲突。

## 玩法会话录制 / 复现（给 agent 排错）

进 Play 后可用菜单 **DesalEra → Session → Start/Stop Recording**（快捷键 Ctrl+Shift+R / Ctrl+Shift+E），或 CLI：

```powershell
$cli = "C:\Users\Anantian\tools\unity-cli\unity-cli.exe"
# 无参命令（推荐，避开 PowerShell 剥 JSON 引号的问题）
& $cli session_start
# …你自己操作一段时间…
& $cli session_stop
& $cli session_list
& $cli session_status
& $cli player_state
& $cli player_eat
```

最省事的带参写法是逐个 `--参数名 值`，完全不经过 JSON：

```powershell
& $cli player_look --yaw 135 --pitch 20 --distance 15
& $cli player_build --cell_x 1 --cell_y -1 --index 7 --level 0 --facing 2
& $cli world_storm --on true
```

需要传整段 JSON 时用 `Start-Process -ArgumentList`，不要用 `& $cli --params '{"a":1}'`（引号会被剥掉）：

```powershell
function Cli([string]$cmd, [string]$json) {
  Start-Process -FilePath $cli -ArgumentList @($cmd, '--params', $json) -NoNewWindow -Wait
}
Cli 'session' '{"action":"analyse","id":"session_20261007_123456"}'
Cli 'session' '{"action":"replay","id":"session_20261007_123456","time_scale":2}'
Cli 'player_move' '{"h":0,"v":1,"sprint":false}'
Cli 'player_build' '{"cell_x":0,"cell_y":-1,"index":0}'
Cli 'player_look' '{"yaw":90,"pitch":18,"distance":7}'
```

也可点主工具栏 Play 旁的 **Rec / Stop** 按钮开始/停止录制（无弹窗，结果只打 Console 日志；菜单 DesalEra → Session 与 Ctrl+Shift+R/E 同样静默）。

录制文件：`SessionRecordings/*.json`（动作时间轴 + 0.25s 状态快照）。

| 命令 | 参数 | 对应操作 |
|---|---|---|
| `session_start` / `session_stop` / `session_list` / `session_status` / `session_analyse` | — | 录制控制（analyse 默认识别最新文件） |
| `session` | `action`,`id?`,… | replay / snapshot 等 |
| `player_forward` / `back` / `left` / `right` / `stop` / `sprint` | — | 移动（无 JSON） |
| `player_move` | `h`,`v`,`sprint` | 任意轴向移动 |
| `player_look` | `yaw`,`pitch`,`distance?` | 右键环视 / 滚轮 |
| `player_select` | `index` 0..7 | 1–8 选构件（甲板/立柱/浮筒/斜撑/淡化器/屋顶/墙/楼梯） |
| `player_build` | `cell_x`,`cell_y`,`index?`,`level?`,`facing?` | 左键建造（level 0–2，facing 0东/1北/2西/3南；省略则用当前设置） |
| `player_dismantle` | `cell_x`,`cell_y`,`level?` | 右键单击拆除（无竖向构件时拆以该点为角的屋顶） |
| `player_rotate` | `facing?` | R 旋转朝向（省略则转一步） |
| `player_level` | `level?` | Q 切换建造层（省略则循环） |
| `shelter_demo` | — | 在开局筏上立四柱 + 屋顶并站到下面，端到端验证遮蔽 |
| `house_demo` | — | 两层小屋：四柱 + 三面墙 + 一楼楼板 + 楼梯 + 上层四柱 + 屋顶，站到楼梯脚 |
| `world_storm` | `on`,`wind?` | 立刻开始 / 结束风暴并做结构分析（会触发失效与坍塌） |
| `player_preview` | `cell_x`,`cell_y` 或 `clear` | 让放置预览对准某格点（截图用）/ 交还鼠标 |
| `player_eat` | — | E 吃口粮 |
| `player_teleport` | `x`,`y?`,`z` | 传送（调试）；y 选楼层：4.8 落到一楼楼板，1.5 落到甲板 |
| `player_ui` | `panel`=`pack`\|`none` | Tab / Esc |
| `player_state` | — | 当前快照 |
| `player_visual` | — | 根节点 vs 头/胸/包围盒中心，排查视觉跳变 |
| `anim_diag` | — | 动画脚底/朝向诊断 |

Agent 复现流程：你 `session_start` → 操作 → `session_stop` → 把 `SessionRecordings/*.json` 路径或 `session_analyse` 报告发给 agent → agent 用 `session` replay + `player_*` / `anim_diag` 逐步复现。

## 已验证的实验记录

装好后立刻做了两个真实实验（都在 Play 模式、零重编译）：

**实验一：极端风载**

```csharp
b.Raft.WindLoadKnPerM = 40f;   // 正常 1.5
b.Reanalyse();
```

结果：`converged=True topDisp=0.032m failed=0`

**发现**：纯轴向模型下，钢制木筏对侧向风非常不敏感。X 撑的收益在单跨度下被弯曲刚度缺失掩盖了。

**实验二：无脑堆斜撑**

```csharp
// 塞 40 根斜撑
```

结果：`placed=40 pieces=48 float=False spare=-203.7kN weight=286.9kN`

**发现**：斜撑是实心钢件，加得越多木筏越重，最终沉没。玩家会自然学会"斜撑不能无脑堆"，这正是设计想要的张力。

## 断连排查顺序

1. `status` 显示 `compiling`/`reloading` → 等 10-20 秒（限制 2）。
2. 编辑器 CPU 接近 0 → 失焦被限流，拉到前台（限制 1）。
3. 在前台、CPU 正常，命令仍全部超时 → connector 锁死（限制 4），按 Ctrl+Alt+R。
4. 以上都无效才重启 Unity。

## 清理

Unity 进程被杀后可能残留锁：

```powershell
Get-Process -Name Unity | Stop-Process -Force
Remove-Item "$proj\Temp\UnityLockfile" -Force
```