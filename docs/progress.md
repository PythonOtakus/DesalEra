# 项目进度

> 最后更新：2026-10-08
>
> 本文档记录"已经发生了什么"。待做的事见 `plan.md`。

## 初始地板与甲板随船（2026-10-08）

- **地板**：起始木筏原本只是一圈 6×6 m 梁，中间看起来是海水，但按 `IsOverDeck`
  可以站人。现在 `GameBootstrap.BuildDeckFloor` 按 `DeckFootprint` 铺木地板，
  顶面比梁顶低 1 cm，避免重叠处 z-fighting；甲板扩建后随之重建。地板是纯视觉，
  不参与结构计算。
- **不再滑动**：木筏倾斜最大约 6°，而角色原本只修正高度、保持世界坐标 XZ 不变，
  相对板面来回滑约 0.16 m。现在每帧在木筏移动前记录角色的木筏局部坐标，木筏移动后
  按它放回。实测风浪中站立 4 s：世界坐标摆动 0.35 m，相对木筏漂移 2 mm。站立与
  遮蔽判断也改为用木筏坐标。
- **甲板边缘闪烁**（录像 `session_20261008_223501`）：上一条改动最初直接把角色根节点
  换算成木筏局部坐标。木筏倾斜时，同一竖直线上不同高度的点，局部 XZ 相差
  高度 × sin(倾角) ≈ 0.3 m：站在甲板上算作在界内，落到水面后又算回界内，每帧往复。
  现在改用"过角色的竖直线与甲板平面的交点"（`RaftMotion.LocalOnPlane`），结果与高度
  无关。另外上船阈值 0.3 m、留在船上阈值 0.6 m，形成滞回。实测倾斜 6.2° 时，在
  x=3.3/3.5/3.6/3.75/4.0 各站 5 s，模式切换 0 次。

## 游泳姿态改为俯泳，动画重定向重写（2026-10-08）

现象：游泳时角色竖直站在水里划手，而 Meshy 的 `SwimForward` 是趴着的。

根因：动画 FBX 导入后骨骼的"静止姿态"不是绑定姿态，而是动作里的某一帧。游泳导出
静止时本身就趴着（髋→头方向 `(0.01,0.36,0.93)`）。旧 baker 按
`charRest * inverse(animRest) * src(t)` 做差分，把趴姿当零点减掉，于是烘成了直立。
下文「T-pose 修正」其实是同一个错误的另一面：走路导出静止时双臂下垂，被减掉后就成了
T-pose，再用手臂下垂修正去补。另外脊柱按名匹配是错位的：Mixamo 的 `Spine` 在最下面，
角色的 `Spine` 在最上面，`Spine1/Spine2` 则直接丢失。

修法：带蒙皮的动画导出（Walk）里，绑定姿态和角色逐关节一致，说明是同一副骨架。
baker 改为在源骨架上逐帧采样，把每根角色骨骼设成对应骨骼的世界旋转（Hips 另加
位置），再读局部值写曲线，完全不读动画文件的静止姿态。脊柱用别名表对应。手臂下垂
修正随之删除。12 个 clip 与源动画的六段肢体方向逐帧对比，误差最大 0.3°。

玩法配套：两段游泳动画的水线高度不同，踩水头在 1.31 m，俯泳头在 0 m。根节点深度
因此分为踩水 0.85 m、俯泳 0.05 m，过渡时按 `SwimForward` 的混合权重插值。停游
从瞬切改为交叉淡入淡出，避免趴姿一帧之内立起来。

## 庇护所建造第二阶段（2026-10-07）

设计见 `docs/shelter-building.md`「第二阶段」。已完成：

