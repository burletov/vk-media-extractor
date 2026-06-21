using MediaExtractorForVK;
using System.Drawing;
using System.Text.Json;

var testRoot = Path.Combine(AppContext.BaseDirectory, "test-photos");
if (Directory.Exists(testRoot))
{
    Directory.Delete(testRoot, recursive: true);
}

Directory.CreateDirectory(testRoot);
var originalPath = Path.Combine(testRoot, "original.png");
var duplicatePath = Path.Combine(testRoot, "duplicate.png");
var similarPath = Path.Combine(testRoot, "similar.png");
var screenshotPath = Path.Combine(testRoot, "Screenshot_001.png");
var videoPath = Path.Combine(testRoot, "clip.mp4");
var duplicateVideoPath = Path.Combine(testRoot, "clip-copy.mp4");

CreateImage(originalPath, Color.CornflowerBlue, shift: 0);
File.Copy(originalPath, duplicatePath);
CreateImage(similarPath, Color.CornflowerBlue, shift: 2);
CreateSmallScreenshot(screenshotPath);
await File.WriteAllBytesAsync(videoPath, Enumerable.Range(0, 4096).Select(index => (byte)(index % 251)).ToArray());
File.Copy(videoPath, duplicateVideoPath);

var scanner = new ImageScanner();
var result = await scanner.ScanAsync(
    testRoot,
    similarityDistance: 10,
    enableRecognition: true,
    progress: null,
    CancellationToken.None);

Assert(
    result.Groups.Any(group =>
        group.Kind == FindingKind.ExactDuplicate &&
        group.Photos.Count == 2),
    "Exact duplicate group was not detected.");
Assert(
    result.Groups.Any(group =>
        group.Kind == FindingKind.ExactDuplicate &&
        group.Photos.All(photo => photo.MediaKind == MediaKind.Video)),
    "Exact duplicate videos were not detected.");
Assert(
    result.Groups.Any(group =>
        group.Kind == FindingKind.SimilarImage &&
        group.Photos.Any(photo => photo.FullPath == originalPath) &&
        group.Photos.Any(photo => photo.FullPath == similarPath)),
    "Similar image group was not detected.");
Assert(
    result.Groups.Any(group =>
        group.Kind == FindingKind.ReviewCandidate &&
        group.Photos.Any(photo => photo.FullPath == screenshotPath)),
    "Screenshot review candidate was not detected.");
Assert(
    result.Photos.Single(photo => photo.FullPath == originalPath).PrimaryCategory ==
    PhotoCategory.Unknown,
    "A weak synthetic image should remain unclassified.");
Assert(
    result.Photos.Single(photo => photo.FullPath == screenshotPath).PrimaryCategory ==
    PhotoCategory.Screenshot,
    "Screenshot filename evidence was ignored.");
Assert(
    result.Photos
        .Where(photo => photo.MediaKind == MediaKind.Video)
        .All(photo => photo.PrimaryCategory == PhotoCategory.Video),
    "Video files were not categorized as video.");
Assert(
    result.Photos
        .Where(photo =>
            photo.MediaKind == MediaKind.Image &&
            photo.DecodeError is null)
        .All(photo => Math.Abs(photo.CategoryScores.Values.Sum() - 1.0) < 0.0001),
    "Category probabilities do not sum to one.");

var reportPath = Path.Combine(testRoot, "analysis-report.csv");
await ScanReportExporter.ExportCsvAsync(
    reportPath,
    result,
    CancellationToken.None);
var reportText = await File.ReadAllTextAsync(reportPath);
Assert(
    reportText.Contains("Рекомендация", StringComparison.Ordinal) &&
    reportText.Contains(originalPath, StringComparison.OrdinalIgnoreCase),
    "CSV analysis report is incomplete.");

var cachedResult = await scanner.ScanAsync(
    testRoot,
    similarityDistance: 10,
    enableRecognition: true,
    progress: null,
    CancellationToken.None);
Assert(
    cachedResult.Photos.Count(photo => photo.RecognitionFromCache) == 4,
    "Recognition cache was not reused.");

var quarantine = new QuarantineService();
var moveResult = await quarantine.MoveAsync(
    testRoot,
    new[] { duplicatePath },
    CancellationToken.None);
Assert(moveResult.Moves.Count == 1, "Quarantine move failed.");
Assert(
    Path.GetFileName(moveResult.QuarantineRoot)
        .StartsWith(".MediaExtractorForVK_Quarantine_", StringComparison.Ordinal),
    "The quarantine folder still uses the legacy product name.");
Assert(!File.Exists(duplicatePath), "Original file still exists after quarantine move.");
Assert(File.Exists(moveResult.Moves[0].QuarantinePath), "Quarantined file is missing.");

