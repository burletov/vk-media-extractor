using System.Diagnostics;

namespace MediaExtractorForVK;

internal sealed class VkImportDialog : Form
{
    private readonly VkImportService _service;
    private readonly TextBox _tokenTextBox = new()
    {
        Dock = DockStyle.Fill,
        UseSystemPasswordChar = true
    };
    private readonly Button _tokenButton = new()
    {
        Text = "Получить токен...",
        AutoSize = true
    };
    private readonly TextBox _destinationTextBox = new()
    {
        Dock = DockStyle.Fill
    };
    private readonly CheckBox _photosCheckBox = new()
    {
        Text = "Фотографии",
        Checked = true,
        AutoSize = true
    };
    private readonly CheckBox _videosCheckBox = new()
    {
        Text = "Видеофайлы",
        Checked = true,
        AutoSize = true
    };
    private readonly CheckBox _gifsCheckBox = new()
    {
        Text = "GIF",
        Checked = false,
        AutoSize = true
    };
    private readonly RadioButton _allConversationsRadio = new()
    {
        Text = "Все личные сообщения и беседы",
        Checked = true,
        AutoSize = true
    };
    private readonly RadioButton _singleConversationRadio = new()
    {
        Text = "Только один диалог или беседа",
        AutoSize = true
    };
    private readonly TextBox _peerIdTextBox = new()
    {
        Dock = DockStyle.Fill,
        Enabled = false,
        PlaceholderText = "Скопируйте id чата из адресной строки VK"
    };
    private readonly Label _peerIdLabel = new()
    {
        Text = "ID чата:",
        AutoSize = true,
        Margin = new Padding(0, 7, 12, 3),
        Visible = false
    };
    private readonly Label _peerHelpLabel = new()
    {
        AutoSize = true,
        MaximumSize = new Size(245, 0),
        Tag = "muted",
        Text = "Откройте нужный диалог или беседу VK и скопируйте id из адресной строки.",
        Visible = false
    };
    private readonly CheckBox _sizeLimitCheckBox = new()
    {
        Text = "Ограничить размер файла",
        AutoSize = true
    };
    private readonly PaddedNumericUpDown _maxFileSizeGb = new()
    {
        Minimum = 1,
        Maximum = 1024,
        Value = 1,
        Width = 70,
        Visible = false
    };
    private readonly Label _maxFileSizeUnitLabel = new()
    {
        Text = "ГБ",
        AutoSize = true,
        Margin = new Padding(6, 7, 0, 0),
        Visible = false
    };
    private readonly Label _resumeLabel = new()
    {
        Dock = DockStyle.Fill,
        AutoSize = true,
        Tag = "muted",
        Text = "Контрольная точка для выбранных параметров пока не найдена."
    };
    private readonly Label _stageLabel = new()
    {
        Dock = DockStyle.Fill,
        Text = "Готов к импорту",
        AutoEllipsis = true,
        Font = new Font("Segoe UI Semibold", 10F)
    };
    private readonly ThemedProgressBar _progress = new()
    {
        Dock = DockStyle.Fill,
        Style = ProgressBarStyle.Continuous
    };
    private readonly Label _statisticsLabel = new()
    {
        Dock = DockStyle.Fill,
        Text = "Диалогов: 0  •  Найдено: 0  •  Загружено: 0  •  Пропущено: 0  •  Ошибок: 0"
    };
    private readonly Label _timeLabel = new()
    {
        Dock = DockStyle.Fill,
        Text = "Прошло: 00:00"
    };
    private readonly Label _fileLabel = new()
    {
        Dock = DockStyle.Fill,
        AutoEllipsis = true,
        Text = "Текущий файл: —"
    };
    private readonly ListBox _log = new()
    {
        Dock = DockStyle.Fill,
        HorizontalScrollbar = true
    };
    private readonly Button _startButton = new()
    {
        Text = "Начать импорт",
        AutoSize = true
    };
    private readonly Button _cancelButton = new()
    {
        Text = "Отмена",
        AutoSize = true,
        DialogResult = DialogResult.Cancel
    };
    private readonly Button _browseButton = new()
    {
        Text = "Выбрать...",
        AutoSize = true
    };
    private readonly System.Windows.Forms.Timer _heartbeatTimer = new()
    {
        Interval = 1000
    };

