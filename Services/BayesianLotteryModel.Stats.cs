using LotteryTool.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace LotteryTool.Services
{
    /// <summary>
    /// 引擎的统计计算与组合生成部分（partial class 拆分，主逻辑见 BayesianLotteryModel.cs）。
    /// </summary>
    public partial class BayesianLotteryModel
    {
        /// <summary>一次性预计算的统计量。纯数据对象，可安全用于回测中的多期训练。</summary>
        private sealed class ModelStats
        {
            public double[] FrontProb = new double[FrontMax + 1];
            public double[] BackProb = new double[BackMax + 1];
            public double[,] FrontLift = new double[FrontMax + 1, FrontMax + 1];
            public double[,] BackLift = new double[BackMax + 1, BackMax + 1];
            public double[] FrontWeightedCount = new double[FrontMax + 1];
            public double[] BackWeightedCount = new double[BackMax + 1];
            public int[] FrontWindowCount = new int[FrontMax + 1];
            public int[] BackWindowCount = new int[BackMax + 1];
            public int[] FrontTotalCount = new int[FrontMax + 1];
            public int[] BackTotalCount = new int[BackMax + 1];
            public int[] FrontOmission = new int[FrontMax + 1];
            public int[] BackOmission = new int[BackMax + 1];
            public int[] FrontMaxOmission = new int[FrontMax + 1];
            public int[] BackMaxOmission = new int[BackMax + 1];
            public double[] FrontAvgInterval = new double[FrontMax + 1];
            public double[] BackAvgInterval = new double[BackMax + 1];
            public bool[] FrontRecentHot = new bool[FrontMax + 1];
            public bool[] BackRecentHot = new bool[BackMax + 1];
            public int[]? LastFront;
            public int[]? LastBack;
            public int WindowSize;
            public int TotalDraws;
        }

        /// <summary>
        /// 计算统计量。data 必须已按日期升序排列且完成去重。
        /// 窗口 = 最近 min(N, data.Count) 期；窗口内使用时间衰减权重。
        /// </summary>
        private static ModelStats ComputeStats(List<LotteryData> data, int nPeriods, PredictionOptions opt)
        {
            var s = new ModelStats();
            int total = data.Count;
            s.TotalDraws = total;

            if (total == 0)
            {
                s.WindowSize = 0;
                for (int i = 1; i <= FrontMax; i++) { s.FrontProb[i] = UniformFront; }
                for (int i = 1; i <= BackMax; i++) { s.BackProb[i] = UniformBack; }
                FillLiftWithOnes(s.FrontLift, FrontMax);
                FillLiftWithOnes(s.BackLift, BackMax);
                return s;
            }

            int W = Math.Min(Math.Max(1, nPeriods), total);
            int start = total - W;
            s.WindowSize = W;

            // 时间衰减权重：age=0 为最新一期，半衰期默认 W/3
            double halfLife = opt.RecencyHalfLife > 0 ? opt.RecencyHalfLife : Math.Max(1.0, W / 3.0);
            var weights = new double[W];
            double weightSum = 0;
            for (int k = 0; k < W; k++)
            {
                int age = W - 1 - k;
                weights[k] = Math.Pow(0.5, age / halfLife);
                weightSum += weights[k];
            }
            for (int k = 0; k < W; k++) weights[k] /= weightSum;

            var frontCo = new double[FrontMax + 1, FrontMax + 1];
            var backCo = new double[BackMax + 1, BackMax + 1];

            for (int k = 0; k < W; k++)
            {
                var rec = data[start + k];
                double w = weights[k];
                var f = rec.FrontNumbers;
                var b = rec.BackNumbers;

                foreach (var num in f)
                    if (InRange(num, FrontMax)) { s.FrontWeightedCount[num] += w; s.FrontWindowCount[num]++; }
                foreach (var num in b)
                    if (InRange(num, BackMax)) { s.BackWeightedCount[num] += w; s.BackWindowCount[num]++; }

                for (int a = 0; a < f.Length; a++)
                    for (int c = a + 1; c < f.Length; c++)
                    {
                        int i = f[a], j = f[c];
                        if (InRange(i, FrontMax) && InRange(j, FrontMax))
                        { frontCo[i, j] += w; frontCo[j, i] += w; }
                    }
                for (int a = 0; a < b.Length; a++)
                    for (int c = a + 1; c < b.Length; c++)
                    {
                        int i = b[a], j = b[c];
                        if (InRange(i, BackMax) && InRange(j, BackMax))
                        { backCo[i, j] += w; backCo[j, i] += w; }
                    }
            }

            // 全量历史：出现次数、当前遗漏、最大遗漏、平均间隔、近 10 期热度
            ComputePerNumberStats(data, true, s);
            ComputePerNumberStats(data, false, s);
            s.LastFront = data[total - 1].FrontNumbers;
            s.LastBack = data[total - 1].BackNumbers;

            // 边际概率：Dirichlet 加法平滑，向均匀分布收缩
            // Σ weightedCount = 每期号码数 (前区 5 / 后区 2)，Σ α·u = α，故分母 = 号码数 + α
            double alpha = Math.Max(0.0, opt.PriorStrength);
            for (int i = 1; i <= FrontMax; i++)
                s.FrontProb[i] = (s.FrontWeightedCount[i] + alpha * UniformFront) / (FrontPick + alpha);
            for (int i = 1; i <= BackMax; i++)
                s.BackProb[i] = (s.BackWeightedCount[i] + alpha * UniformBack) / (BackPick + alpha);

            // 共现条件概率收缩：P(A|B) = (co(A,B) + β·P(A)) / (count(B) + β)
            // 再转成相对边际的 lift。β 越大越接近 1（承认稀疏样本无信息量）。
            double beta = Math.Max(1e-6, opt.CoOccurrenceShrinkage);
            FillLiftWithOnes(s.FrontLift, FrontMax);
            FillLiftWithOnes(s.BackLift, BackMax);
            for (int i = 1; i <= FrontMax; i++)
                for (int j = 1; j <= FrontMax; j++)
                {
                    if (i == j) continue;
                    double pGiven = (frontCo[i, j] + beta * s.FrontProb[i]) /
                                    (s.FrontWeightedCount[j] + beta);
                    s.FrontLift[i, j] = Clamp(pGiven / Math.Max(s.FrontProb[i], 1e-12), 0.25, 4.0);
                }
            for (int i = 1; i <= BackMax; i++)
                for (int j = 1; j <= BackMax; j++)
                {
                    if (i == j) continue;
                    double pGiven = (backCo[i, j] + beta * s.BackProb[i]) /
                                    (s.BackWeightedCount[j] + beta);
                    s.BackLift[i, j] = Clamp(pGiven / Math.Max(s.BackProb[i], 1e-12), 0.25, 4.0);
                }

            return s;
        }

        private static void ComputePerNumberStats(List<LotteryData> data, bool isFront, ModelStats s)
        {
            int max = isFront ? FrontMax : BackMax;
            int[] totalCount = isFront ? s.FrontTotalCount : s.BackTotalCount;
            int[] omission = isFront ? s.FrontOmission : s.BackOmission;
            int[] maxOmission = isFront ? s.FrontMaxOmission : s.BackMaxOmission;
            double[] avgInterval = isFront ? s.FrontAvgInterval : s.BackAvgInterval;
            bool[] recentHot = isFront ? s.FrontRecentHot : s.BackRecentHot;

            int n = data.Count;
            var lastSeen = new int[max + 1];
            for (int i = 0; i <= max; i++) lastSeen[i] = -1;

            for (int k = 0; k < n; k++)
            {
                var nums = isFront ? data[k].FrontNumbers : data[k].BackNumbers;
                foreach (var num in nums)
                {
                    if (!InRange(num, max)) continue;
                    totalCount[num]++;
                    int gap = lastSeen[num] >= 0 ? k - lastSeen[num] - 1 : k;
                    if (gap > maxOmission[num]) maxOmission[num] = gap;
                    lastSeen[num] = k;
                }
            }

            int recentFrom = Math.Max(0, n - 10);
            for (int i = 1; i <= max; i++)
            {
                omission[i] = lastSeen[i] >= 0 ? n - 1 - lastSeen[i] : n;
                if (omission[i] > maxOmission[i]) maxOmission[i] = omission[i];
                avgInterval[i] = totalCount[i] > 0 ? (double)n / totalCount[i] : n;
                for (int k = recentFrom; k < n; k++)
                {
                    var nums = isFront ? data[k].FrontNumbers : data[k].BackNumbers;
                    if (Array.IndexOf(nums, i) >= 0) { recentHot[i] = true; break; }
                }
            }
        }

        /// <summary>
        /// 按本次调用的策略构建抽样权重向量（指数竞赛只需相对大小，无需归一化）。
        /// 与统计量解耦：stats 只描述历史数据，权重描述"想怎么选号"。
        /// 三种非均匀策略使用不同信号：
        /// - Hot：窗口内实际出现频率（未平滑），热号倾向；
        /// - Cold：当前遗漏 / 平均间隔，长期未出的号码权重更高；
        /// - Balanced：Dirichlet 平滑后的边际概率，温和加权。
        /// 无论哪种，都不改变期望中奖率。
        /// </summary>
        private static void BuildWeightVectors(
            ModelStats s, PredictionOptions opt,
            out double[] front, out double[] back)
        {
            front = new double[FrontMax + 1];
            back = new double[BackMax + 1];
            for (int i = 1; i <= FrontMax; i++) front[i] = 1.0;
            for (int i = 1; i <= BackMax; i++) back[i] = 1.0;
            if (opt.Strategy == GenerationStrategy.Uniform) return;

            double strength = Clamp(opt.WeightStrength, 0.0, 2.0);
            double gamma = 0.5 * strength;
            int W = Math.Max(1, s.WindowSize);

            for (int i = 1; i <= FrontMax; i++)
            {
                double smoothLift = s.FrontProb[i] / UniformFront;
                double freqLift = (s.FrontWindowCount[i] / (double)W) / UniformFrontRate;
                double omissionRatio = s.FrontOmission[i] / Math.Max(1.0, s.FrontAvgInterval[i]);
                double w = opt.Strategy switch
                {
                    GenerationStrategy.Hot => Math.Pow(Math.Max(freqLift, 1e-6), strength * 2.0),
                    GenerationStrategy.Cold => Math.Pow(1.0 + omissionRatio, strength),
                    _ => Math.Pow(smoothLift, gamma)
                };
                front[i] = Clamp(w, 0.2, 5.0);
            }
            for (int i = 1; i <= BackMax; i++)
            {
                double smoothLift = s.BackProb[i] / UniformBack;
                double freqLift = (s.BackWindowCount[i] / (double)W) / UniformBackRate;
                double omissionRatio = s.BackOmission[i] / Math.Max(1.0, s.BackAvgInterval[i]);
                double w = opt.Strategy switch
                {
                    GenerationStrategy.Hot => Math.Pow(Math.Max(freqLift, 1e-6), strength * 2.0),
                    GenerationStrategy.Cold => Math.Pow(1.0 + omissionRatio, strength),
                    _ => Math.Pow(smoothLift, gamma)
                };
                back[i] = Clamp(w, 0.2, 5.0);
            }
        }

        /// <summary>
        /// Gumbel-top-k / 指数竞赛无放回加权抽样。
        /// 对每个号码取 E_i ~ Exp(1)，令 key_i = E_i / w_i，取 key 最小的 k 个。
        /// 数学上严格等价于 Plackett-Luce 模型（每次按权重抽一个、移除后再抽），
        /// 但一次 O(n log n) 完成，且无需逐次重归一化。
        /// </summary>
        private static int[] WeightedSampleWithoutReplacement(
            double[] weights, int minNum, int maxNum, int count, Random random)
        {
            int n = maxNum - minNum + 1;
            var keys = new double[n];
            var items = new int[n];
            for (int i = 0; i < n; i++)
            {
                int num = minNum + i;
                double w = Math.Max(weights[num], 1e-12);
                double u = 1.0 - random.NextDouble();   // u ∈ (0, 1]
                keys[i] = -Math.Log(u) / w;             // ~ Exp(w)，越小越优先
                items[i] = num;
            }
            Array.Sort(keys, items);
            var result = new int[count];
            Array.Copy(items, result, count);
            Array.Sort(result);
            return result;
        }

        private static PredictionResult GenerateOne(
            ModelStats s, PredictionOptions opt,
            double[] frontWeights, double[] backWeights,
            Random random, int[]? lastFront)
        {
            var p = new PredictionResult
            {
                FrontNumbers = WeightedSampleWithoutReplacement(
                    frontWeights, 1, FrontMax, FrontPick, random),
                BackNumbers = WeightedSampleWithoutReplacement(
                    backWeights, 1, BackMax, BackPick, random),
                StrategyName = PredictionOptions.GetStrategyName(opt.Strategy)
            };
            p.RecalculateFeatures(lastFront);
            p.StructureValid = IsStructureValid(p, opt.Constraints, lastFront);
            p.ModelScore = ComputeScore(p, s);
            return p;
        }

        /// <summary>组合结构过滤：只排除现实中极罕见的形态，不改变单号中奖概率。</summary>
        private static bool IsStructureValid(
            PredictionResult p, CombinationConstraints c, int[]? lastFront)
        {
            if (c == null) return true;
            if (p.FrontSum < c.FrontSumMin || p.FrontSum > c.FrontSumMax) return false;
            if (p.BackSum < c.BackSumMin || p.BackSum > c.BackSumMax) return false;
            if (p.OddCount < c.MinOddCount || p.OddCount > c.MaxOddCount) return false;
            if (p.BigCount < c.MinBigCount || p.BigCount > c.MaxBigCount) return false;
            if (p.ConsecutivePairs > c.MaxConsecutivePairs) return false;
            if (p.AC < c.MinAC) return false;
            if (lastFront != null && p.RepeatWithLastDraw > c.MaxRepeatWithLastDraw) return false;
            if (c.RequireThreeZonesCovered)
            {
                bool z1 = p.FrontNumbers.Any(x => x <= 12);
                bool z2 = p.FrontNumbers.Any(x => x >= 13 && x <= 24);
                bool z3 = p.FrontNumbers.Any(x => x >= 25);
                if (!(z1 && z2 && z3)) return false;
            }
            return true;
        }

        /// <summary>
        /// 可解释评分 0~100：组合相对"均匀随机基准"的对数似然提升 + 号码对共现 lift。
        /// 50 分 ≈ 与随机基准相当。该分数与中奖概率无关。
        /// </summary>
        private static double ComputeScore(PredictionResult p, ModelStats s)
        {
            double ll = 0;
            foreach (var n in p.FrontNumbers)
                ll += Math.Log(Math.Max(s.FrontProb[n], 1e-12) / UniformFront);
            foreach (var n in p.BackNumbers)
                ll += Math.Log(Math.Max(s.BackProb[n], 1e-12) / UniformBack);
            double avgLL = ll / (FrontPick + BackPick);

            double pairLL = 0;
            int pairs = 0;
            var f = p.FrontNumbers;
            for (int i = 0; i < f.Length; i++)
                for (int j = i + 1; j < f.Length; j++)
                { pairLL += Math.Log(Math.Max(s.FrontLift[f[i], f[j]], 1e-6)); pairs++; }
            if (pairs > 0) pairLL /= pairs;

            double raw = 0.6 * avgLL + 0.4 * pairLL;
            double score = 50.0 + 32.0 * Math.Tanh(raw * 2.5);
            score += p.StructureValid ? 2.0 : -5.0;
            return Clamp(score, 0.0, 100.0);
        }

        private static void FillLiftWithOnes(double[,] lift, int max)
        {
            for (int i = 0; i <= max; i++)
                for (int j = 0; j <= max; j++) lift[i, j] = 1.0;
        }

        private static bool InRange(int n, int max) => n >= 1 && n <= max;

        private static double Clamp(double v, double lo, double hi) =>
            v < lo ? lo : (v > hi ? hi : v);

        private static double[] CreateOnes(int size)
        {
            var a = new double[size];
            for (int i = 0; i < size; i++) a[i] = 1.0;
            return a;
        }
    }
}
