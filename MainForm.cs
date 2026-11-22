using LotteryTool.Models;
using LotteryTool.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace LotteryTool
{
    public partial class MainForm : Form
    {
        // ---------------- 主题配色（浅色主题） ----------------
        private static readonly Color Bg = Color.FromArgb(244, 247, 251);
        private static readonly Color Surface = Color.White;
        private static readonly Color Accent = Color.FromArgb(37, 99, 235);
        private static readonly Color AccentDeep = Color.FromArgb(30, 58, 138);
        private static readonly Color TextDark = Color.FromArgb(31, 41, 55);
        private static readonly Color TextMuted = Color.FromArgb(107, 114, 128);
        private static readonly Color Border = Color.FromArgb(226, 232, 240);
        private static readonly Color BackNumber = Color.FromArgb(220, 38, 38);
        private static readonly Font UiFont = new Font("Microsoft YaHei UI", 9F);
        private static readonly Font UiFontSmall = new Font("Microsoft YaHei UI", 8.25F);
        private static readonly Font UiFontBold = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold);
        private static readonly Font TitleFont = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold);

        private const int CardWidth = 392;
        private const int CardX = 12;
        private const int CardGap = 12;

        // ---------------- 数据与模型 ----------------
        private BayesianLotteryModel bayesianModel = null!;
        private DataService dataService = null!;
        private List<LotteryData> historicalData = new List<LotteryData>();
        private BindingList<LotteryData> dataBindingList = null!;
        private string lastLoadSummary = string.Empty;

        // ---------------- 布局容器 ----------------
        private TableLayoutPanel rootTable = null!;
        private SplitContainer splitMain = null!;
        private Panel cardsHost = null!;
        private Label lblSummary = null!;
        private Label lblFileStatus = null!;
        private DataGridView dataGridViewHistory = null!;
        private StatusStrip statusStrip = null!;
        private ToolStripStatusLabel toolStripStatusLabel = null!;
        private ToolStripStatusLabel lblStatusTime = null!;

        // ---------------- 文件操作 ----------------
        private Button btnImport = null!;
        private Button btnExport = null!;
        private Button btnClear = null!;

        // ---------------- 手动输入 ----------------
        private TextBox txtIssue = null!;
        private TextBox txtFront = null!;
        private TextBox txtBack = null!;
        private Button btnAdd = null!;

        // ---------------- 号码生成 / 模型评估 ----------------
        private NumericUpDown numNPeriods = null!;
        private NumericUpDown numPredictCount = null!;
        private ComboBox cmbStrategy = null!;
        private CheckBox chkStructureFilter = null!;
        private CheckBox chkUseSeed = null!;
        private NumericUpDown numSeed = null!;
        private NumericUpDown numBacktestPeriods = null!;
        private NumericUpDown numBacktestBets = null!;
        private Button btnUpdateN = null!;
        private Button btnPredict = null!;
        private Button btnShowProbabilities = null!;
        private Button btnAnalysis = null!;
        private Button btnBacktest = null!;

        // ---------------- 特定号码分析 ----------------
        private TextBox txtTargetFront = null!;
        private TextBox txtTargetBack = null!;
        private Button btnFrontAnalysis = null!;
        private Button btnBackAnalysis = null!;

        public MainForm()
        {
            InitializeComponent();
            InitializeCustomComponents();
        }

        private void InitializeCustomComponents()
        {
            bayesianModel = new BayesianLotteryModel(100);
            dataService = new DataService();
            historicalData = new List<LotteryData>();

            BuildLayout();
            BuildHeader();
            BuildCards();
            BuildGridArea();
            BuildStatusStrip();
            SetupDataGridView();

            AutoLoadDefaultData();
            UpdateDataGridView();
            UpdateStatus();
        }

        #region 布局骨架

        /// <summary>
        /// 根布局用 TableLayoutPanel 明确划分「标题 / 主体 / 状态栏」三行，
        /// 避免 Dock 堆叠顺序带来的不确定性；主体用 SplitContainer 左右分栏。
        /// </summary>
        private void BuildLayout()
        {
            rootTable = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                BackColor = Bg,
                Margin = Padding.Empty
            };
            rootTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            rootTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 78F));
            rootTable.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            rootTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));
            this.Controls.Add(rootTable);

            splitMain = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                FixedPanel = FixedPanel.Panel1,
                IsSplitterFixed = true,
                SplitterWidth = 6,
                BackColor = Border,
                Margin = Padding.Empty
            };
            // 注意：SplitterDistance 不能在控件尚未布局时赋值（此时 Width 仍是默认值），
            // 否则会与 Panel1MinSize/Panel2MinSize 冲突而抛 InvalidOperationException。
            // 真正的赋值推迟到 MainForm_Load，见文件末尾。
            rootTable.Controls.Add(splitMain, 0, 1);

            cardsHost = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = Bg,
                Padding = new Padding(0, CardGap, 0, 40)
            };
            splitMain.Panel1.Controls.Add(cardsHost);
        }

        private void BuildHeader()
        {
            var header = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = AccentDeep,
                Margin = Padding.Empty,
                Padding = new Padding(20, 0, 20, 0)
            };

            var lblTitle = new Label
            {
                Text = "大乐透分析预测系统",
                ForeColor = Color.White,
                Font = new Font("Microsoft YaHei UI", 16F, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(20, 12),
                BackColor = Color.Transparent
            };
            var lblSub = new Label
            {
                Text = "统计建模  ·  随机选号  ·  Walk-forward 回测评估",
                ForeColor = Color.FromArgb(191, 219, 254),
                Font = UiFontSmall,
                AutoSize = true,
                Location = new Point(22, 46),
                BackColor = Color.Transparent
            };
            var lblVersion = new Label
            {
                Text = "v3.0",
                ForeColor = Color.White,
                BackColor = Accent,
                Font = UiFontBold,
                TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(58, 26),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            lblVersion.Location = new Point(header.Width - 250, 16);
            var lblDisclaimer = new Label
            {
                Text = "彩票每期独立均匀随机开奖  ·  仅供统计研究与娱乐，请理性购彩",
                ForeColor = Color.FromArgb(147, 180, 230),
                Font = UiFontSmall,
                AutoSize = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            lblDisclaimer.Location = new Point(header.Width - 250, 48);

            header.Resize += (s, e) =>
            {
                lblVersion.Left = header.Width - 78;
                lblDisclaimer.Left = header.Width - lblDisclaimer.Width - 20;
                lblVersion.Top = 14;
                lblDisclaimer.Top = 48;
            };

            header.Controls.AddRange(new Control[] { lblTitle, lblSub, lblVersion, lblDisclaimer });
            rootTable.Controls.Add(header, 0, 0);
        }

        private void BuildGridArea()
        {
            var rightTable = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = Bg,
                Margin = Padding.Empty
            };
            rightTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F));
            rightTable.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            splitMain.Panel2.Controls.Add(rightTable);

            var summaryTable = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = Surface,
                Margin = Padding.Empty,
                Padding = new Padding(0, 0, 0, 1)
            };
            summaryTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190F));
            summaryTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            var lblGridTitle = new Label
            {
                Text = "历史开奖记录",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Microsoft YaHei UI", 11F, FontStyle.Bold),
                ForeColor = AccentDeep,
                Padding = new Padding(14, 0, 0, 0),
                BackColor = Surface
            };
            lblSummary = new Label
            {
                Text = "尚未加载数据",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleRight,
                Font = UiFontSmall,
                ForeColor = TextMuted,
                Padding = new Padding(0, 0, 14, 0),
                BackColor = Surface,
                AutoEllipsis = true
            };
            summaryTable.Controls.Add(lblGridTitle, 0, 0);
            summaryTable.Controls.Add(lblSummary, 1, 0);
            rightTable.Controls.Add(summaryTable, 0, 0);

            var gridHost = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Bg,
                Padding = new Padding(12, 10, 12, 12),
                Margin = Padding.Empty
            };
            dataGridViewHistory = new DataGridView
            {
                Dock = DockStyle.Fill,
                Margin = Padding.Empty,
                BackgroundColor = Surface,
                BorderStyle = BorderStyle.None
            };
            gridHost.Controls.Add(dataGridViewHistory);
            rightTable.Controls.Add(gridHost, 0, 1);
        }

        private void BuildStatusStrip()
        {
            statusStrip = new StatusStrip
            {
                Dock = DockStyle.Fill,
                BackColor = Surface,
                ForeColor = TextDark,
                Font = UiFontSmall,
                SizingGrip = false,
                Margin = Padding.Empty
            };
            toolStripStatusLabel = new ToolStripStatusLabel
            {
                Text = "就绪",
                Spring = true,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = TextDark
            };
            lblStatusTime = new ToolStripStatusLabel
            {
                Text = DateTime.Now.ToString("HH:mm:ss"),
                ForeColor = TextMuted
            };
            statusStrip.Items.AddRange(new ToolStripItem[] { toolStripStatusLabel, lblStatusTime });
            rootTable.Controls.Add(statusStrip, 0, 2);
        }

        #endregion

        #region 控件工厂（统一创建，规避初始化顺序陷阱）

        /// <summary>
        /// NumericUpDown 的对象初始化器按书写顺序执行：若先给 Value 赋值，
        /// 此时 Maximum 仍是默认值 100，任何大于 100 的初值都会抛
        /// ArgumentOutOfRangeException。这里固定「先范围、后取值」的顺序。
        /// </summary>
        private static NumericUpDown MakeNumeric(decimal min, decimal max, decimal value,
                                                 int x, int y, int width)
        {
            var n = new NumericUpDown
            {
                Location = new Point(x, y),
                Size = new Size(width, 24),
                Font = UiFont,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Surface,
                ForeColor = TextDark,
                TextAlign = HorizontalAlignment.Right,
                Increment = 1,
                ThousandsSeparator = false
            };
            n.Minimum = min;
            n.Maximum = max;
            n.Value = Math.Max(min, Math.Min(max, value));
            return n;
        }

        private static Button MakeButton(string text, int x, int y, int width, int height = 28,
                                         bool primary = false)
        {
            var b = new Button
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(width, height),
                FlatStyle = FlatStyle.Flat,
                Font = primary ? UiFontBold : UiFont,
                BackColor = primary ? Accent : Surface,
                ForeColor = primary ? Color.White : AccentDeep,
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false,
                TextAlign = ContentAlignment.MiddleCenter
            };
            b.FlatAppearance.BorderColor = primary ? Accent : Border;
            b.FlatAppearance.MouseOverBackColor = primary
                ? Color.FromArgb(29, 78, 216)
                : Color.FromArgb(239, 246, 255);
            b.FlatAppearance.BorderSize = 1;
            return b;
        }

        private static Label MakeLabel(string text, int x, int y, bool muted = false,
                                       bool bold = false)
        {
            return new Label
            {
                Text = text,
                Location = new Point(x, y),
                AutoSize = true,
                Font = bold ? UiFontBold : (muted ? UiFontSmall : UiFont),
                ForeColor = muted ? TextMuted : TextDark,
                BackColor = Color.Transparent
            };
        }

        private static TextBox MakeTextBox(string text, int x, int y, int width)
        {
            return new TextBox
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(width, 24),
                Font = UiFont,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Surface,
                ForeColor = TextDark
            };
        }

        private static CheckBox MakeCheckBox(string text, int x, int y, int width, bool checkedState)
        {
            return new CheckBox
            {
                Text = text,
                Checked = checkedState,
                Location = new Point(x, y),
                Size = new Size(width, 22),
                Font = UiFont,
                ForeColor = TextDark,
                BackColor = Color.Transparent,
                FlatStyle = FlatStyle.Flat
            };
        }

        /// <summary>白底卡片：顶部主色条 + 标题 + 细边框，替代系统样式 GroupBox。</summary>
        private CardPanel MakeCard(string title, int y, int height, out int contentTop)
        {
            var card = new CardPanel
            {
                CardTitle = title,
                Location = new Point(CardX, y),
                Size = new Size(CardWidth, height),
                BackColor = Surface,
                Anchor = AnchorStyles.Top | AnchorStyles.Left
            };
            cardsHost.Controls.Add(card);
            contentTop = 44;
            return card;
        }

        /// <summary>统一表格样式：表头深蓝、交替行、号码列区分前后区。</summary>
        private static void ApplyGridStyle(DataGridView g, bool frontBackColors = false)
        {
            g.BackgroundColor = Surface;
            g.BorderStyle = BorderStyle.None;
            g.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            g.GridColor = Border;
            g.EnableHeadersVisualStyles = false;
            g.Font = UiFont;
            g.RowHeadersVisible = false;
            g.AllowUserToAddRows = false;
            g.AllowUserToDeleteRows = false;
            g.AllowUserToResizeRows = false;
            g.ReadOnly = true;
            g.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            g.MultiSelect = false;
            g.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            g.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            g.ColumnHeadersHeight = 34;
            g.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = AccentDeep,
                ForeColor = Color.White,
                Font = UiFontBold,
                Alignment = DataGridViewContentAlignment.MiddleCenter,
                SelectionBackColor = AccentDeep,
                SelectionForeColor = Color.White,
                WrapMode = DataGridViewTriState.False
            };
            g.DefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Surface,
                ForeColor = TextDark,
                Font = UiFont,
                Alignment = DataGridViewContentAlignment.MiddleCenter,
                SelectionBackColor = Color.FromArgb(219, 234, 254),
                SelectionForeColor = AccentDeep
            };
            g.AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Color.FromArgb(248, 250, 252)
            };
            g.RowTemplate.Height = 27;
            g.ScrollBars = ScrollBars.Both;

            if (!frontBackColors) return;
            // 列顺序：期号(0) 日期(1) 前区1-5(2..6) 后区1-2(7..8)
            for (int i = 2; i <= 6 && i < g.Columns.Count; i++)
                g.Columns[i].DefaultCellStyle.ForeColor = Accent;
            for (int i = 7; i <= 8 && i < g.Columns.Count; i++)
                g.Columns[i].DefaultCellStyle.ForeColor = BackNumber;
        }

        #endregion

        #region 卡片内容

        private void BuildCards()
        {
            int y = CardGap;
            CreateFileCard(ref y);
            CreateInputCard(ref y);
            CreatePredictCard(ref y);
            CreateBacktestCard(ref y);
            CreateAnalysisCard(ref y);
            cardsHost.AutoScrollMinSize = new Size(CardWidth + CardX * 2, y + 20);
        }

        private void CreateFileCard(ref int y)
        {
            var card = MakeCard("数据管理", y, 108, out int t);
            btnImport = MakeButton("导入数据", 14, t, 88, 30, true);
            btnExport = MakeButton("导出数据", 110, t, 88);
            btnClear = MakeButton("清空数据", 206, t, 88);
            lblFileStatus = new Label
            {
                Text = "未加载数据",
                Location = new Point(14, t + 40),
                Size = new Size(CardWidth - 28, 20),
                Font = UiFontSmall,
                ForeColor = TextMuted,
                BackColor = Color.Transparent
            };
            btnImport.Click += BtnImport_Click;
            btnExport.Click += BtnExport_Click;
            btnClear.Click += BtnClear_Click;
            card.Controls.AddRange(new Control[] { btnImport, btnExport, btnClear, lblFileStatus });
            y += 108 + CardGap;
        }

        private void CreateInputCard(ref int y)
        {
            var card = MakeCard("手动录入开奖记录", y, 138, out int t);
            txtIssue = MakeTextBox("", 54, t, 84);
            txtFront = MakeTextBox("01 05 10 15 20", 222, t, 150);
            txtBack = MakeTextBox("01 02", 86, t + 36, 70);
            btnAdd = MakeButton("添加记录", 170, t + 34, 90, 28);
            btnAdd.Click += (s, e) => AddManualRecord();
            var tip = MakeLabel("空格分隔；前区 5 个 1-35，后区 2 个 1-12，自动排序去重", 14, t + 74, true);
            card.Controls.AddRange(new Control[] {
                MakeLabel("期号", 14, t + 4), txtIssue,
                MakeLabel("前区", 150, t + 4, true), txtFront,
                MakeLabel("后区", 14, t + 40), txtBack, btnAdd, tip });
            y += 138 + CardGap;
        }

        private void CreatePredictCard(ref int y)
        {
            var card = MakeCard("号码生成", y, 200, out int t);

            numNPeriods = MakeNumeric(5, 50000, 100, 84, t, 70);
            btnUpdateN = MakeButton("更新模型", 160, t - 2, 84, 28);
            btnUpdateN.Click += (s, e) =>
            {
                bayesianModel.SetNPeriods((int)numNPeriods.Value);
                UpdateStatus();
                MessageBox.Show($"已更新为使用最近 {numNPeriods.Value} 期数据进行统计", "模型更新",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            };
            numPredictCount = MakeNumeric(1, 20000, 5, 296, t, 76);

            cmbStrategy = new ComboBox
            {
                Location = new Point(56, t + 32),
                Size = new Size(166, 24),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = UiFont,
                FlatStyle = FlatStyle.Flat,
                BackColor = Surface,
                ForeColor = TextDark
            };
            cmbStrategy.Items.AddRange(new object[]
            { "均衡策略", "均匀随机(基准)", "热号加权", "冷号回补" });
            cmbStrategy.SelectedIndex = 0;
            chkStructureFilter = MakeCheckBox("结构过滤", 234, t + 33, 100, true);

            chkUseSeed = MakeCheckBox("固定种子", 14, t + 62, 88, false);
            numSeed = MakeNumeric(0, int.MaxValue, 2026, 106, t + 62, 92);
            numSeed.Enabled = false;
            chkUseSeed.CheckedChanged += (s, e) => numSeed.Enabled = chkUseSeed.Checked;

            btnPredict = MakeButton("开始生成", 14, t + 94, 116, 32, true);
            btnShowProbabilities = MakeButton("号码画像", 138, t + 94, 104, 32);
            btnAnalysis = MakeButton("统计分析", 250, t + 94, 104, 32);
            btnPredict.Click += (s, e) => MakeBayesianPrediction((int)numPredictCount.Value);
            btnShowProbabilities.Click += (s, e) => ShowNumberProfile();
            btnAnalysis.Click += (s, e) => ShowNumberAnalysis();

            var tip = MakeLabel("策略不改变中奖期望；勾选固定种子可复现同一组结果", 14, t + 136, true);

            card.Controls.AddRange(new Control[] {
                MakeLabel("统计期数", 14, t + 3), numNPeriods, btnUpdateN,
                MakeLabel("组数", 256, t + 3), numPredictCount,
                MakeLabel("策略", 14, t + 36), cmbStrategy, chkStructureFilter,
                chkUseSeed, numSeed, MakeLabel("可复现结果", 206, t + 66, true),
                btnPredict, btnShowProbabilities, btnAnalysis, tip });
            y += 200 + CardGap;
        }

        private void CreateBacktestCard(ref int y)
        {
            var card = MakeCard("模型评估 · 回测", y, 142, out int t);
            numBacktestPeriods = MakeNumeric(10, 2000, 200, 84, t, 72);
            numBacktestBets = MakeNumeric(1, 200, 20, 240, t, 72);
            btnBacktest = MakeButton("回测评估", 14, t + 34, 150, 32);
            btnBacktest.Click += (s, e) => RunBacktest();
            var tip = MakeLabel("Walk-forward：只用当期之前的数据训练，对照均匀随机基准检验策略", 14, t + 78, true);
            card.Controls.AddRange(new Control[] {
                MakeLabel("回测期数", 14, t + 3), numBacktestPeriods,
                MakeLabel("每期注数", 170, t + 3), numBacktestBets,
                btnBacktest, MakeLabel("与均匀随机对照", 174, t + 42, true), tip });
            y += 142 + CardGap;
        }

        private void CreateAnalysisCard(ref int y)
        {
            var card = MakeCard("特定号码分析", y, 110, out int t);
            txtTargetFront = MakeTextBox("08 13", 56, t, 120);
            txtTargetBack = MakeTextBox("08 12", 56, t + 36, 120);
            btnFrontAnalysis = MakeButton("前区分析", 186, t - 2, 96, 28);
            btnBackAnalysis = MakeButton("后区分析", 186, t + 34, 96, 28);
            btnFrontAnalysis.Click += (s, e) => PerformTargetAnalysis(true);
            btnBackAnalysis.Click += (s, e) => PerformTargetAnalysis(false);
            card.Controls.AddRange(new Control[] {
                MakeLabel("前区", 14, t + 4), txtTargetFront, btnFrontAnalysis,
                MakeLabel("后区", 14, t + 40), txtTargetBack, btnBackAnalysis });
            y += 110 + CardGap;
        }

        #endregion

        #region 数据网格与状态栏

        private void SetupDataGridView()
        {
            dataGridViewHistory.AutoGenerateColumns = false;
            dataGridViewHistory.Columns.Clear();
            dataGridViewHistory.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "IssueNumber", HeaderText = "期号", FillWeight = 90,
                MinimumWidth = 70
            });
            dataGridViewHistory.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "DrawDate", HeaderText = "开奖日期", FillWeight = 110,
                MinimumWidth = 90,
                DefaultCellStyle = new DataGridViewCellStyle { Format = "yyyy-MM-dd" }
            });
            for (int i = 0; i < 5; i++)
                dataGridViewHistory.Columns.Add(new DataGridViewTextBoxColumn
                {
                    DataPropertyName = $"Front{i + 1}", HeaderText = $"前{i + 1}",
                    FillWeight = 52, MinimumWidth = 42
                });
            for (int i = 0; i < 2; i++)
                dataGridViewHistory.Columns.Add(new DataGridViewTextBoxColumn
                {
                    DataPropertyName = $"Back{i + 1}", HeaderText = $"后{i + 1}",
                    FillWeight = 52, MinimumWidth = 42
                });
            ApplyGridStyle(dataGridViewHistory, true);
            dataGridViewHistory.DataSource = dataBindingList;
        }

        private void UpdateDataGridView()
        {
            var sortedData = historicalData
                .OrderByDescending(d => d.DrawDate)
                .ThenByDescending(d => d.IssueNumber, StringComparer.Ordinal)
                .ToList();
            dataBindingList = new BindingList<LotteryData>(sortedData);
            dataGridViewHistory.DataSource = dataBindingList;

            if (historicalData.Count > 0)
            {
                var min = historicalData.Min(d => d.DrawDate);
                var max = historicalData.Max(d => d.DrawDate);
                lblSummary.Text = $"共 {historicalData.Count:N0} 期  ·  {min:yyyy-MM-dd} ~ {max:yyyy-MM-dd}  ·  已按期号去重 · 倒序";
            }
            else
            {
                lblSummary.Text = "尚未加载数据，请点击左侧「导入数据」";
            }
        }

        private void UpdateStatus()
        {
            lblFileStatus.Text = historicalData.Count > 0
                ? $"已加载 {historicalData.Count:N0} 期历史开奖数据"
                : "未加载数据";
            lblFileStatus.ForeColor = historicalData.Count > 0 ? Accent : TextMuted;

            string strategy = PredictionOptions.GetStrategyName(SelectedStrategy());
            string filter = chkStructureFilter?.Checked == true ? "开启" : "关闭";
            toolStripStatusLabel.Text =
                $"  数据 {historicalData.Count:N0} 期   |   N = {bayesianModel.GetCurrentNPeriods()}   |   " +
                $"策略：{strategy}   |   结构过滤：{filter}";
            lblStatusTime.Text = DateTime.Now.ToString("HH:mm:ss");
            if (!string.IsNullOrEmpty(lastLoadSummary))
                toolStripStatusLabel.ToolTipText = lastLoadSummary;
        }

        #endregion

        #region 文件操作

        /// <summary>启动时自动加载随程序一同发布的历史数据文件。</summary>
        private void AutoLoadDefaultData()
        {
            try
            {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "大乐透数据.txt");
                if (!File.Exists(path)) return;

                var load = dataService.LoadFromFileDetailed(path);
                if (load.Records.Count == 0) return;

                historicalData = load.Records;
                bayesianModel.TrainModel(historicalData);
                lastLoadSummary = load.Summary;
            }
            catch (Exception ex)
            {
                lastLoadSummary = $"自动加载数据失败: {ex.Message}";
            }
        }

        private void BtnImport_Click(object? sender, EventArgs e)
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Filter = "文本文件|*.txt|所有文件|*.*";
                dialog.Title = "选择历史数据文件";
                if (dialog.ShowDialog() != DialogResult.OK) return;

                try
                {
                    var load = dataService.LoadFromFileDetailed(dialog.FileName);
                    if (load.ValidRecords == 0)
                    {
                        MessageBox.Show($"文件中没有有效数据。\r\n\r\n{load.Summary}", "导入失败",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    historicalData = dataService.MergeRecords(
                        historicalData, load.Records, out int added, out int _);
                    bayesianModel.TrainModel(historicalData);
                    lastLoadSummary = load.Summary;
                    UpdateDataGridView();
                    UpdateStatus();

                    MessageBox.Show(
                        $"导入完成：净新增 {added} 期，当前共 {historicalData.Count} 期。\r\n\r\n{load.Summary}",
                        "导入成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"导入失败: {ex.Message}", "错误",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void BtnExport_Click(object? sender, EventArgs e)
        {
            if (historicalData.Count == 0)
            {
                MessageBox.Show("没有数据可导出", "提示");
                return;
            }

            using (var dialog = new SaveFileDialog())
            {
                dialog.Filter = "文本文件|*.txt|所有文件|*.*";
                dialog.Title = "导出数据到文件";
                dialog.FileName = $"大乐透数据_{DateTime.Now:yyyyMMdd}.txt";
                if (dialog.ShowDialog() != DialogResult.OK) return;

                try
                {
                    dataService.SaveToFile(dialog.FileName, historicalData);
                    MessageBox.Show($"成功导出 {historicalData.Count} 条数据（已按期号去重并排序）",
                        "导出成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"导出失败: {ex.Message}", "错误",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void BtnClear_Click(object? sender, EventArgs e)
        {
            if (MessageBox.Show("确定要清空所有数据吗？", "确认清空",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

            historicalData.Clear();
            bayesianModel.TrainModel(historicalData);
            lastLoadSummary = string.Empty;
            UpdateDataGridView();
            UpdateStatus();
        }

        #endregion

        #region 手动输入

        private void AddManualRecord()
        {
            try
            {
                var frontParts = txtFront.Text.Trim()
                    .Split(new[] { ' ', '\t', ',', '，' }, StringSplitOptions.RemoveEmptyEntries);
                if (frontParts.Length != 5)
                {
                    MessageBox.Show("前区必须输入 5 个号码，用空格分隔", "输入错误");
                    return;
                }

                var front = new int[5];
                for (int i = 0; i < 5; i++)
                {
                    if (!int.TryParse(frontParts[i], out front[i]) || front[i] < 1 || front[i] > 35)
                    {
                        MessageBox.Show($"前区号码必须在 1-35 之间: {frontParts[i]}", "输入错误");
                        return;
                    }
                }

                var backParts = txtBack.Text.Trim()
                    .Split(new[] { ' ', '\t', ',', '，' }, StringSplitOptions.RemoveEmptyEntries);
                if (backParts.Length != 2)
                {
                    MessageBox.Show("后区必须输入 2 个号码，用空格分隔", "输入错误");
                    return;
                }

                var back = new int[2];
                for (int i = 0; i < 2; i++)
                {
                    if (!int.TryParse(backParts[i], out back[i]) || back[i] < 1 || back[i] > 12)
                    {
                        MessageBox.Show($"后区号码必须在 1-12 之间: {backParts[i]}", "输入错误");
                        return;
                    }
                }

                if (front.Distinct().Count() != 5) { MessageBox.Show("前区号码不能重复", "输入错误"); return; }
                if (back.Distinct().Count() != 2) { MessageBox.Show("后区号码不能重复", "输入错误"); return; }

                Array.Sort(front);
                Array.Sort(back);
                string issue = txtIssue.Text.Trim();
                var record = new LotteryData(
                    string.IsNullOrEmpty(issue) ? $"手动{DateTime.Now:MMddHHmmss}" : issue,
                    DateTime.Now, front, back);

                historicalData = dataService.MergeRecords(
                    historicalData, new List<LotteryData> { record }, out _, out _);
                bayesianModel.TrainModel(historicalData);
                UpdateDataGridView();
                UpdateStatus();

                txtIssue.Clear();
                txtFront.Text = "01 05 10 15 20";
                txtBack.Text = "01 02";
                MessageBox.Show("记录添加成功", "成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"添加失败: {ex.Message}", "错误",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        #endregion

        #region 号码生成与模型评估

        private GenerationStrategy SelectedStrategy()
        {
            return cmbStrategy.SelectedIndex switch
            {
                1 => GenerationStrategy.Uniform,
                2 => GenerationStrategy.Hot,
                3 => GenerationStrategy.Cold,
                _ => GenerationStrategy.Balanced
            };
        }

        private PredictionOptions BuildCurrentOptions()
        {
            var opt = bayesianModel.GetOptions();
            opt.Strategy = SelectedStrategy();
            opt.EnableStructureFilter = chkStructureFilter.Checked;
            opt.RandomSeed = chkUseSeed.Checked ? (int?)numSeed.Value : null;
            return opt;
        }

        private void MakeBayesianPrediction(int numberOfPredictions)
        {
            if (historicalData.Count < 5)
            {
                MessageBox.Show("历史数据不足，请至少导入 5 期数据", "数据不足");
                return;
            }

            try
            {
                var opt = BuildCurrentOptions();
                bayesianModel.SetOptions(opt);
                bayesianModel.SetNPeriods((int)numNPeriods.Value);

                Cursor = Cursors.WaitCursor;
                var result = bayesianModel.Predict(numberOfPredictions, opt);
                Cursor = Cursors.Default;
                DisplayBayesianResults(result);
                UpdateStatus();
            }
            catch (Exception ex)
            {
                Cursor = Cursors.Default;
                MessageBox.Show($"生成失败: {ex.Message}", "错误",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void DisplayBayesianResults(BayesianPredictionResult result)
        {
            var form = new Form
            {
                Text = $"号码生成结果 - {result.StrategyName}",
                Size = new Size(900, 580),
                MinimumSize = new Size(760, 460),
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.Sizable,
                MinimizeBox = false,
                MaximizeBox = false,
                BackColor = Bg,
                Font = UiFont
            };

            var table = new DataTable();
            table.Columns.Add("序号", typeof(int));
            table.Columns.Add("前区", typeof(string));
            table.Columns.Add("后区", typeof(string));
            table.Columns.Add("和值", typeof(int));
            table.Columns.Add("奇偶", typeof(string));
            table.Columns.Add("大小", typeof(string));
            table.Columns.Add("AC", typeof(int));
            table.Columns.Add("贴合度", typeof(string));
            table.Columns.Add("结构", typeof(string));

            int shown = Math.Min(result.Predictions.Count, 1000);
            for (int i = 0; i < shown; i++)
            {
                var p = result.Predictions[i];
                table.Rows.Add(
                    i + 1,
                    string.Join(" ", p.FrontNumbers.Select(n => n.ToString("D2"))),
                    string.Join(" ", p.BackNumbers.Select(n => n.ToString("D2"))),
                    p.FrontSum, p.OddEvenRatio, p.BigSmallRatio, p.AC,
                    p.ModelScore.ToString("F1"),
                    p.StructureValid ? "OK" : "放宽");
            }

            var grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                DataSource = table
            };
            ApplyGridStyle(grid);

            var summary = new TextBox
            {
                Dock = DockStyle.Bottom,
                Height = 126,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Font = new Font("Consolas", 9F),
                BackColor = Surface,
                ForeColor = TextDark,
                BorderStyle = BorderStyle.None,
                Text = BuildResultSummary(result, shown)
            };

            var btnExport = new Button
            {
                Dock = DockStyle.Bottom,
                Height = 36,
                Text = "导出全部结果到文本文件...",
                FlatStyle = FlatStyle.Flat,
                BackColor = Surface,
                ForeColor = AccentDeep,
                Font = UiFontBold,
                UseVisualStyleBackColor = false,
                Cursor = Cursors.Hand
            };
            btnExport.FlatAppearance.BorderColor = Border;
            btnExport.FlatAppearance.MouseOverBackColor = Color.FromArgb(239, 246, 255);
            btnExport.Click += (s, e) => ExportPredictionResult(result);

            // Dock 布局：索引大的先停靠，故 Fill 控件最后添加
            form.Controls.Add(grid);
            form.Controls.Add(summary);
            form.Controls.Add(btnExport);
            form.ShowDialog(this);
        }

        private static string BuildResultSummary(BayesianPredictionResult r, int shown)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"策略: {r.StrategyName} | 统计窗口: 最近 {r.WindowSize} 期 / 共 {r.TotalDraws} 期 | " +
                          $"结构过滤: {(r.StructureFilterEnabled ? "开启" : "关闭")}");
            sb.AppendLine($"生成 {r.Predictions.Count} 注 | 耗时 {r.ElapsedMilliseconds:F0} ms" +
                          (r.RelaxedCount > 0 ? $" | 放宽约束 {r.RelaxedCount} 注" : "") +
                          (r.DuplicatesSkipped > 0 ? $" | 跳过重复 {r.DuplicatesSkipped} 注" : "") +
                          (r.Predictions.Count > shown ? $" | 表格仅显示前 {shown} 注" : ""));
            sb.AppendLine($"热号(前区): {FormatNumbers(r.HotFrontNumbers)}    热号(后区): {FormatNumbers(r.HotBackNumbers)}");
            sb.AppendLine($"冷号(前区,按遗漏): {FormatNumbers(r.ColdFrontNumbers)}    冷号(后区): {FormatNumbers(r.ColdBackNumbers)}");
            sb.AppendLine("贴合度仅表示组合与历史统计特征的吻合程度，与中奖概率无关。彩票每期独立随机开奖，请理性购彩。");
            return sb.ToString();
        }

        private static string FormatNumbers(int[] nums) =>
            nums == null || nums.Length == 0
                ? "无"
                : string.Join(" ", nums.Select(n => n.ToString("D2")));

        private void ExportPredictionResult(BayesianPredictionResult result)
        {
            using (var dialog = new SaveFileDialog())
            {
                dialog.Filter = "文本文件|*.txt|所有文件|*.*";
                dialog.FileName = $"生成结果_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
                if (dialog.ShowDialog() != DialogResult.OK) return;

                try
                {
                    var sb = new StringBuilder();
                    sb.AppendLine(result.ToReport());
                    sb.AppendLine();
                    sb.AppendLine("=== 全部组合 ===");
                    foreach (var p in result.Predictions) sb.AppendLine(p.ToString());
                    File.WriteAllText(dialog.FileName, sb.ToString(), new UTF8Encoding(false));
                    MessageBox.Show($"成功导出 {result.Predictions.Count} 注", "导出成功");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"导出失败: {ex.Message}", "错误",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        /// <summary>号码统计画像：概率、提升比、遗漏、平均间隔等。</summary>
        private void ShowNumberProfile()
        {
            if (!EnsureData()) return;

            var ns = bayesianModel.GetNumberStatistics();
            var sb = new StringBuilder();
            sb.AppendLine("=== 号码统计画像 ===");
            sb.AppendLine($"总期数: {ns.TotalDraws} | 统计窗口: 最近 {ns.WindowSize} 期 | " +
                          $"时间衰减半衰期: {ns.RecencyHalfLife:F1} 期");
            sb.AppendLine("出现率 = 归一化抽样概率 × 每期开出号码数；提升比 = 出现率 / 均匀出现率。");
            sb.AppendLine("提升比 > 1 表示近期偏热，< 1 表示偏冷；收缩估计会主动把极端值拉回 1 附近。");
            sb.AppendLine();
            AppendNumberTable(sb, "前区 (均匀基准 5/35 ≈ 14.29%)", ns.Front);
            AppendNumberTable(sb, "后区 (均匀基准 2/12 ≈ 16.67%)", ns.Back);
            ShowTextWindow("号码统计画像", sb.ToString(), new Size(780, 640));
        }

        private static void AppendNumberTable(StringBuilder sb, string title, NumberStatistic[] arr)
        {
            sb.AppendLine($"【{title}】按提升比降序");
            sb.AppendLine(new string('=', 84));
            sb.AppendLine(string.Format("{0,-6}{1,-11}{2,-10}{3,-9}{4,-10}{5,-10}{6,-10}{7,-11}{8,-8}",
                "号码", "出现率", "提升比", "总次数", "窗口次数", "当前遗漏", "最大遗漏", "平均间隔", "近10期"));
            sb.AppendLine(new string('-', 84));
            foreach (var n in arr.OrderByDescending(x => x.Lift).ThenBy(x => x.Number))
            {
                sb.AppendLine(string.Format("{0,-6}{1,-11:P3}{2,-10:F3}{3,-9}{4,-10}{5,-10}{6,-10}{7,-11:F1}{8,-8}",
                    n.Number.ToString("D2"), n.AppearanceRate, n.Lift, n.TotalCount, n.WindowCount,
                    n.Omission, n.MaxOmission, n.AverageInterval, n.IsRecentHot ? "是" : "-"));
            }
            sb.AppendLine();
        }

        private void ShowNumberAnalysis()
        {
            if (!EnsureData()) return;

            var sb = new StringBuilder();
            sb.AppendLine("=== 大乐透统计分析 ===\r\n");
            sb.AppendLine("数据统计:");
            sb.AppendLine($"总期数: {historicalData.Count}（已按期号去重）");
            sb.AppendLine($"数据时间范围: {historicalData.Min(d => d.DrawDate):yyyy-MM-dd} 至 " +
                          $"{historicalData.Max(d => d.DrawDate):yyyy-MM-dd}");
            sb.AppendLine($"当前模型使用期数(N): {bayesianModel.GetCurrentNPeriods()}");
            sb.AppendLine($"策略: {PredictionOptions.GetStrategyName(SelectedStrategy())}\r\n");

            var frontCounts = new Dictionary<int, int>();
            var backCounts = new Dictionary<int, int>();
            for (int i = 1; i <= 35; i++) frontCounts[i] = 0;
            for (int i = 1; i <= 12; i++) backCounts[i] = 0;
            foreach (var record in historicalData)
            {
                foreach (var num in record.FrontNumbers) frontCounts[num]++;
                foreach (var num in record.BackNumbers) backCounts[num]++;
            }

            sb.AppendLine("前区号码出现次数 (全量历史):");
            int counter = 1;
            foreach (var item in frontCounts.OrderByDescending(kvp => kvp.Value).ThenBy(kvp => kvp.Key))
            {
                double rate = (double)item.Value / historicalData.Count;
                sb.AppendLine($"{counter:D2}. 号码 {item.Key:D2}: {item.Value} 次 | " +
                              $"出现率 {rate:P1} | 提升比 {rate / (5.0 / 35):F3}");
                counter++;
            }

            sb.AppendLine("\r\n后区号码出现次数 (全量历史):");
            counter = 1;
            foreach (var item in backCounts.OrderByDescending(kvp => kvp.Value).ThenBy(kvp => kvp.Key))
            {
                double rate = (double)item.Value / historicalData.Count;
                sb.AppendLine($"{counter:D2}. 号码 {item.Key:D2}: {item.Value} 次 | " +
                              $"出现率 {rate:P1} | 提升比 {rate / (2.0 / 12):F3}");
                counter++;
            }

            if (!string.IsNullOrEmpty(lastLoadSummary))
            {
                sb.AppendLine("\r\n数据加载质量:");
                sb.AppendLine(lastLoadSummary);
            }
            ShowTextWindow("统计分析", sb.ToString(), new Size(740, 640));
        }

        private void RunBacktest()
        {
            if (!EnsureData()) return;
            if (bayesianModel.GetTotalDataCount() < 50)
            {
                MessageBox.Show("回测至少需要 50 期历史数据", "数据不足");
                return;
            }

            try
            {
                var opt = BuildCurrentOptions();
                bayesianModel.SetOptions(opt);
                bayesianModel.SetNPeriods((int)numNPeriods.Value);

                Cursor = Cursors.WaitCursor;
                var result = bayesianModel.RunBacktest(
                    (int)numBacktestPeriods.Value, (int)numBacktestBets.Value);
                Cursor = Cursors.Default;
                ShowTextWindow("回测评估报告", result.ToReport(), new Size(800, 660));
            }
            catch (Exception ex)
            {
                Cursor = Cursors.Default;
                MessageBox.Show($"回测失败: {ex.Message}", "错误",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private bool EnsureData()
        {
            if (historicalData.Count > 0) return true;
            MessageBox.Show("请先导入历史数据", "提示");
            return false;
        }

        /// <summary>通用可滚动文本窗口，替代 MessageBox 显示长报告。</summary>
        private void ShowTextWindow(string title, string text, Size? size = null)
        {
            var form = new Form
            {
                Text = title,
                Size = size ?? new Size(740, 600),
                MinimumSize = new Size(560, 400),
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.Sizable,
                BackColor = Surface,
                Font = UiFont
            };
            var box = new TextBox
            {
                Text = text,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Both,
                WordWrap = false,
                Dock = DockStyle.Fill,
                Font = new Font("Consolas", 9.5F),
                BackColor = Surface,
                ForeColor = TextDark,
                BorderStyle = BorderStyle.None
            };
            form.Controls.Add(box);
            form.ShowDialog(this);
        }

        #endregion

        #region 特定号码分析

        private void PerformTargetAnalysis(bool isFront)
        {
            if (!EnsureData()) return;

            string input = isFront ? txtTargetFront.Text.Trim() : txtTargetBack.Text.Trim();
            int maxNum = isFront ? 35 : 12;
            string areaName = isFront ? "前区" : "后区";

            var parts = input.Split(new[] { ' ', '\t', ',', '，' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
            {
                MessageBox.Show($"请输入{areaName}号码", "输入错误");
                return;
            }

            var targetNumbers = new List<int>();
            foreach (var part in parts)
            {
                if (!int.TryParse(part, out int num) || num < 1 || num > maxNum)
                {
                    MessageBox.Show($"{areaName}号码必须在 1-{maxNum} 之间: {part}", "输入错误");
                    return;
                }
                if (!targetNumbers.Contains(num)) targetNumbers.Add(num);
            }

            var matchingRecords = historicalData.Where(r =>
                targetNumbers.All(tn => (isFront ? r.FrontNumbers : r.BackNumbers).Contains(tn))
            ).ToList();

            var sb = new StringBuilder();
            sb.AppendLine($"=== {areaName}特定号码分析 ===\r\n");
            sb.AppendLine($"目标号码: {string.Join(" ", targetNumbers.Select(n => n.ToString("D2")))}");
            sb.AppendLine($"匹配期数: {matchingRecords.Count} / {historicalData.Count} 期");
            sb.AppendLine($"出现概率: {(double)matchingRecords.Count / historicalData.Count:P2}\r\n");

            if (matchingRecords.Count == 0)
            {
                sb.AppendLine("没有找到同时包含这些号码的期数");
            }
            else
            {
                sb.AppendLine("【详细期数列表】");
                sb.AppendLine(new string('=', 65));
                sb.AppendLine("期号        日期        前区号码              后区号码");
                sb.AppendLine(new string('-', 65));
                foreach (var record in matchingRecords.OrderBy(r => r.DrawDate))
                {
                    string front = string.Join(" ", record.FrontNumbers.OrderBy(n => n).Select(n => n.ToString("D2")));
                    string back = string.Join(" ", record.BackNumbers.OrderBy(n => n).Select(n => n.ToString("D2")));
                    sb.AppendLine($"{record.IssueNumber,-12}{record.DrawDate:yyyy-MM-dd}  {front,-20}{back}");
                }
                sb.AppendLine();

                if (isFront)
                {
                    var otherFront = new List<int>();
                    var allBack = new List<int>();
                    foreach (var record in matchingRecords)
                    {
                        otherFront.AddRange(record.FrontNumbers.Where(n => !targetNumbers.Contains(n)));
                        allBack.AddRange(record.BackNumbers);
                    }
                    AppendFrequency(sb, "其他前区号码频率", otherFront, matchingRecords.Count);
                    AppendFrequency(sb, "后区号码频率", allBack, matchingRecords.Count);
                }
                else
                {
                    var allFront = new List<int>();
                    var otherBack = new List<int>();
                    foreach (var record in matchingRecords)
                    {
                        allFront.AddRange(record.FrontNumbers);
                        otherBack.AddRange(record.BackNumbers.Where(n => !targetNumbers.Contains(n)));
                    }
                    AppendFrequency(sb, "前区号码频率", allFront, matchingRecords.Count);
                    if (otherBack.Count > 0)
                        AppendFrequency(sb, "其他后区号码频率", otherBack, matchingRecords.Count);
                }
            }

            sb.AppendLine();
            sb.AppendLine($"分析完成时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            ShowTextWindow($"{areaName}特定号码分析结果", sb.ToString(), new Size(660, 540));
        }

        private static void AppendFrequency(StringBuilder sb, string title, List<int> numbers, int drawCount)
        {
            sb.AppendLine($"【{title}】");
            sb.AppendLine(new string('=', 65));
            sb.AppendLine("号码  出现次数  出现概率");
            sb.AppendLine(new string('-', 65));
            var groups = numbers.GroupBy(n => n)
                .Select(g => new { Number = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count).ThenBy(x => x.Number);
            foreach (var g in groups)
            {
                double freq = (double)g.Count / drawCount * 100;
                sb.AppendLine($"{g.Number:D2}     {g.Count,2}次      {freq,5:0.00}%");
            }
            sb.AppendLine();
        }

        #endregion

        #region 卡片控件（自绘标题栏）

        private sealed class CardPanel : Panel
        {
            public string CardTitle { get; set; } = string.Empty;

            public CardPanel()
            {
                DoubleBuffered = true;
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                         ControlStyles.OptimizedDoubleBuffer, true);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.Clear(Surface);
                using (var accent = new SolidBrush(Accent))
                    g.FillRectangle(accent, 0, 0, 4, 34);
                TextRenderer.DrawText(g, CardTitle, TitleFont, new Point(15, 8), AccentDeep);
                using (var pen = new Pen(Border))
                {
                    g.DrawLine(pen, 12, 34, Width - 12, 34);
                    g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
                }
            }
        }

        #endregion

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            SuspendLayout();
            AutoScaleMode = AutoScaleMode.Font;
            Font = UiFont;
            BackColor = Bg;
            ForeColor = TextDark;
            ClientSize = new Size(1240, 900);
            MinimumSize = new Size(1020, 640);
            StartPosition = FormStartPosition.CenterScreen;
            Text = "大乐透分析预测系统 v3.0";
            Name = "MainForm";
            Load += MainForm_Load;
            ResumeLayout(false);
        }

        /// <summary>布局完成后再设定左右分栏宽度，避免 SplitterDistance 约束冲突。</summary>
        private void MainForm_Load(object? sender, EventArgs e)
        {
            // 小屏幕（如 1366x768）上避免窗口超出工作区
            var wa = Screen.FromControl(this).WorkingArea;
            int w = Math.Max(MinimumSize.Width, Math.Min(1240, wa.Width - 40));
            int h = Math.Max(MinimumSize.Height, Math.Min(900, wa.Height - 60));
            Size = new Size(w, h);

            int upper = splitMain.Width - splitMain.Panel2MinSize;
            splitMain.SplitterDistance = Math.Max(splitMain.Panel1MinSize, Math.Min(440, upper));
        }

        #endregion
    }
}