- 新构件：墙（7，两端立柱，对角杆当剪力撑，受风 9 m²）、楼梯（8，爬一层，6 级踏板渲染）；屋顶兼上一层楼板
- 支撑改为“接地”判定：从甲板高度节点与龙骨锚点沿存活杆件可达，判定时排除构件自身杆件；墙要求两端各有一根存活竖杆
- 坍塌连锁 `RaftState.CollapseUnsupported`：拆除后和每次结构分析后执行，杆件全断或失去支撑的构件移除、不退款，插槽可重建
- 风载：`Member.WindAreaM2 / WindNormal`，`TrussSolver.ApplyWind` 按面积与迎风角加载，仍幂等
- 角色在楼板 / 楼梯上行走：`RaftState.TryGetRaisedSurface` + `PlayerController.SurfaceLocalY`，单步可上 0.6 m，从楼板下方走过不被抬起
- 放置预览：`RaftState.CanPlace` 拆出校验（返回 `PlacementPlan` 几何），`BuildPreview` 绿 / 红虚影，建造栏标题显示层、朝向与原因
- 风暴开始 / 结束立即 `Reanalyse`；`GameBootstrap.SetStorm`；`StructureNotice` 事件把坍塌提示推到状态条
- CLI：`house_demo`、`world_storm`、`player_preview`；快照新增 `surfaceY`，`soleGap` 改为相对实际站立面
- 修复（实现中发现，均为既有问题）：
  - `TrussSolver.AssembleMember` 刚度随杆件朝向变化（屋顶两根对角线一根双倍、一根为零），改为与朝向无关的 `EA/L × (aaᵀ + t(I−aaᵀ))`，沿轴杆件结果不变
  - 立柱落在 6 m 起始梁中点时柱脚是孤立节点：新建节点落在杆件中段时拆分该杆件
  - 风暴周期：结束时间按“下一场开始 + 12 s”算，导致风暴常驻；改为独立的 `stormDurationSeconds`
  - 建造栏高亮只在库存变化时刷新，快捷键 / CLI 选件后高亮不跟随
- 测试：新增 `HouseTests` 21 项；两项旧测试改到甲板角点（避免拆梁改变杆件数）；EditMode 153 项全过
- Play 验证：`house_demo` 沿楼梯上楼（站立面 2.02 → 4.24 → 4.72，上层屋顶下 `sheltered=true`）；预览绿 / 红虚影；`world_storm` 后楼梯与两面侧墙坍塌、迎风墙保留，暴露角色掉 12 生命，12 s 后风暴结束

## 庇护所建造第一阶段（2026-10-07）

设计见 `docs/shelter-building.md`，计划项 P1-5。已完成：

- 占用表改为 `(格点, 层, 插槽)`（`PlacementKey`），立柱与甲板可共享格点；西 / 南向甲板归一到相邻格点，同一根梁不会放两次
- 建造层 0–2（`StoreyHeightM = 3 m`），1 层起必须有“仍有构件连接”的节点支撑——拆掉的立柱留下的孤立节点不算
- 朝向：`R` 旋转、`Q` 切层；`RaftState.RotateOffset` 只转水平分量
- 新构件“屋顶”：四角立柱支撑，结构上是屋面内两根交叉对角杆，渲染为平板（`GameBootstrap.BuildRoofSlab`）
- 遮蔽判定 `RaftState.IsSheltered` → `GameBootstrap.PlayerSheltered` → 状态条“有遮蔽”、风暴文字“已遮蔽 / 暴露中”
- `SurvivalModel.ApplyWeather`：风暴暴露掉体力 / 生命，遮蔽下免除；平时遮蔽下体力恢复 ×1.5
- 录制 / CLI：动作记录 `level` / `facing`，新动作 `rotate` / `level`；快照新增 `sheltered` / `storm` / `buildLevel` / `buildFacing`；新增 `player_rotate`、`player_level`、`shelter_demo`
- 顺带修复：状态条标签文字被截断不可见（`UiFactory.Text` 默认只占上半格）
- 测试：`ShelterTests` 15 项全过
- 随后修复 `MaterialTests` 4 项：测试改为按“每个面独立顶点、UV 以米计”断言新网格；同时修正 `MeshFactory.Extrude` 的绕序（侧面与端盖原先朝内，网格内外翻转），并对顺时针轮廓自动反向。EditMode 131 项全过

## 海水与庇护所（2026-10-07）

设计见 `docs/sea-shelter.md`。进行中：

- 海面：Gerstner 涌浪 + 高度场法线 + 菲涅尔天空全景反射（`SeaWater.shader` / `SeaWave`）
- 打捞物跟浪起伏与轻倾侧（`Pickup`）
- 龙骨吃水 **0.6 m → 1.5 m**（`RaftState.KeelDepthM`）；开局筏仍漂浮
- `RaftMotion`：四角采样涌浪 → heave + 轻 pitch/roll；甲板玩家跟随
- 文档：`docs/sea-shelter.md`，计划项 P0-3

## 当前状态

**阶段**：可玩灰盒垂直切片已交付

