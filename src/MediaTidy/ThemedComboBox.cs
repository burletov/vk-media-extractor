namespace MediaTidy;

internal sealed class ThemedComboBox : ComboBox
{
    private const int PaintMessage = 0x000F;

    public ThemedComboBox()
    {
        DrawMode = DrawMode.OwnerDrawFixed;
        DropDownStyle = ComboBoxStyle.DropDownList;
        ItemHeight = Font.Height + 5;
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        ItemHeight = Font.Height + 5;
    }

    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        if (e.Index < 0)
        {
            return;
        }

        var selected = (e.State & DrawItemState.Selected) != 0;
        var background = selected ? AppTheme.Selection : AppTheme.Window;
        using var brush = new SolidBrush(background);
        e.Graphics.FillRectangle(brush, e.Bounds);
        TextRenderer.DrawText(
            e.Graphics,
            GetItemText(Items[e.Index]),
            Font,
            Rectangle.Inflate(e.Bounds, -4, 0),
            selected ? Color.White : AppTheme.Text,
            TextFormatFlags.VerticalCenter |
            TextFormatFlags.Left |
            TextFormatFlags.EndEllipsis |
            TextFormatFlags.NoPrefix);
    }

    protected override void WndProc(ref Message message)
    {
        base.WndProc(ref message);
        if (message.Msg != PaintMessage || !AppTheme.IsDark || !IsHandleCreated)
        {
            return;
        }

        using var graphics = Graphics.FromHwnd(Handle);
        var bounds = ClientRectangle;
        using var background = new SolidBrush(AppTheme.Window);
        graphics.FillRectangle(background, bounds);

        var arrowWidth = Math.Max(22, SystemInformation.VerticalScrollBarWidth + 4);
        var arrowBounds = new Rectangle(
            Math.Max(0, bounds.Right - arrowWidth),
            bounds.Top,
            arrowWidth,
            bounds.Height);
        using var arrowBackground = new SolidBrush(AppTheme.Surface);
        graphics.FillRectangle(arrowBackground, arrowBounds);
        using var border = new Pen(AppTheme.Border);
        graphics.DrawRectangle(
            border,
            0,
            0,
            Math.Max(0, bounds.Width - 1),
            Math.Max(0, bounds.Height - 1));
        graphics.DrawLine(
            border,
            arrowBounds.Left,
            arrowBounds.Top,
            arrowBounds.Left,
            arrowBounds.Bottom);

        var textBounds = new Rectangle(
            7,
            1,
            Math.Max(0, bounds.Width - arrowWidth - 10),
            Math.Max(0, bounds.Height - 2));
        TextRenderer.DrawText(
            graphics,
            Text,
            Font,
            textBounds,
            Enabled ? AppTheme.Text : AppTheme.DisabledText,
            TextFormatFlags.VerticalCenter |
            TextFormatFlags.Left |
            TextFormatFlags.EndEllipsis |
            TextFormatFlags.NoPrefix);

        var centerX = arrowBounds.Left + arrowBounds.Width / 2;
        var centerY = arrowBounds.Top + arrowBounds.Height / 2;
        Point[] triangle =
        [
            new(centerX - 4, centerY - 2),
            new(centerX + 4, centerY - 2),
            new(centerX, centerY + 3)
        ];
        using var arrow = new SolidBrush(
            Enabled ? AppTheme.Text : AppTheme.DisabledText);
        graphics.FillPolygon(arrow, triangle);
    }
}
