namespace MediaExtractorForVK;

internal sealed class ScanSettingsDialog : Form
{
    private readonly ThemedComboBox _profile = new()
    {
        DropDownStyle = ComboBoxStyle.DropDownList,
        Width = 230
    };
    private readonly Button _saveProfileButton = new()
    {
        Text = "Сохранить как...",
        AutoSize = true
    };
    private readonly Button _deleteProfileButton = new()
    {
        Text = "Удалить",
        AutoSize = true
    };
    private readonly PaddedNumericUpDown _distance = new()
    {
        Minimum = 1,
        Maximum = 16,
        Width = 80
    };
    private readonly CheckBox _recognition = new()
    {
        Text = "Включить локальное распознавание категорий и текста",
        AutoSize = true
    };
    private readonly ThemedComboBox _model = new()
    {
        DropDownStyle = ComboBoxStyle.DropDownList,
        Dock = DockStyle.Top
    };
    private readonly Label _modelDescription = new()
    {
        AutoSize = true,
        MaximumSize = new Size(570, 0)
    };
    private readonly Label _modelStatus = new()
    {
        AutoSize = true
    };
    private readonly ThemedProgressBar _downloadProgress = new()
    {
        Width = 220,
        Height = 22,
        Visible = false
    };
    private readonly Button _downloadButton = new()
    {
        Text = "Загрузить модель",
        AutoSize = true
    };
    private readonly RecognitionModelInstaller _installer = new();
    private readonly CancellationTokenSource _cancellation = new();
    private readonly List<ScanProfile> _profiles;
    private bool _applyingProfile;

    public ScanSettingsDialog(
        int similarityDistance,
        bool recognitionEnabled,
        string recognitionModelId,
        IReadOnlyList<ScanProfile> profiles,
        string activeProfileName,
        bool darkTheme)
    {
        Text = "Параметры анализа";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(680, 590);
        Font = new Font("Segoe UI", 9F);
        _profiles = profiles.Select(profile => profile.Clone()).ToList();

        _distance.Value = Math.Clamp(similarityDistance, 1, 16);
        _recognition.Checked = recognitionEnabled;
        _model.Items.AddRange(RecognitionModels.All.Cast<object>().ToArray());
        _model.DisplayMember = nameof(RecognitionModelInfo.DisplayName);
        _model.SelectedItem = RecognitionModels.Get(recognitionModelId);
        if (_model.SelectedIndex < 0)
        {
            _model.SelectedIndex = 0;
        }
        RefreshProfiles(activeProfileName);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(20),
            ColumnCount = 1,
            RowCount = 6
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        root.Controls.Add(CreateProfileSection(), 0, 0);
        root.Controls.Add(CreateSimilaritySection(), 0, 1);
        root.Controls.Add(CreateRecognitionSection(), 0, 2);
        root.Controls.Add(new Label
        {
            AutoSize = true,
            MaximumSize = new Size(590, 0),
            Margin = new Padding(0, 12, 0, 0),
            Tag = "muted",
            Text =
                "Категории являются вероятностными подсказками. Приложение не удаляет и не перемещает файлы автоматически на основании модели."
        }, 0, 3);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            Padding = new Padding(0, 12, 0, 0)
        };
        var cancel = new Button
        {
            Text = "Отмена",
            DialogResult = DialogResult.Cancel,
            AutoSize = true
        };
        var save = new Button
        {
            Text = "Сохранить",
            AutoSize = true
        };
        save.Click += (_, _) => SaveAndClose();
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(save);
        root.Controls.Add(buttons, 0, 5);