**核心结论**：立体建造的物理模型可行，无需自研引擎。三维交叉支撑已验证，第一大 USP 技术上成立。灰盒可运行。

**验证状态**：

| 项目 | 状态 |
|---|---|
| Unity 编译 | ✅ 零错误 |
| EditMode 测试 | ✅ 75/75 通过 |
| dotnet headless 测试 | ✅ 24/24 通过，约 40 ms |
| **可执行文件构建** | ✅ `Builds/DesalEra.exe` 90 MB（含角色资产） |
| **实际运行验证** | ✅ 进程存活，渲染正常，HUD 工作，无异常 |
| **玩家角色渲染** | ✅ 银发角色可见，74083 顶点，三张贴图全部绑定 |
| **角色行走/奔跑动画** | ✅ Walk/Run 两态，Idle→Walk→Run→Idle 切换正确 |
| 三维交叉支撑 | ✅ 已验证（直接刚度法） |
| 刚架弯曲刚度（EI） | ❌ 未做，纯轴力模型 |
| 失败恢复机制 | ⚠️ 仅有"死亡后重置"，无局部修复 |

**灰盒玩法**：打捞残骸 → 建造（甲板/柱/浮筒/斜撑/淡化器）→ 应对周期风暴 → 生存值压制。全部运行时构建，场景文件仅 180 行。

**已知问题**（见截图）：画面偏暗、无音效、无存档、角色部分骨骼未烘焙。

## 角色资产替换（2026-10-03）

旧的中年男性 Meshy 资产已全部删除，替换为银发街机风格角色。

| 项 | 值 |
|---|---|
| 源目录 | `C:\Users\Anantian\Downloads\Meshy_AI_Silver_Haired_Street__biped` |
| 模型 | `Character_output.fbx`，74083 顶点，28 骨骼 |
| 动画 | Walking 1.04 s、Running 0.67 s，各 350 条曲线 |
| 贴图 | 4 张 2048×2048（Albedo / Normal / Roughness / Metallic） |
| `globalScale` | 1（Blender 实测约 1.7 m，非厘米导出） |
| 构建体积 | 70 MB → 90 MB |

### 动画重定向：两个静默失败

Meshy 把角色与动画导出成两个 FBX，直接套用会失败两次，两次都不会报错：

1. **路径不匹配** — 动画骨架 `target_character/mixamorig:Hips`，角色骨架 `Armature/Hips`。
   曲线路径全部落空，clip 在播放、时间在推进，但**一根骨骼都不动**。
2. **绑定姿势不同** — `LeftArm` 静止角在角色是 `(352,240,2)`、动画是 `(59,180,344)`。
   路径修好后姿势仍然错：双臂举过头顶、头部前俯。

修法是 `charRest * inverse(animRest) * src(t)` 差分重定向，见
`SurvivorClipBaker.cs`。**只有渲染出图才能发现第 2 类问题**——路径全对、
零未命中解析时曲线看起来完全正常。

途中还排除了 legacy `Animation` 组件：它的时间会累加到 494 s 而不循环，
且不驱动骨骼。改用 Mecanim。

### AnimatorController 资产不可用，改为 Playables（2026-10-03）

最初用脚本创建 `AnimatorController` 并 `AddState` 写入 12 个状态。编辑器里
Play 模式完全正常，但**构建版里角色始终是 T-pose**。这一条花了最长时间，因为
它同时满足三个条件：编辑器测试全绿、没有任何报错、日志里唯一的警告还指错了方向。

根因分两层：

1. **`AnimatorStateMachine.AddState` 创建的状态不是合法子资源。** 它们会被写进
   `.controller` 文件（12 个 `AnimatorState` 文档、`m_ChildStates` 里 12 个 fileID
   引用），但资产一旦被重新导入，Unity 就把它们全丢掉，`m_ChildStates` 变回 `[]`。
   构建时 `SceneBuilder.Build()` 里的 `AssetDatabase.Refresh()` 正好触发重新导入。
   先后试过「清空已有 controller 的状态」和「在临时路径构建再移动」，都产出同样
   的坏文件——只有全新路径的一次性创建是好的。
2. **编辑器与构建版读的是不同的东西。** Play 模式用内存里的对象，所以状态齐全；
   构建版用序列化结果，所以是空状态机。角色因此退回网格绑定姿势，也就是 T-pose。

