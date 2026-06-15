namespace MediaTidy;

internal sealed class ThemedProgressBar : Control
{
    private readonly System.Windows.Forms.Timer _animationTimer = new()
    {
        Interval = 35
    };
    private int _minimum;
    private int _maximum = 100;
    private int _value;
    private int _marqueeOffset;
    private ProgressBarStyle _style = ProgressBarStyle.Continuous;

    public ThemedProgressBar()
    {
        DoubleBuffered = true;
        Height = 20;
        _animationTimer.Tick += (_, _) =>
        {
            _marqueeOffset = (_marqueeOffset + 8) % Math.Max(1, Width + 80);
            Invalidate();
        };
    }

    public int Minimum
    {
        get => _minimum;
        set
        {
            _minimum = value;
            if (_maximum < _minimum)
            {
                _maximum = _minimum;
            }

            Value = _value;
        }
    }

    public int Maximum
    {
        get => _maximum;
        set
        {
            _maximum = Math.Max(_minimum, value);
            Value = _value;
        }
    }

    public int Value
    {
        get => _value;
        set
        {
            _value = Math.Clamp(value, _minimum, _maximum);
            Invalidate();
        }
    }

    public ProgressBarStyle Style
    {
        get => _style;
        set
        {
            _style = value;
            UpdateAnimation();
            Invalidate();
        }
    }

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        UpdateAnimation();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var bounds = ClientRectangle;
        using var track = new SolidBrush(
            AppTheme.IsDark ? Color.FromArgb(24, 26, 30) : Color.FromArgb(232, 235, 240));
        e.Graphics.FillRectangle(track, bounds);
        var inner = Rectangle.Inflate(bounds, -1, -1);
        if (inner.Width > 0 && inner.Height > 0)
        {
            using var fill = new SolidBrush(AppTheme.Accent);
            if (_style == ProgressBarStyle.Marquee)
            {
                var blockWidth = Math.Max(50, inner.Width / 4);
                var x = inner.Left + _marqueeOffset - blockWidth;
                e.Graphics.FillRectangle(
                    fill,
                    new Rectangle(x, inner.Top, blockWidth, inner.Height));
            }
            else if (_maximum > _minimum)
            {
                var fraction = (_value - _minimum) / (double)(_maximum - _minimum);
                var width = (int)Math.Round(inner.Width * fraction);
                if (width > 0)
                {
                    e.Graphics.FillRectangle(
                        fill,
                        new Rectangle(inner.Left, inner.Top, width, inner.Height));
                }
            }
        }

        using var border = new Pen(AppTheme.Border);
        e.Graphics.DrawRectangle(
            border,
            0,
            0,
            Math.Max(0, bounds.Width - 1),
            Math.Max(0, bounds.Height - 1));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _animationTimer.Dispose();
        }

        base.Dispose(disposing);
    }

    private void UpdateAnimation()
    {
        _animationTimer.Enabled = Visible && _style == ProgressBarStyle.Marquee;
    }
}
