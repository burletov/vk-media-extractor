using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace MediaTidy;

internal static class AppTheme
{
    private static readonly ConditionalWeakTable<Button, object> ThemedButtons = new();
    private static readonly ConditionalWeakTable<Control, object> NativeThemedControls = new();
    private static readonly ConditionalWeakTable<Control, object> EnabledThemedControls = new();
    public static bool IsDark { get; private set; }

    public static Color Window => IsDark ? Color.FromArgb(27, 29, 33) : SystemColors.Window;
    public static Color Control => IsDark ? Color.FromArgb(34, 37, 42) : SystemColors.Control;
    public static Color Surface => IsDark ? Color.FromArgb(43, 46, 52) : Color.FromArgb(246, 248, 251);
    public static Color Text => IsDark ? Color.FromArgb(239, 241, 244) : SystemColors.ControlText;
    public static Color Muted => IsDark ? Color.FromArgb(174, 181, 191) : SystemColors.GrayText;
    public static Color DisabledText => IsDark ? Color.FromArgb(125, 132, 142) : SystemColors.GrayText;
    public static Color Border => IsDark ? Color.FromArgb(76, 81, 90) : SystemColors.ControlDark;
    public static Color Accent => IsDark ? Color.FromArgb(91, 164, 235) : SystemColors.Highlight;
    public static Color AccentSurface => IsDark ? Color.FromArgb(34, 58, 80) : Color.AliceBlue;
    public static Color Selection => IsDark ? Color.FromArgb(48, 91, 140) : SystemColors.Highlight;
    public static Color Warning => IsDark ? Color.FromArgb(255, 197, 92) : Color.FromArgb(145, 91, 0);
    public static Color Success => IsDark ? Color.FromArgb(117, 201, 139) : Color.FromArgb(28, 112, 55);

    public static void Apply(Form form, bool dark)
    {
        IsDark = dark;
        ApplyControl(form);
    }

    private static void ApplyControl(Control control)
    {
        control.ForeColor = ResolveTextColor(control);
        control.BackColor = ResolveBackground(control);

        switch (control)
        {
            case Button button:
                button.UseVisualStyleBackColor = false;
                button.FlatStyle = FlatStyle.Flat;
                button.FlatAppearance.BorderSize = 0;
                button.FlatAppearance.MouseOverBackColor = IsDark
                    ? Color.FromArgb(55, 60, 68)
                    : Color.FromArgb(235, 239, 245);
                button.FlatAppearance.MouseDownBackColor = AccentSurface;
                button.BackColor = Surface;
                button.ForeColor = Text;
                AttachButtonTheme(button);
                break;

            case ComboBox comboBox:
                comboBox.FlatStyle = FlatStyle.Flat;
                comboBox.BackColor = Window;
                comboBox.ForeColor = Text;
                ApplyNativeTheme(comboBox, "DarkMode_CFD");
                break;

            case TextBoxBase textBox when control is not RichTextBox:
                textBox.BorderStyle = BorderStyle.FixedSingle;
                textBox.BackColor = control.Enabled ? Window : Surface;
                textBox.ForeColor = ResolveTextColor(control);
                ApplyNativeTheme(textBox, "DarkMode_Explorer");
                break;

            case NumericUpDown numeric:
                numeric.BorderStyle = BorderStyle.FixedSingle;
                numeric.BackColor = Window;
                numeric.ForeColor = Text;
                ApplyNativeTheme(numeric, "DarkMode_Explorer");
                if (numeric is PaddedNumericUpDown paddedNumeric)
                {
                    paddedNumeric.ApplyTextPadding();
                }
                break;

            case DataGridView grid:
                ApplyGridTheme(grid);
                break;

            case ToolStrip strip:
                ApplyToolStripTheme(strip);
                break;

            case TabControl tabs:
                tabs.BackColor = Control;
                tabs.ForeColor = Text;
                break;

            case RichTextBox richTextBox:
                richTextBox.BorderStyle = BorderStyle.FixedSingle;
                break;

            case CheckBox checkBox:
                checkBox.UseVisualStyleBackColor = false;
                checkBox.FlatStyle = FlatStyle.Flat;
                checkBox.BackColor = Control;
                checkBox.ForeColor = ResolveTextColor(checkBox);
                break;

            case RadioButton radioButton:
                radioButton.UseVisualStyleBackColor = false;
                radioButton.FlatStyle = FlatStyle.Flat;
                radioButton.BackColor = Control;
                radioButton.ForeColor = ResolveTextColor(radioButton);
                break;
        }

        if (control is Label or CheckBox or RadioButton or TextBoxBase or
            ComboBox or NumericUpDown)
        {
            AttachEnabledTheme(control);
        }

        foreach (Control child in control.Controls)
        {
            ApplyControl(child);
        }
    }

