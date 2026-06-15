namespace MediaTidy;

internal sealed class MultiFolderDialog : Form
{
    private string _lastBrowseFolder;
    private readonly ListBox _folders = new()
    {
        Dock = DockStyle.Fill,
        HorizontalScrollbar = true,
        SelectionMode = SelectionMode.MultiExtended
    };
    private readonly CheckBox _mergeCheckBox = new()
    {
        Text = "Скопировать выбранные фото и видео в одну общую папку",
        AutoSize = true
    };
    private readonly TextBox _mergeName = new()
    {
        Text = "Общая медиатека",
        Width = 260,
        Enabled = false
    };
    private readonly TextBox _mergeParent = new()
    {
        ReadOnly = true,
        Dock = DockStyle.Fill,
        Enabled = false
    };
    private readonly Button _mergeParentButton = new()
    {
        Text = "Куда...",
        AutoSize = true,
        Enabled = false
    };

    public MultiFolderDialog(
        IEnumerable<string> initialFolders,
        string? lastBrowseFolder,
        bool darkTheme)
    {
        Text = "Папки для сканирования";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(760, 560);
        Size = new Size(820, 620);
        Font = new Font("Segoe UI", 9F);
        _lastBrowseFolder = Directory.Exists(lastBrowseFolder)
            ? lastBrowseFolder
            : Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
        _mergeParent.Text = _lastBrowseFolder;

        foreach (var folder in initialFolders.Where(Directory.Exists))
        {
            AddFolder(folder);
        }

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(16),
            ColumnCount = 1,
            RowCount = 5
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        root.Controls.Add(new Label
        {
            AutoSize = true,
            MaximumSize = new Size(750, 0),
            Margin = new Padding(0, 0, 0, 12),
            Text =
                "Добавьте одну или несколько папок. Дубликаты будут искаться между всеми источниками, а исходные файлы не изменятся до явного выбора действия."
        }, 0, 0);

        var folderArea = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2
        };
        folderArea.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        folderArea.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        folderArea.Controls.Add(_folders, 0, 0);
        var folderButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            Width = 150,
            FlowDirection = FlowDirection.TopDown,
            Padding = new Padding(10, 0, 0, 0),
            WrapContents = false
        };
        var addButton = new Button { Text = "Добавить папки...", AutoSize = true };
        var removeButton = new Button { Text = "Убрать выбранные", AutoSize = true };
        var clearButton = new Button { Text = "Очистить список", AutoSize = true };
        addButton.Click += (_, _) => BrowseFolder();
        removeButton.Click += (_, _) =>
        {
            foreach (var item in _folders.SelectedItems.Cast<string>().ToArray())
            {
                _folders.Items.Remove(item);
            }
        };
        clearButton.Click += (_, _) => _folders.Items.Clear();
        folderButtons.Controls.Add(addButton);
        folderButtons.Controls.Add(removeButton);
        folderButtons.Controls.Add(clearButton);
        folderArea.Controls.Add(folderButtons, 1, 0);
        root.Controls.Add(folderArea, 0, 1);

        var mergeGroup = new GroupBox
        {
            Text = "Дополнительно",
            Dock = DockStyle.Top,
            AutoSize = true,
            Padding = new Padding(14),
            Margin = new Padding(0, 14, 0, 0)
        };
        var mergeLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 3
        };
        mergeLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        mergeLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        mergeLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        mergeLayout.Controls.Add(_mergeCheckBox, 0, 0);
        mergeLayout.SetColumnSpan(_mergeCheckBox, 3);
        var mergeHelp = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(700, 0),
            ForeColor = AppTheme.Muted,
            Margin = new Padding(0, 5, 0, 10),
            Text =
                "Создаётся новая копия медиатеки. Оригиналы остаются на месте. Имена получают номер и дату, поэтому файлы идут от новых к старым.\n\n" +
                "Если включить общую папку, карантин будет создан внутри неё. Если работать с исходными папками без объединения, отдельная папка карантина будет создаваться в каждой папке, где вы перемещаете файлы."
        };
        mergeLayout.Controls.Add(mergeHelp, 0, 1);
        mergeLayout.SetColumnSpan(mergeHelp, 3);
        mergeLayout.Controls.Add(CreateLabel("Название:"), 0, 2);
        mergeLayout.Controls.Add(_mergeName, 1, 2);
        mergeLayout.SetColumnSpan(_mergeName, 2);
        mergeLayout.Controls.Add(CreateLabel("Расположение:"), 0, 3);
        mergeLayout.Controls.Add(_mergeParent, 1, 3);
        mergeLayout.Controls.Add(_mergeParentButton, 2, 3);
        mergeGroup.Controls.Add(mergeLayout);
        root.Controls.Add(mergeGroup, 0, 2);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            Padding = new Padding(0, 14, 0, 0)
        };
        var cancelButton = new Button
        {
            Text = "Отмена",
            DialogResult = DialogResult.Cancel,
            AutoSize = true
        };
        var okButton = new Button { Text = "Готово", AutoSize = true };
        okButton.Click += (_, _) => AcceptSelection();
        buttons.Controls.Add(cancelButton);
        buttons.Controls.Add(okButton);
        root.Controls.Add(buttons, 0, 4);

        _mergeCheckBox.CheckedChanged += (_, _) =>
        {
            _mergeName.Enabled = _mergeCheckBox.Checked;
            _mergeParent.Enabled = _mergeCheckBox.Checked;
            _mergeParentButton.Enabled = _mergeCheckBox.Checked;
        };
        _mergeParentButton.Click += (_, _) => BrowseMergeParent();

        Controls.Add(root);
        AcceptButton = okButton;
        CancelButton = cancelButton;
        AppTheme.Apply(this, darkTheme);
    }

    public IReadOnlyList<string> SelectedFolders =>
        _folders.Items.Cast<string>().ToArray();
    public string LastBrowseFolder => _lastBrowseFolder;
    public bool MergeRequested => _mergeCheckBox.Checked;
    public string MergeFolderName => _mergeName.Text.Trim();
    public string MergeParentPath => _mergeParent.Text.Trim();

    private static Label CreateLabel(string text) => new()
    {
        Text = text,
        AutoSize = true,
        Margin = new Padding(0, 7, 12, 3)
    };

    private void BrowseFolder()
    {
        var selected = NativeFolderPicker.PickFolders(
            this,
            _folders.SelectedItem as string ?? _lastBrowseFolder);
        foreach (var path in selected)
        {
            AddFolder(path);
            _lastBrowseFolder = path;
            if (!_mergeCheckBox.Checked)
            {
                _mergeParent.Text = Directory.GetParent(path)?.FullName ?? path;
            }
        }
    }

    private void BrowseMergeParent()
    {
        var selected = NativeFolderPicker.PickFolders(
            this,
            Directory.Exists(_mergeParent.Text) ? _mergeParent.Text : _lastBrowseFolder);
        if (selected.Count > 0)
        {
            _mergeParent.Text = selected[0];
            _lastBrowseFolder = selected[0];
        }
    }

    private void AddFolder(string path)
    {
        var fullPath = Path.GetFullPath(path);
        if (!_folders.Items.Cast<string>().Contains(fullPath, StringComparer.OrdinalIgnoreCase))
        {
            _folders.Items.Add(fullPath);
        }
    }

    private void AcceptSelection()
    {
        if (_folders.Items.Count == 0)
        {
            MessageBox.Show(
                this,
                "Добавьте хотя бы одну существующую папку.",
                "MediaTidy",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        if (_mergeCheckBox.Checked &&
            (string.IsNullOrWhiteSpace(_mergeName.Text) ||
             !Directory.Exists(_mergeParent.Text)))
        {
            MessageBox.Show(
                this,
                "Укажите название и существующую папку для общей медиатеки.",
                "Объединение папок",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        DialogResult = DialogResult.OK;
        Close();
    }
}
