using System;

namespace LotteryTool.Models
{
    /// <summary>
    /// 时间序列 walk-forward 回测结果：模型策略 vs 均匀随机基准。
    /// 每一步只用"该期之前"的数据训练，预测下一期，避免使用未来信息。
    /// </summary>
    public sealed class BacktestResult
    {
        public string StrategyName { get; set; } = string.Empty;
        public int TestedDraws { get; set; }
        public int BetsPerDraw { get; set; }
        public int TotalBets => TestedDraws * BetsPerDraw;

        public double ModelAvgFrontHits { get; set; }
        public double RandomAvgFrontHits { get; set; }
        public double ModelAvgBackHits { get; set; }
        public double RandomAvgBackHits { get; set; }
        public double ModelAvgTotalHits { get; set; }
        public double RandomAvgTotalHits { get; set; }

        /// <summary>前区命中 0~5 个的注数分布</summary>
        public int[] ModelFrontHitHistogram { get; set; } = new int[6];
        public int[] RandomFrontHitHistogram { get; set; } = new int[6];
        /// <summary>后区命中 0~2 个的注数分布</summary>
        public int[] ModelBackHitHistogram { get; set; } = new int[3];
        public int[] RandomBackHitHistogram { get; set; } = new int[3];

        public int ModelJackpots { get; set; }
        public int RandomJackpots { get; set; }

        /// <summary>理论期望（均匀随机）：前区 5*5/35，后区 2*2/12</summary>
        public static double TheoreticalFrontHits => 25.0 / 35.0;
        public static double TheoreticalBackHits => 4.0 / 12.0;

        /// <summary>相对基准的差异（百分点意义上的命中数差）</summary>
        public double FrontHitDelta => ModelAvgFrontHits - RandomAvgFrontHits;
        public double BackHitDelta => ModelAvgBackHits - RandomAvgBackHits;

        public string Conclusion { get; set; } = string.Empty;

        public string ToReport()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("=== Walk-forward 回测报告 ===");
            sb.AppendLine();
            sb.AppendLine($"策略: {StrategyName}");
            sb.AppendLine($"测试期数: {TestedDraws} 期 | 每期注数: {BetsPerDraw} 注 | 合计: {TotalBets} 注");
            sb.AppendLine($"训练方式: 每期仅使用该期之前的历史数据（无未来信息泄漏）");
            sb.AppendLine($"对照组: 均匀随机选号（与真实开奖机制一致）");
            sb.AppendLine();
            sb.AppendLine(new string('=', 62));
            sb.AppendLine(string.Format("{0,-22}{1,-14}{2,-14}{3,-12}",
                "指标", "模型策略", "随机基准", "理论期望"));
            sb.AppendLine(new string('-', 62));
            sb.AppendLine(string.Format("{0,-22}{1,-14:F4}{2,-14:F4}{3,-12:F4}",
                "前区平均命中", ModelAvgFrontHits, RandomAvgFrontHits, TheoreticalFrontHits));
            sb.AppendLine(string.Format("{0,-22}{1,-14:F4}{2,-14:F4}{3,-12:F4}",
                "后区平均命中", ModelAvgBackHits, RandomAvgBackHits, TheoreticalBackHits));
            sb.AppendLine(string.Format("{0,-22}{1,-14:F4}{2,-14:F4}{3,-12:F4}",
                "总平均命中", ModelAvgTotalHits, RandomAvgTotalHits,
                TheoreticalFrontHits + TheoreticalBackHits));
            sb.AppendLine(new string('=', 62));
            sb.AppendLine();
            sb.AppendLine("前区命中个数分布 (模型 / 随机):");
            for (int i = 0; i <= 5; i++)
                sb.AppendLine($"  命中 {i} 个: {ModelFrontHitHistogram[i],7} / {RandomFrontHitHistogram[i],7}");
            sb.AppendLine();
            sb.AppendLine("后区命中个数分布 (模型 / 随机):");
            for (int i = 0; i <= 2; i++)
                sb.AppendLine($"  命中 {i} 个: {ModelBackHitHistogram[i],7} / {RandomBackHitHistogram[i],7}");
            sb.AppendLine();
            sb.AppendLine($"一等奖(5+2)命中: 模型 {ModelJackpots} 次 / 随机 {RandomJackpots} 次");
            sb.AppendLine();
            sb.AppendLine("【结论】");
            sb.AppendLine(Conclusion);
            sb.AppendLine();
            sb.AppendLine("提示: 本工具仅用于数据分析与学习研究，预测结果不构成投注建议。");
            return sb.ToString();
        }
    }
}
