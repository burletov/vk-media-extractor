namespace MediaExtractorForVK;

internal sealed class ImageComparisonForm : Form
{
    private readonly ZoomableImageControl _left = new() { Dock = DockStyle.Fill };
    private readonly ZoomableImageControl _right = new() { Dock = DockStyle.Fill };
    private readonly CheckBox _synchronize = new()
    {
        Text = "Синхронный масштаб и перемещение",
        Checked = true,
        AutoSize = true
    };
    private bool _syncing;

    public ImageComparisonForm(
        string leftPath,
        string rightPath,
        bool darkTheme)
    {
        Text = "Сравнение изображений";
        StartPosition = FormStartPosition.CenterParent;
        WindowState = FormWindowState.Maximized;
        MinimumSize = new Size(1000, 650);
        Font = new Font("Segoe UI", 9F);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            ColumnCount = 1,
            Padding = new Padding(8)
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            Padding = new Padding(0, 0, 0, 8)
        };
        var fitButton = new Button { Text = "Вписать оба", AutoSize = true };
        fitButton.Click += (_, _) =>
        {
            _left.ResetView();
            _right.ResetView();
        };
        toolbar.Controls.Add(fitButton);
        toolbar.Controls.Add(_synchronize);
        toolbar.Controls.Add(new Label
        {
            AutoSize = true,
            Margin = new Padding(18, 7, 0, 0),
            Tag = "muted",
            Text = "Колесо мыши — масштаб, перетаскивание — перемещение"
        });

        var images = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2
        };
        images.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        images.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        images.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        images.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        images.Controls.Add(CreateFileLabel(leftPath), 0, 0);
        images.Controls.Add(CreateFileLabel(rightPath), 1, 0);
        images.Controls.Add(_left, 0, 1);
        images.Controls.Add(_right, 1, 1);

        root.Controls.Add(toolbar, 0, 0);
        root.Controls.Add(images, 0, 1);
        Controls.Add(root);

        _left.ViewChanged += (_, eventArgs) =>
            Synchronize(_right, eventArgs);
        _right.ViewChanged += (_, eventArgs) =>
            Synchronize(_left, eventArgs);
        _left.LoadImage(leftPath);
        _right.LoadImage(rightPath);
        AppTheme.Apply(this, darkTheme);
        _left.BackColor = Color.FromArgb(20, 20, 20);
        _right.BackColor = Color.FromArgb(20, 20, 20);
    }

    private static Label CreateFileLabel(string path) => new()
    {
        Dock = DockStyle.Fill,
        AutoEllipsis = true,
        Font = new Font("Segoe UI Semibold", 9F),
        Padding = new Padding(8, 5, 8, 7),
        Text = Path.GetFileName(path),
        Tag = "accent"
    };

    private void Synchronize(
        ZoomableImageControl target,
        ImageViewChangedEventArgs eventArgs)
    {
        if (!_synchronize.Checked || _syncing)
        {
            return;
        }

        _syncing = true;
        target.SetView(eventArgs.Zoom, eventArgs.Center);
        _syncing = false;
    }
}
