namespace MediaTidy;

internal sealed class ImageViewChangedEventArgs(
    double zoom,
    PointF center) : EventArgs
{
    public double Zoom { get; } = zoom;
    public PointF Center { get; } = center;
}

internal sealed class ZoomableImageControl : Control
{
    private Image? _image;
    private double _zoom = 1;
    private PointF _center = new(0.5F, 0.5F);
    private Point _lastMouse;
    private bool _dragging;
    private bool _suppressEvent;

    public ZoomableImageControl()
    {
        DoubleBuffered = true;
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);
        BackColor = Color.FromArgb(24, 24, 24);
        Cursor = Cursors.Hand;
    }

    public event EventHandler<ImageViewChangedEventArgs>? ViewChanged;

    public void LoadImage(string path)
    {
        using var source = Image.FromFile(path);
        _image?.Dispose();
        _image = new Bitmap(source);
        ResetView();
    }

    public void ResetView()
    {
        _zoom = 1;
        _center = new PointF(0.5F, 0.5F);
        Invalidate();
        RaiseViewChanged();
    }

    public void SetView(double zoom, PointF center)
    {
        _suppressEvent = true;
        _zoom = Math.Clamp(zoom, 1, 20);
        _center = ClampCenter(center);
        Invalidate();
        _suppressEvent = false;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.Clear(BackColor);
        if (_image is null || ClientSize.Width <= 0 || ClientSize.Height <= 0)
        {
            return;
        }

        e.Graphics.InterpolationMode =
            _zoom > 4
                ? System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor
                : System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        var fit = Math.Min(
            (double)ClientSize.Width / _image.Width,
            (double)ClientSize.Height / _image.Height);
        var width = _image.Width * fit * _zoom;
        var height = _image.Height * fit * _zoom;
        var x = ClientSize.Width / 2D - _center.X * width;
        var y = ClientSize.Height / 2D - _center.Y * height;
        e.Graphics.DrawImage(
            _image,
            new RectangleF((float)x, (float)y, (float)width, (float)height));
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        if (_image is null)
        {
            return;
        }

        var previous = _zoom;
        _zoom = Math.Clamp(
            _zoom * (e.Delta > 0 ? 1.2 : 1 / 1.2),
            1,
            20);
        if (Math.Abs(previous - _zoom) > 0.001)
        {
            Invalidate();
            RaiseViewChanged();
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left || _zoom <= 1)
        {
            return;
        }

        _dragging = true;
        _lastMouse = e.Location;
        Capture = true;
        Cursor = Cursors.SizeAll;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!_dragging || _image is null)
        {
            return;
        }

        var fit = Math.Min(
            (double)ClientSize.Width / _image.Width,
            (double)ClientSize.Height / _image.Height);
        var width = Math.Max(1, _image.Width * fit * _zoom);
        var height = Math.Max(1, _image.Height * fit * _zoom);
        _center = ClampCenter(new PointF(
            _center.X - (float)((e.X - _lastMouse.X) / width),
            _center.Y - (float)((e.Y - _lastMouse.Y) / height)));
        _lastMouse = e.Location;
        Invalidate();
        RaiseViewChanged();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button == MouseButtons.Left)
        {
            _dragging = false;
            Capture = false;
            Cursor = Cursors.Hand;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _image?.Dispose();
        }

        base.Dispose(disposing);
    }

    private void RaiseViewChanged()
    {
        if (!_suppressEvent)
        {
            ViewChanged?.Invoke(
                this,
                new ImageViewChangedEventArgs(_zoom, _center));
        }
    }

    private static PointF ClampCenter(PointF value) => new(
        Math.Clamp(value.X, 0F, 1F),
        Math.Clamp(value.Y, 0F, 1F));
}
