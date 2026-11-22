# 更新日志

本项目遵循 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/) 的格式约定，
版本号采用 `主版本.次版本.修订号`。所有 notable 变更记录在此，完整版本说明见 README。

## [Unreleased]

### 文档
- 补全项目文档体系：README 增加仓库地址、克隆/构建说明、环境要求、界面截图、
  参数汇总表、回测实测结论、FAQ 与许可证章节
- 新增 `LICENSE`（MIT）、`CONTRIBUTING.md`、`docs/ALGORITHM.md`（算法与验证细节）
- 新增界面截图 `docs/screenshots/main.png`、`docs/screenshots/prediction.png`
- 统一版本号表述为 v3.0.1

## [v3.0.1] - 2026-11-21

### 修复
- 修复启动即退出且**无任何提示**的问题：`NumericUpDown` 对象初始化器按书写顺序赋值，
  先写 `Value = 2026` 时 `Maximum` 仍是默认值 100，抛出 `ArgumentOutOfRangeException`；
  统一改用 `MakeNumeric(min, max, value, ...)` 工厂，先设范围再设值
- `SplitContainer` 的 `SplitterDistance` / `Panel1MinSize` / `Panel2MinSize` 改为在
  `MainForm_Load`（布局完成后）设置，避免控件未布局时校验冲突
- 新增全局异常兜底：`ThreadException` + `UnhandledException` + `Main` try/catch，
  出错弹窗并写 `startup_error.log`，不再静默退出

### 变更
- 界面重做：根 `TableLayoutPanel` + 左右 `SplitContainer` 分栏、卡片式分区、
  深蓝标题栏与版本徽标、表格配色（前区蓝字 / 后区红字）、统一字体与按钮样式
- 开启高 DPI 支持；窗口尺寸按屏幕工作区自动钳制
- 预测结果窗口、长报告窗口同步套用新样式
- 左侧五张卡片收紧为一屏完整显示，无需滚动

## [v3.0] - 2026-11-21

### 新增
- 4 种生成策略：均衡 / 均匀随机（基准）/ 热号加权 / 冷号回补
- 结构约束：和值、奇偶、大小、连号、AC 值、与上期重复数
- 组合去重、可复现固定种子、可解释「贴合度」评分
- 号码画像：出现率、提升比、遗漏、平均间隔、近 10 期热度
- **Walk-forward 回测**与均匀随机基准对照，如实展示策略无显著优势
- 新版数据层：多日期格式 / 混用号码位数解析、逐行校验、按期号去重、质量报告、UTF-8 导出

### 修复
- 随机源：`new Random(Guid...)` 高频创建 → 单一可复现随机源 + 策略独立流
- 先验归一化：前区总和应为 5 却按 1 归一 → Dirichlet 平滑，概率严格和为 1
- `∏(1+likelihood)` 伪贝叶斯连乘 → 共现 β 收缩估计，`lift` 仅用于评分
- 50 期窗口直接估 `P(A|B)`（期望共现 < 1 次，几乎纯噪声）→ 收缩回边际
- `N > 数据量` 时静默退化成均匀随机 → `min(N, Count)` 始终有效
- 后区选号未走依赖更新 → 前后区统一处理
- 逐次轮盘赌 + 手工改概率 → Gumbel-top-k 指数竞赛抽样
- 「置信度」易误解为中奖率 → 明确标注为「贴合度」并声明与中奖概率无关

### 性能
- 二维数组预计算 + 惰性重算脏标记；2738 期数据下生成 20000 注约 146 ms，
  200 期 × 20 注回测约 87 ms

## [v2.0] - 2026-09-06

- 合并 LotteryAnalysisApp 与 LotteryPrediction 为统一 WinForms 工具
- 新增特定号码关联分析

## [v1.0]

- 三个独立项目：LotteryAnalysisApp / LotteryPrediction / 控制台版大乐透