最终改用 **Playables 图**：`AnimationPlayableOutput` + `AnimationMixerPlayable`，
12 个 `AnimationClipPlayable` 各占一个输入，代码里按速度选择。没有资产要序列化，
编辑器与构建版跑同一份代码。

改用 Playables 后又暴露一个自己写的 bug：过渡逻辑用 `_playing` 列表做增量权重
管理，当「正在淡入的 clip」被再次选中时 `_fadeFrom == _fadeTo`，收尾那句
`SetInputWeight(_fadeFrom, 0)` 会把刚拉满权重的目标自己清零，**所有输入权重变成
0**，角色又不动了。改成显式的 `_activeInput` / `_fadingFrom` / `_fadingTo` 三个
字段后，任何时刻只有两个权重被触碰，可直接读数核对。

最终验证（构建版 exe 实测，非编辑器）：角色双臂下垂、行走步态正常、体力随移动
下降、点击可建造（PIECES 8→9、载荷 58→56 kN）。

### 游泳与打捞闭环（2026-10-03）

修掉唯一被验证为坏掉的核心循环：14 个漂浮物里 8 个够不到。

**先确认设计意图**再动手。`docs/research.md` 1.1 写的是"打捞海上漂流资源、潜水探索
沉没城市获取基础材料"——下水本就是目标循环的一部分，不是可选项。而且 SwimIdle /
SwimForward 两个 clip 早已烘焙并接进图里，只差玩法。

原先 `PlayerController` 把玩家夹在 ±15.6 m 的方形内，拾取环在 12–34 m，几何上
8/14 不可达。改为两种移动模式：

- `IsOverDeck` 读木筏自己的 **footprint**（甲板层关节的 XZ 凸包），走出即切换
  成游泳。建甲板会真的扩大可走范围。
- 游泳不能冲刺、额外吃体力（1.1/秒）。否则游水永远劣于走甲板，没有扩建动机。
- 水中禁止建造与拆除。
- 动画切到 `SwimForward` / `SwimIdle`，复用已烘焙的 clip。

两处只有跑起来才会暴露的缺陷：

1. **模式判定原本只在移动时执行**。站着不动时永远不会重新判定，于是拆掉脚下的
   甲板后，角色会一直保持"在甲板上"并悬在空中。改为每帧无条件判定。
2. **高度同样只在移动路径上更新**。站着不动被切到游泳模式时不会下沉。
   抽出 `SurfaceHeight()` 让两条路径共用一个来源。

**放弃的方案**：给初始木筏中心补两根横梁，让它从"方框"变成真平台（视觉上确实
一直是个洞，玩家出生点就在洞上）。补完后桁架刚度矩阵奇异，
`Raft_OpeningStateIsStructurallyStable` 失败——排查发现原结构本身就是数值上勉强
收敛的（8 关节 8 构件通过，加到 9 关节 12 构件就奇异），这属于 P1-4 推迟的刚架
弯曲刚度问题。为了一个视觉缺陷去动已调好的物理不划算，因此保持物理结构不动，
用 footprint 判定解决玩法问题，把"甲板中间没铺板"记为已知视觉缺陷。

构建版实测：HUD 显示 `[SWIMMING]`，体力 100→76，角色游到开阔水面；75/75 测试通过。

### 方向修正：固定视角 SLG → 第三人称（2026-10-03）

交付后判定手感不对：原型本质是 SLG 而不是 3A 动作生存。根因单一而明确——
`GameEntry.LateUpdate` 里的固定偏移相机 `(0,11,-12)` 加 LookAt。角色只能被俯视，
构图永远不变，移动是恒速瞬时启停。

改动：

- **新增 `ThirdPersonCamera`**：右键拖动转视角、滚轮缩放（2.4–20 m）、俯角
  −14°–74°、0.6 m 偏肩偏移、枢轴平滑跟随、遮挡时拉近（SphereCast）。
  刻意**不用锁定指针**：建造靠左键放置，隐藏光标就无从下手；拖动转视角让双手
  都在鼠标上。
- **滚轮从"切换建造件"改为相机缩放**，建造件移到 1–5 数字键。一个输入只做一件事。
- **移动带加减速**（加速 34、减速 26 m/s²）。恒速移动一松键就停死，是光标操作
  建造游戏的手感特征。转向也只在真的在移动时发生。

