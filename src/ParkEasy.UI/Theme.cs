namespace ParkEasy.UI;

/// <summary>
/// Centralized theme constants for the ParkEasy dark UI.
/// </summary>
public static class Theme
{
    // Background colors
    public static readonly Color Background = Color.FromArgb(30, 30, 46);
    public static readonly Color Surface = Color.FromArgb(45, 45, 68);
    public static readonly Color SurfaceLight = Color.FromArgb(55, 55, 80);
    public static readonly Color SurfaceHover = Color.FromArgb(65, 65, 95);

    // Accent colors
    public static readonly Color Primary = Color.FromArgb(124, 58, 237);    // Purple
    public static readonly Color PrimaryHover = Color.FromArgb(139, 92, 246);
    public static readonly Color Success = Color.FromArgb(34, 197, 94);     // Green
    public static readonly Color Danger = Color.FromArgb(239, 68, 68);      // Red
    public static readonly Color Warning = Color.FromArgb(245, 158, 11);    // Amber
    public static readonly Color Info = Color.FromArgb(59, 130, 246);       // Blue
    public static readonly Color WashReadyHighlight = Color.FromArgb(19, 78, 74);        // Teal — lavagem concluída, aguardando retirada
    public static readonly Color WashReadyHighlightSelected = Color.FromArgb(15, 118, 110);
    public static readonly Color WashInProgressHighlight = Color.FromArgb(28, 51, 61);   // Teal sutil — lavagem pendente/em andamento
    public static readonly Color WashInProgressHighlightSelected = Color.FromArgb(34, 68, 80);

    // Text colors
    public static readonly Color TextPrimary = Color.FromArgb(224, 224, 224);
    public static readonly Color TextSecondary = Color.FromArgb(160, 160, 180);
    public static readonly Color TextMuted = Color.FromArgb(120, 120, 140);
    public static readonly Color TextOnPrimary = Color.White;

    // Font
    public static readonly Font FontNormal = new("Segoe UI", 10f);
    public static readonly Font FontMedium = new("Segoe UI Semibold", 10f);
    public static readonly Font FontLarge = new("Segoe UI Semibold", 13f);
    public static readonly Font FontTitle = new("Segoe UI Bold", 16f);
    public static readonly Font FontHuge = new("Segoe UI Bold", 24f);
    public static readonly Font FontGrid = new("Segoe UI", 9.5f);
    public static readonly Font FontGridHeader = new("Segoe UI Semibold", 9.5f);
    public static readonly Font FontButton = new("Segoe UI Semibold", 11f);

    // Sizing
    public const int ButtonHeight = 42;
    public const int InputHeight = 36;
    public const int Padding = 16;
    public const int BorderRadius = 8;

    /// <summary>
    /// Applies theme to a form.
    /// </summary>
    public static void ApplyTo(Form form)
    {
        form.BackColor = Background;
        form.ForeColor = TextPrimary;
        form.Font = FontNormal;
    }

    /// <summary>
    /// Creates a styled primary button.
    /// </summary>
    public static Button CreatePrimaryButton(string text, int width = 220)
    {
        return new Button
        {
            Text = text,
            Size = new Size(width, ButtonHeight),
            BackColor = Primary,
            ForeColor = TextOnPrimary,
            FlatStyle = FlatStyle.Flat,
            Font = FontButton,
            Cursor = Cursors.Hand,
            FlatAppearance =
            {
                BorderSize = 0,
                MouseOverBackColor = PrimaryHover
            }
        };
    }

    /// <summary>
    /// Creates a styled success button.
    /// </summary>
    public static Button CreateSuccessButton(string text, int width = 220)
    {
        var btn = CreatePrimaryButton(text, width);
        btn.BackColor = Success;
        btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(22, 163, 74);
        return btn;
    }

    /// <summary>
    /// Creates a styled danger button.
    /// </summary>
    public static Button CreateDangerButton(string text, int width = 220)
    {
        var btn = CreatePrimaryButton(text, width);
        btn.BackColor = Danger;
        btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(220, 38, 38);
        return btn;
    }

    /// <summary>
    /// Creates a styled secondary/cancel button.
    /// </summary>
    public static Button CreateSecondaryButton(string text, int width = 220)
    {
        return new Button
        {
            Text = text,
            Size = new Size(width, ButtonHeight),
            BackColor = Surface,
            ForeColor = TextPrimary,
            FlatStyle = FlatStyle.Flat,
            Font = FontButton,
            Cursor = Cursors.Hand,
            FlatAppearance =
            {
                BorderSize = 1,
                BorderColor = SurfaceLight,
                MouseOverBackColor = SurfaceHover
            }
        };
    }

    /// <summary>
    /// Creates a styled text input.
    /// </summary>
    public static TextBox CreateInput(int width = 300)
    {
        return new TextBox
        {
            Size = new Size(width, InputHeight),
            BackColor = SurfaceLight,
            ForeColor = TextPrimary,
            Font = FontNormal,
            BorderStyle = BorderStyle.FixedSingle
        };
    }

    /// <summary>
    /// Creates a themed label.
    /// </summary>
    public static Label CreateLabel(string text, Font? font = null, Color? color = null)
    {
        return new Label
        {
            Text = text,
            AutoSize = true,
            Font = font ?? FontNormal,
            ForeColor = color ?? TextPrimary,
            BackColor = Color.Transparent
        };
    }

    /// <summary>
    /// Styles a DataGridView with the dark theme.
    /// </summary>
    public static void StyleDataGridView(DataGridView grid)
    {
        grid.BackgroundColor = Background;
        grid.GridColor = SurfaceLight;
        grid.BorderStyle = BorderStyle.None;
        grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;

        grid.EnableHeadersVisualStyles = false;
        grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Surface,
            ForeColor = TextPrimary,
            Font = FontGridHeader,
            SelectionBackColor = Surface,
            SelectionForeColor = TextPrimary,
            Padding = new Padding(8, 4, 8, 4),
            Alignment = DataGridViewContentAlignment.MiddleLeft
        };
        grid.ColumnHeadersHeight = 40;
        grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;

        grid.DefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Background,
            ForeColor = TextPrimary,
            Font = FontGrid,
            SelectionBackColor = SurfaceLight,
            SelectionForeColor = TextPrimary,
            Padding = new Padding(8, 4, 8, 4)
        };

        grid.AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Color.FromArgb(35, 35, 52),
            ForeColor = TextPrimary,
            SelectionBackColor = SurfaceLight,
            SelectionForeColor = TextPrimary
        };

        grid.RowTemplate.Height = 38;
        grid.RowHeadersVisible = false;
        grid.AllowUserToAddRows = false;
        grid.AllowUserToDeleteRows = false;
        grid.AllowUserToResizeRows = false;
        grid.ReadOnly = true;
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        grid.MultiSelect = false;
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
    }
}
