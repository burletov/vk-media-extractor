using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace MediaTidy;

internal sealed class VkImportOptions
{
    public required string AccessToken { get; init; }
    public required string DestinationPath { get; init; }
    public bool DownloadPhotos { get; init; } = true;
    public bool DownloadVideos { get; init; } = true;
    public bool DownloadGifs { get; init; }
    public long? PeerId { get; init; }
    public long? MaxFileSizeBytes { get; init; }
}

internal sealed class VkImportProgress
{
    public required string Stage { get; init; }
    public int Current { get; init; }
    public int Total { get; init; }
    public int Conversations { get; init; }
    public int FoundFiles { get; init; }
    public int Downloaded { get; init; }
    public int Skipped { get; init; }
    public int ErrorCount { get; init; }
    public TimeSpan Elapsed { get; init; }
    public TimeSpan? Remaining { get; init; }
    public string? ErrorMessage { get; init; }
    public string? LogMessage { get; init; }
    public string? CurrentFile { get; init; }
    public long BytesDownloaded { get; init; }
    public long? TotalBytes { get; init; }
    public bool IsIndeterminate { get; init; }
    public bool IsDownloading { get; init; }
    public DateTime LastActivityUtc { get; init; } = DateTime.UtcNow;
}

internal sealed class VkImportResult
{
    public required int Conversations { get; init; }
    public required int FoundFiles { get; init; }
    public required int Downloaded { get; init; }
    public required int Skipped { get; init; }
    public required IReadOnlyList<string> Errors { get; init; }
}

internal sealed class VkImportResumeInfo
{
    public required int FoundFiles { get; init; }
    public required int CompletedFiles { get; init; }
    public required int FailedFiles { get; init; }
    public required DateTime UpdatedUtc { get; init; }
}

internal sealed class VkImportService
{
    private const string ApiVersion = "5.199";
    private const int HistoryPageSize = 200;
    private const int VideoBatchSize = 100;
    private readonly HttpClient _httpClient;
    private readonly SemaphoreSlim _checkpointWriteLock = new(1, 1);