顺手修掉一个自己引入的缺陷：`Input.GetMouseButtonDown(1)` 与相机拖动共用右键，
拆除会误触发。加了 `&& !Input.GetMouseButton(1)`——按下那一帧两者都为 true，
所以必须等松开。

**已知缺口**：世界里没有任何碰撞体，所以相机遮挡拉近不生效，角色也不会被木筏
挡住。这是下一步第一优先。

网格建造没有废弃：它是这个产品的第一大 USP，要改的是视角和手感，不是把建造变成
动作游戏。方向与优先级记录在 `docs/plan.md` 第 0.0 节。

构建版实测：右键拖动可转视角（能看到地平线与角色正面）、滚轮可缩放（角色明显变大）、
建造正常。75/75 测试通过。

### 动画包接入（2026-10-03 追加）

Downloads 里 10 个 Meshy 压缩包已全部解压进 `Assets/Art/Characters/Survivor/Animations/`：

| zip | 项目文件名 | 时长 |
|---|---|---|
| (无) | `Survivor_Idle.fbx` | 1.88 s |
| (1) | `Survivor_Jump.fbx` | 1.88 s |
| (2) | `Survivor_JumpObstacle.fbx` | 0.88 s |
| (3) | `Survivor_SwimIdle.fbx` | 3.00 s |
| (4) | `Survivor_SwimForward.fbx` | 4.50 s |
| (5) | `Survivor_LadderMountStart.fbx` | 2.00 s |
| (6) | `Survivor_LadderClimbLoop.fbx` | 1.63 s |
| (7) | `Survivor_LadderClimbFinish.fbx` | 4.00 s |
| (8) | `Survivor_RopeHangIdle.fbx` | 2.58 s |
| (9) | `Survivor_RopeSwingToGround.fbx` | 4.33 s |

每包都附带了同一套 4 张贴图，与工程内已有副本逐字节同源，已跳过。
这些是 `without_skin` 导出，只有骨架与动画，无网格，单个约 0.2 MB；
已有的 walk/run 是 `withSkin`，各含一份重复网格、各 28.7 MB。

baker 改为自动发现目录下所有 FBX，状态名由文件名去掉 `Survivor_` 前缀得到，
新增动画不再需要改代码。12 个 clip 全部烘焙成功，各 23 根骨骼完成差分重定向。

**关键发现：每个 FBX 的参考姿势都不一样。** Idle 的 `LeftArm` 静止角是
`(49,275,77)`，SwimForward 是 `(50,264,3)`，LadderClimbLoop 是 `(38,301,50)`，
角色本身是 `(352,240,2)`。差分重定向逐文件读取各自的 `animRest` 才成立，
用统一参考姿势会全部错位。

**顺带解决了一个旧问题**：此前没有 Idle 动画，`PlayerAnimator` 只能把走路姿势
冻结在 `speed=0` 冒充站立。现在 Idle 以全速播放真实的 Idle clip。

**新暴露的问题**：Meshy 的动画全部以「双臂水平张开」为中立姿势。实测 Idle 循环
全程 `LeftArm` 只从 `(352,240,2)` 变到 `(345,246,5)`，共 7°，所以角色呈现 T-pose，
像稻草人。差分重定向本身正确（nt0 恰好等于绑定姿势，证明 `animRest` 取的就是
clip 第 0 帧），这是授权姿势问题。

### T-pose 修正（2026-10-03）

> 2026-10-08 已被取代：T-pose 的真正原因是差分零点取错，见顶部「游泳姿态改为俯泳」。

修正烘焙进曲线，运行时零开销，实现在 `SurvivorClipBaker.CorrectedRest`。

**先量后改**：肩 `y=1.31`、腕 `y=1.24`，下垂 7 cm 而臂长 54 cm——几乎完全水平，
确认是 T-pose 而非视觉误差。

**旋转量从几何反推**：轴取「骨骼当前指向子节点的方向」与 `Vector3.down` 的叉积，
角度由 `Vector3.Angle` 现算。换任何模型都不会错，也不留下无解释的魔法数字。

**必须分散到三个关节**。只转锁骨（45%→全量 67°）会把腋下蒙皮撕开一个可见的洞：
肩部顶点同时蒙皮到脊柱、锁骨和上臂，单关节的大角度旋转把它们拉开。最终分配为
锁骨 45%、上臂 40%、前臂 15%。分摊后每个关节形变都小，出图确认撕裂消失。