    private CancellationTokenSource? _cancellation;
    private bool _running;
    private bool _completed;
    private string _lastBrowseFolder;
    private VkImportProgress? _lastProgress;
    private readonly Stopwatch _uiStopwatch = new();

    public VkImportDialog(
        VkImportService service,
        string destinationPath,
        string? lastBrowseFolder,
        bool darkTheme)
    {
        _service = service;
        _lastBrowseFolder = Directory.Exists(lastBrowseFolder)
            ? lastBrowseFolder
            : Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);

        Text = "Импорт фото и видео из VK";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(800, 680);
        Size = new Size(880, 740);
        Font = new Font("Segoe UI", 9F);
        _destinationTextBox.Text = destinationPath;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(18),
            ColumnCount = 3,
            RowCount = 16
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        for (var index = 1; index <= 13; index++)
        {
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        }
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var description = new Label
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            MaximumSize = new Size(820, 0),
            Text =
                "Импорт требует подключения к интернету: приложение обращается к api.vk.com и адресам медиафайлов. " +
                "Токен используется только в этом окне и не сохраняется. В зависимости от объёма истории импорт может занять десятки часов или до суток. " +
                "Процесс можно остановить и продолжить позже с той же папкой и параметрами: контрольная точка и незавершённые .part-файлы сохраняются автоматически.",
            Padding = new Padding(0, 0, 0, 10)
        };
        layout.Controls.Add(description, 0, 0);
        layout.SetColumnSpan(description, 3);

        layout.Controls.Add(CreateLabel("Токен доступа:"), 0, 1);
        layout.Controls.Add(_tokenTextBox, 1, 1);
        _tokenButton.Click += (_, _) => OpenVkHost();
        layout.Controls.Add(_tokenButton, 2, 1);

        layout.Controls.Add(CreateLabel("Папка:"), 0, 2);
        layout.Controls.Add(_destinationTextBox, 1, 2);
        _browseButton.Click += (_, _) => BrowseDestination();
        layout.Controls.Add(_browseButton, 2, 2);

        var conversationMode = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            WrapContents = true
        };
        conversationMode.Controls.Add(_allConversationsRadio);
        conversationMode.Controls.Add(_singleConversationRadio);
        _allConversationsRadio.CheckedChanged += (_, _) => UpdateConversationMode();
        _singleConversationRadio.CheckedChanged += (_, _) => UpdateConversationMode();
        layout.Controls.Add(CreateLabel("Источник:"), 0, 3);
        layout.Controls.Add(conversationMode, 1, 3);
        layout.SetColumnSpan(conversationMode, 2);

        layout.Controls.Add(_peerIdLabel, 0, 4);
        layout.Controls.Add(_peerIdTextBox, 1, 4);
        layout.Controls.Add(_peerHelpLabel, 2, 4);

        var mediaOptions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true
        };
        mediaOptions.Controls.Add(_photosCheckBox);
        mediaOptions.Controls.Add(_videosCheckBox);
        mediaOptions.Controls.Add(_gifsCheckBox);
        layout.Controls.Add(CreateLabel("Скачать:"), 0, 5);
        layout.Controls.Add(mediaOptions, 1, 5);
        layout.SetColumnSpan(mediaOptions, 2);

        var limitOptions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true
        };
        limitOptions.Controls.Add(_sizeLimitCheckBox);
        limitOptions.Controls.Add(_maxFileSizeGb);
        limitOptions.Controls.Add(_maxFileSizeUnitLabel);
        layout.Controls.Add(CreateLabel("Размер:"), 0, 6);
        layout.Controls.Add(limitOptions, 1, 6);
        layout.SetColumnSpan(limitOptions, 2);

        var accessWarning = new Label
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            Tag = "warning",
            Text =
                "Важно: доступ к messages.* выдаётся не всем приложениям VK. Не закрывайте целевую папку и не удаляйте .part или .MediaExtractorForVK_VK_Import_*.json: " +
                "они нужны для продолжения. Ошибки доступа и неудачные загрузки останутся в очереди и будут показаны в журнале."
        };
        layout.Controls.Add(accessWarning, 0, 7);
        layout.SetColumnSpan(accessWarning, 3);

        layout.Controls.Add(_resumeLabel, 0, 8);
        layout.SetColumnSpan(_resumeLabel, 3);

        layout.Controls.Add(_stageLabel, 0, 9);
        layout.SetColumnSpan(_stageLabel, 3);
        layout.Controls.Add(_progress, 0, 10);
        layout.SetColumnSpan(_progress, 3);
        layout.Controls.Add(_statisticsLabel, 0, 11);
        layout.SetColumnSpan(_statisticsLabel, 3);
        layout.Controls.Add(_fileLabel, 0, 12);
        layout.SetColumnSpan(_fileLabel, 3);
        layout.Controls.Add(_timeLabel, 0, 13);
        layout.SetColumnSpan(_timeLabel, 3);

        var logGroup = new GroupBox
        {
            Dock = DockStyle.Fill,
            Text = "Журнал импорта",
            Padding = new Padding(8)
        };
        logGroup.Controls.Add(_log);
        layout.Controls.Add(logGroup, 0, 14);
        layout.SetColumnSpan(logGroup, 3);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            Padding = new Padding(0, 10, 0, 0)
        };
        _startButton.Click += async (_, _) => await StartOrCloseAsync();
        _cancelButton.Click += (_, _) =>
        {
            if (_running)
            {
                AddLog("Остановка запрошена пользователем.");
                _cancellation?.Cancel();
            }
        };
        buttons.Controls.Add(_cancelButton);
        buttons.Controls.Add(_startButton);
        layout.Controls.Add(buttons, 0, 15);
        layout.SetColumnSpan(buttons, 3);

        Controls.Add(layout);
        AcceptButton = _startButton;
        CancelButton = _cancelButton;
        FormClosing += OnFormClosing;
        _heartbeatTimer.Tick += (_, _) => UpdateHeartbeat();
        _destinationTextBox.TextChanged += (_, _) => UpdateResumeState();
        _photosCheckBox.CheckedChanged += (_, _) => UpdateResumeState();
        _videosCheckBox.CheckedChanged += (_, _) => UpdateResumeState();
        _gifsCheckBox.CheckedChanged += (_, _) => UpdateResumeState();
        _peerIdTextBox.TextChanged += (_, _) => UpdateResumeState();
        _sizeLimitCheckBox.CheckedChanged += (_, _) => UpdateSizeLimitVisibility();
        AppTheme.Apply(this, darkTheme);
        UpdateConversationMode();
        UpdateSizeLimitVisibility();
        UpdateResumeState();
    }

    public VkImportResult? Result { get; private set; }
    public string DestinationPath => _destinationTextBox.Text.Trim();
    public string LastBrowseFolder => _lastBrowseFolder;

    private static Label CreateLabel(string text) => new()
    {
        Text = text,
        AutoSize = true,
        Margin = new Padding(0, 7, 12, 3)
    };

    private async Task StartOrCloseAsync()
    {
        if (_completed)
        {
            DialogResult = Result is null ? DialogResult.Cancel : DialogResult.OK;
            Close();
            return;
        }

        if (_running)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_tokenTextBox.Text) ||
            string.IsNullOrWhiteSpace(_destinationTextBox.Text) ||
            (!_photosCheckBox.Checked && !_videosCheckBox.Checked && !_gifsCheckBox.Checked))
        {
            MessageBox.Show(
                this,
                "Укажите токен, папку и хотя бы один тип файлов.",
                "Импорт VK",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        long? peerId = null;
        if (_singleConversationRadio.Checked)
        {
            if (!VkImportService.TryParsePeerId(_peerIdTextBox.Text, out var parsedPeerId))
            {
                MessageBox.Show(
                    this,
                    "Не удалось определить ID чата. Откройте нужный диалог или беседу VK и скопируйте id из адресной строки.",
                    "Импорт VK",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            peerId = parsedPeerId;
        }

        _running = true;
        _log.Items.Clear();
        _cancellation = new CancellationTokenSource();
        SetInputEnabled(false);
        _startButton.Enabled = false;
        _cancelButton.Text = "Остановить";
        _cancelButton.DialogResult = DialogResult.None;
        _progress.Style = ProgressBarStyle.Marquee;
        _uiStopwatch.Restart();
        _heartbeatTimer.Start();
        AddLog("Импорт запущен. Устанавливаем соединение.");

        var options = new VkImportOptions
        {
            AccessToken = _tokenTextBox.Text.Trim(),
            DestinationPath = Path.GetFullPath(_destinationTextBox.Text.Trim()),
            DownloadPhotos = _photosCheckBox.Checked,
            DownloadVideos = _videosCheckBox.Checked,
            DownloadGifs = _gifsCheckBox.Checked,
            PeerId = peerId,
            MaxFileSizeBytes = _sizeLimitCheckBox.Checked
                ? (long)_maxFileSizeGb.Value * 1024L * 1024L * 1024L
                : null
        };
        var progress = new Progress<VkImportProgress>(UpdateProgress);

        try
        {
            Result = await _service.ImportAllConversationsAsync(
                options,
                progress,
                _cancellation.Token);
            _stageLabel.Text = Result.Errors.Count == 0
                ? "Импорт завершён"
                : "Импорт завершён с ошибками";
            AddLog(
                $"Завершено: скачано {Result.Downloaded:N0}, " +
                $"пропущено {Result.Skipped:N0}, ошибок {Result.Errors.Count:N0}.");
            _completed = true;
        }
        catch (OperationCanceledException)
        {
            _stageLabel.Text = "Импорт остановлен. Можно продолжить с сохранённого места.";
            AddLog("Импорт остановлен. Контрольная точка и незавершённые файлы сохранены.");
            _completed = false;
        }
        catch (Exception exception) when (
            exception is HttpRequestException or IOException or InvalidOperationException or
            System.Text.Json.JsonException)
        {
            _stageLabel.Text = "Импорт не выполнен";
            AddLog($"ОШИБКА: {exception.Message}");
            _completed = false;
        }
        finally
        {
            _heartbeatTimer.Stop();
            _uiStopwatch.Stop();
            _running = false;
            _cancellation.Dispose();
            _cancellation = null;
            _progress.Style = ProgressBarStyle.Continuous;
            _startButton.Text = _completed ? "Закрыть" : "Продолжить импорт";
            _startButton.Enabled = true;
            _cancelButton.Enabled = !_completed;
            _cancelButton.Text = "Отмена";
            _cancelButton.DialogResult = DialogResult.Cancel;
            if (!_completed)
            {
                SetInputEnabled(true);
                UpdateResumeState();
            }
        }
    }

    private void UpdateProgress(VkImportProgress value)
    {
        _lastProgress = value;
        _stageLabel.Text = value.Stage;
        if (value.IsIndeterminate || value.Total <= 0)
        {
            _progress.Style = ProgressBarStyle.Marquee;
        }
        else
        {
            _progress.Style = ProgressBarStyle.Continuous;
            _progress.Maximum = Math.Max(1, value.Total);
            _progress.Value = Math.Clamp(value.Current, 0, _progress.Maximum);
        }

        _statisticsLabel.Text =
            $"Диалогов: {value.Conversations:N0}  •  Найдено: {value.FoundFiles:N0}  •  " +
            $"Загружено: {value.Downloaded:N0}  •  Пропущено: {value.Skipped:N0}  •  " +
            $"Ошибок: {value.ErrorCount:N0}";
        _fileLabel.Text = string.IsNullOrWhiteSpace(value.CurrentFile)
            ? "Текущий файл: —"
            : $"Текущий файл: {value.CurrentFile}" +
              (value.TotalBytes is > 0
                  ? $"  •  {FormatBytes(value.BytesDownloaded)} / {FormatBytes(value.TotalBytes.Value)}"
                  : value.BytesDownloaded > 0
                      ? $"  •  {FormatBytes(value.BytesDownloaded)}"
                      : "");
        UpdateHeartbeat();
        if (!string.IsNullOrWhiteSpace(value.LogMessage))
        {
            AddLog(value.LogMessage);
        }

        if (!string.IsNullOrWhiteSpace(value.ErrorMessage))
        {
            AddLog($"ОШИБКА: {value.ErrorMessage}");
        }
    }

    private void UpdateHeartbeat()
    {
        _timeLabel.Text = $"Прошло: {FormatDuration(_uiStopwatch.Elapsed)}";
    }

    private void AddLog(string message)
    {
        var line = $"{DateTime.Now:HH:mm:ss}  {message}";
        if (_log.Items.Count == 0 || !Equals(_log.Items[^1], line))
        {
            _log.Items.Add(line);
            _log.TopIndex = _log.Items.Count - 1;
        }
    }

    private void SetInputEnabled(bool enabled)
    {
        _tokenTextBox.Enabled = enabled;
        _tokenButton.Enabled = enabled;
        _destinationTextBox.Enabled = enabled;
        _photosCheckBox.Enabled = enabled;
        _videosCheckBox.Enabled = enabled;
        _gifsCheckBox.Enabled = enabled;
        _allConversationsRadio.Enabled = enabled;
        _singleConversationRadio.Enabled = enabled;
        _peerIdTextBox.Enabled = enabled && _singleConversationRadio.Checked;
        _sizeLimitCheckBox.Enabled = enabled;
        _maxFileSizeGb.Enabled = enabled && _sizeLimitCheckBox.Checked;
        _browseButton.Enabled = enabled;
    }

    private void BrowseDestination()
    {
        var selected = NativeFolderPicker.PickFolders(
            this,
            Directory.Exists(_destinationTextBox.Text)
                ? _destinationTextBox.Text
                : _lastBrowseFolder);
        if (selected.Count > 0)
        {
            _destinationTextBox.Text = selected[0];
            _lastBrowseFolder = selected[0];
        }
    }

    private void OnFormClosing(object? sender, FormClosingEventArgs eventArgs)
    {
        if (!_running)
        {
            return;
        }

        _cancellation?.Cancel();
        eventArgs.Cancel = true;
    }

    private void UpdateConversationMode()
    {
        _peerIdTextBox.Enabled = !_running && _singleConversationRadio.Checked;
        _peerIdTextBox.Visible = _singleConversationRadio.Checked;
        _peerIdLabel.Visible = _singleConversationRadio.Checked;
        _peerHelpLabel.Visible = _singleConversationRadio.Checked;
        UpdateResumeState();
    }

    private void UpdateSizeLimitVisibility()
    {
        if (_sizeLimitCheckBox.Checked && _maxFileSizeGb.Value < 1)
        {
            _maxFileSizeGb.Value = 1;
        }

        _maxFileSizeGb.Visible = _sizeLimitCheckBox.Checked;
        _maxFileSizeUnitLabel.Visible = _sizeLimitCheckBox.Checked;
        _maxFileSizeGb.Enabled = !_running && _sizeLimitCheckBox.Checked;
    }

    private void UpdateResumeState()
    {
        if (_running || string.IsNullOrWhiteSpace(_destinationTextBox.Text))
        {
            return;
        }

        long? peerId = null;
        if (_singleConversationRadio.Checked)
        {
            if (!VkImportService.TryParsePeerId(_peerIdTextBox.Text, out var parsed))
            {
                _resumeLabel.Text = "Введите корректный ID или адрес диалога, чтобы проверить возможность продолжения.";
                return;
            }

            peerId = parsed;
        }

        var info = VkImportService.GetResumeInfo(
            _destinationTextBox.Text.Trim(),
            _photosCheckBox.Checked,
            _videosCheckBox.Checked,
            _gifsCheckBox.Checked,
            peerId);
        if (info is null)
        {
            _resumeLabel.Text = "Контрольная точка для выбранных параметров пока не найдена.";
            _startButton.Text = "Начать импорт";
            return;
        }

        _resumeLabel.Text =
            $"Можно продолжить: найдено {info.FoundFiles:N0}, готово {info.CompletedFiles:N0}, " +
            $"в очереди ошибок {info.FailedFiles:N0}. Сохранено {info.UpdatedUtc.ToLocalTime():g}.";
        _startButton.Text = "Продолжить импорт";
    }

    private static string FormatDuration(TimeSpan value) =>
        value.TotalHours >= 1
            ? value.ToString(@"h\:mm\:ss")
            : value.ToString(@"m\:ss");

    private static string FormatBytes(long value)
    {
        string[] units = ["Б", "КБ", "МБ", "ГБ"];
        var size = (double)value;
        var unit = 0;
        while (size >= 1024 && unit < units.Length - 1)
        {
            size /= 1024;
            unit++;
        }

        return $"{size:0.#} {units[unit]}";
    }

    private void OpenVkHost()
    {
        var answer = MessageBox.Show(
            this,
            "VKHost — сторонний неофициальный сервис. Полученный токен может дать доступ к личным сообщениям.\n\n" +
            "Проверяйте название приложения и запрашиваемые права. Приложение не получает и не сохраняет токен. " +
            "Открыть vkhost.github.io?",
            "Внешний сервис VKHost",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);
        if (answer != DialogResult.Yes)
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo("https://vkhost.github.io/")
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
                "Не удалось открыть браузер.",
                "Импорт VK",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
    }
}
