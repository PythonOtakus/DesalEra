# DesalEra / 淡化器纪元

海平面上升后的海上生存建造。当前状态：**可玩的灰盒垂直切片**。

![灰盒截图](docs/screenshot.png)

> 截图由游戏相机直接渲染得到，不含 HUD 的 OnGUI 叠加层。

## 怎么运行

**已构建的可执行文件**（70 MB）：

```
Builds/DesalEra.exe
```

**从源码重新构建**：

```powershell
$unity = "C:\Program Files\Unity\Hub\Editor\2022.3.62f3c1\Editor\Unity.exe"
$proj = "C:\Users\Anantian\source\repos\DesalEra"

& $unity -batchmode -nographics -quit -projectPath $proj `
  -executeMethod DesalEra.EditorTools.SceneBuilder.BuildPlayer `
  -logFile "$env:TEMP\build.log"
```

或者在编辑器里：菜单 `DesalEra → Build Windows Player`。

## 操作

第三人称越肩视角，右键拖动转视角。

| 键 | 作用 |
|---|---|
| WASD | 走动（相机相对）；走上甲板是走，走出甲板是游泳 |
| Shift | 疾行（水中无效） |
| 右键拖动 | 转动视角 |
| 滚轮 | 拉近／拉远 |
| 1–5 | 选择建造件：甲板 / 支撑柱 / 浮筒 / 斜撑 / 蒸馏器 |
| 左键 | 建造（需站在甲板上） |
| 右键单击 | 拆除（返还一半材料） |
| E | 吃一份口粮 |

滚轮从"切换建造件"改为相机缩放，建造件移到数字键：一个输入只做一件事，
而滚轮作为相机控制更值钱。

## 现在的玩法

1. 木筏漂浮在水上，你在甲板上走动，走出甲板即下水游泳
2. 游到漂浮的残骸处自动打捞，获得木板/废料/金属/淡水/食物
3. 回到甲板用材料扩建：甲板、支撑柱、浮筒、斜撑、海水淡化器
4. 建造会实时调用结构求解器 —— 浮力、风载、应力
5. 建甲板会真的扩大可走范围
6. 生存值随时间下降：饥饿、缺水、体力、健康
7. 周期性风暴，风载从 1.2 升到 14 kN/m，可能把结构吹垮
8. 造得太重会下沉，构件会按应力从绿变红直到断裂

## 架构

```
Assets/Scripts/
├── Core/                 # 无引擎依赖，可 dotnet 直接测
│   ├── Structure/        # 求解器：重力流 + 桁架刚度法
│   ├── Game/             # 生存、库存、建造规则
│   └── Samples/          # 确定性测试夹具
├── Unity/                # 表现层，全部运行时构建
└── Tests/EditMode/       # 106 个测试
```

**关键约束：场景文件只有 180 行。** 整个世界（相机、灯光、海面、木筏、玩家）在运行时由代码生成。原因写在 `docs/progress.md` —— `.unity` 是 GUID 链接的 YAML，人和 agent 都难以可靠编辑。

### 相机与移动

`ThirdPersonCamera` 是第三人称环绕相机：右键拖动转视角，滚轮缩放，距离
2.4–20 m，俯角 −14°–74°，带 0.6 m 偏肩偏移。

不用锁定指针是刻意的。锁定指针是纯Third-person做法，但建造用左键放置，
隐藏光标就无从下手。拖动转视角让双手都在鼠标上，左键保持可用。

移动带**加减速**（加速 34 m/s²、减速 26 m/s²），不是瞬时启停。恒速移动
一松键就停死，正是"光标操作建造游戏"的手感特征，而不是角色的手感。转向也
只在真的在移动时发生——站着不动时朝目标方向转身是很常见也很干扰的破绽。

已知缺口：世界里还没有任何碰撞体，所以相机的 SphereCast 遮挡拉近暂时不生效，
角色也不会被木筏挡住。补碰撞体是下一步。

## 测试

```powershell
# Unity EditMode
& "C:\Program Files\Unity\Hub\Editor\2022.3.62f3c1\Editor\Unity.exe" -batchmode -nographics `
  -runTests -testPlatform EditMode -projectPath "C:\Users\Anantian\source\repos\DesalEra" `
  -testResults "$env:TEMP\results.xml" -logFile "$env:TEMP\tests.log"

# 或纯 dotnet，约 40ms，不启动编辑器
```

当前：**106/106 通过**（新增 31 个覆盖材质通道、颜色空间与网格 UV）。

## 素材来源

角色为 Meshy AI 生成（银发街机风格幸存者），模型与动画为独立 FBX 导出，
经 `SurvivorClipBaker` 重定向后烘焙进工程。

