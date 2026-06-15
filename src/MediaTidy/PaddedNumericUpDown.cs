using System.Runtime.InteropServices;

namespace MediaTidy;

internal sealed class PaddedNumericUpDown : NumericUpDown
{
    private const int EmSetMargins = 0x00D3;
    private const int EcLeftMargin = 0x0001;

    public int LeftTextPadding { get; set; } = 6;

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        ApplyTextPadding();
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        ApplyTextPadding();
    }

    internal void ApplyTextPadding()
    {
        if (!IsHandleCreated)
        {
            return;
        }

        var edit = Controls
            .Cast<Control>()
            .FirstOrDefault(control =>
                control.GetType().Name.Contains(
                    "UpDownEdit",
                    StringComparison.Ordinal));
        if (edit is null)
        {
            return;
        }

        SendMessage(
            edit.Handle,
            EmSetMargins,
            (IntPtr)EcLeftMargin,
            (IntPtr)Math.Max(0, LeftTextPadding));
    }

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(
        IntPtr windowHandle,
        int message,
        IntPtr wParam,
        IntPtr lParam);
}
