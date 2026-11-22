using System;

namespace LotteryTool.Models
{
    /// <summary>单个号码的统计画像（用于"号码分析"与概率展示）。</summary>
    public sealed class NumberStatistic
    {
        public int Number { get; set; }
        public int TotalCount { get; set; }
        public int WindowCount { get; set; }
        /// <summary>时间衰减加权后的出现次数</summary>
        public double WeightedCount { get; set; }
        /// <summary>收缩后的归一化抽样概率（全部号码之和 = 1）</summary>
        public double Probability { get; set; }
        /// <summary>
        /// 估计的每期出现率 = Probability × 每期开出号码数。
        /// 均匀基准为前区 5/35 ≈ 14.29%、后区 2/12 ≈ 16.67%。
        /// </summary>
        public double AppearanceRate { get; set; }
        /// <summary>相对均匀基准的提升比：AppearanceRate / 均匀出现率</summary>
        public double Lift { get; set; }
        /// <summary>当前遗漏期数（距上次开出间隔了多少期）</summary>
        public int Omission { get; set; }
        /// <summary>历史最大遗漏期数</summary>
        public int MaxOmission { get; set; }
        /// <summary>平均出现间隔（期）</summary>
        public double AverageInterval { get; set; }
        /// <summary>最近 10 期是否出现过</summary>
        public bool IsRecentHot { get; set; }
    }

    /// <summary>号码统计汇总。</summary>
    public sealed class NumberStatisticsResult
    {
        public NumberStatistic[] Front { get; set; } = Array.Empty<NumberStatistic>();
        public NumberStatistic[] Back { get; set; } = Array.Empty<NumberStatistic>();
        public int WindowSize { get; set; }
        public int TotalDraws { get; set; }
        public double RecencyHalfLife { get; set; }
    }
}