var restoreResult = await quarantine.RestoreAsync(moveResult.Moves, CancellationToken.None);
Assert(restoreResult.Restored.Count == 1, "Quarantine restore failed.");
Assert(File.Exists(duplicatePath), "Restored file is missing.");

var organizer = new CategoryOrganizerService();
var organizeResult = await organizer.OrganizeAsync(
    testRoot,
    result.Photos,
    minimumConfidence: 0,
    includeUnknown: true,
    preserveOriginalFolders: true,
    CancellationToken.None);
Assert(organizeResult.Moves.Count == 6, "Category organization did not move every media file.");
Assert(
    organizeResult.Moves.All(move =>
        File.Exists(move.CategoryPath) &&
        move.CategoryPath.StartsWith(
            organizeResult.DestinationRoot,
            StringComparison.OrdinalIgnoreCase)),
    "An organized image is missing or outside the category root.");

var latestOrganization = await organizer.FindLatestOperationAsync(
    testRoot,
    CancellationToken.None);
Assert(latestOrganization.Count == 6, "Category operation journal was not found.");

var categoryRestoreResult = await organizer.RestoreAsync(
    organizeResult.Moves,
    CancellationToken.None);
Assert(categoryRestoreResult.Restored.Count == 6, "Category organization restore failed.");
Assert(
    new[]
    {
        originalPath,
        duplicatePath,
        similarPath,
        screenshotPath,
        videoPath,
        duplicateVideoPath
    }.All(File.Exists),
    "One or more media files were not restored to their original paths.");

var secondRoot = Path.Combine(AppContext.BaseDirectory, "test-photos-second");
if (Directory.Exists(secondRoot))
{
    Directory.Delete(secondRoot, recursive: true);
}

Directory.CreateDirectory(secondRoot);
var crossFolderDuplicate = Path.Combine(secondRoot, "cross-folder-copy.png");
File.Copy(originalPath, crossFolderDuplicate);
var multiFolderResult = await scanner.ScanAsync(
    new[] { testRoot, secondRoot },
    similarityDistance: 10,
    enableRecognition: false,
    progress: null,
    CancellationToken.None);
Assert(multiFolderResult.RootPaths.Count == 2, "Multiple scan roots were not retained.");
Assert(
    multiFolderResult.Groups.Any(group =>
        group.Kind == FindingKind.ExactDuplicate &&
        group.Photos.Any(photo => photo.FullPath == originalPath) &&
        group.Photos.Any(photo => photo.FullPath == crossFolderDuplicate)),
    "A duplicate across two selected folders was not detected.");

using var vkJson = JsonDocument.Parse(
    """
    {
      "attachments": [
        {
          "type": "photo",
          "photo": {
            "owner_id": 1,
            "id": 2,
            "sizes": [
              { "width": 100, "height": 100, "url": "https://example.com/s.jpg" },
              { "width": 1000, "height": 800, "url": "https://example.com/x.jpg" }
            ]
          }
        },
        {
          "type": "video",
          "video": { "owner_id": 3, "id": 4, "access_key": "key" }
        },
        {
          "type": "doc",
          "doc": {
            "owner_id": 7,
            "id": 8,
            "ext": "gif",
            "title": "meme.gif",
            "url": "https://example.com/meme.gif"
          }
        }
      ],
      "fwd_messages": [
        {
          "attachments": [
            {
              "type": "doc",
              "doc": {
                "owner_id": 5,
                "id": 6,
                "ext": "mp4",
                "title": "forwarded.mp4",
                "url": "https://example.com/f.mp4"
              }
            }
          ]
        }
      ]
    }
    """);
var vkDownloads = new List<VkMediaDownload>();
var vkVideos = new Dictionary<string, VkVideoReference>();
VkImportService.CollectMedia(
    vkJson.RootElement,
    new VkImportOptions
    {
        AccessToken = "test",
        DestinationPath = testRoot,
        DownloadPhotos = true,
        DownloadVideos = true,
        DownloadGifs = false
    },
    vkDownloads,
    vkVideos);
Assert(vkDownloads.Count == 2, "VK photo/document parser failed.");
Assert(vkVideos.Count == 1, "VK video parser failed.");
vkDownloads.Clear();
vkVideos.Clear();
VkImportService.CollectMedia(
    vkJson.RootElement,
    new VkImportOptions
    {
        AccessToken = "test",
        DestinationPath = testRoot,
        DownloadPhotos = false,
        DownloadVideos = false,
        DownloadGifs = true
    },
    vkDownloads,
    vkVideos);
Assert(
    vkDownloads.Count == 1 &&
    vkDownloads[0].FileName.Contains("meme", StringComparison.OrdinalIgnoreCase),
    "VK GIF parser should be controlled by the separate GIF option.");

