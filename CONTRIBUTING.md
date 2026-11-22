# 贡献指南

感谢你愿意改进 LotteryTool。下面是让改动能顺利合入的必要信息。

## 环境要求

| 项目 | 要求 |
|---|---|
| 操作系统 | Windows 10 / 11（WinForms，仅 Windows） |
| 开发 SDK | .NET 8 SDK（`net8.0-windows`）或 Visual Studio 2022 17.8+ |
| 运行依赖 | `Microsoft.WindowsDesktop.App 8.x` |
| 编码 | 源码 UTF-8，换行不敏感（git 配置了 `core.autocrlf=true`） |

```bash
git clone https://github.com/chan3263/LotteryTool.git
cd LotteryTool
dotnet build -c Release
dotnet run --project LotteryTool.csproj
```

`大乐透数据.txt` 会在编译时复制到输出目录，程序启动自动加载，无需手动导入。

## 代码结构约定

- 分层：`Models/`（数据模型与结果对象）、`Services/`（数据层与算法引擎）、`MainForm.cs`（UI）
- `BayesianLotteryModel` 是 **partial class**，公共 API 在主文件，统计/权重/抽样/约束在 `.Stats.cs`
- 类名沿用 `BayesianLotteryModel` 只为兼容，v3.0 起**不是**朴素贝叶斯分类器，请勿按朴素贝叶斯改
- UI 控件全部代码创建（非设计器），样式集中在 `MainForm.cs` 的工厂方法与主题常量

### 几个必须遵守的坑

1. **`ModelStats` 与策略权重解耦**：`ComputeStats` 只描述历史数据；
   权重必须由 `BuildWeightVectors(s, opt, out fw, out bw)` 按**本次调用**的策略显式构建并传入
   `GenerateOne`。把权重塞回 stats 会导致 `Predict(count, opt)` 传入的策略不生效。
2. **`NumericUpDown` 初始化顺序**：必须「先 Minimum/Maximum、后 Value」，
   统一用 `MakeNumeric(min, max, value, ...)` 工厂。默认 `Maximum=100`，
   先写大初值会抛 `ArgumentOutOfRangeException` 并导致启动静默崩溃。
3. **`SplitContainer` 布局时机**：`SplitterDistance` 及 min size 一律推迟到 `MainForm_Load`，
   控件未布局时赋值会抛异常。
4. **随机源语义**：固定种子 → 每次生成从 `seed + 策略偏移` 重新开始（可复现）；
   未指定种子 → 复用同一条随机流（连续点击结果不同）。
5. **概率尺度**：`FrontProb` / `BackProb` 是归一化抽样概率（和为 1）；
   号码「每期出现率」= 概率 × 5（前区）/ × 2（后区）。UI 展示用 `AppearanceRate`。

## 算法验证

不要用 GUI 手工验证算法。建议建临时控制台项目，直接引用 `Models/*.cs` 与 `Services/*.cs`
跑真实数据回归，至少覆盖：

- 前区/后区概率和是否为 1
- 号码合法性（范围、不重复、升序）
- 大批量生成（≥ 20000 注）的性能、去重与结构约束满足率
- 固定种子复现、不同策略结果确实不同
- 各策略权重**倾向性**是否真的生效（不能只看随机流不同）
- 回测命中数与理论期望（前区 25/35、后区 4/12）对照

## 诚实原则（重要）

大乐透每期开奖是独立均匀随机事件，**任何策略都不改变期望中奖率**。因此：

- 不得在 UI、文档或报告中宣称「提高中奖率」「预测中奖」
- 「贴合度」只表示与历史统计特征的吻合程度，不是中奖置信度
- 回测必须保留与均匀随机基准的对照，结论要如实写明「无显著差异」
- 新增策略时请同步提供基准对照，避免把噪声波动包装成效果

## 提交规范

- commit message 用简短的祈使句，例如 `fix: 修正固定种子复现`、`docs: 补全构建说明`
- 一个 commit 只做一件事；改算法时附上回归验证结果
- 提交前确保 `dotnet build` 零警告零错误
- 不要提交 `bin/`、`obj/`、`.vs/`、`.workbuddy/`、`startup_error.log`（已在 `.gitignore`）

## 报告问题

请附上：复现步骤、程序版本、以及程序目录下的 `startup_error.log`（如有）。
若窗口完全不出现且没有日志，可查看 Windows 事件日志 `Application` 中
`.NET Runtime` 的 Id 1026 事件，其中含完整堆栈。
