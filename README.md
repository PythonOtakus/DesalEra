# CrazyAquarium / 狂涛纪元

海平面上升后的海上生存建造。当前状态：**可玩的灰盒垂直切片**。

![灰盒截图](docs/screenshot.png)

## 怎么运行

**已构建的可执行文件**（70 MB）：

```
Builds/CrazyAquarium.exe
```

**从源码重新构建**：

```powershell
$unity = "C:\Program Files\Unity\Hub\Editor\2022.3.62f3c1\Editor\Unity.exe"
$proj = "C:\Users\Anantian\source\repos\CrazyAquarium"

& $unity -batchmode -nographics -quit -projectPath $proj `
  -executeMethod CrazyAquarium.EditorTools.SceneBuilder.BuildPlayer `
  -logFile "$env:TEMP\build.log"
```

或者在编辑器里：菜单 `CrazyAquarium → Build Windows Player`。

## 操作

| 键 | 作用 |
|---|---|
| WASD | 走动（相机相对） |
| Shift | 疾行 |
| 滚轮 | 切换建造件 |
| 左键 | 建造 |
| 右键 | 拆除（返还一半材料） |
| E | 吃一份口粮 |

## 现在的玩法

1. 木筏漂浮在水上，你在甲板上走动
2. 走向漂浮的残骸自动打捞，获得木板/废料/金属/淡水/食物
3. 用材料扩建：甲板、支撑柱、浮筒、斜撑、海水淡化器
4. 建造会实时调用结构求解器 —— 浮力、风载、应力
5. 生存值随时间下降：饥饿、缺水、体力、健康
6. 周期性风暴，风载从 1.2 升到 14 kN/m，可能把结构吹垮
7. 造得太重会下沉，构件会按应力从绿变红直到断裂

## 架构

```
Assets/Scripts/
├── Core/                 # 无引擎依赖，可 dotnet 直接测
│   ├── Structure/        # 求解器：重力流 + 桁架刚度法
│   ├── Game/             # 生存、库存、建造规则
│   └── Samples/          # 确定性测试夹具
├── Unity/                # 表现层，全部运行时构建
└── Tests/EditMode/       # 75 个测试
```

**关键约束：场景文件只有 180 行。** 整个世界（相机、灯光、海面、木筏、玩家）在运行时由代码生成。原因写在 `docs/progress.md` —— `.unity` 是 GUID 链接的 YAML，人和 agent 都难以可靠编辑。

## 测试

```powershell
# Unity EditMode
& "C:\Program Files\Unity\Hub\Editor\2022.3.62f3c1\Editor\Unity.exe" -batchmode -nographics `
  -runTests -testPlatform EditMode -projectPath "C:\Users\Anantian\source\repos\CrazyAquarium" `
  -testResults "$env:TEMP\results.xml" -logFile "$env:TEMP\tests.log"

# 或纯 dotnet，约 40ms，不启动编辑器
```

当前：**75/75 通过**。

## 素材来源

全部 CC0，来自 [ambientCG](https://ambientcg.com)：

- `rust_Metal063.jpg` — Metal 063
- `woodfloor_WoodFloor064.jpg` — Wood Floor 064
- `wood_Wood035.jpg` — Wood 035
- `concrete_Concrete034.jpg` — Concrete 034
- `plaster_Plaster001.jpg` — Plaster 001

## 已知问题

- **玩家角色不可见**：角色没有渲染器，需要加一个可见代理
- **画面偏暗**：环境光和雾需要调
- **没有音效**
- **斜撑效果在单跨度下不明显**：纯轴力模型不含弯曲刚度，见 `docs/research.md` 3.5.3
- **没有存档**

## 文档

- `docs/research.md` — 市场调研 + 策划案 + 技术验证实测数据
- `docs/plan.md` — 待办计划、验收标准、风险登记
- `docs/progress.md` — 环境事实、验证命令、bug 根因
