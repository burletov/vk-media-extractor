using System.Globalization;
using System.Text;

namespace MediaTidy;

internal static class ScanReportExporter
{
    public static async Task ExportCsvAsync(
        string path,
        ScanResult result,
        CancellationToken cancellationToken)
    {
        var groupMembership = result.Groups
            .SelectMany(group => group.Photos.Select(photo => (group, photo)))
            .GroupBy(item => item.photo.FullPath, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.Select(item => item.group).ToArray(),
                StringComparer.OrdinalIgnoreCase);
        var builder = new StringBuilder();
        builder.AppendLine(
            "Файл;Дата;Источник даты;Тип;Категория;Уверенность;Слов OCR;" +
            "Ширина;Высота;Размер, байт;Резкость;Лиц;Закрытых глаз;EXIF камеры;Группы;Рекомендация");

        foreach (var photo in result.Photos.OrderByDescending(photo => photo.CapturedAtUtc))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var groups = groupMembership.GetValueOrDefault(photo.FullPath) ?? [];
            var groupText = string.Join(
                ", ",
                groups.Select(group =>
                    $"{GroupName(group.Kind)} #{group.Id}: {group.Reason}"));
            var recommended = groups.Any(group =>
                ReferenceEquals(group.RecommendedKeep, photo))
                ? "оставить как лучший экземпляр"
                : groups.Any(group => group.Kind is FindingKind.ExactDuplicate or FindingKind.SimilarImage)
                    ? "проверить перед перемещением"
                    : "";

            AppendRow(
                builder,
                photo.FullPath,
                photo.CapturedAtUtc.ToLocalTime().ToString(
                    "yyyy-MM-dd HH:mm:ss",
                    CultureInfo.InvariantCulture),
                photo.DateSource,
                photo.MediaKind == MediaKind.Video ? "Видео" : "Изображение",
                CategoryNames.Russian(photo.PrimaryCategory),
                photo.PrimaryCategory == PhotoCategory.Unknown
                    ? ""
                    : photo.CategoryConfidence.ToString("P1", CultureInfo.InvariantCulture),
                photo.OcrWordCount.ToString(CultureInfo.InvariantCulture),
                photo.Width.ToString(CultureInfo.InvariantCulture),
                photo.Height.ToString(CultureInfo.InvariantCulture),
                photo.Size.ToString(CultureInfo.InvariantCulture),
                photo.SharpnessScore.ToString("F2", CultureInfo.InvariantCulture),
                photo.FaceAnalysisAvailable
                    ? photo.FaceCount.ToString(CultureInfo.InvariantCulture)
                    : "",
                photo.ClosedEyeCount >= 0
                    ? photo.ClosedEyeCount.ToString(CultureInfo.InvariantCulture)
                    : "не анализировались",
                photo.HasCameraMetadata ? "да" : "нет",
                groupText,
                recommended);
        }

        await File.WriteAllTextAsync(
            path,
            builder.ToString(),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: true),
            cancellationToken);
    }

    private static string GroupName(FindingKind kind) => kind switch
    {
        FindingKind.ExactDuplicate => "точные дубликаты",
        FindingKind.SimilarImage => "похожие файлы",
        FindingKind.ReviewCandidate => "ручная проверка",
        _ => "категория"
    };

    private static void AppendRow(StringBuilder builder, params string[] values)
    {
        builder.AppendLine(string.Join(';', values.Select(Escape)));
    }

    private static string Escape(string value) =>
        $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
}