    public VkImportService(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient
        {
            Timeout = Timeout.InfiniteTimeSpan
        };
        _httpClient.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("MediaTidy", "2.6.2"));
    }

    public static VkImportResumeInfo? GetResumeInfo(
        string destinationPath,
        bool downloadPhotos,
        bool downloadVideos,
        bool downloadGifs,
        long? peerId)
    {
        try
        {
            var path = CheckpointPath(
                destinationPath,
                downloadPhotos,
                downloadVideos,
                downloadGifs,
                peerId);
            if (!File.Exists(path))
            {
                return null;
            }

            var checkpoint = JsonSerializer.Deserialize<VkImportCheckpoint>(
                File.ReadAllText(path));
            return checkpoint is null
                ? null
                : new VkImportResumeInfo
                {
                    FoundFiles = checkpoint.Downloads.Count,
                    CompletedFiles = checkpoint.Downloads.Count(item =>
                        item.Status == VkDownloadStatus.Completed),
                    FailedFiles = checkpoint.Downloads.Count(item =>
                        item.Status == VkDownloadStatus.Failed),
                    UpdatedUtc = checkpoint.UpdatedUtc
                };
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    public async Task<VkImportResult> ImportAllConversationsAsync(
        VkImportOptions options,
        IProgress<VkImportProgress>? progress,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(options.DestinationPath);
        var stopwatch = Stopwatch.StartNew();
        var errors = new ConcurrentQueue<string>();
        var checkpointPath = CheckpointPath(
            options.DestinationPath,
            options.DownloadPhotos,
            options.DownloadVideos,
            options.DownloadGifs,
            options.PeerId);
        var checkpoint = await LoadCheckpointAsync(checkpointPath, options, cancellationToken);
        ResetInterruptedDownloads(checkpoint, options.DestinationPath);

        progress?.Report(new VkImportProgress
        {
            Stage = options.PeerId.HasValue
                ? "Подготовка выбранного диалога"
                : "Подключение к VK и получение списка диалогов",
            IsIndeterminate = true,
            Elapsed = stopwatch.Elapsed,
            FoundFiles = checkpoint.Downloads.Count,
            Downloaded = CountCompleted(checkpoint, options.DestinationPath),
            ErrorCount = checkpoint.Downloads.Count(item => item.Status == VkDownloadStatus.Failed),
            LogMessage = checkpoint.Downloads.Count > 0
                ? $"Найдена контрольная точка: файлов {checkpoint.Downloads.Count:N0}. Продолжаем импорт."
                : "Устанавливаем соединение с VK."
        });

        var conversations = options.PeerId.HasValue
            ? [new VkConversation(options.PeerId.Value, $"Диалог {options.PeerId.Value}")]
            : await LoadConversationsAsync(
                options.AccessToken,
                progress,
                stopwatch,
                cancellationToken);

        for (var index = 0; index < conversations.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var conversation = conversations[index];
            if (checkpoint.CompletedPeerIds.Contains(conversation.PeerId))
            {
                progress?.Report(new VkImportProgress
                {
                    Stage = $"Диалог уже обработан: {conversation.Title}",
                    Current = index + 1,
                    Total = conversations.Count,
                    Conversations = conversations.Count,
                    FoundFiles = checkpoint.Downloads.Count,
                    Downloaded = CountCompleted(checkpoint, options.DestinationPath),
                    ErrorCount = checkpoint.Downloads.Count(item =>
                        item.Status == VkDownloadStatus.Failed),
                    Elapsed = stopwatch.Elapsed,
                    LogMessage = $"Пропускаем ранее обработанный диалог: {conversation.Title}"
                });
                continue;
            }

            var startOffset = checkpoint.HistoryOffsets.GetValueOrDefault(
                conversation.PeerId);
            progress?.Report(new VkImportProgress
            {
                Stage = $"Поиск вложений: {conversation.Title}",
                Current = index,
                Total = conversations.Count,
                Conversations = conversations.Count,
                FoundFiles = checkpoint.Downloads.Count,
                Downloaded = CountCompleted(checkpoint, options.DestinationPath),
                ErrorCount = errors.Count,
                Elapsed = stopwatch.Elapsed,
                LogMessage = startOffset > 0
                    ? $"Продолжаем историю {conversation.Title} с сообщения {startOffset:N0}."
                    : $"Читаем историю: {conversation.Title}"
            });

            try
            {
                await LoadConversationMediaAsync(
                    options.AccessToken,
                    conversation,
                    startOffset,
                    options,
                    errors,
                    async (nextOffset, media) =>
                    {
                        AddCheckpointDownloads(
                            checkpoint,
                            options.DestinationPath,
                            conversation,
                            media);
                        checkpoint.HistoryOffsets[conversation.PeerId] = nextOffset;
                        await SaveCheckpointAsync(
                            checkpointPath,
                            checkpoint,
                            cancellationToken);
                    },
                    (stage, log) => progress?.Report(new VkImportProgress
                    {
                        Stage = stage,
                        Current = index,
                        Total = conversations.Count,
                        Conversations = conversations.Count,
                        FoundFiles = checkpoint.Downloads.Count,
                        Downloaded = CountCompleted(checkpoint, options.DestinationPath),
                        ErrorCount = errors.Count,
                        Elapsed = stopwatch.Elapsed,
                        LogMessage = log
                    }),
                    cancellationToken);

                checkpoint.CompletedPeerIds.Add(conversation.PeerId);
                checkpoint.HistoryOffsets.Remove(conversation.PeerId);
                await SaveCheckpointAsync(checkpointPath, checkpoint, cancellationToken);
            }
            catch (Exception exception) when (
                exception is HttpRequestException or IOException or
                InvalidOperationException or JsonException)
            {
                var message = $"{conversation.Title}: {exception.Message}";
                errors.Enqueue(message);
                progress?.Report(new VkImportProgress
                {
                    Stage = $"Ошибка при чтении: {conversation.Title}",
                    Current = index + 1,
                    Total = conversations.Count,
                    Conversations = conversations.Count,
                    FoundFiles = checkpoint.Downloads.Count,
                    Downloaded = CountCompleted(checkpoint, options.DestinationPath),
                    ErrorCount = errors.Count,
                    Elapsed = stopwatch.Elapsed,
                    ErrorMessage = message
                });
            }
        }

        foreach (var item in checkpoint.Downloads.Where(item =>
                     item.Status is VkDownloadStatus.Failed or VkDownloadStatus.Downloading))
        {
            item.Status = VkDownloadStatus.Pending;
            item.Error = "";
        }

        var initialCompleted = CountCompleted(checkpoint, options.DestinationPath);
        var pending = checkpoint.Downloads
            .Where(item => item.Status is not VkDownloadStatus.Completed and
                not VkDownloadStatus.Skipped)
            .ToArray();
        var processed = 0;
        var skipped = CountSkipped(checkpoint);
        var downloadStopwatch = Stopwatch.StartNew();

        progress?.Report(new VkImportProgress
        {
            Stage = pending.Length == 0
                ? "Все найденные файлы уже загружены"
                : "Начинаем загрузку файлов",
            Current = initialCompleted,
            Total = checkpoint.Downloads.Count,
            Conversations = conversations.Count,
            FoundFiles = checkpoint.Downloads.Count,
            Downloaded = initialCompleted,
            ErrorCount = errors.Count,
            Elapsed = stopwatch.Elapsed,
            IsDownloading = pending.Length > 0,
            LogMessage = pending.Length == 0
                ? "Очередь загрузки пуста."
                : $"В очереди {pending.Length:N0} файлов. Незавершённые .part-файлы будут продолжены."
        });

        foreach (var item in pending)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var targetPath = Path.Combine(options.DestinationPath, item.RelativePath);
            try
            {
                if (File.Exists(targetPath))
                {
                    item.Status = VkDownloadStatus.Completed;
                    skipped++;
                }
                else
                {
                    item.Status = VkDownloadStatus.Downloading;
                    await SaveCheckpointAsync(checkpointPath, checkpoint, cancellationToken);
                    await DownloadWithRetriesAsync(
                        new Uri(item.Url),
                        targetPath,
                        (bytes, totalBytes) => ReportDownloadProgress(
                            progress,
                            item,
                            checkpoint,
                            options.DestinationPath,
                            conversations.Count,
                            stopwatch,
                            downloadStopwatch,
                            initialCompleted,
                            processed,
                            skipped,
                            errors.Count,
                            bytes,
                            totalBytes),
                        message => progress?.Report(new VkImportProgress
                        {
                            Stage = $"Повтор загрузки: {item.FileName}",
                            Current = initialCompleted + processed,
                            Total = checkpoint.Downloads.Count,
                            Conversations = conversations.Count,
                            FoundFiles = checkpoint.Downloads.Count,
                            Downloaded = CountCompleted(checkpoint, options.DestinationPath),
                            Skipped = skipped,
                            ErrorCount = errors.Count,
                            Elapsed = stopwatch.Elapsed,
                            CurrentFile = item.FileName,
                            IsDownloading = true,
                            LogMessage = message
                        }),
                        options.MaxFileSizeBytes,
                        cancellationToken);
                    item.Status = VkDownloadStatus.Completed;
                    item.Error = "";
                }
            }
            catch (VkDownloadSkippedException exception)
            {
                item.Status = VkDownloadStatus.Skipped;
                item.Error = exception.Message;
                skipped++;
                progress?.Report(new VkImportProgress
                {
                    Stage = $"Пропущено: {item.FileName}",
                    Current = initialCompleted + processed,
                    Total = checkpoint.Downloads.Count,
                    Conversations = conversations.Count,
                    FoundFiles = checkpoint.Downloads.Count,
                    Downloaded = CountCompleted(checkpoint, options.DestinationPath),
                    Skipped = skipped,
                    ErrorCount = errors.Count,
                    Elapsed = stopwatch.Elapsed,
                    CurrentFile = item.FileName,
                    IsDownloading = true,
                    LogMessage = $"{item.FileName}: {exception.Message}"
                });
            }
            catch (Exception exception) when (
                !cancellationToken.IsCancellationRequested &&
                exception is HttpRequestException or IOException or
                UnauthorizedAccessException or InvalidOperationException)
            {
                item.Status = VkDownloadStatus.Failed;
                item.Error = exception.Message;
                var message = $"{item.ConversationTitle}: {item.FileName}: {exception.Message}";
                errors.Enqueue(message);
                progress?.Report(new VkImportProgress
                {
                    Stage = $"Ошибка загрузки: {item.FileName}",
                    Current = initialCompleted + processed,
                    Total = checkpoint.Downloads.Count,
                    Conversations = conversations.Count,
                    FoundFiles = checkpoint.Downloads.Count,
                    Downloaded = CountCompleted(checkpoint, options.DestinationPath),
                    Skipped = skipped,
                    ErrorCount = errors.Count,
                    Elapsed = stopwatch.Elapsed,
                    CurrentFile = item.FileName,
                    IsDownloading = true,
                    ErrorMessage = message
                });
            }
            finally
            {
                processed++;
                await SaveCheckpointAsync(
                    checkpointPath,
                    checkpoint,
                    CancellationToken.None);
                progress?.Report(new VkImportProgress
                {
                    Stage = item.Status switch
                    {
                        VkDownloadStatus.Completed => $"Готово: {item.FileName}",
                        VkDownloadStatus.Skipped => $"Пропущено: {item.FileName}",
                        _ => $"Оставлено в очереди ошибок: {item.FileName}"
                    },
                    Current = Math.Min(
                        checkpoint.Downloads.Count,
                        initialCompleted + processed),
                    Total = checkpoint.Downloads.Count,
                    Conversations = conversations.Count,
                    FoundFiles = checkpoint.Downloads.Count,
                    Downloaded = CountCompleted(checkpoint, options.DestinationPath),
                    Skipped = skipped,
                    ErrorCount = errors.Count + checkpoint.Downloads.Count(download =>
                        download.Status == VkDownloadStatus.Failed),
                    Elapsed = stopwatch.Elapsed,
                    Remaining = EstimateRemaining(
                        downloadStopwatch.Elapsed,
                        processed,
                        pending.Length),
                    CurrentFile = item.FileName,
                    IsDownloading = true,
                    LogMessage = item.Status switch
                    {
                        VkDownloadStatus.Completed => $"Загружено: {item.FileName}",
                        VkDownloadStatus.Skipped => $"Пропущено: {item.FileName}",
                        _ => null
                    }
                });
            }
        }

        var failedMessages = checkpoint.Downloads
            .Where(item => item.Status == VkDownloadStatus.Failed)
            .Select(item =>
                $"{item.ConversationTitle}: {item.FileName}: {item.Error}")
            .Where(message => !string.IsNullOrWhiteSpace(message));
        foreach (var message in failedMessages)
        {
            errors.Enqueue(message);
        }

        var finalErrors = errors.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var finalDownloaded = CountCompleted(checkpoint, options.DestinationPath);
        progress?.Report(new VkImportProgress
        {
            Stage = finalErrors.Length == 0
                ? "Импорт завершён"
                : "Импорт завершён с очередью ошибок",
            Current = checkpoint.Downloads.Count,
            Total = checkpoint.Downloads.Count,
            Conversations = conversations.Count,
            FoundFiles = checkpoint.Downloads.Count,
            Downloaded = finalDownloaded,
            Skipped = skipped,
            ErrorCount = finalErrors.Length,
            Elapsed = stopwatch.Elapsed,
            Remaining = TimeSpan.Zero,
            IsDownloading = pending.Length > 0,
            LogMessage = finalErrors.Length == 0
                ? "Все доступные файлы обработаны."
                : "Неудачные загрузки сохранены и будут повторены при следующем запуске."
        });

        return new VkImportResult
        {
            Conversations = conversations.Count,
            FoundFiles = checkpoint.Downloads.Count,
            Downloaded = finalDownloaded,
            Skipped = skipped,
            Errors = finalErrors
        };
    }

    internal static bool TryParsePeerId(string? value, out long peerId)
    {
        peerId = 0;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var text = value.Trim();
        if (Uri.TryCreate(text, UriKind.Absolute, out var uri))
        {
            var query = uri.Query.TrimStart('?')
                .Split('&', StringSplitOptions.RemoveEmptyEntries)
                .Select(part => part.Split('=', 2))
                .Where(parts => parts.Length == 2)
                .ToDictionary(
                    parts => Uri.UnescapeDataString(parts[0]),
                    parts => Uri.UnescapeDataString(parts[1]),
                    StringComparer.OrdinalIgnoreCase);
            text = query.GetValueOrDefault("sel") ??
                   query.GetValueOrDefault("peer") ??
                   query.GetValueOrDefault("peer_id") ??
                   text;
        }

        if (text.StartsWith('c') &&
            long.TryParse(text.AsSpan(1), out var chatId) &&
            chatId > 0)
        {
            peerId = 2_000_000_000L + chatId;
            return true;
        }

        return long.TryParse(text, out peerId) && peerId != 0;
    }

    private static void ReportDownloadProgress(
        IProgress<VkImportProgress>? progress,
        VkCheckpointDownload item,
        VkImportCheckpoint checkpoint,
        string destinationPath,
        int conversationCount,
        Stopwatch stopwatch,
        Stopwatch downloadStopwatch,
        int initialCompleted,
        int processed,
        int skipped,
        int errorCount,
        long bytes,
        long? totalBytes)
    {
        progress?.Report(new VkImportProgress
        {
            Stage = $"Загрузка: {item.FileName}",
            Current = Math.Min(checkpoint.Downloads.Count, initialCompleted + processed),
            Total = checkpoint.Downloads.Count,
            Conversations = conversationCount,
            FoundFiles = checkpoint.Downloads.Count,
            Downloaded = CountCompleted(checkpoint, destinationPath),
            Skipped = skipped,
            ErrorCount = errorCount,
            Elapsed = stopwatch.Elapsed,
            Remaining = EstimateRemaining(
                downloadStopwatch.Elapsed,
                processed,
                Math.Max(1, checkpoint.Downloads.Count - initialCompleted)),
            CurrentFile = item.FileName,
            BytesDownloaded = bytes,
            TotalBytes = totalBytes,
            IsDownloading = true
        });
    }

    private async Task<List<VkConversation>> LoadConversationsAsync(
        string token,
        IProgress<VkImportProgress>? progress,
        Stopwatch stopwatch,
        CancellationToken cancellationToken)
    {
        var result = new List<VkConversation>();
        for (var offset = 0; ;)
        {
            using var document = await CallApiAsync(
                "messages.getConversations",
                token,
                new Dictionary<string, string>
                {
                    ["offset"] = offset.ToString(),
                    ["count"] = "200",
                    ["extended"] = "1"
                },
                cancellationToken);
            var response = document.RootElement.GetProperty("response");
            var profiles = ReadProfiles(response);
            var groups = ReadGroups(response);
            var items = response.GetProperty("items");

            foreach (var item in items.EnumerateArray())
            {
                var conversation = item.GetProperty("conversation");
                var peer = conversation.GetProperty("peer");
                result.Add(new VkConversation(
                    peer.GetProperty("id").GetInt64(),
                    ResolveConversationTitle(conversation, peer, profiles, groups)));
            }

            progress?.Report(new VkImportProgress
            {
                Stage = "Получение списка диалогов",
                Conversations = result.Count,
                Elapsed = stopwatch.Elapsed,
                IsIndeterminate = true,
                LogMessage = $"Получено диалогов: {result.Count:N0}"
            });

            if (items.GetArrayLength() < 200)
            {
                break;
            }

            offset += items.GetArrayLength();
        }

        return result.DistinctBy(item => item.PeerId).ToList();
    }

    private async Task LoadConversationMediaAsync(
        string token,
        VkConversation conversation,
        int startOffset,
        VkImportOptions options,
        ConcurrentQueue<string> errors,
        Func<int, IReadOnlyList<VkMediaDownload>, Task> pageCompleted,
        Action<string, string>? reportActivity,
        CancellationToken cancellationToken)
    {
        for (var offset = startOffset; ;)
        {
            using var document = await CallApiAsync(
                "messages.getHistory",
                token,
                new Dictionary<string, string>
                {
                    ["peer_id"] = conversation.PeerId.ToString(),
                    ["offset"] = offset.ToString(),
                    ["count"] = HistoryPageSize.ToString()
                },
                cancellationToken);
            var items = document.RootElement
                .GetProperty("response")
                .GetProperty("items");
            var pageDownloads = new List<VkMediaDownload>();
            var unresolvedVideos = new Dictionary<string, VkVideoReference>(
                StringComparer.OrdinalIgnoreCase);
            foreach (var message in items.EnumerateArray())
            {
                CollectMedia(message, options, pageDownloads, unresolvedVideos);
            }

            if (unresolvedVideos.Count > 0)
            {
                var resolved = await ResolveVideosAsync(
                    token,
                    unresolvedVideos.Values.ToArray(),
                    conversation.Title,
                    errors,
                    cancellationToken);
                pageDownloads.AddRange(resolved);
            }

            var nextOffset = offset + items.GetArrayLength();
            await pageCompleted(nextOffset, pageDownloads);
            reportActivity?.Invoke(
                $"Поиск вложений: {conversation.Title}",
                $"Обработано сообщений: {nextOffset:N0}; найдено на странице: {pageDownloads.Count:N0}");

            if (items.GetArrayLength() < HistoryPageSize)
            {
                break;
            }

            offset = nextOffset;
        }
    }

    internal static void CollectMedia(
        JsonElement node,
        VkImportOptions options,
        ICollection<VkMediaDownload> downloads,
        IDictionary<string, VkVideoReference> unresolvedVideos)
    {
        if (node.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        if (node.TryGetProperty("attachments", out var attachments) &&
            attachments.ValueKind == JsonValueKind.Array)
        {
            foreach (var attachment in attachments.EnumerateArray())
            {
                CollectAttachment(attachment, options, downloads, unresolvedVideos);
            }
        }

        if (node.TryGetProperty("fwd_messages", out var forwarded) &&
            forwarded.ValueKind == JsonValueKind.Array)
        {
            foreach (var message in forwarded.EnumerateArray())
            {
                CollectMedia(message, options, downloads, unresolvedVideos);
            }
        }

        if (node.TryGetProperty("reply_message", out var reply) &&
            reply.ValueKind == JsonValueKind.Object)
        {
            CollectMedia(reply, options, downloads, unresolvedVideos);
        }
    }

    private static void CollectAttachment(
        JsonElement attachment,
        VkImportOptions options,
        ICollection<VkMediaDownload> downloads,
        IDictionary<string, VkVideoReference> unresolvedVideos)
    {
        var type = attachment.TryGetProperty("type", out var typeElement)
            ? typeElement.GetString()
            : null;
        if (type == "photo" && options.DownloadPhotos &&
            attachment.TryGetProperty("photo", out var photo))
        {
            var sizes = photo.GetProperty("sizes")
                .EnumerateArray()
                .Where(size => size.TryGetProperty("url", out _))
                .OrderByDescending(size =>
                    size.GetProperty("width").GetInt32() *
                    (long)size.GetProperty("height").GetInt32())
                .ToArray();
            if (sizes.Length > 0 &&
                Uri.TryCreate(
                    sizes[0].GetProperty("url").GetString(),
                    UriKind.Absolute,
                    out var photoUri))
            {
                var ownerId = photo.GetProperty("owner_id").GetInt64();
                var id = photo.GetProperty("id").GetInt64();
                var extension = ExtensionFromUri(photoUri, ".jpg");
                downloads.Add(new VkMediaDownload(
                    $"photo:{ownerId}:{id}",
                    photoUri,
                    $"photo_{ownerId}_{id}{extension}",
                    $"photo_{id}.jpg"));
            }
        }
        else if (type == "video" && options.DownloadVideos &&
                 attachment.TryGetProperty("video", out var video))
        {
            var reference = ReadVideoReference(video);
            unresolvedVideos[reference.Identity] = reference;
        }
        else if (type == "doc" &&
                 attachment.TryGetProperty("doc", out var document) &&
                 document.TryGetProperty("url", out var documentUrl))
        {
            var url = documentUrl.GetString();
            var extension = document.TryGetProperty("ext", out var extensionElement)
                ? $".{extensionElement.GetString()?.TrimStart('.')}"
                : Path.GetExtension(url);
            var isVideo = IsVideoExtension(extension);
            var isImage = IsImageExtension(extension);
            if (((isVideo && options.DownloadVideos) ||
                (isImage && !IsGifExtension(extension) && options.DownloadPhotos) ||
                (IsGifExtension(extension) && options.DownloadGifs)) &&
                Uri.TryCreate(url, UriKind.Absolute, out var documentUri))
            {
                var ownerId = document.GetProperty("owner_id").GetInt64();
                var id = document.GetProperty("id").GetInt64();
                var title = document.TryGetProperty("title", out var titleElement)
                    ? titleElement.GetString()
                    : null;
                var documentName = string.IsNullOrWhiteSpace(title)
                    ? $"document_{ownerId}_{id}{extension}"
                    : $"{Path.GetFileNameWithoutExtension(title)}_{ownerId}_{id}{extension}";
                downloads.Add(new VkMediaDownload(
                    $"doc:{ownerId}:{id}",
                    documentUri,
                    documentName,
                    $"document_{ownerId}_{id}{extension}"));
            }
        }

        CollectMedia(attachment, options, downloads, unresolvedVideos);
    }

    private async Task<IReadOnlyList<VkMediaDownload>> ResolveVideosAsync(
        string token,
        IReadOnlyList<VkVideoReference> videos,
        string conversationTitle,
        ConcurrentQueue<string> errors,
        CancellationToken cancellationToken)
    {
        var result = new List<VkMediaDownload>();
        foreach (var batch in videos.Chunk(VideoBatchSize))
        {
            using var document = await CallApiAsync(
                "video.get",
                token,
                new Dictionary<string, string>
                {
                    ["videos"] = string.Join(',', batch.Select(video => video.ApiId))
                },
                cancellationToken);
            var items = document.RootElement
                .GetProperty("response")
                .GetProperty("items");
            var resolvedIdentities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in items.EnumerateArray())
            {
                var ownerId = item.GetProperty("owner_id").GetInt64();
                var id = item.GetProperty("id").GetInt64();
                var identity = $"video:{ownerId}:{id}";
                if (TryCreateVideoDownload(item, identity, ownerId, id, out var download))
                {
                    result.Add(download);
                    resolvedIdentities.Add(identity);
                }
            }

            foreach (var unresolved in batch.Where(video =>
                         !resolvedIdentities.Contains(video.Identity)))
            {
                errors.Enqueue(
                    $"{conversationTitle}: видео {unresolved.OwnerId}_{unresolved.Id} " +
                    "не содержит доступной MP4-ссылки");
            }
        }

        return result;
    }

    private static bool TryCreateVideoDownload(
        JsonElement item,
        string identity,
        long ownerId,
        long id,
        out VkMediaDownload download)
    {
        download = null!;
        if (!item.TryGetProperty("files", out var files))
        {
            return false;
        }

        var candidate = files.EnumerateObject()
            .Where(property =>
                property.Name.StartsWith("mp4_", StringComparison.OrdinalIgnoreCase) &&
                property.Value.ValueKind == JsonValueKind.String)
            .OrderByDescending(property => ParseQuality(property.Name))
            .FirstOrDefault();
        if (candidate.Value.ValueKind != JsonValueKind.String ||
            !Uri.TryCreate(candidate.Value.GetString(), UriKind.Absolute, out var uri))
        {
            return false;
        }

        download = new VkMediaDownload(
            identity,
            uri,
            $"video_{ownerId}_{id}.mp4",
            $"video_{id}.mp4");
        return true;
    }

    private async Task<JsonDocument> CallApiAsync(
        string method,
        string token,
        IReadOnlyDictionary<string, string> parameters,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(45));
        var form = parameters
            .Append(new KeyValuePair<string, string>("access_token", token))
            .Append(new KeyValuePair<string, string>("v", ApiVersion));
        using var content = new FormUrlEncodedContent(form);
        HttpResponseMessage response;
        try
        {
            response = await _httpClient.PostAsync(
                $"https://api.vk.com/method/{method}",
                content,
                timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new HttpRequestException(
                $"VK API не ответил за 45 секунд ({method}).");
        }

        using (response)
        {
            response.EnsureSuccessStatusCode();
            var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
            var document = await JsonDocument.ParseAsync(
                stream,
                cancellationToken: timeout.Token);
            if (document.RootElement.TryGetProperty("error", out var error))
            {
                var code = error.TryGetProperty("error_code", out var codeElement)
                    ? codeElement.GetInt32()
                    : 0;
                var message = error.TryGetProperty("error_msg", out var messageElement)
                    ? messageElement.GetString()
                    : "Неизвестная ошибка VK API";
                document.Dispose();
                throw new InvalidOperationException($"VK API {code}: {message}");
            }

            await Task.Delay(340, cancellationToken);
            return document;
        }
    }

    private async Task DownloadWithRetriesAsync(
        Uri url,
        string targetPath,
        Action<long, long?>? reportBytes,
        Action<string>? reportStatus,
        long? maxFileSizeBytes,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        Exception? lastError = null;
        for (var attempt = 1; attempt <= 4; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await DownloadOnceAsync(
                    url,
                    targetPath,
                    reportBytes,
                    maxFileSizeBytes,
                    cancellationToken);
                return;
            }
            catch (VkDownloadSkippedException)
            {
                TryDeletePartial(targetPath);
                throw;
            }
            catch (Exception exception) when (
                !cancellationToken.IsCancellationRequested &&
                exception is HttpRequestException or IOException or TaskCanceledException)
            {
                lastError = exception;
                if (attempt < 4)
                {
                    reportStatus?.Invoke(
                        $"Попытка {attempt} прервана: {exception.Message} " +
                        "Повторяем с сохранённой позиции.");
                    await Task.Delay(TimeSpan.FromSeconds(attempt * 2), cancellationToken);
                }
            }
        }

        throw new IOException(
            "Загрузка не удалась после четырёх попыток. " +
            (lastError?.Message ?? "Неизвестная сетевая ошибка."),
            lastError);
    }

    private async Task DownloadOnceAsync(
        Uri url,
        string targetPath,
        Action<long, long?>? reportBytes,
        long? maxFileSizeBytes,
        CancellationToken cancellationToken)
    {
        var temporaryPath = targetPath + ".part";
        var existingBytes = File.Exists(temporaryPath)
            ? new FileInfo(temporaryPath).Length
            : 0;
        if (maxFileSizeBytes is > 0 && existingBytes > maxFileSizeBytes.Value)
        {
            throw new VkDownloadSkippedException(
                $"размер превышает лимит {FormatBytes(maxFileSizeBytes.Value)}");
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (existingBytes > 0)
        {
            request.Headers.Range = new RangeHeaderValue(existingBytes, null);
        }

        using var headerTimeout =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        headerTimeout.CancelAfter(TimeSpan.FromSeconds(45));
        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            headerTimeout.Token);
        if (response.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable)
        {
            File.Delete(temporaryPath);
            throw new IOException("Сервер отклонил продолжение файла; загрузка начнётся заново.");
        }

        response.EnsureSuccessStatusCode();
        headerTimeout.CancelAfter(Timeout.InfiniteTimeSpan);
        var append = existingBytes > 0 && response.StatusCode == HttpStatusCode.PartialContent;
        if (!append)
        {
            existingBytes = 0;
        }

        long? totalBytes = response.Content.Headers.ContentLength is { } contentLength
            ? existingBytes + contentLength
            : null;
        if (maxFileSizeBytes is > 0 &&
            totalBytes is > 0 &&
            totalBytes.Value > maxFileSizeBytes.Value)
        {
            throw new VkDownloadSkippedException(
                $"размер {FormatBytes(totalBytes.Value)} превышает лимит " +
                $"{FormatBytes(maxFileSizeBytes.Value)}");
        }

        {
            await using var source =
                await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var destination = new FileStream(
                temporaryPath,
                append ? FileMode.Append : FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                1024 * 1024,
                useAsync: true);
            var buffer = new byte[1024 * 1024];
            var written = existingBytes;
            reportBytes?.Invoke(written, totalBytes);
            while (true)
            {
                using var idleTimeout =
                    CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                idleTimeout.CancelAfter(TimeSpan.FromSeconds(60));
                int read;
                try
                {
                    read = await source.ReadAsync(buffer, idleTimeout.Token);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    throw new IOException(
                        "Сервер не передавал данные 60 секунд. Загрузка будет продолжена повторно.");
                }

                if (read == 0)
                {
                    break;
                }

                await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                written += read;
                if (maxFileSizeBytes is > 0 && written > maxFileSizeBytes.Value)
                {
                    throw new VkDownloadSkippedException(
                        $"размер превышает лимит {FormatBytes(maxFileSizeBytes.Value)}");
                }

                reportBytes?.Invoke(written, totalBytes);
            }

            await destination.FlushAsync(cancellationToken);
        }

        File.Move(temporaryPath, targetPath, overwrite: true);
    }

    private static void AddCheckpointDownloads(
        VkImportCheckpoint checkpoint,
        string destinationPath,
        VkConversation conversation,
        IReadOnlyList<VkMediaDownload> media)
    {
        var existing = checkpoint.Downloads
            .Select(item => item.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var chatDirectory = SanitizeFileName(
            $"{conversation.Title} [{conversation.PeerId}]",
            $"Диалог {conversation.PeerId}");
        foreach (var item in media)
        {
            var key = $"{conversation.PeerId}|{item.Identity}";
            if (!existing.Add(key))
            {
                continue;
            }

            var fileName = SanitizeFileName(item.FileName, item.FallbackFileName);
            var relativePath = Path.Combine(chatDirectory, fileName);
            checkpoint.Downloads.Add(new VkCheckpointDownload
            {
                Key = key,
                ConversationTitle = conversation.Title,
                Url = item.Url.ToString(),
                FileName = fileName,
                RelativePath = relativePath,
                Status = File.Exists(Path.Combine(destinationPath, relativePath))
                    ? VkDownloadStatus.Completed
                    : VkDownloadStatus.Pending
            });
        }
    }

    private static async Task<VkImportCheckpoint> LoadCheckpointAsync(
        string path,
        VkImportOptions options,
        CancellationToken cancellationToken)
    {
        try
        {
            if (File.Exists(path))
            {
                var json = await File.ReadAllTextAsync(path, cancellationToken);
                var checkpoint = JsonSerializer.Deserialize<VkImportCheckpoint>(json);
                if (checkpoint is not null)
                {
                    return checkpoint;
                }
            }
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or JsonException)
        {
            // A corrupt state file should not block a fresh import.
        }

        return new VkImportCheckpoint
        {
            DownloadPhotos = options.DownloadPhotos,
            DownloadVideos = options.DownloadVideos,
            DownloadGifs = options.DownloadGifs,
            PeerId = options.PeerId
        };
    }

    private async Task SaveCheckpointAsync(
        string path,
        VkImportCheckpoint checkpoint,
        CancellationToken cancellationToken)
    {
        await _checkpointWriteLock.WaitAsync(cancellationToken);
        try
        {
            checkpoint.UpdatedUtc = DateTime.UtcNow;
            var temporaryPath = path + ".tmp";
            await File.WriteAllTextAsync(
                temporaryPath,
                JsonSerializer.Serialize(
                    checkpoint,
                    new JsonSerializerOptions { WriteIndented = true }),
                cancellationToken);
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            _checkpointWriteLock.Release();
        }
    }

    private static void ResetInterruptedDownloads(
        VkImportCheckpoint checkpoint,
        string destinationPath)
    {
        foreach (var item in checkpoint.Downloads)
        {
            var targetPath = Path.Combine(destinationPath, item.RelativePath);
            if (item.Status == VkDownloadStatus.Completed && !File.Exists(targetPath))
            {
                item.Status = VkDownloadStatus.Pending;
            }
            else if (item.Status == VkDownloadStatus.Downloading)
            {
                item.Status = VkDownloadStatus.Pending;
            }
        }
    }

    private static int CountCompleted(
        VkImportCheckpoint checkpoint,
        string destinationPath) =>
        checkpoint.Downloads.Count(item =>
            item.Status == VkDownloadStatus.Completed);

    private static int CountSkipped(VkImportCheckpoint checkpoint) =>
        checkpoint.Downloads.Count(item => item.Status == VkDownloadStatus.Skipped);

    private static TimeSpan? EstimateRemaining(
        TimeSpan elapsed,
        int completed,
        int total)
    {
        if (completed <= 0 || total <= completed)
        {
            return total <= completed ? TimeSpan.Zero : null;
        }

        return TimeSpan.FromSeconds(
            Math.Max(0, elapsed.TotalSeconds / completed * (total - completed)));
    }

    private static string CheckpointPath(
        string destinationPath,
        bool photos,
        bool videos,
        bool gifs,
        long? peerId)
    {
        var scope = peerId.HasValue
            ? $"peer-{peerId.Value}".Replace('-', 'm')
            : "all";
        var media = $"{(photos ? "p" : "")}{(videos ? "v" : "")}{(gifs ? "g" : "")}";
        return Path.Combine(
            destinationPath,
            $".MediaTidy_VK_Import_{scope}_{media}.json");
    }

    private static Dictionary<long, string> ReadProfiles(JsonElement response) =>
        response.TryGetProperty("profiles", out var profiles)
            ? profiles.EnumerateArray().ToDictionary(
                profile => profile.GetProperty("id").GetInt64(),
                profile =>
                    $"{profile.GetProperty("first_name").GetString()} " +
                    $"{profile.GetProperty("last_name").GetString()}".Trim())
            : [];

    private static Dictionary<long, string> ReadGroups(JsonElement response) =>
        response.TryGetProperty("groups", out var groups)
            ? groups.EnumerateArray().ToDictionary(
                group => group.GetProperty("id").GetInt64(),
                group => group.GetProperty("name").GetString() ?? "Сообщество")
            : [];

    private static string ResolveConversationTitle(
        JsonElement conversation,
        JsonElement peer,
        IReadOnlyDictionary<long, string> profiles,
        IReadOnlyDictionary<long, string> groups)
    {
        if (conversation.TryGetProperty("chat_settings", out var chatSettings) &&
            chatSettings.TryGetProperty("title", out var titleElement) &&
            !string.IsNullOrWhiteSpace(titleElement.GetString()))
        {
            return titleElement.GetString()!;
        }

        var peerId = peer.GetProperty("id").GetInt64();
        var peerType = peer.TryGetProperty("type", out var typeElement)
            ? typeElement.GetString()
            : "";
        if (peerType == "user" && profiles.TryGetValue(peerId, out var profile))
        {
            return profile;
        }

        if (peerType == "group" && groups.TryGetValue(Math.Abs(peerId), out var group))
        {
            return group;
        }

        return $"Диалог {peerId}";
    }

    private static VkVideoReference ReadVideoReference(JsonElement video)
    {
        var ownerId = video.GetProperty("owner_id").GetInt64();
        var id = video.GetProperty("id").GetInt64();
        var accessKey = video.TryGetProperty("access_key", out var accessKeyElement)
            ? accessKeyElement.GetString()
            : null;
        return new VkVideoReference(ownerId, id, accessKey);
    }

    private static int ParseQuality(string name) =>
        int.TryParse(name.AsSpan("mp4_".Length), out var quality) ? quality : 0;

    private static string ExtensionFromUri(Uri uri, string fallback)
    {
        var extension = Path.GetExtension(uri.AbsolutePath);
        return string.IsNullOrWhiteSpace(extension) || extension.Length > 8
            ? fallback
            : extension;
    }

    private static bool IsVideoExtension(string? extension) =>
        extension?.ToLowerInvariant() is
            ".mp4" or ".mov" or ".m4v" or ".avi" or ".mkv" or ".webm" or ".wmv";

    private static bool IsImageExtension(string? extension) =>
        extension?.ToLowerInvariant() is
            ".jpg" or ".jpeg" or ".png" or ".gif" or ".webp" or ".bmp" or
            ".tif" or ".tiff";

    private static bool IsGifExtension(string? extension) =>
        string.Equals(extension, ".gif", StringComparison.OrdinalIgnoreCase);

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

    private static void TryDeletePartial(string targetPath)
    {
        try
        {
            File.Delete(targetPath + ".part");
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            // A leftover partial file is harmless and can be removed by the user later.
        }
    }

    private static string SanitizeFileName(string? value, string fallback)
    {
        var result = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        foreach (var invalidCharacter in Path.GetInvalidFileNameChars())
        {
            result = result.Replace(invalidCharacter, '_');
        }

        result = result.Trim().TrimEnd('.');
        return string.IsNullOrWhiteSpace(result)
            ? fallback
            : result.Length <= 120 ? result : result[..120];
    }
}

