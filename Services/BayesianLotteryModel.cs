using LotteryTool.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace LotteryTool.Services
{
    /// <summary>
    /// 大乐透统计分析与随机选号引擎（v3.0）。
    ///
    /// 【历史命名】类名沿用 BayesianLotteryModel 以保持 UI 兼容，但 v3.0 起已不是
    /// 朴素贝叶斯分类器。旧实现的 ∏(1+likelihood) 连乘并非贝叶斯公式，且会被
    /// 稀疏共现噪声主导，已整体替换。
    ///
    /// 【v3.0 算法要点】
    /// 1. 时间衰减加权：窗口内按半衰期指数衰减，替代"硬截断取最近 N 期"。
    /// 2. Dirichlet 平滑：边际概率向均匀分布收缩，抑制小样本过拟合。
    /// 3. 共现收缩估计：P(A|B) 以 β 伪计数收缩回边际概率。50 期窗口内
    ///    任意号码对共现期望不足 1 次，直接估计几乎全是噪声。
    /// 4. Gumbel-top-k 指数竞赛无放回加权抽样：一次 O(n log n) 生成整注，
    ///    严格等价于 Plackett-Luce 依次抽样，替代逐次轮盘赌。
    /// 5. 结构约束：和值 / 奇偶 / 大小 / 连号 / AC 值 / 上期重复数。
    /// 6. 单一可复现随机源（支持固定种子），杜绝 new Random(Guid...) 的种子相关问题。
    /// 7. 组合去重、可解释评分、walk-forward 回测与均匀随机对照。
    ///
    /// 【前提】大乐透每期独立均匀随机开奖，本引擎任何策略都不改变期望中奖率。
    /// 所谓"预测"本质是"按指定分布生成号码"，仅供统计研究与娱乐。
    /// </summary>
    public partial class BayesianLotteryModel
    {
        private const int FrontMax = 35;
        private const int BackMax = 12;
        private const int FrontPick = 5;
        private const int BackPick = 2;

        private static readonly double UniformFront = 1.0 / FrontMax;
        private static readonly double UniformBack = 1.0 / BackMax;
        /// <summary>均匀开奖下单个号码每期的出现率：前区 5/35、后区 2/12</summary>
        private static readonly double UniformFrontRate = (double)FrontPick / FrontMax;
        private static readonly double UniformBackRate = (double)BackPick / BackMax;
        private static readonly double[] UniformFrontWeights = CreateOnes(FrontMax + 1);
        private static readonly double[] UniformBackWeights = CreateOnes(BackMax + 1);

        private List<LotteryData> allHistoricalData = new List<LotteryData>();
        private int nPeriods;
        private PredictionOptions options = new PredictionOptions();
        private ModelStats? stats;
        private bool isDirty = true;

        private Random rng = new Random();
        private int? activeSeed = null;

        public BayesianLotteryModel(int nPeriods = 50)
        {
            this.nPeriods = Math.Max(1, nPeriods);
        }

        // ------------------------------------------------------------------
        // 公共 API（与 v2.0 兼容）
        // ------------------------------------------------------------------

        /// <summary>
        /// 装载历史数据：校验 → 按日期升序 → 按期号去重 → 惰性计算统计量。
        /// 重复点击"开始预测"不会重复训练。
        /// </summary>
        public void TrainModel(List<LotteryData> historicalData)
        {
            if (historicalData == null)
            {
                allHistoricalData = new List<LotteryData>();
            }
            else
            {
                allHistoricalData = historicalData
                    .Where(d => d != null && IsValidRecord(d))
                    .OrderBy(d => d.DrawDate)
                    .ThenBy(d => d.IssueNumber, StringComparer.OrdinalIgnoreCase)
                    .GroupBy(d => d.IssueNumber ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.Last())
                    .OrderBy(d => d.DrawDate)
                    .ThenBy(d => d.IssueNumber, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
            isDirty = true;
        }

        public void SetNPeriods(int n)
        {
            nPeriods = Math.Max(1, n);
            isDirty = true;
        }

        public int GetCurrentNPeriods() => nPeriods;
        public int GetTotalDataCount() => allHistoricalData.Count;

        public Dictionary<int, double> GetFrontProbabilities()
        {
            EnsureStats();
            return ToDictionary(stats!.FrontProb, FrontMax);
        }

        public Dictionary<int, double> GetBackProbabilities()
        {
            EnsureStats();
            return ToDictionary(stats!.BackProb, BackMax);
        }

        public BayesianPredictionResult Predict(int numberOfPredictions = 5)
            => Predict(numberOfPredictions, options);

        // ------------------------------------------------------------------
        // v3.0 新增 API
        // ------------------------------------------------------------------

        public void SetOptions(PredictionOptions newOptions)
        {
            options = (newOptions ?? new PredictionOptions()).Clone();
            isDirty = true;
        }

        public PredictionOptions GetOptions() => options.Clone();

        public void SetRandomSeed(int? seed)
        {
            options.RandomSeed = seed;
            ResetRng(seed, options.Strategy);
        }

        public NumberStatisticsResult GetNumberStatistics()
        {
            EnsureStats();
            var s = stats!;
            return new NumberStatisticsResult
            {
                WindowSize = s.WindowSize,
                TotalDraws = s.TotalDraws,
                RecencyHalfLife = options.RecencyHalfLife > 0
                    ? options.RecencyHalfLife
                    : Math.Max(1.0, s.WindowSize / 3.0),
                Front = BuildNumberStatistics(s, true),
                Back = BuildNumberStatistics(s, false)
            };
        }

        /// <summary>批量生成号码组合（带结构过滤与去重）。</summary>
        public BayesianPredictionResult Predict(int numberOfPredictions, PredictionOptions predictOptions)
        {
            var sw = Stopwatch.StartNew();
            var opt = (predictOptions ?? options).Clone();
            EnsureStats();
            ResetRng(opt.RandomSeed, opt.Strategy);

            var s = stats!;
            BuildWeightVectors(s, opt, out var frontWeights, out var backWeights);
            int count = Math.Max(1, numberOfPredictions);
            int maxAttempts = Math.Max(1, opt.MaxAttemptsPerCombination);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var predictions = new List<PredictionResult>(count);
            int relaxed = 0;
            int duplicates = 0;
            int[]? lastFront = s.LastFront;

            for (int i = 0; i < count; i++)
            {
                PredictionResult? accepted = null;

                for (int attempt = 0; attempt < maxAttempts; attempt++)
                {
                    var candidate = GenerateOne(s, opt, frontWeights, backWeights, rng, lastFront);
                    string key = candidate.CombinationKey;

                    if (opt.AvoidDuplicateCombinations && seen.Contains(key))
                    {
                        duplicates++;
                        continue;
                    }
                    if (opt.EnableStructureFilter && !candidate.StructureValid)
                        continue;

                    if (opt.AvoidDuplicateCombinations) seen.Add(key);
                    accepted = candidate;
                    break;
                }

                if (accepted == null)
                {
                    // 兜底：约束过严或组合空间接近耗尽时接受最后一注，避免死循环
                    accepted = GenerateOne(s, opt, frontWeights, backWeights, rng, lastFront);
                    if (opt.AvoidDuplicateCombinations) seen.Add(accepted.CombinationKey);
                    relaxed++;
                }

                predictions.Add(accepted);
            }

            sw.Stop();
            return BuildResult(s, predictions, opt, relaxed, duplicates, sw.Elapsed.TotalMilliseconds);
        }

        /// <summary>
        /// 时间序列 walk-forward 回测：每期只用该期之前的数据训练，
        /// 与"施加相同结构过滤的均匀随机选号"对照，用于检验策略是否真的有效。
        /// </summary>
        public BacktestResult RunBacktest(int testPeriods = 100, int betsPerDraw = 20)
        {
            var result = new BacktestResult
            {
                StrategyName = PredictionOptions.GetStrategyName(options.Strategy),
                BetsPerDraw = Math.Max(1, betsPerDraw)
            };

            int total = allHistoricalData.Count;
            const int MinTrain = 30;
            int start = Math.Max(MinTrain, total - Math.Max(1, testPeriods));
            if (start >= total)
            {
                result.Conclusion = $"数据不足：至少需要 {MinTrain + 1} 期才能回测，当前仅有 {total} 期。";
                return result;
            }

            var opt = options.Clone();
            opt.AvoidDuplicateCombinations = false;
            var uniformOpt = opt.Clone();
            uniformOpt.Strategy = GenerationStrategy.Uniform;

            int seed = options.RandomSeed ?? Environment.TickCount;
            var rngModel = new Random(seed);
            var rngRandom = new Random(unchecked(seed ^ 0x5f3759df));

            double modelFrontSum = 0, modelBackSum = 0;
            double randomFrontSum = 0, randomBackSum = 0;
            int modelJackpots = 0, randomJackpots = 0;
            var modelFH = new int[6];
            var modelBH = new int[3];
            var randomFH = new int[6];
            var randomBH = new int[3];

            for (int t = start; t < total; t++)
            {
                var train = allHistoricalData.GetRange(0, t);
                var s = ComputeStats(train, nPeriods, opt);
                BuildWeightVectors(s, opt, out var frontWeights, out var backWeights);
                var actual = allHistoricalData[t];
                int[]? lastFront = t > 0 ? allHistoricalData[t - 1].FrontNumbers : null;

                for (int b = 0; b < result.BetsPerDraw; b++)
                {
                    var modelBet = GenerateOne(s, opt, frontWeights, backWeights, rngModel, lastFront);
                    var randomBet = GenerateOne(s, uniformOpt,
                        UniformFrontWeights, UniformBackWeights, rngRandom, lastFront);
                    Tally(modelBet, actual, ref modelFrontSum, ref modelBackSum,
                          modelFH, modelBH, ref modelJackpots);
                    Tally(randomBet, actual, ref randomFrontSum, ref randomBackSum,
                          randomFH, randomBH, ref randomJackpots);
                }
            }

            result.TestedDraws = total - start;
            int bets = result.TotalBets;
            result.ModelAvgFrontHits = modelFrontSum / bets;
            result.ModelAvgBackHits = modelBackSum / bets;
            result.ModelAvgTotalHits = (modelFrontSum + modelBackSum) / bets;
            result.RandomAvgFrontHits = randomFrontSum / bets;
            result.RandomAvgBackHits = randomBackSum / bets;
            result.RandomAvgTotalHits = (randomFrontSum + randomBackSum) / bets;
            result.ModelFrontHitHistogram = modelFH;
            result.ModelBackHitHistogram = modelBH;
            result.RandomFrontHitHistogram = randomFH;
            result.RandomBackHitHistogram = randomBH;
            result.ModelJackpots = modelJackpots;
            result.RandomJackpots = randomJackpots;
            result.Conclusion = BuildBacktestConclusion(result);
            return result;
        }

        // ------------------------------------------------------------------
        // 内部辅助
        // ------------------------------------------------------------------

        private void EnsureStats()
        {
            if (!isDirty && stats != null) return;
            stats = ComputeStats(allHistoricalData, nPeriods, options);
            isDirty = false;
        }

        /// <summary>
        /// 随机源复位语义：
        /// - 指定种子：每次生成都从"种子 + 策略偏移"重新开始，既保证结果可完全复现，
        ///   又让不同策略使用相互独立的随机流（避免切换策略却得到同一注）；
        /// - 未指定种子：复用同一条随机流，连续多次点击得到不同结果；
        /// - 从固定种子切回真随机时重建随机源。
        /// </summary>
        private void ResetRng(int? seed, GenerationStrategy strategy)
        {
            if (seed.HasValue)
            {
                int offset = ((int)strategy + 1) * 7919;
                rng = new Random(unchecked(seed.Value + offset));
                activeSeed = seed;
            }
            else if (activeSeed != null)
            {
                rng = new Random();
                activeSeed = null;
            }
        }

        private BayesianPredictionResult BuildResult(
            ModelStats s, List<PredictionResult> predictions, PredictionOptions opt,
            int relaxed, int duplicates, double elapsed)
        {
            return new BayesianPredictionResult
            {
                Predictions = predictions,
                StrategyName = PredictionOptions.GetStrategyName(opt.Strategy),
                WindowSize = s.WindowSize,
                TotalDraws = s.TotalDraws,
                StructureFilterEnabled = opt.EnableStructureFilter,
                RelaxedCount = relaxed,
                DuplicatesSkipped = duplicates,
                ElapsedMilliseconds = elapsed,
                HotFrontNumbers = Enumerable.Range(1, FrontMax)
                    .OrderByDescending(i => s.FrontProb[i]).ThenBy(i => i).Take(10).ToArray(),
                HotBackNumbers = Enumerable.Range(1, BackMax)
                    .OrderByDescending(i => s.BackProb[i]).ThenBy(i => i).Take(5).ToArray(),
                // 冷号按"当前遗漏期数"定义（而非概率最低），更符合彩票分析习惯
                ColdFrontNumbers = Enumerable.Range(1, FrontMax)
                    .OrderByDescending(i => s.FrontOmission[i]).ThenByDescending(i => s.FrontAvgInterval[i])
                    .Take(10).ToArray(),
                ColdBackNumbers = Enumerable.Range(1, BackMax)
                    .OrderByDescending(i => s.BackOmission[i]).ThenByDescending(i => s.BackAvgInterval[i])
                    .Take(5).ToArray()
            };
        }

        private static NumberStatistic[] BuildNumberStatistics(ModelStats s, bool isFront)
        {
            int max = isFront ? FrontMax : BackMax;
            double u = isFront ? UniformFront : UniformBack;
            var arr = new NumberStatistic[max];
            for (int i = 1; i <= max; i++)
            {
                arr[i - 1] = new NumberStatistic
                {
                    Number = i,
                    TotalCount = isFront ? s.FrontTotalCount[i] : s.BackTotalCount[i],
                    WindowCount = isFront ? s.FrontWindowCount[i] : s.BackWindowCount[i],
                    WeightedCount = isFront ? s.FrontWeightedCount[i] : s.BackWeightedCount[i],
                    Probability = isFront ? s.FrontProb[i] : s.BackProb[i],
                    AppearanceRate = (isFront ? s.FrontProb[i] : s.BackProb[i]) *
                                     (isFront ? FrontPick : BackPick),
                    Lift = (isFront ? s.FrontProb[i] : s.BackProb[i]) / u,
                    Omission = isFront ? s.FrontOmission[i] : s.BackOmission[i],
                    MaxOmission = isFront ? s.FrontMaxOmission[i] : s.BackMaxOmission[i],
                    AverageInterval = isFront ? s.FrontAvgInterval[i] : s.BackAvgInterval[i],
                    IsRecentHot = isFront ? s.FrontRecentHot[i] : s.BackRecentHot[i]
                };
            }
            return arr;
        }

        private static void Tally(
            PredictionResult bet, LotteryData actual,
            ref double frontSum, ref double backSum,
            int[] frontHist, int[] backHist, ref int jackpots)
        {
            int f = bet.FrontNumbers.Intersect(actual.FrontNumbers).Count();
            int b = bet.BackNumbers.Intersect(actual.BackNumbers).Count();
            frontSum += f;
            backSum += b;
            frontHist[Math.Clamp(f, 0, frontHist.Length - 1)]++;
            backHist[Math.Clamp(b, 0, backHist.Length - 1)]++;
            if (f == FrontPick && b == BackPick) jackpots++;
        }

        private static string BuildBacktestConclusion(BacktestResult r)
        {
            double delta = r.FrontHitDelta;
            string judgment = Math.Abs(delta) < 0.03
                ? "模型与随机基准的平均命中数几乎一致"
                : (delta > 0 ? "模型略高于随机基准" : "模型略低于随机基准");

            return $"在 {r.TestedDraws} 期、共 {r.TotalBets} 注的样本中，{judgment}" +
                   $"（前区平均命中差 {delta:+0.0000;-0.0000;0.0000} 个/注）。\n" +
                   "大乐透每期开奖相互独立且均匀，理论上任何历史数据策略的前区期望命中数恒为 " +
                   "25/35 ≈ 0.7143、后区恒为 4/12 ≈ 0.3333，与所选号码完全无关。\n" +
                   "实测差异属于抽样波动，样本量越大越趋近于 0，不能作为策略有效的证据。\n" +
                   "结论：历史数据无法预测未来开奖，所有选号策略的期望中奖率相同。\n" +
                   "本工具仅用于统计分析与学习研究，预测结果不构成投注建议。";
        }

        private static bool IsValidRecord(LotteryData d)
        {
            if (d.FrontNumbers == null || d.FrontNumbers.Length != FrontPick) return false;
            if (d.BackNumbers == null || d.BackNumbers.Length != BackPick) return false;
            if (d.FrontNumbers.Any(n => !InRange(n, FrontMax))) return false;
            if (d.BackNumbers.Any(n => !InRange(n, BackMax))) return false;
            if (d.FrontNumbers.Distinct().Count() != FrontPick) return false;
            if (d.BackNumbers.Distinct().Count() != BackPick) return false;
            return true;
        }

        private static Dictionary<int, double> ToDictionary(double[] probs, int max)
        {
            var dict = new Dictionary<int, double>(max);
            for (int i = 1; i <= max; i++) dict[i] = probs[i];
            return dict;
        }
    }
}