    private static Color ResolveTextColor(Control control) =>
        !control.Enabled ? DisabledText : control.Tag?.ToString() switch
        {
            "muted" => Muted,
            "warning" => Warning,
            "success" => Success,
            _ => Text
        };

    private static Color ResolveBackground(Control control)
    {
        if (control.Tag?.ToString() == "accent")
        {
            return AccentSurface;
        }

        return control switch
        {
            TextBoxBase => Window,
            ListBox => Window,
            ListView => Window,
            TreeView => Window,
            DataGridView => Window,
            PictureBox => Window,
            GroupBox => Control,
            ToolStrip => Control,
            _ => Control
        };
    }

    private static void ApplyGridTheme(DataGridView grid)
    {
        grid.BackgroundColor = Window;
        grid.GridColor = Border;
        grid.BorderStyle = BorderStyle.FixedSingle;
        grid.DefaultCellStyle.BackColor = Window;
        grid.DefaultCellStyle.ForeColor = Text;
        grid.DefaultCellStyle.SelectionBackColor = Selection;
        grid.DefaultCellStyle.SelectionForeColor = Color.White;
        grid.AlternatingRowsDefaultCellStyle.BackColor = IsDark
            ? Color.FromArgb(31, 34, 39)
            : Color.FromArgb(250, 251, 253);
        grid.AlternatingRowsDefaultCellStyle.ForeColor = Text;
        grid.ColumnHeadersDefaultCellStyle.BackColor = Surface;
        grid.ColumnHeadersDefaultCellStyle.ForeColor = Text;
        grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Surface;
        grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = Text;
        grid.RowHeadersDefaultCellStyle.BackColor = Surface;
        grid.RowHeadersDefaultCellStyle.ForeColor = Text;
        grid.RowHeadersDefaultCellStyle.SelectionBackColor = Selection;
        grid.EnableHeadersVisualStyles = false;
    }

    private static void AttachButtonTheme(Button button)
    {
        if (!ThemedButtons.TryAdd(button, new object()))
        {
            button.Invalidate();
            return;
        }

        button.Paint += (_, eventArgs) => DrawButton(button, eventArgs.Graphics);
        button.MouseEnter += (_, _) => button.Invalidate();
        button.MouseLeave += (_, _) => button.Invalidate();
        button.EnabledChanged += (_, _) => button.Invalidate();
    }

    private static void AttachEnabledTheme(Control control)
    {
        if (!EnabledThemedControls.TryAdd(control, new object()))
        {
            ApplyEnabledTheme(control);
            return;
        }

        control.EnabledChanged += (_, _) => ApplyEnabledTheme(control);
        ApplyEnabledTheme(control);
    }

    private static void ApplyEnabledTheme(Control control)
    {
        control.ForeColor = ResolveTextColor(control);
        switch (control)
        {
            case TextBoxBase textBox when control is not RichTextBox:
                textBox.BackColor = control.Enabled ? Window : Surface;
                break;

            case ComboBox comboBox:
                comboBox.BackColor = control.Enabled ? Window : Surface;
                break;

            case NumericUpDown numeric:
                numeric.BackColor = control.Enabled ? Window : Surface;
                if (numeric is PaddedNumericUpDown paddedNumeric)
                {
                    paddedNumeric.ApplyTextPadding();
                }
                break;
        }

        control.Invalidate();
    }

