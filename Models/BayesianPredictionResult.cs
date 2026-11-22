using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LotteryTool.Models
{
    /// <summary>一次批量生成的结果汇总。</summary>
    public class BayesianPredictionResult
    {
        public List<PredictionResult> Predictions { get; set; } = new List<PredictionResult>();

        public int[] HotFrontNumbers { get; set; } = Array.Empty<int>();
        public int[] HotBackNumbers { get; set; } = Array.Empty<int>();
        public int[] ColdFrontNumbers { get; set; } = Array.Empty<int>();
        public int[] ColdBackNumbers { get; set; } = Array.Empty<int>();

        public string StrategyName { get; set; } = string.Empty;
        public int WindowSize { get; set; }
        public int TotalDraws { get; set; }
        public bool StructureFilterEnabled { get; set; }

        /// <summary>因采样次数用尽而放宽结构约束的注数（0 表示全部满足约束）</summary>
        public int RelaxedCount { get; set; }
        /// <summary>被去重逻辑丢弃的重复组合数量</summary>
        public int DuplicatesSkipped { get; set; }
        public double ElapsedMilliseconds { get; set; }

        public string ToReport()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== 号码生成结果 ===");
            sb.AppendLine();
            sb.AppendLine($"策略: {StrategyName} | 统计窗口: 最近 {WindowSize} 期 / 共 {TotalDraws} 期");
            sb.AppendLine($"结构过滤: {(StructureFilterEnabled ? "开启" : "关闭")} | " +
                          $"生成 {Predictions.Count} 注 | 耗时 {ElapsedMilliseconds:F0} ms");
            if (RelaxedCount > 0)
                sb.AppendLine($"注意: {RelaxedCount} 注因约束过严已放宽结构过滤");
            if (DuplicatesSkipped > 0)
                sb.AppendLine($"已跳过 {DuplicatesSkipped} 注重复组合");
            sb.AppendLine();
            sb.AppendLine(new string('=', 78));
            sb.AppendLine(string.Format("{0,-5}{1,-24}{2,-12}{3,-8}{4,-8}{5,-7}{6,-7}{7,-7}",
                "序号", "前区", "后区", "和值", "奇偶", "AC", "贴合度", "结构"));
            sb.AppendLine(new string('-', 78));

            int shown = Math.Min(Predictions.Count, 200);
            for (int i = 0; i < shown; i++)
            {
                var p = Predictions[i];
                string front = string.Join(" ", p.FrontNumbers.Select(n => n.ToString("D2")));
                string back = string.Join(" ", p.BackNumbers.Select(n => n.ToString("D2")));
                sb.AppendLine(string.Format("{0,-5}{1,-24}{2,-12}{3,-8}{4,-8}{5,-7}{6,-7}{7,-7}",
                    i + 1, front, back, p.FrontSum, p.OddEvenRatio, p.AC,
                    p.ModelScore.ToString("F1"), p.StructureValid ? "OK" : "放宽"));
            }
            if (Predictions.Count > shown)
                sb.AppendLine($"... 其余 {Predictions.Count - shown} 注已省略（可在表格中查看）");
            sb.AppendLine(new string('=', 78));

            sb.AppendLine();
            sb.AppendLine("=== 号码冷热分析 ===");
            sb.AppendLine($"热号(前区, 按加权频率): {Format(HotFrontNumbers)}");
            sb.AppendLine($"热号(后区, 按加权频率): {Format(HotBackNumbers)}");
            sb.AppendLine($"冷号(前区, 按当前遗漏): {Format(ColdFrontNumbers)}");
            sb.AppendLine($"冷号(后区, 按当前遗漏): {Format(ColdBackNumbers)}");
            sb.AppendLine();
            sb.AppendLine("说明: 「贴合度」只表示与历史统计特征的吻合程度，不代表中奖概率。");
            sb.AppendLine("      彩票每期独立随机开奖，任何策略都无法提高中奖概率，请理性购彩。");
            return sb.ToString();
        }

        private static string Format(int[] nums) =>
            nums == null || nums.Length == 0
                ? "无"
                : string.Join(" ", nums.Select(n => n.ToString("D2")));
    }
}
