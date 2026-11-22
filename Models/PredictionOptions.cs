using System;

namespace LotteryTool.Models
{
    /// <summary>
    /// 选号策略。
    ///
    /// 重要说明：大乐透每期开奖是独立、均匀的随机事件，任何基于历史数据的策略
    /// 都无法提高中奖概率。策略只决定"按什么分布生成号码"，用于统计研究与娱乐，
    /// 不构成投注建议。Uniform 是与真实开奖机制一致的数学基准。
    /// </summary>
    public enum GenerationStrategy
    {
        /// <summary>均匀随机（与真实开奖机制一致，作为对照基准）</summary>
        Uniform = 0,

        /// <summary>频率加权：倾向近期高频号码（热号）</summary>
        Hot = 1,

        /// <summary>遗漏回补：倾向长期未开出的号码（冷号）</summary>
        Cold = 2,

        /// <summary>温和加权 + 结构约束（默认）：避免极端偏态，并过滤掉罕见形态组合</summary>
        Balanced = 3
    }

    /// <summary>
    /// 组合结构约束。用于避免生成现实中极少出现的形态（如全奇、全小、和值极端、
    /// 连号过多、AC 值过低）。注意：这只是形态过滤，不改变任何号码的中奖概率。
    /// </summary>
    public sealed class CombinationConstraints
    {
        /// <summary>前区 5 个号码之和的允许范围（理论均值 90，标准差约 22.6）</summary>
        public int FrontSumMin { get; set; } = 55;
        public int FrontSumMax { get; set; } = 130;

        /// <summary>后区 2 个号码之和的允许范围（理论均值 13）</summary>
        public int BackSumMin { get; set; } = 5;
        public int BackSumMax { get; set; } = 21;

        /// <summary>前区奇数个数范围（排除全奇 / 全偶）</summary>
        public int MinOddCount { get; set; } = 1;
        public int MaxOddCount { get; set; } = 4;

        /// <summary>前区大号（>= 18）个数范围（排除全大 / 全小）</summary>
        public int MinBigCount { get; set; } = 1;
        public int MaxBigCount { get; set; } = 4;

        /// <summary>相邻连号对数上限（如 08 09 记 1 对）</summary>
        public int MaxConsecutivePairs { get; set; } = 2;

        /// <summary>AC 值（算术复杂性）下限，标准定义 = 两两差的不同值个数 - (n-1)</summary>
        public int MinAC { get; set; } = 3;

        /// <summary>与上一期前区号码的重复个数上限</summary>
        public int MaxRepeatWithLastDraw { get; set; } = 3;

        /// <summary>是否要求三个区间（1-12 / 13-24 / 25-35）各至少出现 1 个号码</summary>
        public bool RequireThreeZonesCovered { get; set; } = false;

        public CombinationConstraints Clone()
        {
            return new CombinationConstraints
            {
                FrontSumMin = FrontSumMin,
                FrontSumMax = FrontSumMax,
                BackSumMin = BackSumMin,
                BackSumMax = BackSumMax,
                MinOddCount = MinOddCount,
                MaxOddCount = MaxOddCount,
                MinBigCount = MinBigCount,
                MaxBigCount = MaxBigCount,
                MaxConsecutivePairs = MaxConsecutivePairs,
                MinAC = MinAC,
                MaxRepeatWithLastDraw = MaxRepeatWithLastDraw,
                RequireThreeZonesCovered = RequireThreeZonesCovered
            };
        }
    }

    /// <summary>模型与生成参数。</summary>
    public sealed class PredictionOptions
    {
        /// <summary>选号策略，默认 Balanced</summary>
        public GenerationStrategy Strategy { get; set; } = GenerationStrategy.Balanced;

        /// <summary>是否启用组合结构过滤</summary>
        public bool EnableStructureFilter { get; set; } = true;

        /// <summary>是否对生成的组合去重（大批量生成时有效）</summary>
        public bool AvoidDuplicateCombinations { get; set; } = true;

        /// <summary>
        /// 随机种子。为 null 时使用时间相关的真随机源；设置为固定值可完全复现结果，
        /// 便于回测与调试。
        /// </summary>
        public int? RandomSeed { get; set; } = null;

        /// <summary>
        /// 权重强度 0~2。0 等价于均匀随机，越大越偏向模型估计的号码分布。
        /// 仅对 Hot / Cold / Balanced 生效。
        /// </summary>
        public double WeightStrength { get; set; } = 1.0;

        /// <summary>单注组合的最大采样尝试次数（结构过滤 + 去重失败时的兜底上限）</summary>
        public int MaxAttemptsPerCombination { get; set; } = 200;

        /// <summary>结构约束</summary>
        public CombinationConstraints Constraints { get; set; } = new CombinationConstraints();

        /// <summary>时间衰减半衰期（期数）。越小越看重近期数据；0 表示不衰减</summary>
        public double RecencyHalfLife { get; set; } = 0;

        /// <summary>边际概率的 Dirichlet 伪计数强度，越大越向均匀分布收缩（抑制小样本过拟合）</summary>
        public double PriorStrength { get; set; } = 20.0;

        /// <summary>共现概率的收缩强度，越大越向边际概率收缩（抑制稀疏噪声）</summary>
        public double CoOccurrenceShrinkage { get; set; } = 25.0;

        public PredictionOptions Clone()
        {
            return new PredictionOptions
            {
                Strategy = Strategy,
                EnableStructureFilter = EnableStructureFilter,
                AvoidDuplicateCombinations = AvoidDuplicateCombinations,
                RandomSeed = RandomSeed,
                WeightStrength = WeightStrength,
                MaxAttemptsPerCombination = MaxAttemptsPerCombination,
                Constraints = Constraints.Clone(),
                RecencyHalfLife = RecencyHalfLife,
                PriorStrength = PriorStrength,
                CoOccurrenceShrinkage = CoOccurrenceShrinkage
            };
        }

        public static string GetStrategyName(GenerationStrategy strategy)
        {
            switch (strategy)
            {
                case GenerationStrategy.Uniform: return "均匀随机(基准)";
                case GenerationStrategy.Hot: return "热号加权";
                case GenerationStrategy.Cold: return "冷号回补";
                case GenerationStrategy.Balanced: return "均衡策略";
                default: return strategy.ToString();
            }
        }
    }
}
