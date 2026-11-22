using LotteryTool.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace LotteryTool.Services
{
    /// <summary>数据加载结果，附带解析质量统计。</summary>
    public class DataLoadResult
    {
        public List<LotteryData> Records { get; set; } = new List<LotteryData>();
        public int TotalLines { get; set; }
        public int ValidRecords => Records.Count;
        public int SkippedLines { get; set; }
        public int DuplicatesRemoved { get; set; }
        public int InvalidRecords { get; set; }

        public string Summary =>
            $"读取 {TotalLines} 行，有效 {ValidRecords} 期；" +
            $"跳过 {SkippedLines} 行（空行/表头/格式不符），" +
            $"剔除重复期号 {DuplicatesRemoved} 条，无效记录 {InvalidRecords} 条。";
    }

    /// <summary>
    /// 历史开奖数据的加载 / 保存 / 校验。
    ///
    /// v3.0 修复：
    /// - 兼容混用的日期格式（yyyy/M/d、yyyy-M-d 等）与带/不带前导零的号码；
    /// - 跳过表头与空行，逐行容错，单行出错不再影响整个文件；
    /// - 严格校验号码范围、个数、重复性，并自动升序；
    /// - 按期号去重（原数据中存在大量完全重复的记录，会成倍污染频率统计）；
    /// - 合并导入时去重，避免重复导入同一文件；
    /// - 统一 UTF-8（无 BOM）输出。
    /// </summary>
    public class DataService
    {
        private static readonly string[] DateFormats =
        {
            "yyyy-M-d", "yyyy-MM-dd", "yyyy/M/d", "yyyy/MM/dd",
            "yyyy.M.d", "yyyy.MM.dd", "yyyyMMdd", "yyyy年M月d日"
        };

        private static readonly char[] Separators = { '\t', ' ', ',', '，', ';' };

        /// <summary>兼容旧版：仅返回记录列表。</summary>
        public List<LotteryData> LoadFromFile(string filePath)
            => LoadFromFileDetailed(filePath).Records;

        /// <summary>加载并返回解析质量统计。</summary>
        public DataLoadResult LoadFromFileDetailed(string filePath)
        {
            var result = new DataLoadResult();
            if (!File.Exists(filePath))
                throw new FileNotFoundException("数据文件不存在", filePath);

            try
            {
                // File.ReadLines 为流式读取，且会自动识别 UTF-8 BOM，适合大文件
                foreach (var line in File.ReadLines(filePath, Encoding.UTF8))
                {
                    result.TotalLines++;
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        result.SkippedLines++;
                        continue;
                    }

                    if (TryParseLine(line, out var record) && record != null)
                    {
                        result.Records.Add(record);
                    }
                    else
                    {
                        // 用"第 2 列能否解析为日期"区分表头/噪声与格式错误的数据行
                        var parts = line.Split(Separators, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 9 && TryParseDate(parts[1], out _))
                            result.InvalidRecords++;
                        else
                            result.SkippedLines++;
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"读取文件失败: {ex.Message}", ex);
            }

            result.Records = Normalize(result.Records, out int removed, out int invalid);
            result.DuplicatesRemoved = removed;
            result.InvalidRecords += invalid;
            return result;
        }

        private static bool TryParseLine(string line, out LotteryData? record)
        {
            record = null;
            var parts = line.Split(Separators, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 9) return false;
            // 期号允许任意文本（手动录入的期号为「手动+时间戳」）；
            // 用第 2 列能否解析为日期来挡住表头行。
            if (!TryParseDate(parts[1], out var date)) return false;

            var nums = new int[7];
            for (int i = 0; i < 7; i++)
            {
                if (!int.TryParse(parts[2 + i], NumberStyles.Integer,
                        CultureInfo.InvariantCulture, out nums[i]))
                    return false;
            }

            var front = nums.Take(5).OrderBy(n => n).ToArray();
            var back = nums.Skip(5).Take(2).OrderBy(n => n).ToArray();
            var data = new LotteryData(parts[0], date, front, back);
            if (!IsValid(data)) return false;

            record = data;
            return true;
        }

        private static bool TryParseDate(string text, out DateTime date)
        {
            string s = text.Trim();
            if (DateTime.TryParseExact(s, DateFormats, CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out date))
                return true;
            return DateTime.TryParse(s, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out date);
        }

        public void SaveToFile(string filePath, List<LotteryData> data)
        {
            try
            {
                var normalized = Normalize(data ?? new List<LotteryData>(), out _, out _);
                var lines = normalized.Select(d => d.ToString()).ToArray();
                File.WriteAllLines(filePath, lines, new UTF8Encoding(false));
            }
            catch (Exception ex)
            {
                throw new Exception($"保存文件失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 合并已有数据与新导入数据：校验 + 按期号去重 + 按日期升序。
        /// 返回合并后的新列表，并输出净新增数与剔除的重复数。
        /// </summary>
        public List<LotteryData> MergeRecords(
            List<LotteryData> existing, List<LotteryData> incoming,
            out int added, out int duplicatesRemoved)
        {
            var before = Normalize(existing ?? new List<LotteryData>(), out _, out _);
            var combined = new List<LotteryData>(before);
            combined.AddRange(incoming ?? new List<LotteryData>());
            var after = Normalize(combined, out duplicatesRemoved, out _);
            added = Math.Max(0, after.Count - before.Count);
            return after;
        }

        /// <summary>规范化：过滤非法记录、按期号去重、按日期升序。</summary>
        public static List<LotteryData> Normalize(
            IEnumerable<LotteryData> data, out int duplicatesRemoved, out int invalidRemoved)
        {
            var all = data.ToList();
            var valid = all.Where(d => d != null && IsValid(d)).ToList();
            invalidRemoved = all.Count - valid.Count;

            var dedup = valid
                .GroupBy(d => d.IssueNumber ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.Last())
                .OrderBy(d => d.DrawDate)
                .ThenBy(d => d.IssueNumber, StringComparer.OrdinalIgnoreCase)
                .ToList();

            duplicatesRemoved = valid.Count - dedup.Count;
            return dedup;
        }

        /// <summary>兼容旧版实例方法。</summary>
        public bool ValidateLotteryData(LotteryData data) => IsValid(data);

        private static bool IsValid(LotteryData data)
        {
            if (data == null) return false;
            if (data.FrontNumbers == null || data.FrontNumbers.Length != 5) return false;
            if (data.BackNumbers == null || data.BackNumbers.Length != 2) return false;
            if (data.FrontNumbers.Any(n => n < 1 || n > 35)) return false;
            if (data.BackNumbers.Any(n => n < 1 || n > 12)) return false;
            if (data.FrontNumbers.Distinct().Count() != 5) return false;
            if (data.BackNumbers.Distinct().Count() != 2) return false;
            return true;
        }
    }
}
