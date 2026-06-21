namespace MediaExtractorForVK;

internal sealed class ProfileNameDialog : Form
{
    private readonly TextBox _name = new()
    {
        Dock = DockStyle.Top,
        MaxLength = 60
    };

    public ProfileNameDialog(string currentName, bool darkTheme)
    {
        Text = "Сохранить профиль";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(420, 145);
        Font = new Font("Segoe UI", 9F);
        _name.Text = currentName;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(16),
            RowCount = 3,
            ColumnCount = 1
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.Controls.Add(new Label
        {
            Text = "Название профиля:",
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 6)
        }, 0, 0);
        root.Controls.Add(_name, 0, 1);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(0, 12, 0, 0)
        };
        var cancel = new Button
        {
            Text = "Отмена",
            AutoSize = true,
            DialogResult = DialogResult.Cancel
        };
        var save = new Button
        {
            Text = "Сохранить",
            AutoSize = true
        };
        save.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(_name.Text))
            {
                return;
            }

            DialogResult = DialogResult.OK;
            Close();
        };
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(save);
        root.Controls.Add(buttons, 0, 2);
        Controls.Add(root);
        AcceptButton = save;
        CancelButton = cancel;
        AppTheme.Apply(this, darkTheme);
        Shown += (_, _) =>
        {
            _name.Focus();
            _name.SelectAll();
        };
    }

    public string ProfileName => _name.Text.Trim();
}