    private static void DrawButton(Button button, Graphics graphics)
    {
        var bounds = button.ClientRectangle;
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        var hovered = button.Enabled &&
                      bounds.Contains(button.PointToClient(Cursor.Position));
        var pressed = hovered &&
                      button.Capture &&
                      System.Windows.Forms.Control.MouseButtons == MouseButtons.Left;
        var background = !button.Enabled
            ? (IsDark ? Color.FromArgb(39, 42, 47) : SystemColors.Control)
            : pressed
                ? AccentSurface
                : hovered
                    ? (IsDark ? Color.FromArgb(55, 60, 68) : Color.FromArgb(235, 239, 245))
                    : Surface;
        using var backgroundBrush = new SolidBrush(background);
        graphics.FillRectangle(backgroundBrush, bounds);
        using var borderPen = new Pen(Border);
        graphics.DrawRectangle(
            borderPen,
            0,
            0,
            Math.Max(0, bounds.Width - 1),
            Math.Max(0, bounds.Height - 1));

        TextRenderer.DrawText(
            graphics,
            button.Text,
            button.Font,
            bounds,
            button.Enabled ? Text : DisabledText,
            TextFormatFlags.HorizontalCenter |
            TextFormatFlags.VerticalCenter |
            TextFormatFlags.EndEllipsis |
            TextFormatFlags.NoPrefix);
        if (button.Focused)
        {
            ControlPaint.DrawFocusRectangle(
                graphics,
                Rectangle.Inflate(bounds, -4, -4),
                button.Enabled ? Text : DisabledText,
                background);
        }
    }

    private static void ApplyNativeTheme(Control control, string darkTheme)
    {
        try
        {
            SetWindowTheme(
                control.Handle,
                IsDark ? darkTheme : null,
                null);
            if (NativeThemedControls.TryAdd(control, new object()))
            {
                control.HandleCreated += (_, _) =>
                    SetWindowTheme(
                        control.Handle,
                        IsDark ? darkTheme : null,
                        null);
            }
        }
        catch (DllNotFoundException)
        {
            // Older Windows versions continue with the managed colors.
        }
    }

    private static void ApplyToolStripTheme(ToolStrip strip)
    {
        strip.BackColor = Control;
        strip.ForeColor = Text;
        strip.RenderMode = ToolStripRenderMode.Professional;
        strip.Renderer = new AppToolStripRenderer();
        ApplyToolStripItems(strip.Items);
    }

    private static void ApplyToolStripItems(ToolStripItemCollection items)
    {
        foreach (ToolStripItem item in items)
        {
            item.BackColor = Control;
            item.ForeColor = item.Enabled ? Text : DisabledText;
            if (item is ToolStripMenuItem menuItem)
            {
                ApplyToolStripItems(menuItem.DropDownItems);
            }
        }
    }

    private sealed class AppToolStripRenderer : ToolStripProfessionalRenderer
    {
        public AppToolStripRenderer()
            : base(new AppColorTable())
        {
            RoundedEdges = false;
        }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            e.ArrowColor = e.Item?.Enabled != false ? Text : DisabledText;
            base.OnRenderArrow(e);
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item?.Enabled != false ? Text : DisabledText;
            base.OnRenderItemText(e);
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            var y = e.Item.Height / 2;
            using var pen = new Pen(Border);
            e.Graphics.DrawLine(pen, 4, y, e.Item.Width - 4, y);
        }
    }

    private sealed class AppColorTable : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => Control;
        public override Color MenuBorder => Border;
        public override Color MenuItemBorder => Border;
        public override Color MenuItemSelected => AccentSurface;
        public override Color MenuItemSelectedGradientBegin => AccentSurface;
        public override Color MenuItemSelectedGradientEnd => AccentSurface;
        public override Color MenuItemPressedGradientBegin => AccentSurface;
        public override Color MenuItemPressedGradientMiddle => AccentSurface;
        public override Color MenuItemPressedGradientEnd => AccentSurface;
        public override Color ImageMarginGradientBegin => Control;
        public override Color ImageMarginGradientMiddle => Control;
        public override Color ImageMarginGradientEnd => Control;
        public override Color ToolStripGradientBegin => Control;
        public override Color ToolStripGradientMiddle => Control;
        public override Color ToolStripGradientEnd => Control;
    }

    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
    private static extern int SetWindowTheme(
        IntPtr windowHandle,
        string? subAppName,
        string? subIdList);
}
