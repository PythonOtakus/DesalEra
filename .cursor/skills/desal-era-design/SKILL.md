---
name: desal-era-design
description: DesalEra（淡化器纪元，Unity 海上木筏生存建造游戏）实现业务需求和修 bug 的完整流程：读文档定位上下文、写设计、Core 优先实现与测试、用 unity-cli 在 Play 中验证、按会话录制复现 bug、更新 progress/plan/README。在本仓库里新增玩法功能、改动建造/结构/游泳/木筏/动画等系统、排查用户报告的 bug 或录制文件中的问题时使用。
---

# DesalEra 需求与 Bug 流程

所有回复用中文。**只有用户明确要求才提交**；不改 git 配置，不 force push。

## 0. 先读上下文（两类任务都要做）

| 文档 | 用途 |
|---|---|
| `docs/progress.md` | 已发生的事：顶部是按日期倒序的最新条目；后面有「已修复的 bug」表、「关键设计决策」 |
| `docs/plan.md` | 待办与任务状态（P0/P1/P2）、明确不做的事、待决策项 |
| `docs/<系统>.md` | 系统设计：`shelter-building.md`（建造）、`sea-shelter.md`（海水与木筏）、`research.md`（策划与物理验证） |
| `README.md` | 架构、操作、素材与动画管线、已知问题 |
| `docs/unity-cli.md` | CLI 用法、玩法 CLI 工具表、录制 / 回放 |

驱动编辑器前先读 unity-cli skill（`~/.cursor/skills/unity-cli/SKILL.md`），尤其是"connector 锁死"一节。

## 工程约束

- **程序集**：`Core`（`Assets/Scripts/Core`，无引擎依赖，含求解器与游戏规则）← `Unity`（表现层）← `Editor`（CLI 工具、烘焙器）；`Tests/EditMode` 引用前两者。`Core` 不能引用 `Unity`。
- **场景几乎为空**：世界全部由代码在运行时构建（入口 `GameEntry` / `GameBootstrap`）。不要手改 `.unity`，新物体在 `GameBootstrap` 里生成，网格用 `MeshFactory`。
- **动画**：用 Playables（`PlayerAnimator`），不用 AnimatorController。clip 由 `Assets/Scripts/Editor/SurvivorClipBaker.cs` 从 Meshy FBX 按世界旋转逐帧烘焙到 `Assets/Resources/Survivor_*.anim`。FBX 的导入姿态是动作里的某一帧，不是绑定姿态，不能拿它做差分。
- **木筏运动**：`RaftMotion`（LateUpdate，执行顺序 50）先动，`PlayerController`（顺序 100）后动。甲板上的位置要用木筏局部坐标，经 `RaftMotion.LocalOnPlane` 求得（与高度无关）。
- 环境：Unity 2022.3.62f3c1，内置渲染管线。

## A. 实现业务需求

```
- [ ] 1. 理解与确认范围
- [ ] 2. 写设计文档
- [ ] 3. Core 规则 + 测试
- [ ] 4. Unity 表现 + 交互
- [ ] 5. CLI 工具
- [ ] 6. 验证（编译 → 测试 → Play → 截图）
- [ ] 7. 更新文档
```