结果：手到肩下垂量 `0.07 m → 0.54 m`（等于整条手臂长度），臂长未变；走路时手到
身体中轴的横向距离在 `0.119–0.389 m` 之间摆动，始终在身体外侧，不插进躯干。

### 顺带修掉的两个问题

- **baker 曾在 Play 模式下运行**：重建 controller 会删除并重新创建资产，运行中的
  玩家因此持有已销毁的引用，`runtimeAnimatorController` 读出来是 `null`，唯一
  症状是角色悄悄不动。baker 现在直接拒绝在 Play 模式执行。
- **`MaterialLibrary.OnDestroy` 每次退出 Play 都报错**：它试图销毁从 Resources
  加载的贴图，而那是 Unity 的资产，`Destroy` 会被拒绝
  （"Destroying assets is not permitted to avoid data loss"），且退出 Play 时
  `Application.isPlaying` 在 `OnDestroy` 内仍短暂为 true。这些贴图的生存期归 Unity
  管，不该手动销毁；现在只销毁自己 `new` 出来的材质。进出 Play 一轮零错误零警告。

## 环境事实（已验证）

| 项 | 值 |
|---|---|
| Unity | 2022.3.62f3c1（`C:\Program Files\Unity\Hub\Editor\2022.3.62f3c1`） |
| 许可证 | `C:\ProgramData\Unity\Unity_lic.ulf`，StopDate 2026-10-05 |
| 内存 | 15.7 GB 总量，约 4.8-5.8 GB 空闲 |
| GPU | RTX 5070 Ti Laptop，12 GB VRAM |
| CPU | 32 逻辑核 |
| .NET SDK | 9.0.308 |
| Python | 3.13.11（AssetMCP 用） |

**注意事项**：

- `com.unity.ide.rider` 无法从 `download-packages.unity.cn` 下载，已从 manifest 移除。不影响构建。
- 显存 12 GB 对 3A 画面偏紧，倾向风格化路线。
- 内存不足以同时开多个 Unity 实例，批处理需串行。

## 工程结构

```
DesalEra/
├── Assets/Scripts/
│   ├── Core/                      # 求解器，无表现逻辑
│   │   ├── DesalEra.Core.asmdef
│   │   ├── Structure/
│   │   │   ├── Material.cs        # 材料参数、承载力、弹性模量
│   │   │   ├── Member.cs          # 构件、节点、求解报告
│   │   │   ├── StructureSolver.cs # 重力流：载荷、浮力、倾覆、坍塌
│   │   │   └── TrussSolver.cs     # 直接刚度法：矩阵解算、侧向分析
│   │   └── Samples/
│   │       ├── Structures.cs      # 确定性测试夹具（筏架、塔、偏载筏）
│   │       └── BracedFrames.cs    # 带斜撑的框架夹具
│   ├── Unity/                     # 表现层
│   │   ├── DesalEra.Unity.asmdef
│   │   ├── StructureView.cs       # 按利用率染色
│   │   └── StructurePrototype.cs  # 原型驱动，Inspector 可调
│   ├── Editor/                    # 预留
│   └── Tests/EditMode/            # 36 个测试
├── Packages/manifest.json
├── ProjectSettings/               # 线性色彩空间
└── docs/
    ├── research.md                # 调研 + 策划案 + 技术验证
    ├── plan.md                    # 待办计划
    └── progress.md                # 本文档
```

**程序集边界**：`Core` 不引用 `Unity`，`Unity` 引用 `Core`，`Tests` 引用两者。边界保证求解器可被独立测试和复用。

## 验证命令（可直接复制）

### Unity EditMode 测试

```powershell
$unity = "C:\Program Files\Unity\Hub\Editor\2022.3.62f3c1\Editor\Unity.exe"
$proj = "C:\Users\Anantian\source\repos\DesalEra"
$results = "$env:TEMP\unity_results.xml"
$log = "$env:TEMP\unity_tests.log"

$p = Start-Process -FilePath $unity -ArgumentList `
  "-batchmode","-nographics","-runTests","-testPlatform","EditMode",`
  "-projectPath","`"$proj`"","-testResults","`"$results`"","-logFile","`"$log`"" `
  -PassThru -NoNewWindow
$p | Wait-Process -Timeout 1800