        Controls.Add(root);
        AcceptButton = save;
        CancelButton = cancel;
        _model.SelectedIndexChanged += (_, _) => UpdateModelState();
        _recognition.CheckedChanged += (_, _) => UpdateModelState();
        _profile.SelectedIndexChanged += (_, _) => ApplySelectedProfile();
        _saveProfileButton.Click += (_, _) => SaveCurrentProfile();
        _deleteProfileButton.Click += (_, _) => DeleteSelectedProfile();
        _downloadButton.Click += async (_, _) => await DownloadSelectedModelAsync();
        FormClosed += (_, _) => _cancellation.Cancel();
        UpdateModelState();
        AppTheme.Apply(this, darkTheme);
    }

    public int SimilarityDistance => (int)_distance.Value;
    public bool RecognitionEnabled => _recognition.Checked;
    public string RecognitionModelId =>
        (_model.SelectedItem as RecognitionModelInfo)?.Id ?? RecognitionModels.StandardId;
    public IReadOnlyList<ScanProfile> Profiles =>
        _profiles.Select(profile => profile.Clone()).ToArray();
    public string ActiveProfileName =>
        (_profile.SelectedItem as ScanProfile)?.Name ?? "";

    private Control CreateProfileSection()
    {
        var group = new GroupBox
        {
            Text = "Профиль сканирования",
            Dock = DockStyle.Top,
            AutoSize = true,
            Padding = new Padding(14)
        };
        var row = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            WrapContents = true
        };
        row.Controls.Add(new Label
        {
            Text = "Профиль:",
            AutoSize = true,
            Margin = new Padding(0, 7, 8, 3)
        });
        row.Controls.Add(_profile);
        row.Controls.Add(_saveProfileButton);
        row.Controls.Add(_deleteProfileButton);
        group.Controls.Add(row);
        return group;
    }

    private Control CreateSimilaritySection()
    {
        var group = new GroupBox
        {
            Text = "Поиск похожих файлов",
            Dock = DockStyle.Top,
            AutoSize = true,
            Padding = new Padding(14)
        };
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 2
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.Controls.Add(new Label
        {
            Text = "Допустимое различие:",
            AutoSize = true,
            Margin = new Padding(0, 7, 12, 3)
        }, 0, 0);
        layout.Controls.Add(_distance, 1, 0);
        var explanation = new Label
        {
            Text =
                "1–3 находят почти идентичные кадры, 4–8 подходят для обычного поиска, 9–16 создают более широкие группы. Меньше число — строже сравнение.",
            AutoSize = true,
            MaximumSize = new Size(550, 0),
            Tag = "muted",
            Margin = new Padding(0, 8, 0, 0)
        };
        layout.Controls.Add(explanation, 0, 1);
        layout.SetColumnSpan(explanation, 2);
        group.Controls.Add(layout);
        return group;
    }

    private Control CreateRecognitionSection()
    {
        var group = new GroupBox
        {
            Text = "Локальное распознавание",
            Dock = DockStyle.Top,
            AutoSize = true,
            Padding = new Padding(14),
            Margin = new Padding(0, 14, 0, 0)
        };
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 1
        };
        layout.Controls.Add(_recognition, 0, 0);
        layout.Controls.Add(new Label
        {
            Text = "Модель:",
            AutoSize = true,
            Margin = new Padding(0, 12, 0, 4)
        }, 0, 1);
        layout.Controls.Add(_model, 0, 2);
        _modelDescription.Margin = new Padding(0, 8, 0, 4);
        _modelDescription.Tag = "muted";
        layout.Controls.Add(_modelDescription, 0, 3);

        var installRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            WrapContents = false,
            Margin = new Padding(0, 8, 0, 0)
        };
        installRow.Controls.Add(_downloadButton);
        installRow.Controls.Add(_downloadProgress);
        _modelStatus.Margin = new Padding(10, 7, 0, 0);
        installRow.Controls.Add(_modelStatus);
        layout.Controls.Add(installRow, 0, 4);
        group.Controls.Add(layout);
        return group;
    }

    private void UpdateModelState()
    {
        var selected = (RecognitionModelInfo)_model.SelectedItem!;
        var installed = RecognitionModels.IsInstalled(selected);
        _model.Enabled = _recognition.Checked;
        _downloadButton.Enabled = _recognition.Checked && !installed && !_downloadProgress.Visible;
        _downloadButton.Visible = !selected.Bundled && !installed;
        var availableMemoryGb =
            GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / 1024D / 1024D / 1024D;
        var memoryWarning = availableMemoryGb > 0 &&
                            availableMemoryGb < selected.MinimumRamGb
            ? $"\nВнимание: приложению доступно около {availableMemoryGb:0.#} ГБ ОЗУ, " +
              $"что меньше указанного минимума."
            : "";
        _modelDescription.Text =
            $"{selected.Description}\n{selected.Requirements}{memoryWarning}";
        _downloadButton.Text =
            selected.Bundled
                ? "Включена в приложение"
                : $"Загрузить ({selected.DownloadSizeBytes / 1024D / 1024D:0} МБ)";
        _modelStatus.Text = installed ? "Готова к работе" : "Требуется загрузка";
        _modelStatus.Tag = installed ? "success" : "warning";
        _modelStatus.ForeColor = installed ? AppTheme.Success : AppTheme.Warning;
    }

    private async Task DownloadSelectedModelAsync()
    {
        var selected = (RecognitionModelInfo)_model.SelectedItem!;
        _downloadButton.Enabled = false;
        _model.Enabled = false;
        _downloadProgress.Visible = true;
        _downloadProgress.Value = 0;
        _modelStatus.Text = "Загрузка...";
        try
        {
            await _installer.InstallAsync(
                selected,
                new Progress<int>(value => _downloadProgress.Value = value),
                _cancellation.Token);
            _modelStatus.Text = "Модель установлена";
        }
        catch (OperationCanceledException)
        {
            _modelStatus.Text = "Загрузка отменена";
        }
        catch (Exception exception) when (
            exception is HttpRequestException or IOException or UnauthorizedAccessException)
        {
            _modelStatus.Text = "Не удалось загрузить";
            MessageBox.Show(
                this,
                exception.Message,
                "Загрузка модели",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            _downloadProgress.Visible = false;
            _model.Enabled = _recognition.Checked;
            UpdateModelState();
        }
    }

    private void SaveAndClose()
    {
        var selected = (RecognitionModelInfo)_model.SelectedItem!;
        if (_recognition.Checked && !RecognitionModels.IsInstalled(selected))
        {
            MessageBox.Show(
                this,
                "Сначала загрузите выбранную модель или выберите уже установленную.",
                "Параметры анализа",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        DialogResult = DialogResult.OK;
        Close();
    }

    private void RefreshProfiles(string? selectedName)
    {
        _applyingProfile = true;
        _profile.Items.Clear();
        _profile.Items.AddRange(_profiles.Cast<object>().ToArray());
        _profile.DisplayMember = nameof(ScanProfile.Name);
        var selected = _profiles.FindIndex(profile =>
            profile.Name.Equals(selectedName, StringComparison.OrdinalIgnoreCase));
        _profile.SelectedIndex = selected >= 0 ? selected : (_profiles.Count > 0 ? 0 : -1);
        _deleteProfileButton.Enabled = _profile.SelectedIndex >= 0;
        _applyingProfile = false;
    }

    private void ApplySelectedProfile()
    {
        _deleteProfileButton.Enabled = _profile.SelectedItem is ScanProfile;
        if (_applyingProfile || _profile.SelectedItem is not ScanProfile profile)
        {
            return;
        }

        _applyingProfile = true;
        _distance.Value = Math.Clamp(profile.SimilarityDistance, 1, 16);
        _recognition.Checked = profile.RecognitionEnabled;
        _model.SelectedItem = RecognitionModels.Get(profile.RecognitionModelId);
        _applyingProfile = false;
        UpdateModelState();
    }

    private void SaveCurrentProfile()
    {
        using var dialog = new ProfileNameDialog(
            (_profile.SelectedItem as ScanProfile)?.Name ?? "",
            AppTheme.IsDark);
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var profile = new ScanProfile
        {
            Name = dialog.ProfileName,
            SimilarityDistance = SimilarityDistance,
            RecognitionEnabled = RecognitionEnabled,
            RecognitionModelId = RecognitionModelId
        };
        var existing = _profiles.FindIndex(item =>
            item.Name.Equals(profile.Name, StringComparison.OrdinalIgnoreCase));
        if (existing >= 0)
        {
            _profiles[existing] = profile;
        }
        else
        {
            _profiles.Add(profile);
        }

        RefreshProfiles(profile.Name);
    }

    private void DeleteSelectedProfile()
    {
        if (_profile.SelectedItem is not ScanProfile profile)
        {
            return;
        }

        _profiles.RemoveAll(item =>
            item.Name.Equals(profile.Name, StringComparison.OrdinalIgnoreCase));
        RefreshProfiles(_profiles.FirstOrDefault()?.Name);
    }
}