var vkImportDestination = Path.Combine(testRoot, "vk-import");
Assert(
    VkImportService.TryParsePeerId("c42", out var chatPeerId) &&
    chatPeerId == 2_000_000_042L,
    "VK chat ID parser failed.");
Assert(
    VkImportService.TryParsePeerId(
        "https://vk.com/im?sel=12345",
        out var personalPeerId) &&
    personalPeerId == 12345,
    "VK dialog URL parser failed.");
var fakeVkHandler = new FakeVkHandler();
var vkService = new VkImportService(new HttpClient(fakeVkHandler));
var vkProgress = new List<VkImportProgress>();
var vkImportResult = await vkService.ImportAllConversationsAsync(
    new VkImportOptions
    {
        AccessToken = "test-token",
        DestinationPath = vkImportDestination,
        DownloadPhotos = true,
        DownloadVideos = false
    },
    new InlineProgress<VkImportProgress>(vkProgress.Add),
    CancellationToken.None);
Assert(vkImportResult.Conversations == 1, "VK conversations were not enumerated.");
Assert(vkImportResult.FoundFiles == 1, "VK media discovery count is incorrect.");
Assert(
    vkImportResult.Downloaded == 1,
    $"VK media was not downloaded: {string.Join(" | ", vkImportResult.Errors)}");
Assert(
    vkProgress.Any(value => value.Stage.StartsWith("Поиск вложений", StringComparison.Ordinal)) &&
    vkProgress.Any(value => value.Stage.StartsWith("Загрузка", StringComparison.Ordinal)) &&
    vkProgress.Any(value => value.BytesDownloaded > 0) &&
    vkProgress.Any(value => !string.IsNullOrWhiteSpace(value.LogMessage)) &&
    vkProgress[^1].FoundFiles == 1 &&
    vkProgress[^1].Remaining == TimeSpan.Zero,
    "VK progress phases or final counters are incorrect.");
Assert(
    Directory.GetFiles(vkImportDestination, "*.jpg", SearchOption.AllDirectories).Length == 1,
    "VK downloaded file is missing.");
var historyCallsAfterFirstRun = fakeVkHandler.HistoryCalls;
var resumedResult = await vkService.ImportAllConversationsAsync(
    new VkImportOptions
    {
        AccessToken = "test-token",
        DestinationPath = vkImportDestination,
        DownloadPhotos = true,
        DownloadVideos = false
    },
    progress: null,
    CancellationToken.None);
Assert(
    resumedResult.Downloaded == 1 &&
    fakeVkHandler.HistoryCalls == historyCallsAfterFirstRun,
    "VK checkpoint did not skip an already processed conversation.");
Assert(
    VkImportService.GetResumeInfo(
        vkImportDestination,
        downloadPhotos: true,
        downloadVideos: false,
        downloadGifs: false,
        peerId: null)?.CompletedFiles == 1,
    "VK resume summary is unavailable.");

var limitedImportDestination = Path.Combine(testRoot, "vk-import-limited");
var limitedVkService = new VkImportService(new HttpClient(new FakeVkHandler()));
var limitedResult = await limitedVkService.ImportAllConversationsAsync(
    new VkImportOptions
    {
        AccessToken = "test-token",
        DestinationPath = limitedImportDestination,
        DownloadPhotos = true,
        DownloadVideos = false,
        DownloadGifs = false,
        MaxFileSizeBytes = 1
    },
    progress: null,
    CancellationToken.None);
Assert(
    limitedResult.Downloaded == 0 &&
    limitedResult.Skipped == 1 &&
    limitedResult.Errors.Count == 0,
    "VK file size limit should skip oversized files without treating them as errors.");

var strongModel = RecognitionModels.Get(RecognitionModels.StrongId);
Assert(
    strongModel.DownloadUri is not null &&
    File.Exists(RecognitionModels.EmbeddingsPath(strongModel)),
    "The optional stronger recognition model is not configured correctly.");
var largeModel = RecognitionModels.Get(RecognitionModels.LargeId);
using (var largeEmbeddings = JsonDocument.Parse(
           await File.ReadAllTextAsync(RecognitionModels.EmbeddingsPath(largeModel))))
{
    Assert(
        largeModel.DownloadUri is not null &&
        largeEmbeddings.RootElement.GetProperty("dimensions").GetInt32() == 768,
        "The CLIP L/14 model or its embeddings are not configured correctly.");
}
Assert(
    RecognitionModels.All.All(model =>
        !model.DisplayName.Contains("Стандарт", StringComparison.OrdinalIgnoreCase) &&
        !model.DisplayName.Contains("Усил", StringComparison.OrdinalIgnoreCase)),
    "Recognition model names still contain legacy quality labels.");