[xml]$x = Get-Content $results
$x.'test-run' | Select-Object total, passed, failed, result

# 编译错误
Get-Content $log | Select-String "error CS" | Select-Object -First 10
```

预期：`total=36 passed=36 failed=0 result=Passed`，退出码 0。

### dotnet headless 测试（秒级迭代）

不启动编辑器，直接编译 Core + 测试。适合调数值。

```powershell
# 一次性创建测试工程
$tmp = "$env:TEMP\ca-tests"
New-Item -ItemType Directory -Force -Path $tmp | Out-Null
$core = "C:\Users\Anantian\source\repos\DesalEra\Assets\Scripts\Core"
$tests = "C:\Users\Anantian\source\repos\DesalEra\Assets\Scripts\Tests\EditMode"
$unityDll = "C:\Program Files\Unity\Hub\Editor\2022.3.62f3c1\Editor\Data\Managed\UnityEngine\UnityEngine.CoreModule.dll"

$proj = @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <Nullable>disable</Nullable>
    <LangVersion>9.0</LangVersion>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageReference Include="NUnit" Version="3.14.0" />
    <PackageReference Include="NUnit3TestAdapter" Version="4.6.0" />
  </ItemGroup>
  <ItemGroup>
    <Reference Include="UnityEngine.CoreModule">
      <HintPath>$unityDll</HintPath><Private>true</Private>
    </Reference>
  </ItemGroup>
  <ItemGroup>
    <Compile Include="$($core -replace '\\','\\')\**\*.cs" />
    <Compile Include="$($tests -replace '\\','\\')\*.cs" />
  </ItemGroup>
</Project>
"@
Set-Content "$tmp\tests.csproj" $proj -Encoding UTF8

