using NetCafeGameAssistant.Models;

namespace NetCafeGameAssistant;

public sealed class MainForm : Form
{
    private readonly GoCoreRunner _runner = new();
    private readonly DataGridView _grid = new();
    private readonly TextBox _logBox = new();
    private readonly Button _checkButton = new();
    private readonly Button _repairButton = new();
    private readonly Button _xboxButton = new();
    private readonly Button _firewallButton = new();
    private readonly Button _exportLogButton = new();
    private readonly System.Windows.Forms.Timer _logTimer = new() { Interval = 600 };
    private DateTime _lastLogWriteTimeUtc = DateTime.MinValue;

    public MainForm()
    {
        Text = "网吧游戏环境助手 · 免重启版";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1060, 720);
        Size = new Size(1180, 780);
        BackColor = Color.FromArgb(24, 26, 31);
        ForeColor = Color.Gainsboro;
        Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);

        InitializeLayout();
        InitializeLogTimer();

        Shown += async (_, _) =>
        {
            try
            {
                GoCoreExtractor.Extract();
                await RunCoreAsync("check");
            }
            catch (Exception ex)
            {
                AppendLog("启动失败：" + ex.Message);
                MessageBox.Show(ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        };
    }

    private void InitializeLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(16),
            BackColor = BackColor
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 180));

        var title = new Label
        {
            Dock = DockStyle.Fill,
            Text = "网吧游戏环境助手 · 免重启版",
            Font = new Font("Microsoft YaHei UI", 20F, FontStyle.Bold, GraphicsUnit.Point),
            ForeColor = Color.White,
            TextAlign = ContentAlignment.MiddleLeft
        };

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = BackColor
        };

        ConfigureButton(_checkButton, "一键体检");
        ConfigureButton(_repairButton, "免重启修复");
        ConfigureButton(_xboxButton, "离线修复 Xbox");
        ConfigureButton(_firewallButton, "防火墙兼容修复");
        ConfigureButton(_exportLogButton, "导出日志");

        _checkButton.Click += async (_, _) => await RunCoreAsync("check");
        _repairButton.Click += async (_, _) => await RunCoreAsync("repair");
        _xboxButton.Click += async (_, _) => await RunCoreAsync("xbox");
        _firewallButton.Click += async (_, _) => await RunCoreAsync("firewall");
        _exportLogButton.Click += (_, _) => ExportLog();

        buttonPanel.Controls.AddRange([
            _checkButton,
            _repairButton,
            _xboxButton,
            _firewallButton,
            _exportLogButton
        ]);

        ConfigureGrid();
        ConfigureLogBox();

        root.Controls.Add(title, 0, 0);
        root.Controls.Add(buttonPanel, 0, 1);
        root.Controls.Add(_grid, 0, 2);
        root.Controls.Add(_logBox, 0, 3);
        Controls.Add(root);
    }

    private static void ConfigureButton(Button button, string text)
    {
        button.Text = text;
        button.Width = 150;
        button.Height = 36;
        button.Margin = new Padding(0, 2, 10, 0);
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderColor = Color.FromArgb(70, 76, 88);
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(54, 60, 72);
        button.FlatAppearance.MouseDownBackColor = Color.FromArgb(66, 74, 88);
        button.BackColor = Color.FromArgb(38, 42, 51);
        button.ForeColor = Color.WhiteSmoke;
    }

    private void ConfigureGrid()
    {
        _grid.Dock = DockStyle.Fill;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.AllowUserToResizeRows = false;
        _grid.ReadOnly = true;
        _grid.RowHeadersVisible = false;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.MultiSelect = false;
        _grid.BackgroundColor = Color.FromArgb(30, 33, 40);
        _grid.BorderStyle = BorderStyle.FixedSingle;
        _grid.GridColor = Color.FromArgb(56, 62, 74);
        _grid.EnableHeadersVisualStyles = false;
        _grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(42, 47, 57);
        _grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.WhiteSmoke;
        _grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(42, 47, 57);
        _grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = Color.WhiteSmoke;
        _grid.DefaultCellStyle.BackColor = Color.FromArgb(30, 33, 40);
        _grid.DefaultCellStyle.ForeColor = Color.Gainsboro;
        _grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(55, 61, 74);
        _grid.DefaultCellStyle.SelectionForeColor = Color.White;
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
        _grid.Columns.Add("Type", "类型");
        _grid.Columns.Add("Item", "项目");
        _grid.Columns.Add("Status", "状态");
        _grid.Columns.Add("Description", "说明");
        _grid.Columns["Type"]!.Width = 150;
        _grid.Columns["Item"]!.Width = 260;
        _grid.Columns["Status"]!.Width = 110;
        _grid.Columns["Description"]!.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
    }

    private void ConfigureLogBox()
    {
        _logBox.Dock = DockStyle.Fill;
        _logBox.Multiline = true;
        _logBox.ReadOnly = true;
        _logBox.ScrollBars = ScrollBars.Vertical;
        _logBox.BackColor = Color.FromArgb(14, 17, 23);
        _logBox.ForeColor = Color.Gainsboro;
        _logBox.Font = new Font("Consolas", 9.5F, FontStyle.Regular, GraphicsUnit.Point);
        _logBox.BorderStyle = BorderStyle.FixedSingle;
    }

    private void InitializeLogTimer()
    {
        _logTimer.Tick += (_, _) => RefreshLogBox();
        _logTimer.Start();
    }

    private async Task RunCoreAsync(string command)
    {
        SetButtonsEnabled(false);
        UseWaitCursor = true;
        try
        {
            AppendLog("执行命令：" + command);
            var items = await _runner.RunAsync(command);
            BindItems(items);
            RefreshLogBox(force: true);
        }
        catch (Exception ex)
        {
            AppendLog("执行失败：" + ex.Message);
            MessageBox.Show(ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            UseWaitCursor = false;
            Cursor.Current = Cursors.Default;
            SetButtonsEnabled(true);
        }
    }

    private void BindItems(IEnumerable<CheckItem> items)
    {
        _grid.Rows.Clear();
        foreach (var item in items)
        {
            var rowIndex = _grid.Rows.Add(item.Type, item.Item, item.Status, item.Description);
            _grid.Rows[rowIndex].Cells["Status"]!.Style.ForeColor = item.Status switch
            {
                "正常" => Color.FromArgb(96, 214, 126),
                "停止" => Color.FromArgb(244, 196, 81),
                _ => Color.FromArgb(240, 106, 106)
            };
        }
    }

    private void SetButtonsEnabled(bool enabled)
    {
        _checkButton.Enabled = enabled;
        _repairButton.Enabled = enabled;
        _xboxButton.Enabled = enabled;
        _firewallButton.Enabled = enabled;
        _exportLogButton.Enabled = enabled;
    }

    private void ExportLog()
    {
        try
        {
            var exportPath = GoCoreRunner.ExportLog();
            MessageBox.Show("日志已导出：\r\n" + exportPath, Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show("导出日志失败：\r\n" + ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void RefreshLogBox(bool force = false)
    {
        try
        {
            if (!File.Exists(GoCoreExtractor.LatestLogPath))
            {
                return;
            }

            var lastWrite = File.GetLastWriteTimeUtc(GoCoreExtractor.LatestLogPath);
            if (!force && lastWrite == _lastLogWriteTimeUtc)
            {
                return;
            }

            _lastLogWriteTimeUtc = lastWrite;
            _logBox.Text = File.ReadAllText(GoCoreExtractor.LatestLogPath);
            _logBox.SelectionStart = _logBox.TextLength;
            _logBox.ScrollToCaret();
        }
        catch
        {
            // Ignore transient file sharing while GoRepairCore writes logs.
        }
    }

    private void AppendLog(string message)
    {
        _logBox.AppendText($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
    }
}
