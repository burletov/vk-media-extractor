namespace MediaTidy;

internal static class CategoryNames
{
    public static string Russian(PhotoCategory category) => category switch
    {
        PhotoCategory.Photo => "Фото",
        PhotoCategory.Video => "Видео",
        PhotoCategory.Screenshot => "Скриншот",
        PhotoCategory.Meme => "Мем",
        PhotoCategory.Document => "Документ",
        PhotoCategory.Receipt => "Чек",
        PhotoCategory.Blurry => "Размытое",
        PhotoCategory.Graphic => "Графика",
        _ => "Не определено"
    };

    public static PhotoCategory ParseModelName(string name) => name switch
    {
        "photo" => PhotoCategory.Photo,
        "video" => PhotoCategory.Video,
        "screenshot" => PhotoCategory.Screenshot,
        "meme" => PhotoCategory.Meme,
        "document" => PhotoCategory.Document,
        "receipt" => PhotoCategory.Receipt,
        "blurry" => PhotoCategory.Blurry,
        "graphic" => PhotoCategory.Graphic,
        _ => PhotoCategory.Unknown
    };
}
