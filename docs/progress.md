# 项目进度

> 最后更新：2026-10-02
>
> 本文档记录"已经发生了什么"。待做的事见 `plan.md`。

## 当前状态

**阶段**：可玩灰盒垂直切片已交付

**核心结论**：立体建造的物理模型可行，无需自研引擎。三维交叉支撑已验证，第一大 USP 技术上成立。灰盒可运行。

**验证状态**：

| 项目 | 状态 |
|---|---|
| Unity 编译 | ✅ 零错误 |
| EditMode 测试 | ✅ 75/75 通过 |
| dotnet headless 测试 | ✅ 24/24 通过，约 40 ms |
| **可执行文件构建** | ✅ `Builds/CrazyAquarium.exe` 90 MB（含角色资产） |
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
且不驱动骨骼。改用 Mecanim + 烘焙的 `AnimatorController`。

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
CrazyAquarium/
├── Assets/Scripts/
│   ├── Core/                      # 求解器，无表现逻辑
│   │   ├── CrazyAquarium.Core.asmdef
│   │   ├── Structure/
│   │   │   ├── Material.cs        # 材料参数、承载力、弹性模量
│   │   │   ├── Member.cs          # 构件、节点、求解报告
│   │   │   ├── StructureSolver.cs # 重力流：载荷、浮力、倾覆、坍塌
│   │   │   └── TrussSolver.cs     # 直接刚度法：矩阵解算、侧向分析
│   │   └── Samples/
│   │       ├── Structures.cs      # 确定性测试夹具（筏架、塔、偏载筏）
│   │       └── BracedFrames.cs    # 带斜撑的框架夹具
│   ├── Unity/                     # 表现层
│   │   ├── CrazyAquarium.Unity.asmdef
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
$proj = "C:\Users\Anantian\source\repos\CrazyAquarium"
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
$core = "C:\Users\Anantian\source\repos\CrazyAquarium\Assets\Scripts\Core"
$tests = "C:\Users\Anantian\source\repos\CrazyAquarium\Assets\Scripts\Tests\EditMode"
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
