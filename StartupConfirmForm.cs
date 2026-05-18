using System.Reflection;

namespace CafeGameEnvironmentAssistant;

public sealed class StartupConfirmForm : Form
{
    public StartupConfirmForm()
    {
        Text = "启动确认";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        BackColor = Color.FromArgb(24, 26, 31);
        ForeColor = Color.Gainsboro;
        Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
        ClientSize = new Size(520, 700);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(18),
            BackColor = BackColor
        };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));

        var pictureBox = new PictureBox
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(18, 20, 24),
            SizeMode = PictureBoxSizeMode.Zoom,
            Image = LoadEmbeddedImage()
        };

        var promptLabel = new Label
        {
            Dock = DockStyle.Fill,
            Text = "是否继续运行程序？",
            ForeColor = Color.WhiteSmoke,
            Font = new Font("Microsoft YaHei UI", 12F, FontStyle.Bold, GraphicsUnit.Point),
            TextAlign = ContentAlignment.MiddleCenter
        };

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(0, 6, 0, 0)
        };

        var confirmButton = CreateButton("确认");
        confirmButton.DialogResult = DialogResult.OK;

        var cancelButton = CreateButton("取消");
        cancelButton.DialogResult = DialogResult.Cancel;

        AcceptButton = confirmButton;
        CancelButton = cancelButton;

        buttonPanel.Controls.Add(confirmButton);
        buttonPanel.Controls.Add(cancelButton);

        root.Controls.Add(pictureBox, 0, 0);
        root.Controls.Add(promptLabel, 0, 1);
        root.Controls.Add(buttonPanel, 0, 2);
        Controls.Add(root);
    }

    private static Button CreateButton(string text)
    {
        var button = new Button
        {
            Text = text,
            Width = 110,
            Height = 36,
            Margin = new Padding(10, 0, 0, 0),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(38, 42, 51),
            ForeColor = Color.WhiteSmoke
        };
        button.FlatAppearance.BorderColor = Color.FromArgb(70, 76, 88);
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(54, 60, 72);
        button.FlatAppearance.MouseDownBackColor = Color.FromArgb(66, 74, 88);
        return button;
    }

    private static Image LoadEmbeddedImage()
    {
        using var stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream("assets/startup-confirm.jpg")
            ?? throw new InvalidOperationException("未找到启动确认图片资源。");

        using var sourceImage = Image.FromStream(stream);
        return new Bitmap(sourceImage);
    }
}