var mergeParent = Path.Combine(AppContext.BaseDirectory, "merge-output");
var mergeSourceA = Path.Combine(AppContext.BaseDirectory, "merge-source-a");
var mergeSourceB = Path.Combine(AppContext.BaseDirectory, "merge-source-b");
if (Directory.Exists(mergeParent))
{
    Directory.Delete(mergeParent, recursive: true);
}
if (Directory.Exists(mergeSourceA))
{
    Directory.Delete(mergeSourceA, recursive: true);
}
if (Directory.Exists(mergeSourceB))
{
    Directory.Delete(mergeSourceB, recursive: true);
}

Directory.CreateDirectory(mergeParent);
Directory.CreateDirectory(mergeSourceA);
Directory.CreateDirectory(mergeSourceB);
var olderMergeFile = Path.Combine(mergeSourceA, "older.png");
var newerMergeFile = Path.Combine(mergeSourceB, "newer.png");
File.Copy(originalPath, olderMergeFile);
File.Copy(originalPath, newerMergeFile);
var olderDate = DateTime.UtcNow.AddDays(-2);
var newerDate = DateTime.UtcNow.AddDays(-1);
File.SetLastWriteTimeUtc(olderMergeFile, olderDate);
File.SetLastWriteTimeUtc(newerMergeFile, newerDate);
var mergeResult = await new FolderMergeService().CopyNewestFirstAsync(
    new[] { mergeSourceA, mergeSourceB },
    mergeParent,
    "Merged",
    progress: null,
    CancellationToken.None);
var mergedFiles = Directory.GetFiles(mergeResult.DestinationPath)
    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
    .ToArray();
Assert(mergeResult.Copied > 0, "Folder merge did not copy media files.");
Assert(
    Path.GetFileName(mergedFiles[0]).StartsWith("000001_", StringComparison.Ordinal) &&
    Path.GetFileName(mergedFiles[0]).Contains("newer.png", StringComparison.Ordinal) &&
    File.GetLastWriteTimeUtc(mergedFiles[0]) == newerDate,
    "Merged files are not prefixed in newest-first order.");

Console.WriteLine(
    $"PASS photos={result.Photos.Count} groups={result.Groups.Count} " +
    $"exact={result.Groups.Count(group => group.Kind == FindingKind.ExactDuplicate)} " +
    $"similar={result.Groups.Count(group => group.Kind == FindingKind.SimilarImage)} " +
    $"categories={string.Join(',', result.Photos.Select(photo => photo.PrimaryCategory))}");

static void CreateImage(string path, Color background, int shift)
{
    using var bitmap = new Bitmap(1000, 700);
    using var graphics = Graphics.FromImage(bitmap);
    graphics.Clear(background);
    using var brush = new SolidBrush(Color.Gold);
    graphics.FillEllipse(brush, 180 + shift, 120, 400, 350);
    using var pen = new Pen(Color.DarkSlateBlue, 20);
    graphics.DrawLine(pen, 50, 650 - shift, 950, 50 + shift);
    bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
}

static void CreateSmallScreenshot(string path)
{
    using var bitmap = new Bitmap(320, 480);
    using var graphics = Graphics.FromImage(bitmap);
    graphics.Clear(Color.White);
    using var brush = new SolidBrush(Color.Black);
    graphics.DrawString("test screenshot", SystemFonts.DefaultFont, brush, 20, 20);
    bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
}

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

internal sealed class FakeVkHandler : HttpMessageHandler
{
    public int HistoryCalls { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var path = request.RequestUri?.AbsolutePath ?? "";
        if (path.EndsWith("/messages.getConversations", StringComparison.Ordinal))
        {
            return Json(
                """
                {
                  "response": {
                    "count": 1,
                    "items": [
                      { "conversation": { "peer": { "id": 1, "type": "user" } } }
                    ],
                    "profiles": [
                      { "id": 1, "first_name": "Test", "last_name": "User" }
                    ],
                    "groups": []
                  }
                }
                """);
        }

        if (path.EndsWith("/messages.getHistory", StringComparison.Ordinal))
        {
            HistoryCalls++;
            return Json(
                """
                {
                  "response": {
                    "count": 1,
                    "items": [
                      {
                        "attachments": [
                          {
                            "type": "photo",
                            "photo": {
                              "owner_id": 1,
                              "id": 10,
                              "sizes": [
                                {
                                  "width": 800,
                                  "height": 600,
                                  "url": "https://media.test/photo.jpg"
                                }
                              ]
                            }
                          }
                        ]
                      }
                    ]
                  }
                }
                """);
        }

        if (request.RequestUri?.Host == "media.test")
        {
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new ByteArrayContent([1, 2, 3, 4])
            });
        }

        return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.NotFound));
    }

    private static Task<HttpResponseMessage> Json(string json) =>
        Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
        });
}

internal sealed class InlineProgress<T>(Action<T> report) : IProgress<T>
{
    public void Report(T value) => report(value);
}
