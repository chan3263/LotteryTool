using System;
using System.Linq;

namespace LotteryTool.Models
{
    /// <summary>一注预测（生成）号码及其可解释的评分明细。</summary>
    public class PredictionResult
    {
        public int[] FrontNumbers { get; set; } = new int[5];
        public int[] BackNumbers { get; set; } = new int[2];

        /// <summary>
        /// 模型贴合度评分 0~100。仅表示该组合与历史统计特征（频率、共现、结构）的贴合程度，
        /// 与中奖概率无关。50 分表示与均匀随机基准相当。
        /// </summary>
        public double ModelScore { get; set; }

        /// <summary>兼容旧版字段：等价于 ModelScore / 100。请勿理解为中奖置信度。</summary>
        public double Confidence
        {
            get => ModelScore / 100.0;
            set => ModelScore = value * 100.0;
        }

        public string StrategyName { get; set; } = string.Empty;

        // ---- 组合结构特征（用于展示与校验）----
        public int FrontSum { get; set; }
        public int BackSum { get; set; }
        public int OddCount { get; set; }
        public int BigCount { get; set; }
        public int ConsecutivePairs { get; set; }
        public int AC { get; set; }
        public int RepeatWithLastDraw { get; set; }
        public bool StructureValid { get; set; }

        /// <summary>奇偶比描述，如 "3:2"</summary>
        public string OddEvenRatio => $"{OddCount}:{5 - OddCount}";
        /// <summary>大小比描述，如 "2:3"</summary>
        public string BigSmallRatio => $"{BigCount}:{5 - BigCount}";

        public string CombinationKey =>
            string.Join(",", FrontNumbers) + "|" + string.Join(",", BackNumbers);

        public override string ToString()
        {
            string front = string.Join(" ", FrontNumbers.Select(n => n.ToString("D2")));
            string back = string.Join(" ", BackNumbers.Select(n => n.ToString("D2")));
            return $"前区: {front} | 后区: {back} | 贴合度: {ModelScore:F1}/100";
        }

        public void RecalculateFeatures(int[]? lastFront = null)
        {
            FrontSum = FrontNumbers.Sum();
            BackSum = BackNumbers.Sum();
            OddCount = FrontNumbers.Count(n => n % 2 != 0);
            BigCount = FrontNumbers.Count(n => n >= 18);

            var sorted = FrontNumbers.OrderBy(n => n).ToArray();
            ConsecutivePairs = 0;
            for (int i = 1; i < sorted.Length; i++)
                if (sorted[i] - sorted[i - 1] == 1) ConsecutivePairs++;

            var diffs = new System.Collections.Generic.HashSet<int>();
            for (int i = 0; i < sorted.Length; i++)
                for (int j = i + 1; j < sorted.Length; j++)
                    diffs.Add(sorted[j] - sorted[i]);
            AC = diffs.Count - (sorted.Length - 1);

            RepeatWithLastDraw = lastFront == null
                ? 0
                : FrontNumbers.Count(n => lastFront.Contains(n));
        }
    }
}