internal sealed class VkImportCheckpoint
{
    public int Version { get; set; } = 1;
    public bool DownloadPhotos { get; set; }
    public bool DownloadVideos { get; set; }
    public bool DownloadGifs { get; set; }
    public long? PeerId { get; set; }
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
    public HashSet<long> CompletedPeerIds { get; set; } = [];
    public Dictionary<long, int> HistoryOffsets { get; set; } = [];
    public List<VkCheckpointDownload> Downloads { get; set; } = [];
}

internal sealed class VkCheckpointDownload
{
    public string Key { get; set; } = "";
    public string ConversationTitle { get; set; } = "";
    public string Url { get; set; } = "";
    public string FileName { get; set; } = "";
    public string RelativePath { get; set; } = "";
    public VkDownloadStatus Status { get; set; }
    public string Error { get; set; } = "";
}

internal enum VkDownloadStatus
{
    Pending,
    Downloading,
    Completed,
    Failed,
    Skipped
}

internal sealed class VkDownloadSkippedException(string message) : Exception(message);

internal sealed record VkConversation(long PeerId, string Title);

internal sealed record VkVideoReference(long OwnerId, long Id, string? AccessKey)
{
    public string Identity => $"video:{OwnerId}:{Id}";
    public string ApiId => string.IsNullOrWhiteSpace(AccessKey)
        ? $"{OwnerId}_{Id}"
        : $"{OwnerId}_{Id}_{AccessKey}";
}

internal sealed record VkMediaDownload(
    string Identity,
    Uri Url,
    string FileName,
    string FallbackFileName);