1. **范围**：对照 `plan.md` 找到对应任务项和"明确不做"。需求跨多个系统、或需要用户拍板时（玩法取舍、数值方向），先用 AskQuestion 确认。大功能分阶段做。
2. **设计**：在对应的 `docs/<系统>.md` 追加，没有就新建。沿用现有结构：`问题 / 目标 / 设计 / 验收 / 非目标`。验收条件要写成可以用 CLI 或测试验证的形式。
3. **Core 优先**：规则、几何与判定放进 `Core`（如 `RaftState`、`SurvivalModel`、求解器），先写测试。测试优先暴露**单调性、守恒、幂等、边界与滞回**这类错误，只读代码是发现不了它们的（见 progress 的 bug 表）。
4. **表现**：`Unity` 层只做渲染、输入与跟随；UI 在 `Unity/Ui`。改了会被重复构建的物体，要保证重建幂等，并在 `OnDestroy` 释放自己 `new` 出来的网格和材质。
5. **CLI**：新动作或状态要能被 agent 驱动和观测，在 `Assets/Scripts/Editor/Cli/PlayCliTools.cs` 加 `[UnityCliTool]`。复杂功能加一个 `*_demo` 端到端演示命令（参考 `house_demo`）。快照需要新字段时一并加上，并同步 `docs/unity-cli.md` 的工具表。
6. **验证**：见下文"验证闭环"。
7. **文档**：见下文"收尾"。

## B. 修 Bug

```
- [ ] 1. 复现
- [ ] 2. 定位根因（有证据）
- [ ] 3. 回归测试
- [ ] 4. 修复，并检查修复是否引入新问题
- [ ] 5. Play 验证
- [ ] 6. 记录
```

1. **复现**：
   - 用户给了录制文件 `SessionRecordings/session_*.json`：先 `session_analyse`（默认取最新文件），读动作时间轴和 0.25 s 状态快照，找到异常时刻；再用 `session` 的 replay 复现，或用 `player_teleport` / `player_move` / `player_look` 摆到出事位置。
   - 诊断命令：`player_state`（快照）、`player_visual`（根节点与头/胸/包围盒，查视觉跳变）、`anim_diag`（脚底与朝向）。
   - 需要更细的数据时，用 `exec` 在 Play 中逐帧采样（注册 `EditorApplication.update` 要加 `--allow-async`），量化后再下结论。
2. **根因**：给出机制和数据，不要只描述现象。先查 progress 的"已修复的 bug"表和"关键设计决策"，看是否是旧问题复发，或者是刻意设计（例如材料比强度不单调）。注意"局部正确、组合起来错"的情况。
3. **回归测试**：能在 `Core` 或 EditMode 里固定的，先写一个会失败的测试。
4. **修复后反查**：修复常常会在边界上引出新问题。例如甲板锚定修好了滑动，却引出边缘闪烁；坐标或判定一变，就要检查阈值边界（必要时加滞回）和依赖同一判定的其他系统。
5. **Play 验证**：按录制里的条件复现，确认现象消失，有量化指标的要给出数值（如"站 5 s 模式切换 0 次"）。
6. **记录**：progress 加条目，写清现象、根因、修法、实测；能固定为测试的，在 bug 表加一行。

## 验证闭环

1. 改完脚本：`editor refresh --compile`，再 `console --type error` 确认零编译错误。新类型用 `exec` 确认已加载。
2. 测试：先 `status`，**不是 `playing` 才跑** `test --mode EditMode`（Play 中跑会锁死 connector；万一锁死，把 Unity 切到前台按 Ctrl+Alt+R 恢复）。要求全部通过；测试总数变化时同步文档里的数字。
3. Play：`editor play --wait` → 用 `*_demo` / `player_*` 驱动 → `player_state` 读数 → `screenshot`，读截图确认画面正确。截图存 `Temp/*.png`，在回复中内嵌展示。
4. 结束后 `editor stop`。不要让编辑器停留在 Play 模式。

## 收尾

- `docs/progress.md`：在顶部加 `## <标题>（YYYY-MM-DD）` 条目：做了什么、根因 / 设计要点、测试数、Play 实测结果；更新"最后更新"日期。被推翻的旧结论标注"已被取代"，不要删除。
- `docs/plan.md`：更新任务状态（✅ / 🔧）。
- `README.md`：操作、架构或管线有变化时更新。
- `docs/unity-cli.md`：新增 CLI 命令或新踩的坑要写进去。
- 向用户汇报（中文）：先说结论，再说根因或设计、验证数据、截图，以及未解决的问题。不提交。
