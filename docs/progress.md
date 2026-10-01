# 项目进度

> 最后更新：2026-10-02
>
> 本文档记录"已经发生了什么"。待做的事见 `plan.md`。

## 当前状态

**阶段**：技术可行性验证（垂直切片前置）

**核心结论**：立体建造的物理模型可行，无需自研引擎。但三维交叉支撑尚未验证，是第一大 USP 的最大风险。

**验证状态**：

| 项目 | 状态 |
|---|---|
| Unity 编译 | ✅ 零错误 |
| EditMode 测试 | ✅ 36/36 通过 |
| dotnet headless 测试 | ✅ 24/24 通过，约 40 ms |
| 三维交叉支撑 | ❌ 未验证 |
| 失败恢复机制 | ❌ 未定义 |
| 生存指标系统 | ❌ 未开始 |

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
│   │   │   ├── Material.cs        # 材料参数 + 承载力计算
│   │   │   ├── Member.cs          # 构件、节点、求解报告
│   │   │   └── StructureSolver.cs # 载荷流、浮力、倾覆、坍塌
│   │   └── Samples/Structures.cs  # 确定性测试夹具
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

**其中 1、2、3、6 是模型错误，只读代码发现不了，是测试逼出来的。** 写新系统时优先写能暴露单调性/守恒关系的测试。

## 关键设计决策

### 载荷流用不动点而非单向遍历

单向遍历无法处理"质量需要横向传递后再向下"的场景。改为工作队列 + 每构件记录已传递量，代价是有终止条件约束（`MaxLoadFlowSteps`），收益是能正确表达任意拓扑。

### 承载力必须随截面缩放

见 bug #6。轴向 ∝ 面积，抗弯 ∝ 截面模量（面积的 1.5 次方）。

### 悬臂构件需要显式标记

`Member.IsCantilever`。两端支撑的构件对半分配质量，悬臂全部挂在根部。不标记会导致甲板梁一半重量悬空。

### 材料比强度刻意不单调

见 `research.md` 3.3。这是有意的设计取舍，不是待修的 bug。已用 `Cascade_StrengthToWeightRatioIsNotMonotonic` 固定。

## 提交历史

| commit | 内容 |
|---|---|
| `fa55e1d` | Unity 工程骨架 + 结构物理原型 + 36 个测试 |
| `2bd794a` | First Commit（仅 `docs/research.md`） |

## 待决策（阻塞下一步）

见 `plan.md` 第 5 节。简列：

1. v1 是否接受"单人 + 无 SLG"的收窄范围
2. 材料比强度取舍是否接受
3. 先做三维支撑还是失败恢复
4. 是否需要重写策划案（现有 2.7 排期与内容量不匹配）
