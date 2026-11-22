using System;

namespace LotteryTool.Models
{
    public class LotteryData
    {
        public string IssueNumber { get; set; } = string.Empty;
        public DateTime DrawDate { get; set; }
        public int[] FrontNumbers { get; set; } = new int[5];
        public int[] BackNumbers { get; set; } = new int[2];

        // 单独属性用于 DataGridView 绑定（DataPropertyName 不支持数组索引器）
        public int Front1 => FrontNumbers.Length > 0 ? FrontNumbers[0] : 0;
        public int Front2 => FrontNumbers.Length > 1 ? FrontNumbers[1] : 0;
        public int Front3 => FrontNumbers.Length > 2 ? FrontNumbers[2] : 0;
        public int Front4 => FrontNumbers.Length > 3 ? FrontNumbers[3] : 0;
        public int Front5 => FrontNumbers.Length > 4 ? FrontNumbers[4] : 0;
        public int Back1 => BackNumbers.Length > 0 ? BackNumbers[0] : 0;
        public int Back2 => BackNumbers.Length > 1 ? BackNumbers[1] : 0;

        public LotteryData() { }

        public LotteryData(string issue, DateTime date, int[] front, int[] back)
        {
            IssueNumber = issue;
            DrawDate = date;
            FrontNumbers = front;
            BackNumbers = back;
        }

        public override string ToString()
        {
            return $"{IssueNumber}\t{DrawDate:yyyy-MM-dd}\t" +
                   $"{FrontNumbers[0]:D2}\t{FrontNumbers[1]:D2}\t{FrontNumbers[2]:D2}\t" +
                   $"{FrontNumbers[3]:D2}\t{FrontNumbers[4]:D2}\t" +
                   $"{BackNumbers[0]:D2}\t{BackNumbers[1]:D2}";
        }
    }
}
