namespace MediaExtractorForVK;

internal sealed class ImagePreviewForm : Form
{
    public ImagePreviewForm(string path)
    {
        Text = Path.GetFileName(path);
        StartPosition = FormStartPosition.CenterParent;
        WindowState = FormWindowState.Maximized;
        BackColor = Color.FromArgb(24, 24, 24);
        KeyPreview = true;

        var picture = new PictureBox
        {
            Dock = DockStyle.Fill,
            BackColor = BackColor,
            SizeMode = PictureBoxSizeMode.Zoom
        };
        var hint = new Label
        {
            Dock = DockStyle.Top,
            Height = 34,
            ForeColor = Color.WhiteSmoke,
            BackColor = Color.FromArgb(40, 40, 40),
            TextAlign = ContentAlignment.MiddleCenter,
            Text = "Esc — закрыть"
        };
        Controls.Add(picture);
        Controls.Add(hint);

        try
        {
            using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            using var image = Image.FromStream(stream);
            var previewImage = new Bitmap(image);
            ImageScanner.ApplyExifOrientation(previewImage);
            picture.Image = previewImage;
        }
        catch
        {
            Text = "Не удалось открыть изображение";
        }

        KeyDown += (_, eventArgs) =>
        {
            if (eventArgs.KeyCode == Keys.Escape)
            {
                Close();
            }
        };
        FormClosed += (_, _) => picture.Image?.Dispose();
    }
}
