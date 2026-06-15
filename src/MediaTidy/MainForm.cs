using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;

namespace MediaTidy;

internal sealed class MainForm : Form
{
    private readonly SplitContainer _mainSplit = new()
    {
        Dock = DockStyle.Fill,
        SplitterWidth = 6
    };
    private readonly ToolTip _toolTip = new()
    {
        AutoPopDelay = 12000,
        InitialDelay = 350,
        ReshowDelay = 100,
        ShowAlways = true
    };
    private readonly Label _folderSummaryLabel = new()
    {
        AutoSize = true,
        Font = new Font("Segoe UI Semibold", 11F),
        Text = "Папки не выбраны"
    };
    private readonly Label _folderDetailsLabel = new()
    {
        AutoSize = true,
        AutoEllipsis = true,
        ForeColor = SystemColors.GrayText,
        MaximumSize = new Size(760, 0),
        Text = "Добавьте папки с фотографиями и видео"
    };
    private readonly Label _scanSettingsLabel = new()
    {
        AutoSize = true,
        ForeColor = SystemColors.GrayText
    };
    private readonly Button _browseButton = new() { Text = "Выбрать папки...", AutoSize = true };
    private readonly Button _scanButton = new() { Text = "Сканировать", AutoSize = true };
    private readonly Button _cancelButton = new() { Text = "Отмена", AutoSize = true, Enabled = false };
    private readonly ThemedComboBox _filterComboBox = new()
    {
        DropDownStyle = ComboBoxStyle.DropDownList,
        Width = 170
    };
    private readonly ThemedComboBox _categoryComboBox = new()
    {
        DropDownStyle = ComboBoxStyle.DropDownList,
        Width = 130
    };
    private readonly ToolStripMenuItem _selectExactItem = new()
    {
        Text = "Выбрать точные копии",
        Enabled = false
    };
    private readonly ToolStripMenuItem _selectSimilarItem = new()
    {
        Text = "Выбрать похожие фото и видео",
        Enabled = false
    };
    private readonly ToolStripMenuItem _clearSelectionItem = new()
    {
        Text = "Снять выбор",
        Enabled = false
    };
    private readonly ToolStripMenuItem _compareItem = new()
    {
        Text = "Сравнить два отмеченных изображения...",
        Enabled = false
    };
    private readonly ToolStripMenuItem _exportReportItem = new()
    {
        Text = "Экспортировать отчёт CSV...",
        Enabled = false
    };
    private readonly ToolStripMenuItem _quarantineItem = new()
    {
        Text = "Переместить в карантин",
        Enabled = false
    };
    private readonly ToolStripMenuItem _undoItem = new()
    {
        Text = "Вернуть последнее из карантина",
        Enabled = false
    };
    private readonly ToolStripMenuItem _organizeItem = new()
    {
        Text = "Разнести по категориям...",
        Enabled = false
    };
    private readonly ToolStripMenuItem _undoOrganizeItem = new()
    {
        Text = "Отменить раскладку по категориям",
        Enabled = false
    };
    private readonly ToolStripMenuItem _openQuarantineItem = new()
    {
        Text = "Открыть папку карантина",
        Enabled = false
    };
    private readonly ContextMenuStrip _actionsMenu = new();
    private readonly Button _actionsButton = new()
    {
        Text = "Действия",
        AutoSize = false,
        Width = 130,
        Enabled = false
    };
    private readonly ToolStripMenuItem _chooseFoldersMenuItem = new("Выбрать папки...");
    private readonly ToolStripMenuItem _vkImportMenuItem = new("Импортировать из VK...");
    private readonly ToolStripMenuItem _scanSettingsMenuItem = new("Параметры анализа...");
    private readonly ToolStripMenuItem _lightThemeMenuItem = new("Светлая тема");
    private readonly ToolStripMenuItem _darkThemeMenuItem = new("Тёмная тема");
    private readonly ToolStripMenuItem _fieldHelpMenuItem = new("Что означают поля");
    private readonly ToolStripMenuItem _aboutMenuItem = new("О программе");
    private readonly Button _previousButton = new()
    {
        Text = "◀",
        Width = 34,
        Enabled = false
    };
    private readonly Button _nextButton = new()
    {
        Text = "▶",
        Width = 34,
        Enabled = false
    };
    private readonly Button _showInExplorerButton = new()
    {
        Text = "Показать файл",
        AutoSize = true,
        Enabled = false
    };
    private readonly Button _openPreviewButton = new()
    {
        Text = "На весь экран",
        AutoSize = true,
        Enabled = false
    };
    private readonly Label _previewTitle = new()
    {
        AutoEllipsis = true,
        Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.MiddleLeft,
        Padding = new Padding(6, 0, 6, 0),
        Text = "Выберите изображение"
    };
    private readonly DataGridView _grid = new()
    {
        Dock = DockStyle.Fill,
        AutoGenerateColumns = false,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        AllowUserToOrderColumns = true,
        AllowUserToResizeRows = false,
        MultiSelect = false,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        RowHeadersVisible = false,
        BackgroundColor = SystemColors.Window,
        BorderStyle = BorderStyle.Fixed3D
    };
    private readonly PictureBox _preview = new()
    {
        Dock = DockStyle.Fill,
        SizeMode = PictureBoxSizeMode.Zoom,
        BackColor = Color.FromArgb(32, 32, 32)
    };
    private readonly RichTextBox _details = new()
    {
        Dock = DockStyle.Fill,
        ReadOnly = true,
        BorderStyle = BorderStyle.None,
        DetectUrls = false,
        ScrollBars = RichTextBoxScrollBars.Vertical
    };
    private readonly ThemedProgressBar _progressBar = new()
    {
        Dock = DockStyle.Fill,
        Style = ProgressBarStyle.Continuous
    };
    private readonly Label _statusLabel = new()
    {
        AutoSize = true,
        Text = "Выберите одну или несколько папок с фотографиями и видео."
    };

    private readonly ImageScanner _scanner = new();
    private readonly QuarantineService _quarantineService = new();
    private readonly CategoryOrganizerService _categoryOrganizerService = new();
    private readonly FolderMergeService _folderMergeService = new();
    private readonly VkImportService _vkImportService = new();
    private readonly AppSettings _settings = AppSettings.Load();
    private readonly List<string> _selectedFolders = [];
    private readonly BindingList<FindingRow> _visibleRows = [];
    private List<FindingRow> _allRows = [];
    private CancellationTokenSource? _scanCancellation;
    private ScanResult? _scanResult;
    private IReadOnlyList<QuarantineMove> _lastMoves = Array.Empty<QuarantineMove>();
    private IReadOnlyList<CategoryMove> _lastCategoryMoves = Array.Empty<CategoryMove>();
    private PhotoRecord? _previewedPhoto;
    private FindingRow? _previewedRow;
    private string? _columnSortProperty;
    private bool _columnSortAscending = true;
    private bool _suppressPreviewUpdate;
    private bool _busy;
    private readonly Font _detailsHeaderFont;
    private readonly Font _detailsBoldFont;

    public MainForm()
    {
        Text = "MediaTidy";
        MinimumSize = new Size(1180, 700);
        Size = new Size(1400, 820);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9F);
        _detailsHeaderFont = new Font(Font.FontFamily, 10F, FontStyle.Bold);
        _detailsBoldFont = new Font(Font, FontStyle.Bold);
        KeyPreview = true;
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        _selectedFolders.AddRange(
            _settings.Folders
                .Where(Directory.Exists)
                .Select(Path.GetFullPath)
                .Distinct(StringComparer.OrdinalIgnoreCase));

