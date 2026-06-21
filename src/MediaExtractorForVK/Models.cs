using System.ComponentModel;

namespace MediaExtractorForVK;

internal enum FindingKind
{
    ClassifiedImage,
    ExactDuplicate,
    SimilarImage,
    ReviewCandidate
}

internal enum MediaKind
{
    Image,
    Video
}

internal enum PhotoCategory
{
    Photo,
    Video,
    Screenshot,
    Meme,
    Document,
    Receipt,
    Blurry,
    Graphic,
    Unknown
}

internal sealed class PhotoRecord
{
    public required string FullPath { get; set; }
    public required long Size { get; init; }
    public required DateTime LastWriteTimeUtc { get; init; }
    public MediaKind MediaKind { get; init; }
    public DateTime CapturedAtUtc { get; set; }
    public string DateSource { get; set; } = "Файл";
    public int Width { get; set; }
    public int Height { get; set; }
    public TimeSpan Duration { get; set; }
    public ulong? DifferenceHash { get; set; }
    public string? Sha256 { get; set; }
    public bool HasCameraMetadata { get; set; }
    public double DetailScore { get; set; }
    public double SharpnessScore { get; set; }
    public int FaceCount { get; set; }
    public int ClosedEyeCount { get; set; } = -1;
    public bool FaceAnalysisAvailable { get; set; }
    public int OcrWordCount { get; set; }
    public int OcrTextLength { get; set; }
    public double OcrTextAreaRatio { get; set; }
    public string OcrPreview { get; set; } = "";
    public PhotoCategory PrimaryCategory { get; set; } = PhotoCategory.Unknown;
    public double CategoryConfidence { get; set; }
    public IReadOnlyDictionary<PhotoCategory, double> CategoryScores { get; set; } =
        new Dictionary<PhotoCategory, double>();
    public bool RecognitionFromCache { get; set; }
    public string? RecognitionError { get; set; }
    public string? DecodeError { get; set; }

    public long PixelCount => (long)Width * Height;
    public double AspectRatio => Height == 0 ? 0 : (double)Width / Height;
}

internal sealed class FindingGroup
{
    public required int Id { get; init; }
    public required FindingKind Kind { get; init; }
    public required string Reason { get; init; }
    public required IReadOnlyList<PhotoRecord> Photos { get; init; }
    public required PhotoRecord RecommendedKeep { get; init; }
}

internal sealed class ScanResult
{
    public required string RootPath { get; init; }
    public required IReadOnlyList<string> RootPaths { get; init; }
    public required IReadOnlyList<PhotoRecord> Photos { get; init; }
    public required IReadOnlyList<FindingGroup> Groups { get; init; }
    public required int FailedToDecode { get; init; }
    public required bool RecognitionEnabled { get; init; }
    public required string RecognitionStatus { get; init; }
}

internal sealed class ScanProgress
{
    public required string Stage { get; init; }
    public required int Current { get; init; }
    public required int Total { get; init; }
}

internal sealed class FindingRow : INotifyPropertyChanged
{
    private bool _selected;

    public bool Selected
    {
        get => _selected;
        set
        {
            if (_selected == value)
            {
                return;
            }

            _selected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Selected)));
        }
    }

    public required int GroupId { get; init; }
    public required FindingKind Kind { get; init; }
    public required string Type { get; init; }
    public required string FileName { get; init; }
    public required string Folder { get; init; }
    public required string Size { get; init; }
    public required long SizeBytes { get; init; }
    public required string Resolution { get; init; }
    public required long PixelCount { get; init; }
    public required string Date { get; init; }
    public required DateTime SortDateUtc { get; init; }
    public required string Category { get; init; }
    public required PhotoCategory CategoryValue { get; init; }
    public required string Confidence { get; init; }
    public required double ConfidenceValue { get; init; }
    public required string OcrWords { get; init; }
    public required int OcrWordCount { get; init; }
    public required string Reason { get; init; }
    public required bool RecommendedKeep { get; init; }
    public required PhotoRecord Photo { get; init; }

    public event PropertyChangedEventHandler? PropertyChanged;
}

internal sealed class QuarantineMove
{
    public required string OriginalPath { get; init; }
    public required string QuarantinePath { get; init; }
}

internal sealed class CategoryMove
{
    public required string OriginalPath { get; init; }
    public required string CategoryPath { get; init; }
    public required PhotoCategory Category { get; init; }
}

internal sealed class OcrAnalysis
{
    public static readonly OcrAnalysis Empty = new();

    public int WordCount { get; init; }
    public int TextLength { get; init; }
    public double TextAreaRatio { get; init; }
    public string Preview { get; init; } = "";
}