# 运行
Push-Location $tmp; dotnet test tests.csproj --nologo -v q; Pop-Location
```

注意：headless 通道只跑 `Core` 相关测试（24 个）。`Unity/StructureView.cs` 依赖编辑器实例，不在此通道覆盖。

**注意**：这条通道把 `UnityEngine.CoreModule.dll` 作为普通引用，绕过了 asmdef 的 `noEngineReferences`。修改 Core 时若引入新的引擎类型，需同步更新此工程。

## 已修复的 bug

全部有回归测试固定。记录原因以免重犯。

| # | 问题 | 根因 | 测试 |
|---|---|---|---|
| 1 | 塔基永远只扛一层重量 | kg 与 kN 混入同一累加器 | `LoadFlow_BaseScalesWithTowerHeight` |
| 2 | 水平梁一半质量消失在悬空端 | 严格自上而下遍历，死端无法回传 | `LoadFlow_AnchoredBaseReceivesTheFullWeight` |
| 3 | 所有塔基柱恒为 0.4 kN | 锚点被直接跳过 | 同上 |
| 4 | 柱子凭空背弯矩 | 向上构件落入"水平"分支 | `Bending_LevelBeamsBendUnderTheirOwnWeight` |
| 5 | 悬臂梁利用率恒为 0 | 悬臂不传载荷，也就没算弯矩 | `Bending_CantileverBeamsCarryMomentWithoutAxialLoad` |
| 6 | **加粗柱子反而更脆** | 承载力是每材料固定值，不随截面缩放 | `CrossSection_WiderMembersAreStrongerNotHeavier` |
| 7 | 构件越接近断裂看起来越安全 | 调色板红通道非单调（琥珀 0.85 > 临界 0.78） | `Palette_RedChannelRisesMonotonically` |
| 8 | 每次重建场景翻倍 | edit mode 下 `Destroy` 延迟不到 | `Rebuild_IsIdempotent` |
| 9 | 编译失败 | `Color.Lerp` 是静态方法不是扩展方法 | — |
| 10 | 类名冲突 | `Prefabs` 与 UnityEngine 概念冲突 | — |
| 11 | **加斜撑后位移反而变大** | **逐节点 Gauss-Seidel 无法求解超静定结构** | `Lateral_XBraceReducesTopDisplacement` |
| 12 | 所有构件轴力恒为 0 | 锚固端构件被跳过，载荷路径断开 | `Baseline_ExternalWindIsActuallyApplied` |
| 13 | 风载加了两遍 | `ClearWind` 按当前设置重算，首次求解时减掉了不存在的量 | `Determinism_RepeatSolveOnSameInstanceIsIdempotent` |
| 14 | 斜撑方向没交替 | `AddMember(left,right)` 的 Axis.x 恒为正，两根斜撑同向 | `Lateral_SingleDiagonalResistsOneDirectionOnly` |
| 15 | 颜色斜坡断言失效 | 改调色板后 0.9 与 1.2 利用率都饱和在同一颜色 | `ColorFor_UtilizationRampsTowardFailure` |
| 16 | **开局木筏必沉** | 0.12 m² 塑料浮筒排水 0.24 m³，需 0.55 m³。固体重塑料只有 50 kg/m³ 净浮力 | `Raft_OpeningStateFloats` |
| 17 | **开局木筏是机构** | 支撑全在中轴线上，面外无约束，桁架矩阵奇异 | `Raft_OpeningStateIsStructurallyStable` |
| 18 | 密封体积没生效 | `BuoyancyScale` 只乘材料浮力，忽略了 `SealedVolumeM3` | `Raft_SealedPontoonsAreWhatProvideBuoyancy` |
| 19 | 建造后结构变机构 | 玩家构件单点连接，未形成闭合结构（**这是正确行为**，测试改为断言此行为） | `Raft_BuildingReusesExistingJoints` |

**其中 1、2、3、6、11、13、14 是模型错误，只读代码发现不了，是测试逼出来的。** 写新系统时优先写能暴露单调性、守恒、幂等性的测试。

**11 值得特别记录**：逐节点松弛在静定结构上完全正常，只有引入斜撑（使其超静定）才暴露。这是"局部正确的实现组合起来是错的"的典型案例——单看每一处代码都没问题。

## 关键设计决策

### 载荷流用不动点而非单向遍历

单向遍历无法处理"质量需要横向传递后再向下"的场景。改为工作队列 + 每构件记录已传递量，代价是有终止条件约束（`MaxLoadFlowSteps`），收益是能正确表达任意拓扑。

### 承载力必须随截面缩放

见 bug #6。轴向 ∝ 面积，抗弯 ∝ 截面模量（面积的 1.5 次方）。

### 悬臂构件需要显式标记

`Member.IsCantilever`。两端支撑的构件对半分配质量，悬臂全部挂在根部。不标记会导致甲板梁一半重量悬空。

### 材料比强度刻意不单调

见 `research.md` 3.3。这是有意的设计取舍，不是待修的 bug。已用 `Cascade_StrengthToWeightRatioIsNotMonotonic` 固定。

### 两个求解器并存，各自负责不同问题

| | `StructureSolver` | `TrussSolver` |
|---|---|---|
| 方法 | 自上而下的载荷流 + 工作队列 | 直接刚度法（矩阵解） |
| 擅长 | 竖向承重、浮力、倾覆、重力坍塌 | 侧向荷载、斜撑、静不定结构 |
| 复杂度 | O(构件数 × 迭代) | O(节点数³) |
| 尺寸上限 | 数百构件 | 数十节点（矩阵法固有限制） |

**为什么不用一个统一的方法**：重力流在几百个构件时比矩阵法快几个数量级，而竖向承重是每帧都要算的；矩阵法只在结构编辑后或受风时跑一次。这是刻意的性能分层，不是重复。

**什么时候需要合并**：如果刚架模型（P1-4）实现后两者差异缩小，应重新评估。

### 拉压分离承载力

细长构件受压强、受拉弱是真实的。`Member.Utilization` 按 `AxialLoadKn` 符号选择承载力。副作用是产生了一个真实的设计空间：X 撑（压杆）与拉杆不可互换，混凝土几乎不能做拉杆。

## 提交历史

| commit | 内容 |
|---|---|
| `8236727` | 三维交叉支撑验证：TrussSolver 直接刚度法 + 16 个测试 |
| `933be2c` | 技术验证结论、开发计划、进度文档 |
| `fa55e1d` | Unity 工程骨架 + 结构物理原型 + 36 个测试 |
| `2bd794a` | First Commit（仅 `docs/research.md`） |

## 待决策（阻塞下一步）

见 `plan.md` 第 5 节。简列：

1. v1 是否接受"单人 + 无 SLG"的收窄范围
2. 材料比强度取舍是否接受
3. 先做三维支撑还是失败恢复
4. 是否需要重写策划案（现有 2.7 排期与内容量不匹配）
