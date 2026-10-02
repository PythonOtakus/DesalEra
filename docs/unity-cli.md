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

## 三个必须知道的限制

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

### 2. 进/出 Play 模式会触发域重载

此刻 HTTP listener 会短暂消失，`exec` 会报 `no Unity instances running`。等 10-20 秒后恢复。

`status` 读的是磁盘上的心跳文件，所以它可能显示 `playing` 而 `exec` 仍失败——两者不一致就说明 listener 还没起来。

### 3. 场景必须在 Build Settings 里

Play 模式加载的是 Build Settings 的首个场景，不是"最近打开的"。用 `-executeMethod` 构建 exe 时可以手动传场景数组绕过，但编辑器里 Play 不行。

本项目已把 `Main.unity` 设为唯一构建场景。

## 标准操作流程

```powershell
$cli = "C:\Users\Anantian\tools\unity-cli\unity-cli.exe"

# 0. 启动编辑器并保持前台
$unity = "C:\Program Files\Unity\Hub\Editor\2022.3.62f3c1\Editor\Unity.exe"
$proj  = "C:\Users\Anantian\source\repos\CrazyAquarium"
$p = Start-Process $unity -ArgumentList "-projectPath","`"$proj`"","-logFile","`"$env:TEMP\editor.log`"" -PassThru
Start-Sleep -Seconds 60

# 1. 把窗口拉到前台（否则下面全部超时）
Add-Type @"
using System; using System.Runtime.InteropServices;
public class U {
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int n);
  [DllImport("user32.dll")] public static extern bool BringWindowToTop(IntPtr h);
}
"@
$h = (Get-Process -Id $p.Id).MainWindowHandle
[U]::ShowWindow($h,9)|Out-Null; [U]::BringWindowToTop($h)|Out-Null; [U]::SetForegroundWindow($h)|Out-Null

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
var b = UnityEngine.Object.FindObjectOfType<CrazyAquarium.Unity.GameBootstrap>();
var buoy = b.Raft.SolveBuoyancy();
return $"pieces={b.Raft.MemberCount} float={buoy.IsFloating} spare={buoy.NetVerticalForceKn:F1}kN";
'@
$code | & $cli exec

# 6. 改参数，立刻观察（这是关键能力，无需重编译）
$code = @'
var b = UnityEngine.Object.FindObjectOfType<CrazyAquarium.Unity.GameBootstrap>();
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

## 清理

Unity 进程被杀后可能残留锁：

```powershell
Get-Process -Name Unity | Stop-Process -Force
Remove-Item "$proj\Temp\UnityLockfile" -Force
```