        BuildLayout();
        ConfigureGrid();
        ConfigureToolTips();
        WireEvents();
        UpdateFolderSummary();
        UpdateScanSettingsSummary();
        ApplyTheme();
        Shown += (_, _) => EnsurePreviewPanelWidth();
        FormClosed += (_, _) => SaveSettings();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _scanCancellation?.Cancel();
            _scanCancellation?.Dispose();
            _preview.Image?.Dispose();
            _detailsHeaderFont.Dispose();
            _detailsBoldFont.Dispose();
        }

        base.Dispose(disposing);
    }

    private void BuildLayout()
    {
        _filterComboBox.Items.AddRange(
        [
            "Все файлы",
            "Точные дубликаты",
            "Похожие фото и видео",
            "Проверить вручную"
        ]);
        _filterComboBox.SelectedIndex = 0;
        _categoryComboBox.Items.AddRange(
        [
            "Все категории",
            "Фото",
            "Видео",
            "Скриншот",
            "Мем",
            "Документ",
            "Чек",
            "Размытое",
            "Графика",
            "Не определено"
        ]);
        _categoryComboBox.SelectedIndex = 0;
        var menu = new MenuStrip
        {
            Dock = DockStyle.Top,
            RenderMode = ToolStripRenderMode.System
        };
        var fileMenu = new ToolStripMenuItem("Файл");
        fileMenu.DropDownItems.Add(_chooseFoldersMenuItem);
        fileMenu.DropDownItems.Add(_vkImportMenuItem);
        fileMenu.DropDownItems.Add(new ToolStripSeparator());
        fileMenu.DropDownItems.Add(new ToolStripMenuItem(
            "Выход",
            null,
            (_, _) => Close()));
        var settingsMenu = new ToolStripMenuItem("Настройки");
        settingsMenu.DropDownItems.Add(_scanSettingsMenuItem);
        var themeMenu = new ToolStripMenuItem("Тема");
        themeMenu.DropDownItems.Add(_lightThemeMenuItem);
        themeMenu.DropDownItems.Add(_darkThemeMenuItem);
        settingsMenu.DropDownItems.Add(themeMenu);
        var helpMenu = new ToolStripMenuItem("Справка");
        helpMenu.DropDownItems.Add(_fieldHelpMenuItem);
        helpMenu.DropDownItems.Add(_aboutMenuItem);
        menu.Items.Add(fileMenu);
        menu.Items.Add(settingsMenu);
        menu.Items.Add(helpMenu);
        MainMenuStrip = menu;

        var libraryText = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            Margin = new Padding(0)
        };
        libraryText.Controls.Add(_folderSummaryLabel);
        libraryText.Controls.Add(_folderDetailsLabel);
        libraryText.Controls.Add(_scanSettingsLabel);

        var sourceButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoSize = true,
            Margin = new Padding(8, 12, 0, 0)
        };
        sourceButtons.Controls.Add(_browseButton);
        sourceButtons.Controls.Add(_scanButton);
        sourceButtons.Controls.Add(_cancelButton);

        var sourcePanel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            Padding = new Padding(14, 10, 14, 10),
            BackColor = Color.FromArgb(246, 248, 251)
        };
        sourcePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        sourcePanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        sourcePanel.Controls.Add(libraryText, 0, 0);
        sourcePanel.Controls.Add(sourceButtons, 1, 0);

        var actionPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            WrapContents = true,
            Padding = new Padding(8, 4, 8, 8)
        };
        actionPanel.Controls.Add(new Label
        {
            Text = "Показать:",
            AutoSize = true,
            Margin = new Padding(3, 7, 3, 3)
        });
        actionPanel.Controls.Add(_filterComboBox);
        actionPanel.Controls.Add(new Label
        {
            Text = "Категория:",
            AutoSize = true,
            Margin = new Padding(10, 7, 3, 3)
        });
        actionPanel.Controls.Add(_categoryComboBox);
        _actionsMenu.Items.Add(_selectExactItem);
        _actionsMenu.Items.Add(_selectSimilarItem);
        _actionsMenu.Items.Add(_clearSelectionItem);
        _actionsMenu.Items.Add(new ToolStripSeparator());
        _actionsMenu.Items.Add(_compareItem);
        _actionsMenu.Items.Add(_exportReportItem);
        _actionsMenu.Items.Add(new ToolStripSeparator());
        _actionsMenu.Items.Add(_quarantineItem);
        _actionsMenu.Items.Add(_undoItem);
        _actionsMenu.Items.Add(_openQuarantineItem);
        _actionsMenu.Items.Add(new ToolStripSeparator());
        _actionsMenu.Items.Add(_organizeItem);
        _actionsMenu.Items.Add(_undoOrganizeItem);
        _actionsButton.Margin = new Padding(
            10,
            _categoryComboBox.Margin.Top,
            0,
            _categoryComboBox.Margin.Bottom);
        actionPanel.Controls.Add(_actionsButton);
        actionPanel.Layout += (_, _) =>
        {
            var targetHeight = Math.Max(
                _filterComboBox.Height,
                _categoryComboBox.Height);
            if (_actionsButton.Height != targetHeight)
            {
                _actionsButton.Height = targetHeight;
            }
        };

        var toolbar = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 2
        };
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        toolbar.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        toolbar.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        toolbar.Controls.Add(sourcePanel, 0, 0);
        toolbar.Controls.Add(actionPanel, 0, 1);

        var tablePanel = new Panel
        {
            Dock = DockStyle.Fill
        };
        var gridHint = new Label
        {
            Dock = DockStyle.Top,
            Height = 28,
            Padding = new Padding(8, 6, 8, 0),
            BackColor = Color.AliceBlue,
            Text = "Подсказка: двойной щелчок открывает файл. Цвет строки показывает рекомендуемую копию, флажок добавляет файл в выбранное действие."
        };
        tablePanel.Controls.Add(_grid);
        tablePanel.Controls.Add(gridHint);
        _mainSplit.Panel1.Controls.Add(tablePanel);

        var previewToolbar = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 5,
            Padding = new Padding(4)
        };
        previewToolbar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        previewToolbar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        previewToolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        previewToolbar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        previewToolbar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        previewToolbar.Controls.Add(_previousButton, 0, 0);
        previewToolbar.Controls.Add(_nextButton, 1, 0);
        previewToolbar.Controls.Add(_previewTitle, 2, 0);
        previewToolbar.Controls.Add(_showInExplorerButton, 3, 0);
        previewToolbar.Controls.Add(_openPreviewButton, 4, 0);

        var previewLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 3,
            ColumnCount = 1,
            Padding = new Padding(6),
            BackColor = SystemColors.Control
        };
        previewLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        previewLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        previewLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 230));
        _preview.Margin = new Padding(0, 6, 0, 6);
        _preview.BorderStyle = BorderStyle.FixedSingle;
        previewLayout.Controls.Add(previewToolbar, 0, 0);
        previewLayout.Controls.Add(_preview, 0, 1);
        previewLayout.Controls.Add(_details, 0, 2);
        _mainSplit.Panel2.Controls.Add(previewLayout);

        var statusLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 38,
            ColumnCount = 2,
            Padding = new Padding(8, 6, 8, 5)
        };
        statusLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70));
        statusLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
        statusLayout.Controls.Add(_statusLabel, 0, 0);
        statusLayout.Controls.Add(_progressBar, 1, 0);

        Controls.Add(_mainSplit);
        Controls.Add(statusLayout);
        Controls.Add(toolbar);
        Controls.Add(menu);
    }

    private void ConfigureGrid()
    {
        _grid.Columns.Add(new DataGridViewCheckBoxColumn
        {
            DataPropertyName = nameof(FindingRow.Selected),
            HeaderText = "Все",
            Width = 42,
            SortMode = DataGridViewColumnSortMode.NotSortable
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(FindingRow.GroupId),
            HeaderText = "Группа",
            Width = 52,
            ReadOnly = true
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(FindingRow.Date),
            HeaderText = "Дата",
            Width = 105,
            ReadOnly = true
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(FindingRow.Category),
            HeaderText = "Категория",
            Width = 85,
            ReadOnly = true
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(FindingRow.Confidence),
            HeaderText = "Уверенность",
            Width = 92,
            ReadOnly = true
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(FindingRow.OcrWords),
            HeaderText = "Слов OCR",
            Width = 78,
            ReadOnly = true
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(FindingRow.Type),
            HeaderText = "Тип",
            Width = 105,
            ReadOnly = true
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(FindingRow.FileName),
            HeaderText = "Файл",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            MinimumWidth = 160,
            ReadOnly = true
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(FindingRow.Size),
            HeaderText = "Размер",
            Width = 72,
            ReadOnly = true
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(FindingRow.Resolution),
            HeaderText = "Разрешение",
            Width = 115,
            MinimumWidth = 112,
            ReadOnly = true
        });
        foreach (DataGridViewColumn column in _grid.Columns)
        {
            column.Tag = column.HeaderText;
        }

        foreach (DataGridViewColumn column in _grid.Columns.Cast<DataGridViewColumn>().Skip(1))
        {
            column.SortMode = DataGridViewColumnSortMode.Programmatic;
        }

        _grid.DataSource = _visibleRows;
    }

    private void ConfigureToolTips()
    {
        _toolTip.SetToolTip(
            _filterComboBox,
            "Переключает режим просмотра: все распознанные файлы, дубликаты, похожие изображения или кандидаты на ручную проверку.");
        _toolTip.SetToolTip(
            _categoryComboBox,
            "Фильтр по наиболее вероятной категории. Категория является подсказкой и сама по себе не удаляет файл.");
        _toolTip.SetToolTip(
            _browseButton,
            "Можно выбрать несколько папок. Дубликаты будут искаться сразу между всеми выбранными папками.");
        _organizeItem.ToolTipText =
            "Создаёт в каждой исходной папке каталог категорий и переносит туда распознанные файлы.";
        _undoOrganizeItem.ToolTipText =
            "Возвращает файлы из категорий на исходные места по журналу последней раскладки.";
        _quarantineItem.ToolTipText =
            "Перемещает отмеченные файлы в служебную папку карантина без безвозвратного удаления.";
        _selectSimilarItem.ToolTipText =
            "Отмечает кандидатов из групп похожих файлов. Перед карантином их нужно перепроверить вручную.";
        _toolTip.SetToolTip(
            _showInExplorerButton,
            "Открывает Проводник и выделяет именно этот файл.");
        _toolTip.SetToolTip(
            _openPreviewButton,
            "Открывает крупный предпросмотр. Также можно дважды щёлкнуть по изображению.");

        foreach (DataGridViewColumn column in _grid.Columns)
        {
            column.HeaderCell.ToolTipText = column.DataPropertyName switch
            {
                nameof(FindingRow.Selected) => "Нажмите заголовок «Все», чтобы отметить или снять отметку со всех показанных строк.",
                nameof(FindingRow.GroupId) => "Одинаковый номер означает, что файлы входят в одну группу дубликатов или похожих изображений.",
                nameof(FindingRow.Date) => "Дата съёмки из EXIF; если её нет — дата файла.",
                nameof(FindingRow.Category) => "Наиболее вероятная категория по локальной модели и дополнительным признакам.",
                nameof(FindingRow.Confidence) => "Оценка уверенности классификатора. Это подсказка, а не гарантия.",
                nameof(FindingRow.OcrWords) => "Количество слов, найденных локальным распознаванием текста (OCR).",
                nameof(FindingRow.Type) => "Почему строка показана: обычный файл, точный дубликат, похожее изображение или ручная проверка.",
                nameof(FindingRow.FileName) => "Имя файла. Полный путь и подробности показаны справа.",
                nameof(FindingRow.Size) => "Размер файла на диске.",
                nameof(FindingRow.Resolution) => "Ширина и высота изображения в пикселях.",
                nameof(FindingRow.Reason) => "Краткое объяснение результата анализа.",
                _ => ""
            };
        }
    }

    private void WireEvents()
    {
        _browseButton.Click += async (_, _) => await BrowseFoldersAsync();
        _chooseFoldersMenuItem.Click += async (_, _) => await BrowseFoldersAsync();
        _scanButton.Click += async (_, _) => await StartScanAsync();
        _vkImportMenuItem.Click += (_, _) => ImportFromVk();
        _scanSettingsMenuItem.Click += (_, _) => ShowScanSettings();
        _fieldHelpMenuItem.Click += (_, _) => ShowFieldHelp();
        _aboutMenuItem.Click += (_, _) => ShowAbout();
        _cancelButton.Click += (_, _) => _scanCancellation?.Cancel();
        _filterComboBox.SelectedIndexChanged += (_, _) => ApplyFilter();
        _categoryComboBox.SelectedIndexChanged += (_, _) => ApplyFilter();
        _actionsButton.Click += (_, _) =>
            _actionsMenu.Show(_actionsButton, new Point(0, _actionsButton.Height));
        _selectExactItem.Click += (_, _) => SelectExactDuplicates();
        _selectSimilarItem.Click += (_, _) => SelectSimilarCandidates();
        _clearSelectionItem.Click += (_, _) => SetAllSelected(false);
        _compareItem.Click += (_, _) => CompareSelectedImages();
        _exportReportItem.Click += async (_, _) => await ExportReportAsync();
        _quarantineItem.Click += async (_, _) => await MoveSelectedToQuarantineAsync();
        _undoItem.Click += async (_, _) => await RestoreLastOperationAsync();
        _organizeItem.Click += async (_, _) => await OrganizeByCategoryAsync();
        _undoOrganizeItem.Click += async (_, _) => await RestoreCategoryOrganizationAsync();
        _openQuarantineItem.Click += (_, _) => OpenQuarantineFolder();
        _lightThemeMenuItem.Click += (_, _) => SetTheme(false);
        _darkThemeMenuItem.Click += (_, _) => SetTheme(true);
        _previousButton.Click += (_, _) => SelectAdjacentRow(-1);
        _nextButton.Click += (_, _) => SelectAdjacentRow(1);
        _showInExplorerButton.Click += (_, _) => ShowCurrentFileInExplorer();
        _openPreviewButton.Click += (_, _) => ShowCurrentImageFullScreen();
        _preview.DoubleClick += (_, _) => ShowCurrentImageFullScreen();
        _grid.SelectionChanged += (_, _) =>
        {
            if (!_suppressPreviewUpdate)
            {
                UpdatePreview();
            }
        };
        _grid.CellDoubleClick += GridOnCellDoubleClick;
        _grid.CellFormatting += GridOnCellFormatting;
        _grid.CellToolTipTextNeeded += GridOnCellToolTipTextNeeded;
        _grid.ColumnHeaderMouseClick += GridOnColumnHeaderMouseClick;
        _grid.CellValueChanged += (_, eventArgs) =>
        {
            if (eventArgs.RowIndex >= 0 && eventArgs.ColumnIndex == 0)
            {
                UpdateActionButtons();
                _grid.InvalidateRow(eventArgs.RowIndex);
            }
        };
        _grid.CurrentCellDirtyStateChanged += (_, _) =>
        {
            if (_grid.IsCurrentCellDirty)
            {
                _grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            }
        };
    }

    private async Task BrowseFoldersAsync()
    {
        using var dialog = new MultiFolderDialog(
            GetRootPaths(requireExisting: true),
            _settings.LastBrowseFolder,
            IsDarkTheme);

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _settings.LastBrowseFolder = dialog.LastBrowseFolder;
            if (dialog.MergeRequested)
            {
                SetBusy(true);
                try
                {
                    var result = await _folderMergeService.CopyNewestFirstAsync(
                        dialog.SelectedFolders,
                        dialog.MergeParentPath,
                        dialog.MergeFolderName,
                        new Progress<FolderMergeProgress>(value =>
                        {
                            _statusLabel.Text =
                                $"Объединение папок: {value.Current:N0} / {value.Total:N0} — {value.FileName}";
                            _progressBar.Maximum = Math.Max(1, value.Total);
                            _progressBar.Value = Math.Clamp(value.Current, 0, _progressBar.Maximum);
                        }),
                        CancellationToken.None);
                    _selectedFolders.Clear();
                    _selectedFolders.Add(result.DestinationPath);
                    _statusLabel.Text =
                        $"Общая медиатека создана: {result.Copied:N0} файлов. Ошибок: {result.Errors.Count:N0}.";
                    if (result.Errors.Count > 0)
                    {
                        MessageBox.Show(
                            this,
                            string.Join(Environment.NewLine, result.Errors.Take(20)),
                            "Некоторые файлы не скопированы",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning);
                    }
                }
                finally
                {
                    SetBusy(false);
                }
            }
            else
            {
                _selectedFolders.Clear();
                _selectedFolders.AddRange(dialog.SelectedFolders);
            }

            UpdateFolderSummary();
            SaveSettings();
        }
    }

    private IReadOnlyList<string> GetRootPaths(bool requireExisting)
    {
        return _selectedFolders
            .Where(path => !requireExisting || Directory.Exists(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private void UpdateFolderSummary()
    {
        var existing = GetRootPaths(requireExisting: true);
        if (existing.Count == 0)
        {
            _folderSummaryLabel.Text = "Папки не выбраны";
            _folderDetailsLabel.Text = "Добавьте одну или несколько папок с фото и видео";
            _scanButton.Enabled = false;
            return;
        }

        _folderSummaryLabel.Text = existing.Count == 1
            ? "Выбрана 1 папка"
            : $"Выбрано папок: {existing.Count:N0}";
        _folderDetailsLabel.Text = existing.Count == 1
            ? existing[0]
            : $"{existing[0]}  •  ещё {existing.Count - 1:N0}";
        _folderDetailsLabel.Tag = string.Join(Environment.NewLine, existing);
        _toolTip.SetToolTip(_folderDetailsLabel, (string)_folderDetailsLabel.Tag);
        _scanButton.Enabled = true;
    }

    private void UpdateScanSettingsSummary()
    {
        _scanSettingsLabel.Text =
            $"Сходство: {_settings.SimilarityDistance}  •  " +
            $"Модель распознавания: {(_settings.RecognitionEnabled ? RecognitionModels.Get(_settings.RecognitionModelId).DisplayName : "выключена")}" +
            (string.IsNullOrWhiteSpace(_settings.ActiveScanProfileName)
                ? ""
                : $"  •  Профиль: {_settings.ActiveScanProfileName}");
    }

    private void ShowScanSettings()
    {
        using var dialog = new ScanSettingsDialog(
            _settings.SimilarityDistance,
            _settings.RecognitionEnabled,
            _settings.RecognitionModelId,
            _settings.ScanProfiles,
            _settings.ActiveScanProfileName,
            IsDarkTheme);
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        _settings.SimilarityDistance = dialog.SimilarityDistance;
        _settings.RecognitionEnabled = dialog.RecognitionEnabled;
        _settings.RecognitionModelId = dialog.RecognitionModelId;
        _settings.ScanProfiles = dialog.Profiles.ToList();
        _settings.ActiveScanProfileName = dialog.ActiveProfileName;
        UpdateScanSettingsSummary();
        SaveSettings();
    }

    private void SaveSettings()
    {
        _settings.Folders = _selectedFolders.ToList();
        _settings.Save();
    }

    private string? FindOwningRoot(string filePath)
    {
        if (_scanResult is null)
        {
            return null;
        }

        return _scanResult.RootPaths
            .OrderByDescending(root => root.Length)
            .FirstOrDefault(root =>
            {
                var relative = Path.GetRelativePath(root, filePath);
                return !Path.IsPathRooted(relative) &&
                       relative != ".." &&
                       !relative.StartsWith(
                           $"..{Path.DirectorySeparatorChar}",
                           StringComparison.Ordinal);
            });
    }

    private async Task StartScanAsync()
    {
        var rootPaths = GetRootPaths(requireExisting: true);
        if (rootPaths.Count == 0)
        {
            MessageBox.Show(
                this,
                "Выберите хотя бы одну существующую папку.",
                "MediaTidy",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        SetBusy(true);
        _allRows.Clear();
        _visibleRows.Clear();
        _scanResult = null;
        _scanCancellation = new CancellationTokenSource();
        var progress = new Progress<ScanProgress>(UpdateProgress);

        try
        {
            _scanResult = await _scanner.ScanAsync(
                rootPaths,
                _settings.SimilarityDistance,
                _settings.RecognitionEnabled,
                _settings.RecognitionModelId,
                progress,
                _scanCancellation.Token);

            _allRows = BuildRows(_scanResult);
            ApplyFilter();
            _lastMoves = (await Task.WhenAll(_scanResult.RootPaths.Select(root =>
                    _quarantineService.FindLatestOperationAsync(root, CancellationToken.None))))
                .SelectMany(moves => moves)
                .ToArray();
            _undoItem.Enabled = _lastMoves.Any(move => File.Exists(move.QuarantinePath));
            _lastCategoryMoves = (await Task.WhenAll(_scanResult.RootPaths.Select(root =>
                    _categoryOrganizerService.FindLatestOperationAsync(root, CancellationToken.None))))
                .SelectMany(moves => moves)
                .ToArray();
            _undoOrganizeItem.Enabled =
                _lastCategoryMoves.Any(move => File.Exists(move.CategoryPath));
            _organizeItem.Enabled =
                _scanResult.Photos.Any(photo => photo.PrimaryCategory != PhotoCategory.Unknown);

            var exactGroups = _scanResult.Groups.Count(group => group.Kind == FindingKind.ExactDuplicate);
            var similarGroups = _scanResult.Groups.Count(group => group.Kind == FindingKind.SimilarImage);
            var reviewCount = _scanResult.Groups.Count(group => group.Kind == FindingKind.ReviewCandidate);
            var recognizedCount = _scanResult.Photos.Count(
                photo => photo.PrimaryCategory != PhotoCategory.Unknown);
            _statusLabel.Text =
                $"Папок: {_scanResult.RootPaths.Count:N0}. Файлов: {_scanResult.Photos.Count:N0}. " +
                $"Точных групп: {exactGroups:N0}. Похожих: {similarGroups:N0}. " +
                $"Распознано: {recognizedCount:N0}. На проверку: {reviewCount:N0}. " +
                $"Не прочитано: {_scanResult.FailedToDecode:N0}. {_scanResult.RecognitionStatus}.";
        }
        catch (OperationCanceledException)
        {
            _statusLabel.Text = "Сканирование отменено.";
        }
        catch (Exception exception)
        {
            _statusLabel.Text = "Ошибка сканирования.";
            MessageBox.Show(
                this,
                exception.ToString(),
                "Ошибка",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            _scanCancellation.Dispose();
            _scanCancellation = null;
            SetBusy(false);
        }
    }

    private void ImportFromVk()
    {
        var firstRoot = GetRootPaths(requireExisting: true).FirstOrDefault();
        var destination = !string.IsNullOrWhiteSpace(_settings.VkDestinationPath)
            ? _settings.VkDestinationPath
            : Path.Combine(
                firstRoot ?? Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                "VK Import");
        using var dialog = new VkImportDialog(
            _vkImportService,
            destination,
            _settings.LastBrowseFolder,
            IsDarkTheme);
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            _settings.LastBrowseFolder = dialog.LastBrowseFolder;
            _settings.VkDestinationPath = dialog.DestinationPath;
            SaveSettings();
            return;
        }

        _settings.LastBrowseFolder = dialog.LastBrowseFolder;
        _settings.VkDestinationPath = dialog.DestinationPath;
        if (Directory.Exists(dialog.DestinationPath) &&
            !_selectedFolders.Contains(dialog.DestinationPath, StringComparer.OrdinalIgnoreCase))
        {
            _selectedFolders.Add(Path.GetFullPath(dialog.DestinationPath));
        }

        UpdateFolderSummary();
        SaveSettings();
        if (dialog.Result is { } result)
        {
            _statusLabel.Text =
                $"VK: найдено {result.FoundFiles:N0}, скачано {result.Downloaded:N0}, " +
                $"пропущено {result.Skipped:N0}, ошибок {result.Errors.Count:N0}. " +
                "Папка готова к сканированию.";
        }
    }

    private static List<FindingRow> BuildRows(ScanResult result)
    {
        var rows = new List<FindingRow>();
        rows.AddRange(result.Photos.Select(photo => CreateRow(
            groupId: 0,
            kind: FindingKind.ClassifiedImage,
            reason: photo.PrimaryCategory == PhotoCategory.Unknown
                ? "Категория не определена"
                : $"Локальная оценка: {CategoryNames.Russian(photo.PrimaryCategory)} " +
                  $"({photo.CategoryConfidence:P0})",
            recommendedKeep: false,
            photo)));

        foreach (var group in result.Groups)
        {
            foreach (var photo in group.Photos
                         .OrderByDescending(photo => ReferenceEquals(photo, group.RecommendedKeep))
                         .ThenByDescending(photo => photo.PixelCount))
            {
                rows.Add(CreateRow(
                    group.Id,
                    group.Kind,
                    group.Reason,
                    group.Kind != FindingKind.ReviewCandidate &&
                    ReferenceEquals(photo, group.RecommendedKeep),
                    photo));
            }
        }

        return rows;
    }

    private static FindingRow CreateRow(
        int groupId,
        FindingKind kind,
        string reason,
        bool recommendedKeep,
        PhotoRecord photo) =>
        new()
        {
            GroupId = groupId,
            Kind = kind,
            Type = kind == FindingKind.ClassifiedImage
                ? photo.MediaKind == MediaKind.Video ? "Видео" : "Изображение"
                : KindText(kind),
            FileName = Path.GetFileName(photo.FullPath),
            Folder = Path.GetDirectoryName(photo.FullPath) ?? "",
            Size = FormatBytes(photo.Size),
            SizeBytes = photo.Size,
            Resolution = photo.Width > 0 ? $"{photo.Width} × {photo.Height}" : "не прочитано",
            PixelCount = photo.PixelCount,
            Date = photo.CapturedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm"),
            SortDateUtc = photo.CapturedAtUtc,
            Category = CategoryNames.Russian(photo.PrimaryCategory),
            CategoryValue = photo.PrimaryCategory,
            Confidence = photo.PrimaryCategory == PhotoCategory.Unknown
                ? "—"
                : photo.CategoryConfidence.ToString("P0"),
            ConfidenceValue = photo.CategoryConfidence,
            OcrWords = photo.OcrWordCount.ToString("N0"),
            OcrWordCount = photo.OcrWordCount,
            Reason = reason,
            RecommendedKeep = recommendedKeep,
            Photo = photo
        };

    private void ApplyFilter()
    {
        var selectedIndex = _filterComboBox.SelectedIndex;
        var filtered = _allRows.Where(row => selectedIndex switch
        {
            1 => row.Kind == FindingKind.ExactDuplicate,
            2 => row.Kind == FindingKind.SimilarImage,
            3 => row.Kind == FindingKind.ReviewCandidate,
            _ => row.Kind == FindingKind.ClassifiedImage
        });
        var selectedCategory = SelectedCategory();
        if (selectedCategory.HasValue)
        {
            filtered = filtered.Where(row => row.CategoryValue == selectedCategory.Value);
        }

        filtered = _columnSortProperty is not null
            ? ApplyColumnSort(filtered, _columnSortProperty, _columnSortAscending)
            : filtered
                .OrderBy(row => row.SortDateUtc)
                .ThenBy(row => row.FileName, StringComparer.CurrentCultureIgnoreCase);

        _visibleRows.RaiseListChangedEvents = false;
        _visibleRows.Clear();
        foreach (var row in filtered)
        {
            _visibleRows.Add(row);
        }

        _visibleRows.RaiseListChangedEvents = true;
        _visibleRows.ResetBindings();
        UpdateActionButtons();
    }

    private static IEnumerable<FindingRow> ApplyColumnSort(
        IEnumerable<FindingRow> source,
        string propertyName,
        bool ascending)
    {
        IOrderedEnumerable<FindingRow> ordered = propertyName switch
        {
            nameof(FindingRow.GroupId) => ascending
                ? source.OrderBy(row => row.GroupId)
                : source.OrderByDescending(row => row.GroupId),
            nameof(FindingRow.Date) => ascending
                ? source.OrderBy(row => row.SortDateUtc)
                : source.OrderByDescending(row => row.SortDateUtc),
            nameof(FindingRow.Category) => ascending
                ? source.OrderBy(row => row.Category, StringComparer.CurrentCultureIgnoreCase)
                : source.OrderByDescending(row => row.Category, StringComparer.CurrentCultureIgnoreCase),
            nameof(FindingRow.Confidence) => ascending
                ? source.OrderBy(row => row.ConfidenceValue)
                : source.OrderByDescending(row => row.ConfidenceValue),
            nameof(FindingRow.OcrWords) => ascending
                ? source.OrderBy(row => row.OcrWordCount)
                : source.OrderByDescending(row => row.OcrWordCount),
            nameof(FindingRow.Type) => ascending
                ? source.OrderBy(row => row.Type, StringComparer.CurrentCultureIgnoreCase)
                : source.OrderByDescending(row => row.Type, StringComparer.CurrentCultureIgnoreCase),
            nameof(FindingRow.FileName) => ascending
                ? source.OrderBy(row => row.FileName, StringComparer.CurrentCultureIgnoreCase)
                : source.OrderByDescending(row => row.FileName, StringComparer.CurrentCultureIgnoreCase),
            nameof(FindingRow.Size) => ascending
                ? source.OrderBy(row => row.SizeBytes)
                : source.OrderByDescending(row => row.SizeBytes),
            nameof(FindingRow.Resolution) => ascending
                ? source.OrderBy(row => row.PixelCount)
                : source.OrderByDescending(row => row.PixelCount),
            _ => source.OrderBy(row => row.SortDateUtc)
        };

        return ordered.ThenBy(row => row.FileName, StringComparer.CurrentCultureIgnoreCase);
    }

    private void GridOnColumnHeaderMouseClick(
        object? sender,
        DataGridViewCellMouseEventArgs eventArgs)
    {
        if (eventArgs.ColumnIndex == 0)
        {
            SetVisibleSelected(_visibleRows.Any(row => !row.Selected));
            return;
        }

        if (eventArgs.ColumnIndex < 0)
        {
            return;
        }

        var column = _grid.Columns[eventArgs.ColumnIndex];
        var propertyName = column.DataPropertyName;
        if (string.IsNullOrWhiteSpace(propertyName))
        {
            return;
        }

        _columnSortAscending =
            !string.Equals(_columnSortProperty, propertyName, StringComparison.Ordinal) ||
            !_columnSortAscending;
        _columnSortProperty = propertyName;
        ClearSortGlyphs();
        column.HeaderText =
            $"{column.Tag as string ?? column.HeaderText} {(_columnSortAscending ? "▲" : "▼")}";
        ApplyFilter();
    }

    private void ClearSortGlyphs()
    {
        foreach (DataGridViewColumn column in _grid.Columns)
        {
            column.HeaderCell.SortGlyphDirection = SortOrder.None;
            if (column.Tag is string baseHeader)
            {
                column.HeaderText = baseHeader;
            }
        }
    }

    private PhotoCategory? SelectedCategory() => _categoryComboBox.SelectedIndex switch
    {
        1 => PhotoCategory.Photo,
        2 => PhotoCategory.Video,
        3 => PhotoCategory.Screenshot,
        4 => PhotoCategory.Meme,
        5 => PhotoCategory.Document,
        6 => PhotoCategory.Receipt,
        7 => PhotoCategory.Blurry,
        8 => PhotoCategory.Graphic,
        9 => PhotoCategory.Unknown,
        _ => null
    };

    private void SelectExactDuplicates()
    {
        _categoryComboBox.SelectedIndex = 0;
        _filterComboBox.SelectedIndex = 1;
        foreach (var row in _allRows)
        {
            row.Selected = false;
        }

        foreach (var row in _allRows.Where(row => row.Kind == FindingKind.ExactDuplicate))
        {
            row.Selected = !row.RecommendedKeep;
        }

        _visibleRows.ResetBindings();
        UpdateActionButtons();
    }

    private void SelectSimilarCandidates()
    {
        var similarRows = _allRows
            .Where(row => row.Kind == FindingKind.SimilarImage)
            .ToArray();
        if (similarRows.Length == 0)
        {
            return;
        }

        var answer = MessageBox.Show(
            this,
            "Похожие файлы не являются точными копиями. Среди них могут быть уникальные фото или видео.\n\n" +
            "MediaTidy отметит кандидатов, которые не выбраны как рекомендуемый экземпляр. " +
            "Обязательно перепроверьте список перед перемещением в карантин.",
            "Выбор похожих файлов",
            MessageBoxButtons.OKCancel,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);
        if (answer != DialogResult.OK)
        {
            return;
        }

        _categoryComboBox.SelectedIndex = 0;
        _filterComboBox.SelectedIndex = 2;
        foreach (var row in _allRows)
        {
            row.Selected = false;
        }

        foreach (var row in similarRows)
        {
            row.Selected = !row.RecommendedKeep;
        }

        _visibleRows.ResetBindings();
        UpdateActionButtons();
    }

    private void SetVisibleSelected(bool selected)
    {
        foreach (var row in _visibleRows)
        {
            row.Selected = selected;
        }

        _visibleRows.ResetBindings();
        UpdateActionButtons();
    }

    private void SetAllSelected(bool selected)
    {
        foreach (var row in _allRows)
        {
            row.Selected = selected;
        }

        _visibleRows.ResetBindings();
        UpdateActionButtons();
    }

    private async Task MoveSelectedToQuarantineAsync()
    {
        if (_scanResult is null)
        {
            return;
        }

        _grid.EndEdit();
        var selectedPaths = _allRows
            .Where(row => row.Selected)
            .Select(row => row.Photo.FullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(File.Exists)
            .ToArray();

        if (selectedPaths.Length == 0)
        {
            MessageBox.Show(
                this,
                "Сначала отметьте файлы флажками.",
                "MediaTidy",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        var answer = MessageBox.Show(
            this,
            $"Переместить в карантин файлов: {selectedPaths.Length:N0}?\n\n" +
            "Они останутся внутри выбранной папки и их можно будет вернуть кнопкой «Вернуть последнее».",
            "Подтверждение",
            MessageBoxButtons.OKCancel,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);
        if (answer != DialogResult.OK)
        {
            return;
        }

        SetBusy(true);
        try
        {
            var moves = new List<QuarantineMove>();
            var errors = new List<string>();
            foreach (var group in selectedPaths
                         .Select(path => (Path: path, Root: FindOwningRoot(path)))
                         .Where(item => item.Root is not null)
                         .GroupBy(item => item.Root!, StringComparer.OrdinalIgnoreCase))
            {
                var result = await _quarantineService.MoveAsync(
                    group.Key,
                    group.Select(item => item.Path),
                    CancellationToken.None);
                moves.AddRange(result.Moves);
                errors.AddRange(result.Errors);
            }

            _lastMoves = moves;

            var movedPaths = moves
                .Select(move => move.OriginalPath)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            _allRows.RemoveAll(row => movedPaths.Contains(row.Photo.FullPath));
            ApplyFilter();
            _undoItem.Enabled = moves.Count > 0;
            _statusLabel.Text =
                $"Перемещено в карантин: {moves.Count:N0}. Ошибок: {errors.Count:N0}.";

            if (errors.Count > 0)
            {
                MessageBox.Show(
                    this,
                    string.Join(Environment.NewLine, errors.Take(20)),
                    "Некоторые файлы не перемещены",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task RestoreLastOperationAsync()
    {
        if (_lastMoves.Count == 0)
        {
            return;
        }

        SetBusy(true);
        try
        {
            var result = await _quarantineService.RestoreAsync(
                _lastMoves,
                CancellationToken.None);
            _statusLabel.Text =
                $"Возвращено: {result.Restored.Count:N0}. Ошибок: {result.Errors.Count:N0}.";
            _lastMoves = Array.Empty<QuarantineMove>();
            _undoItem.Enabled = false;

            if (result.Errors.Count > 0)
            {
                MessageBox.Show(
                    this,
                    string.Join(Environment.NewLine, result.Errors.Take(20)),
                    "Некоторые файлы не возвращены",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }

            if (_scanResult is not null && result.Restored.Count > 0)
            {
                await StartScanAsync();
            }
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task OrganizeByCategoryAsync()
    {
        if (_scanResult is null)
        {
            return;
        }

        using var dialog = new CategoryOrganizerDialog(_scanResult.Photos);
        AppTheme.Apply(dialog, IsDarkTheme);
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        SetBusy(true);
        try
        {
            var moves = new List<CategoryMove>();
            var errors = new List<string>();
            var skippedLowConfidence = 0;
            var skippedAlreadyOrganized = 0;
            foreach (var group in _scanResult.Photos
                         .Select(photo => (Photo: photo, Root: FindOwningRoot(photo.FullPath)))
                         .Where(item => item.Root is not null)
                         .GroupBy(item => item.Root!, StringComparer.OrdinalIgnoreCase))
            {
                var result = await _categoryOrganizerService.OrganizeAsync(
                    group.Key,
                    group.Select(item => item.Photo).ToArray(),
                    dialog.MinimumConfidence,
                    dialog.IncludeUnknown,
                    dialog.PreserveOriginalFolders,
                    CancellationToken.None);
                moves.AddRange(result.Moves);
                errors.AddRange(result.Errors);
                skippedLowConfidence += result.SkippedLowConfidence;
                skippedAlreadyOrganized += result.SkippedAlreadyOrganized;
            }

            if (moves.Count > 0)
            {
                _lastCategoryMoves = moves;
            }

            var newPaths = moves.ToDictionary(
                move => move.OriginalPath,
                move => move.CategoryPath,
                StringComparer.OrdinalIgnoreCase);
            foreach (var photo in _scanResult.Photos)
            {
                if (newPaths.TryGetValue(photo.FullPath, out var newPath))
                {
                    photo.FullPath = newPath;
                }
            }

            _allRows = BuildRows(_scanResult);
            ApplyFilter();
            _undoOrganizeItem.Enabled = moves.Count > 0;
            _statusLabel.Text =
                $"Разнесено по категориям: {moves.Count:N0}. " +
                $"Пропущено по уверенности: {skippedLowConfidence:N0}. " +
                $"Уже было разложено: {skippedAlreadyOrganized:N0}. " +
                $"Ошибок: {errors.Count:N0}.";

            if (errors.Count > 0)
            {
                MessageBox.Show(
                    this,
                    string.Join(Environment.NewLine, errors.Take(20)),
                    "Некоторые файлы не перемещены",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task RestoreCategoryOrganizationAsync()
    {
        if (_lastCategoryMoves.Count == 0)
        {
            return;
        }

        SetBusy(true);
        try
        {
            var result = await _categoryOrganizerService.RestoreAsync(
                _lastCategoryMoves,
                CancellationToken.None);
            var originalPaths = result.Restored.ToDictionary(
                move => move.CategoryPath,
                move => move.OriginalPath,
                StringComparer.OrdinalIgnoreCase);

            if (_scanResult is not null)
            {
                foreach (var photo in _scanResult.Photos)
                {
                    if (originalPaths.TryGetValue(photo.FullPath, out var originalPath))
                    {
                        photo.FullPath = originalPath;
                    }
                }

                _allRows = BuildRows(_scanResult);
                ApplyFilter();
            }

            var restoredPaths = result.Restored
                .Select(move => move.CategoryPath)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            _lastCategoryMoves = _lastCategoryMoves
                .Where(move =>
                    !restoredPaths.Contains(move.CategoryPath) &&
                    File.Exists(move.CategoryPath))
                .ToArray();
            _undoOrganizeItem.Enabled = _lastCategoryMoves.Count > 0;
            _statusLabel.Text =
                $"Возвращено из категорий: {result.Restored.Count:N0}. " +
                $"Ошибок: {result.Errors.Count:N0}.";

            if (result.Errors.Count > 0)
            {
                MessageBox.Show(
                    this,
                    string.Join(Environment.NewLine, result.Errors.Take(20)),
                    "Некоторые файлы не возвращены",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void UpdateProgress(ScanProgress progress)
    {
        _statusLabel.Text = progress.Total == 0
            ? progress.Stage
            : $"{progress.Stage}: {progress.Current:N0} / {progress.Total:N0}";
        _progressBar.Maximum = Math.Max(1, progress.Total);
        _progressBar.Value = Math.Clamp(progress.Current, 0, _progressBar.Maximum);
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        _browseButton.Enabled = !busy;
        _scanButton.Enabled = !busy && GetRootPaths(requireExisting: true).Count > 0;
        _chooseFoldersMenuItem.Enabled = !busy;
        _vkImportMenuItem.Enabled = !busy;
        _scanSettingsMenuItem.Enabled = !busy;
        _cancelButton.Enabled = busy && _scanCancellation is not null;
        _filterComboBox.Enabled = !busy;
        _categoryComboBox.Enabled = !busy;
        _selectExactItem.Enabled =
            !busy && _allRows.Any(row => row.Kind == FindingKind.ExactDuplicate);
        _selectSimilarItem.Enabled =
            !busy && _allRows.Any(row => row.Kind == FindingKind.SimilarImage);
        _clearSelectionItem.Enabled = !busy && _allRows.Count > 0;
        _compareItem.Enabled = !busy && SelectedImagePaths().Length == 2;
        _exportReportItem.Enabled = !busy && _scanResult is not null;
        _quarantineItem.Enabled = !busy && _allRows.Any(row => row.Selected);
        _undoItem.Enabled = !busy && _lastMoves.Any(move => File.Exists(move.QuarantinePath));
        _organizeItem.Enabled =
            !busy &&
            _scanResult is not null &&
            _scanResult.Photos.Any(photo =>
                File.Exists(photo.FullPath) &&
                photo.PrimaryCategory != PhotoCategory.Unknown);
        _undoOrganizeItem.Enabled =
            !busy && _lastCategoryMoves.Any(move => File.Exists(move.CategoryPath));
        _openQuarantineItem.Enabled = !busy && FindQuarantineFolder() is not null;
        _actionsButton.Enabled =
            !busy && (_allRows.Count > 0 || FindQuarantineFolder() is not null);
        if (!busy)
        {
            _progressBar.Value = 0;
        }
    }

    private void UpdateActionButtons()
    {
        _selectExactItem.Enabled =
            !_busy && _allRows.Any(row => row.Kind == FindingKind.ExactDuplicate);
        _selectSimilarItem.Enabled =
            !_busy && _allRows.Any(row => row.Kind == FindingKind.SimilarImage);
        _clearSelectionItem.Enabled = !_busy && _allRows.Count > 0;
        _compareItem.Enabled = !_busy && SelectedImagePaths().Length == 2;
        _exportReportItem.Enabled = !_busy && _scanResult is not null;
        _quarantineItem.Enabled = !_busy && _allRows.Any(row => row.Selected);
        _organizeItem.Enabled =
            _scanResult is not null &&
            _scanResult.Photos.Any(photo =>
                File.Exists(photo.FullPath) &&
                photo.PrimaryCategory != PhotoCategory.Unknown);
        _undoOrganizeItem.Enabled =
            _lastCategoryMoves.Any(move => File.Exists(move.CategoryPath));
        _openQuarantineItem.Enabled = !_busy && FindQuarantineFolder() is not null;
        _actionsButton.Enabled =
            !_busy && (_allRows.Count > 0 || FindQuarantineFolder() is not null);
    }

    private string[] SelectedImagePaths() =>
        _allRows
            .Where(row => row.Selected && row.Photo.MediaKind == MediaKind.Image)
            .Select(row => row.Photo.FullPath)
            .Where(File.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .ToArray();

    private void CompareSelectedImages()
    {
        var paths = SelectedImagePaths();
        if (paths.Length != 2)
        {
            MessageBox.Show(
                this,
                "Отметьте флажками ровно два изображения.",
                "Сравнение изображений",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        try
        {
            using var comparison = new ImageComparisonForm(
                paths[0],
                paths[1],
                IsDarkTheme);
            comparison.ShowDialog(this);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            MessageBox.Show(
                this,
                $"Не удалось открыть изображения для сравнения.\n\n{exception.Message}",
                "Сравнение изображений",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private async Task ExportReportAsync()
    {
        if (_scanResult is null)
        {
            return;
        }

        using var dialog = new SaveFileDialog
        {
            Title = "Экспорт отчёта анализа",
            Filter = "CSV с разделителем точка с запятой (*.csv)|*.csv",
            DefaultExt = "csv",
            AddExtension = true,
            FileName = $"MediaTidy-report-{DateTime.Now:yyyyMMdd-HHmm}.csv",
            InitialDirectory = GetRootPaths(requireExisting: true).FirstOrDefault()
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            await ScanReportExporter.ExportCsvAsync(
                dialog.FileName,
                _scanResult,
                CancellationToken.None);
            _statusLabel.Text = $"Отчёт сохранён: {dialog.FileName}";
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(
                this,
                exception.Message,
                "Экспорт отчёта",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void UpdatePreview()
    {
        _preview.Image?.Dispose();
        _preview.Image = null;
        _details.Clear();
        _previewTitle.Text = "Выберите изображение";
        _openPreviewButton.Text = "На весь экран";
        _showInExplorerButton.Enabled = false;
        _openPreviewButton.Enabled = false;
        _previewedPhoto = null;
        _previewedRow = null;

        if (_grid.CurrentRow?.DataBoundItem is not FindingRow row)
        {
            UpdateNavigationButtons();
            return;
        }

        _previewTitle.Text = row.FileName;
        _previewedRow = row;
        _previewedPhoto = row.Photo;
        _showInExplorerButton.Enabled = File.Exists(row.Photo.FullPath);
        _openPreviewButton.Enabled = File.Exists(row.Photo.FullPath);
        _openPreviewButton.Text = row.Photo.MediaKind == MediaKind.Video
            ? "Открыть видео"
            : "На весь экран";

        try
        {
            if (row.Photo.MediaKind == MediaKind.Video)
            {
                _preview.Image = VideoThumbnailService.GetShellThumbnail(
                    row.Photo.FullPath,
                    900);
            }
            else
            {
                using var stream = new FileStream(
                    row.Photo.FullPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete);
                using var image = Image.FromStream(stream);
                var previewImage = new Bitmap(image);
                ImageScanner.ApplyExifOrientation(previewImage);
                _preview.Image = previewImage;
            }

            if (_preview.Image is null)
            {
                _previewTitle.Text = $"{row.FileName} — предпросмотр недоступен";
            }
        }
        catch
        {
            _previewTitle.Text = $"{row.FileName} — предпросмотр недоступен";
        }

        RenderDetails(row);
        UpdateNavigationButtons();
    }

    private void RenderDetails(FindingRow row)
    {
        _details.SuspendLayout();
        _details.Clear();
        AppendDetailHeader("Файл");
        AppendDetailLine("Путь", row.Photo.FullPath);
        AppendDetailLine("Тип", row.Type);
        AppendDetailLine("Дата", $"{row.Date} ({row.Photo.DateSource})");
        AppendDetailLine("Причина", row.Reason);

        AppendDetailHeader("Распознавание");
        AppendDetailLine("Категория", $"{row.Category}  •  уверенность {row.Confidence}");
        AppendDetailLine("Оценки", FormatCategoryScores(row.Photo.CategoryScores));
        AppendDetailLine(
            "OCR",
            $"{row.Photo.OcrWordCount:N0} слов  •  текст занимает {row.Photo.OcrTextAreaRatio:P1}");
        if (!string.IsNullOrWhiteSpace(row.Photo.OcrPreview))
        {
            AppendDetailLine("Найденный текст", row.Photo.OcrPreview);
        }

        AppendDetailHeader("Технические данные");
        AppendDetailLine("Размер", $"{row.Size}  •  {row.Resolution}");
        if (row.Photo.MediaKind == MediaKind.Video)
        {
            AppendDetailLine("Длительность", FormatDuration(row.Photo.Duration));
        }
        else
        {
            AppendDetailLine(
                "Камера и качество",
                $"EXIF: {(row.Photo.HasCameraMetadata ? "есть" : "нет")}  •  " +
                $"детализация {row.Photo.DetailScore:F1}  •  резкость {row.Photo.SharpnessScore:F1}");
            AppendDetailLine(
                "Лица в серии",
                row.Photo.FaceAnalysisAvailable
                    ? $"{row.Photo.FaceCount}; закрытые глаза: " +
                      (row.Photo.ClosedEyeCount >= 0
                          ? row.Photo.ClosedEyeCount.ToString()
                          : "отдельная модель не установлена")
                    : "не анализировались для этого файла");
        }

        if (row.RecommendedKeep)
        {
            _details.SelectionFont = _detailsBoldFont;
            _details.SelectionColor = IsDarkTheme ? Color.LightGreen : Color.SeaGreen;
            _details.AppendText("Рекомендация: оставить этот экземпляр");
        }

        _details.SelectionStart = 0;
        _details.ResumeLayout();
    }

    private void AppendDetailHeader(string text)
    {
        if (_details.TextLength > 0)
        {
            _details.AppendText(Environment.NewLine);
        }

        _details.SelectionFont = _detailsHeaderFont;
        _details.SelectionColor = IsDarkTheme ? Color.LightSkyBlue : Color.FromArgb(40, 82, 125);
        _details.AppendText(text + Environment.NewLine);
    }

    private void AppendDetailLine(string label, string value)
    {
        _details.SelectionFont = _detailsBoldFont;
        _details.SelectionColor = AppTheme.Text;
        _details.AppendText(label + ": ");
        _details.SelectionFont = Font;
        _details.SelectionColor = AppTheme.Text;
        _details.AppendText(value + Environment.NewLine);
    }

    private void GridOnCellDoubleClick(object? sender, DataGridViewCellEventArgs eventArgs)
    {
        if (eventArgs.RowIndex < 0 ||
            _grid.Rows[eventArgs.RowIndex].DataBoundItem is not FindingRow row)
        {
            return;
        }

        ShowFileInExplorer(row.Photo.FullPath);
    }

    private void ShowCurrentFileInExplorer()
    {
        if (_previewedPhoto is not null)
        {
            ShowFileInExplorer(_previewedPhoto.FullPath);
        }
    }

    private void ShowFileInExplorer(string path)
    {
        if (ShellFileNavigator.SelectFile(path))
        {
            return;
        }

        MessageBox.Show(
            this,
            "Не удалось открыть Проводник и выделить файл. Возможно, файл уже перемещён.",
            "MediaTidy",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private void ShowCurrentImageFullScreen()
    {
        var photo = _previewedPhoto;
        if (photo is null || !File.Exists(photo.FullPath))
        {
            return;
        }

        if (photo.MediaKind == MediaKind.Video)
        {
            try
            {
                Process.Start(new ProcessStartInfo(photo.FullPath)
                {
                    UseShellExecute = true
                });
            }
            catch (Exception exception) when (
                exception is InvalidOperationException or
                System.ComponentModel.Win32Exception)
            {
                MessageBox.Show(
                    this,
                    "Не удалось открыть видео в системном проигрывателе.",
                    "MediaTidy",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }

            return;
        }

        using var previewForm = new ImagePreviewForm(photo.FullPath);
        previewForm.ShowDialog(this);
    }

    private void SelectAdjacentRow(int offset)
    {
        if (_grid.Rows.Count == 0)
        {
            return;
        }

        var currentIndex = _previewedRow is null
            ? _grid.CurrentRow?.Index ?? 0
            : _visibleRows.IndexOf(_previewedRow);
        if (currentIndex < 0)
        {
            currentIndex = _grid.CurrentRow?.Index ?? 0;
        }
        var targetIndex = Math.Clamp(currentIndex + offset, 0, _grid.Rows.Count - 1);
        var targetRow = _grid.Rows[targetIndex];
        var targetCell = targetRow.Cells
            .Cast<DataGridViewCell>()
            .First(cell => cell.Visible);

        _suppressPreviewUpdate = true;
        try
        {
            _grid.CurrentCell = targetCell;
            _grid.ClearSelection();
            targetRow.Selected = true;
        }
        finally
        {
            _suppressPreviewUpdate = false;
        }

        _grid.FirstDisplayedScrollingRowIndex = targetIndex;
        UpdatePreview();
    }

    private void UpdateNavigationButtons()
    {
        var rowIndex = _previewedRow is null ? -1 : _visibleRows.IndexOf(_previewedRow);
        _previousButton.Enabled = rowIndex > 0;
        _nextButton.Enabled = rowIndex >= 0 && rowIndex < _visibleRows.Count - 1;
    }

    private void GridOnCellToolTipTextNeeded(
        object? sender,
        DataGridViewCellToolTipTextNeededEventArgs eventArgs)
    {
        if (eventArgs.RowIndex < 0 ||
            _grid.Rows[eventArgs.RowIndex].DataBoundItem is not FindingRow row)
        {
            return;
        }

        var propertyName = _grid.Columns[eventArgs.ColumnIndex].DataPropertyName;
        eventArgs.ToolTipText = propertyName switch
        {
            nameof(FindingRow.FileName) => row.Photo.FullPath,
            nameof(FindingRow.Category) =>
                $"{row.Category}: уверенность {row.Confidence}. Все оценки: " +
                FormatCategoryScores(row.Photo.CategoryScores),
            nameof(FindingRow.OcrWords) =>
                $"OCR нашёл {row.Photo.OcrWordCount:N0} слов. " +
                $"Текст занимает примерно {row.Photo.OcrTextAreaRatio:P1} изображения.",
            nameof(FindingRow.Reason) => row.Reason,
            _ => eventArgs.ToolTipText
        };
    }

    private void EnsurePreviewPanelWidth()
    {
        if (_mainSplit.Width <= 0)
        {
            return;
        }

        var desiredPreviewWidth = Math.Clamp(_mainSplit.Width / 3, 420, 560);
        const int minimumTableWidth = 600;
        const int minimumPreviewWidth = 380;
        var maximumDistance =
            _mainSplit.Width - minimumPreviewWidth - _mainSplit.SplitterWidth;
        var minimumDistance = Math.Min(minimumTableWidth, maximumDistance);

        _mainSplit.Panel1MinSize = 0;
        _mainSplit.Panel2MinSize = 0;
        _mainSplit.SplitterDistance = Math.Clamp(
            _mainSplit.Width - desiredPreviewWidth,
            minimumDistance,
            maximumDistance);
        _mainSplit.Panel1MinSize = Math.Min(minimumTableWidth, _mainSplit.SplitterDistance);
        _mainSplit.Panel2MinSize = Math.Min(
            minimumPreviewWidth,
            _mainSplit.Width - _mainSplit.SplitterDistance - _mainSplit.SplitterWidth);
    }

    private bool IsDarkTheme =>
        string.Equals(_settings.Theme, "Dark", StringComparison.OrdinalIgnoreCase);

    private void SetTheme(bool dark)
    {
        _settings.Theme = dark ? "Dark" : "Light";
        ApplyTheme();
        SaveSettings();
        _grid.Invalidate();
        if (_previewedRow is not null)
        {
            RenderDetails(_previewedRow);
        }
    }

    private void ApplyTheme()
    {
        AppTheme.Apply(this, IsDarkTheme);
        _lightThemeMenuItem.Checked = !IsDarkTheme;
        _darkThemeMenuItem.Checked = IsDarkTheme;
        _details.BackColor = AppTheme.Window;
        _details.ForeColor = AppTheme.Text;
        _folderDetailsLabel.ForeColor = AppTheme.Muted;
        _scanSettingsLabel.ForeColor = AppTheme.Muted;
        _actionsMenu.BackColor = AppTheme.Control;
        _actionsMenu.ForeColor = AppTheme.Text;
    }

    private string? FindQuarantineFolder()
    {
        var fromMove = _lastMoves
            .Select(move => Path.GetDirectoryName(move.QuarantinePath))
            .FirstOrDefault(path => Directory.Exists(path));
        if (fromMove is not null)
        {
            return fromMove;
        }

        foreach (var root in GetRootPaths(requireExisting: true))
        {
            try
            {
                var latest = new[] { ".MediaTidy_Quarantine_*", ".PhotoCleaner_Quarantine_*" }
                    .SelectMany(pattern => Directory.EnumerateDirectories(root, pattern))
                    .OrderByDescending(path => path, StringComparer.OrdinalIgnoreCase)
                    .FirstOrDefault();
                if (latest is not null)
                {
                    return latest;
                }
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException)
            {
                // A read-only or disconnected root is simply skipped.
            }
        }

        return null;
    }

    private void OpenQuarantineFolder()
    {
        var path = FindQuarantineFolder();
        if (path is null)
        {
            MessageBox.Show(
                this,
                "Папка карантина ещё не создана.",
                "Карантин",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or
            System.ComponentModel.Win32Exception)
        {
            MessageBox.Show(
                this,
                "Не удалось открыть папку карантина.",
                "Карантин",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
    }

    private void ShowFieldHelp()
    {
        MessageBox.Show(
            this,
            "Сходство — допустимое различие perceptual hash (dHash). Чем меньше число, тем строже сравнение.\n\n" +
            "Слов OCR — сколько слов локально найдено на изображении. Большое значение часто указывает на документ, чек, скриншот или мем.\n\n" +
            "Категория и уверенность — осторожная оценка локальной модели с учётом OCR, EXIF камеры, пропорций, имени файла и исходной папки. Слабый результат становится «Не определено».\n\n" +
            "Видео индексируются, участвуют в поиске точных и визуально похожих копий и открываются в системном проигрывателе.\n\n" +
            "Группа — файлы с одинаковым номером считаются точными дубликатами или похожими изображениями.\n\n" +
            "Зелёная строка — рекомендуемый экземпляр в группе. Красная строка — файл, отмеченный для помещения в карантин.\n\n" +
            "Двойной щелчок по строке открывает Проводник и выделяет конкретный файл.",
            "Что означают поля",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private void ShowAbout()
    {
        MessageBox.Show(
            this,
            "MediaTidy 2.6.2\n\n" +
            "Неофициальный независимый инструмент для локальной сортировки медиафайлов " +
            "и импорта вложений через VK API.\n\n" +
            "Приложение не связано с ООО «В Контакте» и не использует логотип VK. " +
            "VK является товарным обозначением соответствующего правообладателя.\n\n" +
            "Распознавание выполняется локально. Категории являются вероятностными подсказками.",
            "О программе",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private void GridOnCellFormatting(object? sender, DataGridViewCellFormattingEventArgs eventArgs)
    {
        if (eventArgs.RowIndex < 0 ||
            _grid.Rows[eventArgs.RowIndex].DataBoundItem is not FindingRow row)
        {
            return;
        }

        if (row.RecommendedKeep)
        {
            _grid.Rows[eventArgs.RowIndex].DefaultCellStyle.BackColor =
                IsDarkTheme ? Color.FromArgb(35, 70, 50) : Color.Honeydew;
        }
        else if (row.Selected)
        {
            _grid.Rows[eventArgs.RowIndex].DefaultCellStyle.BackColor =
                IsDarkTheme ? Color.FromArgb(82, 45, 48) : Color.MistyRose;
        }
        else
        {
            _grid.Rows[eventArgs.RowIndex].DefaultCellStyle.BackColor = AppTheme.Window;
        }
    }

    private static string KindText(FindingKind kind) => kind switch
    {
        FindingKind.ClassifiedImage => "Файл",
        FindingKind.ExactDuplicate => "Точный дубликат",
        FindingKind.SimilarImage => "Похожее изображение",
        FindingKind.ReviewCandidate => "Проверить",
        _ => kind.ToString()
    };

    private static string FormatCategoryScores(
        IReadOnlyDictionary<PhotoCategory, double> scores) =>
        scores.Count == 0
            ? "нет данных"
            : string.Join(
                "; ",
                scores
                    .OrderByDescending(pair => pair.Value)
                    .Select(pair => $"{CategoryNames.Russian(pair.Key)} {pair.Value:P0}"));

    private static string FormatDuration(TimeSpan duration) =>
        duration <= TimeSpan.Zero
            ? "не определена"
            : duration.TotalHours >= 1
                ? duration.ToString(@"h\:mm\:ss")
                : duration.ToString(@"m\:ss");

    private static string FormatBytes(long bytes)
    {
        string[] suffixes = ["Б", "КБ", "МБ", "ГБ", "ТБ"];
        double value = bytes;
        var suffix = 0;
        while (value >= 1024 && suffix < suffixes.Length - 1)
        {
            value /= 1024;
            suffix++;
        }

        return $"{value:0.##} {suffixes[suffix]}";
    }
}