其余贴图全部 CC0，来自 [ambientCG](https://ambientcg.com)，且是完整 PBR 集而不只是
反照率：

- `rust_Metal063` — Metal 063
- `woodfloor_WoodFloor064` — Wood Floor 064
- `wood_Wood035` — Wood 035
- `concrete_Concrete034` — Concrete 034
- `plaster_Plaster001` — Plaster 001

### 材质管线

每个材质落地三个文件，通道名由反照率文件名派生（`rust_Metal063.jpg` 加
`Roughness` 得到 `rust_Metal063_Roughness.jpg`），所以新增一个表面只需在
`MaterialLibrary.Defaults` 里加一行，不会出现三个文件名互相漂移。

**曾经发出去的"贴图"是 ambientCG 包里的预览球渲染图。** 那个 zip 里同时装着真实
贴图和一张球体预览，之前被当成反照率存了进来 —— 一切都加载成功、没有任何报错，
整个木筏却是在给一张球的照片贴图。通道名派生和 `MaterialTests` 里的像素级断言
就是为了钉死这一类错误。

三个通道各有各的坑，都写进了代码注释：

- **法线用 `NormalGL`**。同一个包里还有 `NormalDX`，绿通道相反，拿错会让凹陷看起来
  像凸起。
- **法线和粗糙度必须按线性加载**。法线存的是方向不是颜色，过一遍 sRGB 解码会提亮
  蓝通道，所有法线因此偏离垂直。`LoadImage` 不保证保留构造函数的 linear 标志，所以
  加载后用 `GraphicsFormatUtility.IsSRGBFormat` 实测，不靠假设。
- **粗糙度要重打包到 gloss map 的 alpha**。内置 Standard 与 URP/Lit 都从 gloss map
  的 **alpha** 通道读 smoothness，方向还是反的。灰度 jpg 的 alpha 恒为 1，直接塞进去
  整个木筏会变成镜面铬。

法线要有意义，UV 就得对，所以构件网格也一并换了：原来的 `UnitColumn` 给六个面
同一个 0–1 UV，把缩放交给 transform —— 而 Unity 不会把缩放应用到 UV，于是 3 m 长的
梁只有一格贴图、还顺着长度抹开。现在 `MeshFactory.Beam` 按米生成 UV，每个面用自己
那两条轴，尺寸按类型缓存复用（初始木筏只有少数几种长度，几十根构件塌缩成几个网格），
并生成切线。法线贴图没有切线会退化成屏幕空间导数，在 UV 不连续处开缝。

### 角色动画管线

Meshy 把角色和动画导出成一批互不兼容的 FBX，直接把动画套到角色上会失败。
`SurvivorClipBaker`（菜单 `DesalEra/Bake Survivor Animation`）负责解决：

1. **曲线路径重映射** — 动画骨架叫 `target_character/mixamorig:Hips`，角色骨架叫
   `Armature/Hips`。baker 按最长无歧义后缀匹配把每条曲线落到角色真实骨骼上。
2. **绑定姿势差分重定向** — 每个 FBX 的参考姿势都不同（角色 `LeftArm` 静止角
   `(352,240,2)`，Idle 导出是 `(49,275,77)`，游泳是 `(50,264,3)`）。动画存的是绝对
   局部旋转，直接套用会让双臂举过头顶、头部前俯。每条曲线按
   `charRest * inverse(animRest) * src(t)` 换算，逐文件读取各自的参考姿势。
3. **烘焙为独立 `.anim`** — FBX 里的 clip 是子资源，运行时无法按名加载。baker 为每个
   动画输出一个 `Survivor_<State>.anim`。

baker 自动发现 `Assets/Art/Characters/Survivor/Animations/` 下的所有 FBX，文件名去掉
`Survivor_` 前缀即 clip 名，新增动画不需要改代码。

已烘焙 **12 个 clip**：`Idle`、`Walk`、`Run`、`Jump`、`JumpObstacle`、`SwimIdle`、
`SwimForward`、`LadderMountStart`、`LadderClimbLoop`、`LadderClimbFinish`、
`RopeHangIdle`、`RopeSwingToGround`。当前玩法只用到 Idle / Walk / Run，其余已接进
图里待接线（梯子与绳索还没有对应的玩法）。

### 游泳

初始木筏只有 6 m 见方，漂浮物却分布在 12–34 m 的环上。玩家原本被夹在甲板上，
14 个漂浮物里有 8 个在几何上根本够不到——最近的一个，玩家能站到的最近距离是
17.88 m，而拾取判定半径只有 1.9 m。核心循环因此是断的。

现在玩家可以下水：`IsOverDeck` 用甲板层关节的 XZ **凸包**判定，走出凸包即切换到
游泳，动画换成 `SwimForward` / `SwimIdle`。这不只是补丁——"打捞海上漂流资源、潜水
探索"本来就是这个项目要对标的核心玩法（`docs/research.md` 1.1）。

几条规则：

- **游泳不能冲刺**。否则游水永远劣于走甲板，玩家没有扩建甲板的理由。
- **游泳额外消耗体力**（1.1/秒，在移动消耗之上）。同上。
- **水中不能建造和拆除**。站远了往木筏上放甲板不叫建造，叫远程施工。
- **建甲板会真的扩大可走范围**，因为判定读的是木筏自己的footprint，不是硬编码半径。

顺带修掉一个更早的视觉问题：初始木筏的"甲板"只是四根周边横梁，中间是空的，
而玩家出生点就在原点——截图里角色一直站在一个方框的空洞上。给中心补上两根横梁
可以让它变成真正的平台，但那会让桁架刚度矩阵奇异、`Raft_OpeningStateIsStructurallyStable`
失败：原结构本身就是数值上勉强收敛的，补一根构件就把它推过检测阈值。这属于
`docs/plan.md` P1-4（刚架弯曲刚度）推迟的那块，因此**没有动物理结构**，改用
footprint 判定解决玩法问题。甲板中间没有铺板是已知的视觉缺陷。

**运行时用 Playables 图，不用 AnimatorController。** 脚本创建的 `AnimatorState` 不是
合法子资源：它能写进资产文件，但资产一旦被重新导入（构建时的 `AssetDatabase.Refresh()`
就会触发）状态全部丢失。编辑器 Play 模式用的是内存对象，所以每次测试都通过；而构建版
拿到的是「1 个层、0 个状态」的 controller，角色直接退回绑定姿势——也就是 T-pose，且
没有任何报错说明真实原因。Playables 图没有资产可序列化，编辑器与构建版跑的是同一份
代码，不可能再分叉。

烘焙必须在 Edit 模式运行：重建资产会让正在运行的玩家持有已销毁的引用，唯一症状是
角色悄悄不动。baker 现在会直接拒绝在 Play 模式执行。

烘焙后仍需**在构建版里验证**：Play 模式全部通过并不能证明构建版可用，这个坑就是这样
漏出去的。路径全对、零未命中解析，也不代表姿势正确——绑定姿势错配只有出图才能发现。

### T-pose 修正

Meshy 把 12 个动画全部以「双臂水平张开」为中立姿势，实测静止时肩到腕只下垂 7 cm
而手臂长 54 cm，所以角色像稻草人。修正写在 baker 里，烘焙进每条曲线，不占运行时
开销：

- **旋转量从骨架几何反推**，不是写死的欧拉角。旋转轴取「当前指向子节点的方向」与
  「正下方」的叉积，因此换模型也不会错。
- **分散到三个关节**：锁骨 45%、上臂 40%、前臂 15%。只转锁骨会把腋下蒙皮撕开
  一个洞——肩部顶点同时蒙皮到脊柱、锁骨和上臂，单关节 67° 的旋转会把它们拉开。
  分摊后每个关节的形变都小得多，这也是 rigger 修正 A-pose 从不只动一个关节的原因。
- 实测结果：手到肩的下垂量 0.07 m → 0.54 m（等于整条手臂长度），手臂长度不变。

## 已知问题

- **画面偏暗**：环境光和雾需要调。之前这条还有一半的原因是材质本身不对（贴图是预览
  球渲染图），那部分已修；剩下的是 `GameEntry.BuildLighting` 的数值问题
- **没有音效**
- **斜撑效果在单跨度下不明显**：纯轴力模型不含弯曲刚度，见 `docs/research.md` 3.5.3
- **没有存档**
- **部分骨骼未烘焙**：模型缺少 `LeftToe_End` 等末端辅助骨骼，`Spine1` 与模型侧
  `Spine01` 命名不一致，这部分脊柱与脚趾不动
- **没有环境贴图**：一盏方向光 + Trilight 环境光 + 指数雾。太阳方向接近垂直
  （`lightDirection` = `(0.4,-0.8,0.45)`），立面几乎吃不到直射光，所以现在这些
  表面基本只靠环境光照亮 —— 这才是"偏暗"剩下的原因，不是材质
- **`GameBootstrap.pickupRespawnSeconds` 是死字段**：序列化了但没有任何代码读它

## 文档

- `docs/research.md` — 市场调研 + 策划案 + 技术验证实测数据
- `docs/plan.md` — 待办计划、验收标准、风险登记
- `docs/progress.md` — 环境事实、验证命令、bug 根因
- `docs/unity-cli.md` — UnityCLI 实时调试用法
