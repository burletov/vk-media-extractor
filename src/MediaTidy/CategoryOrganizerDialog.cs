namespace MediaTidy;

internal sealed class CategoryOrganizerDialog : Form
{
    private readonly PaddedNumericUpDown _confidenceInput = new()
    {
        Minimum = 0,
        Maximum = 100,
        Value = 75,
        Width = 70
    };
    private readonly CheckBox _includeUnknownCheckBox = new()
    {
        Text = "Добавить папку «Не определено»",
        AutoSize = true
    };
    private readonly CheckBox _preserveFoldersCheckBox = new()
    {
        Text = "Сохранять исходную структуру подпапок",
        AutoSize = true,
        Checked = false
    };

    public CategoryOrganizerDialog(IReadOnlyList<PhotoRecord> photos)
    {
        Text = "Разнести медиафайлы по категориям";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(560, 430);
        Font = new Font("Segoe UI", 9F);

        var eligible = photos
            .Where(photo => photo.PrimaryCategory != PhotoCategory.Unknown)
            .GroupBy(photo => photo.PrimaryCategory)
            .OrderBy(group => CategoryNames.Russian(group.Key))
            .Select(group => $"{CategoryOrganizerService.CategoryFolderName(group.Key)}: {group.Count():N0}");

        var description = new Label
        {
            Dock = DockStyle.Top,
            Height = 82,
            Padding = new Padding(12, 12, 12, 4),
            Text =
                $"В выбранной папке будет создан каталог «{CategoryOrganizerService.DestinationFolderName}».\n" +
                "Файлы будут перемещены, но операцию можно вернуть. Низкая уверенность по умолчанию исключается.",
            AutoEllipsis = true
        };

        var summary = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            BackColor = SystemColors.Window,
            Text = string.Join(Environment.NewLine, eligible)
        };

        var options = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 95,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            Padding = new Padding(12, 8, 12, 4)
        };
        options.Controls.Add(new Label
        {
            Text = "Минимальная уверенность:",
            AutoSize = true,
            Margin = new Padding(0, 6, 4, 3)
        });
        options.Controls.Add(_confidenceInput);
        options.Controls.Add(new Label
        {
            Text = "%",
            AutoSize = true,
            Margin = new Padding(0, 6, 20, 3)
        });
        options.Controls.Add(_includeUnknownCheckBox);
        options.Controls.Add(_preserveFoldersCheckBox);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 52,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(8)
        };
        var cancelButton = new Button
        {
            Text = "Отмена",
            DialogResult = DialogResult.Cancel,
            AutoSize = true
        };
        var organizeButton = new Button
        {
            Text = "Разнести файлы",
            DialogResult = DialogResult.OK,
            AutoSize = true
        };
        buttons.Controls.Add(cancelButton);
        buttons.Controls.Add(organizeButton);

        Controls.Add(summary);
        Controls.Add(options);
        Controls.Add(buttons);
        Controls.Add(description);
        AcceptButton = organizeButton;
        CancelButton = cancelButton;
    }

    public double MinimumConfidence => (double)_confidenceInput.Value / 100;
    public bool IncludeUnknown => _includeUnknownCheckBox.Checked;
    public bool PreserveOriginalFolders => _preserveFoldersCheckBox.Checked;
}
