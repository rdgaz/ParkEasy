using System.Diagnostics;
using System.Globalization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ParkEasy.Application.DTOs;
using ParkEasy.Application.Interfaces;
using ParkEasy.Domain.Entities;
using ParkEasy.Domain.Enums;

namespace ParkEasy.UI.Forms;

public class MainForm : Form
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IParkingFeeCalculator _feeCalculator;
    private readonly IConfiguration _configuration;
    private readonly ILogger<MainForm> _logger;

    private Label _lblOccupiedSpaces = null!;
    private Label _lblAvailableSpaces = null!;
    private Label _lblTodayRevenue = null!;
    private TextBox _txtSearch = null!;
    private DataGridView _gridActive = null!;
    private Label _lblStatusText = null!;
    private System.Windows.Forms.Timer _refreshTimer = null!;

    private List<ParkingSession> _activeSessions = [];
    private int _totalSpaces = 50;

    public MainForm(
        IServiceProvider serviceProvider,
        IParkingFeeCalculator feeCalculator,
        IConfiguration configuration,
        ILogger<MainForm> logger)
    {
        _serviceProvider = serviceProvider;
        _feeCalculator = feeCalculator;
        _configuration = configuration;
        _logger = logger;

        InitializeComponent();
    }

    private void InitializeComponent()
    {
        SuspendLayout();

        Text = "ParkEasy — Sistema de Gerenciamento de Estacionamento";
        Size = new Size(1240, 760);
        MinimumSize = new Size(1050, 650);
        StartPosition = FormStartPosition.CenterScreen;
        Theme.ApplyTo(this);
        KeyPreview = true;

        if (int.TryParse(_configuration["Parking:TotalSpaces"], out var spaces) && spaces > 0)
        {
            _totalSpaces = spaces;
        }

        // 1. MenuStrip
        var menuStrip = CreateMenuStrip();
        MainMenuStrip = menuStrip;
        Controls.Add(menuStrip);

        // 2. Main Content Container
        var container = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(Theme.Padding),
            BackColor = Theme.Background
        };

        // 2.1 Cards Panel (Top Dashboard Metrics)
        var cardsPanel = CreateDashboardCardsPanel();
        cardsPanel.Dock = DockStyle.Top;
        container.Controls.Add(cardsPanel);

        // 2.2 Action / Search Bar Panel
        var actionBar = CreateActionBarPanel();
        actionBar.Dock = DockStyle.Top;
        container.Controls.Add(actionBar);

        // 2.3 Status Bar Panel (Bottom)
        var statusPanel = CreateStatusPanel();
        statusPanel.Dock = DockStyle.Bottom;
        container.Controls.Add(statusPanel);

        // 2.4 DataGridView for Active Sessions
        _gridActive = new DataGridView
        {
            Dock = DockStyle.Fill
        };
        Theme.StyleDataGridView(_gridActive);
        SetupGridColumns();
        _gridActive.CellDoubleClick += GridActive_CellDoubleClick;
        _gridActive.KeyDown += GridActive_KeyDown;
        container.Controls.Add(_gridActive);

        // Dock layering order
        container.Controls.SetChildIndex(_gridActive, 0);
        container.Controls.SetChildIndex(statusPanel, 1);
        container.Controls.SetChildIndex(actionBar, 2);
        container.Controls.SetChildIndex(cardsPanel, 3);

        Controls.Add(container);

        // Setup Timer for periodic auto-refresh (every 30s)
        _refreshTimer = new System.Windows.Forms.Timer
        {
            Interval = 30000
        };
        _refreshTimer.Tick += async (_, _) => await LoadDataAsync();
        _refreshTimer.Start();

        KeyDown += MainForm_KeyDown;

        ResumeLayout(false);
        PerformLayout();
    }

    private MenuStrip CreateMenuStrip()
    {
        var menuStrip = new MenuStrip
        {
            BackColor = Theme.Surface,
            ForeColor = Color.White,
            Font = Theme.FontMedium,
            Padding = new Padding(6, 4, 6, 4)
        };

        menuStrip.Renderer = new DarkMenuRenderer();

        // Menu Sistema
        var menuSistema = new ToolStripMenuItem("Sistema");
        
        var itemTestPrinter = new ToolStripMenuItem("Testar Impressora", null, async (_, _) => await TestPrinterAsync());
        var itemBackup = new ToolStripMenuItem("Fazer Backup", null, (_, _) => DoBackup());
        var itemOpenBackups = new ToolStripMenuItem("Abrir Pasta de Backups", null, (_, _) => OpenBackupsFolder());
        var itemExit = new ToolStripMenuItem("Sair", null, (_, _) => Close());

        menuSistema.DropDownItems.Add(itemTestPrinter);
        menuSistema.DropDownItems.Add(new ToolStripSeparator());
        menuSistema.DropDownItems.Add(itemBackup);
        menuSistema.DropDownItems.Add(itemOpenBackups);
        menuSistema.DropDownItems.Add(new ToolStripSeparator());
        menuSistema.DropDownItems.Add(itemExit);

        // Menu Estacionamento
        var menuEstacionamento = new ToolStripMenuItem("Estacionamento");
        var itemEntry = new ToolStripMenuItem("Nova Entrada (F2)", null, (_, _) => OpenEntryForm());
        var itemCheckout = new ToolStripMenuItem("Registrar Saída", null, (_, _) => OpenCheckoutForSelected());
        var itemHistory = new ToolStripMenuItem("Histórico de Permanências (F6)", null, (_, _) => OpenHistoryForm());
        var itemRefresh = new ToolStripMenuItem("Atualizar (F5)", null, async (_, _) => await LoadDataAsync());

        menuEstacionamento.DropDownItems.Add(itemEntry);
        menuEstacionamento.DropDownItems.Add(itemCheckout);
        menuEstacionamento.DropDownItems.Add(new ToolStripSeparator());
        menuEstacionamento.DropDownItems.Add(itemHistory);
        menuEstacionamento.DropDownItems.Add(itemRefresh);

        // Menu Ajuda
        var menuAjuda = new ToolStripMenuItem("Ajuda");
        var itemAbout = new ToolStripMenuItem("Sobre o ParkEasy", null, (_, _) => ShowAboutDialog());
        menuAjuda.DropDownItems.Add(itemAbout);

        menuStrip.Items.Add(menuSistema);
        menuStrip.Items.Add(menuEstacionamento);
        menuStrip.Items.Add(menuAjuda);

        ApplyWhiteForeColor(menuStrip.Items);

        return menuStrip;
    }

    private static void ApplyWhiteForeColor(ToolStripItemCollection items)
    {
        foreach (ToolStripItem item in items)
        {
            item.ForeColor = Color.White;
            if (item is ToolStripMenuItem menuItem && menuItem.HasDropDownItems)
            {
                ApplyWhiteForeColor(menuItem.DropDownItems);
            }
        }
    }

    private Panel CreateDashboardCardsPanel()
    {
        var panel = new TableLayoutPanel
        {
            ColumnCount = 3,
            RowCount = 1,
            Height = 120,                        // Aumentado para acomodar os 20px extras
            Padding = new Padding(0, 20, 0, 10),
            BackColor = Color.Transparent
        };

        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.34f));

        // Card 1: Vagas Ocupadas
        var cardOccupied = CreateStatCard("VAGAS OCUPADAS", "0 / 50", Theme.Warning, out _lblOccupiedSpaces);
        panel.Controls.Add(cardOccupied, 0, 0);

        // Card 2: Vagas Livres
        var cardAvailable = CreateStatCard("VAGAS LIVRES", "50", Theme.Success, out _lblAvailableSpaces);
        panel.Controls.Add(cardAvailable, 1, 0);

        // Card 3: Faturamento Hoje
        var cardRevenue = CreateStatCard("FATURAMENTO HOJE", "R$ 0,00", Theme.Primary, out _lblTodayRevenue);
        panel.Controls.Add(cardRevenue, 2, 0);

        return panel;
    }

    private Panel CreateStatCard(string title, string initialValue, Color accentColor, out Label valueLabel)
    {
        var card = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.Surface,
            Padding = new Padding(16, 12, 16, 12),
            Margin = new Padding(4)
        };

        var titleLbl = Theme.CreateLabel(title, Theme.FontNormal, Theme.TextSecondary);
        titleLbl.Location = new Point(16, 12);
        card.Controls.Add(titleLbl);

        var valLbl = Theme.CreateLabel(initialValue, Theme.FontHuge, accentColor);
        valLbl.Location = new Point(16, 36);
        valLbl.AutoSize = true;
        card.Controls.Add(valLbl);

        valueLabel = valLbl;
        return card;
    }

    private Panel CreateActionBarPanel()
    {
        var panel = new Panel
        {
            Height = 48,
            Margin = new Padding(0, 16, 0, 12), // 16px de respiro em relação aos cards acima
            Padding = new Padding(0),
            BackColor = Color.Transparent
        };

        int buttonY = 4; // Ligeiro alinhamento vertical dos botões

        var btnEntry = Theme.CreateSuccessButton("＋ NOVA ENTRADA (F2)", 220);
        btnEntry.Location = new Point(0, buttonY);
        btnEntry.Click += (_, _) => OpenEntryForm();
        panel.Controls.Add(btnEntry);

        var btnCheckout = Theme.CreatePrimaryButton("REGISTRAR SAÍDA", 210);
        btnCheckout.Location = new Point(230, buttonY);
        btnCheckout.BackColor = Theme.Warning;
        btnCheckout.FlatAppearance.MouseOverBackColor = Color.FromArgb(217, 119, 6);
        btnCheckout.Click += (_, _) => OpenCheckoutForSelected();
        panel.Controls.Add(btnCheckout);

        var btnHistory = Theme.CreateSecondaryButton("HISTÓRICO (F6)", 160);
        btnHistory.Location = new Point(450, buttonY);
        btnHistory.Click += (_, _) => OpenHistoryForm();
        panel.Controls.Add(btnHistory);

        var btnRefresh = Theme.CreateSecondaryButton("ATUALIZAR (F5)", 150);
        btnRefresh.Location = new Point(620, buttonY);
        btnRefresh.Click += async (_, _) => await LoadDataAsync();
        panel.Controls.Add(btnRefresh);

        // Search Input (Direita)
        _txtSearch = Theme.CreateInput(300);
        _txtSearch.PlaceholderText = "Pesquisar placa ou cliente (F4)...";
        _txtSearch.CharacterCasing = CharacterCasing.Upper;
        _txtSearch.Location = new Point(panel.Width - 300, buttonY);
        _txtSearch.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _txtSearch.TextChanged += async (_, _) => await FilterActiveSessionsAsync();
        panel.Controls.Add(_txtSearch);

        return panel;
    }


    private Panel CreateStatusPanel()
    {
        var panel = new Panel
        {
            Height = 30,
            BackColor = Theme.Surface,
            Padding = new Padding(12, 6, 12, 6)
        };

        _lblStatusText = Theme.CreateLabel("Pronto. Pressione F2 para registrar entrada.", Theme.FontGrid, Theme.TextSecondary);
        _lblStatusText.Dock = DockStyle.Left;
        panel.Controls.Add(_lblStatusText);

        var lblShortcuts = Theme.CreateLabel("Atalhos: F2 Nova Entrada | F4 Pesquisar | F5 Atualizar | F6 Histórico", Theme.FontGrid, Theme.TextMuted);
        lblShortcuts.Dock = DockStyle.Right;
        panel.Controls.Add(lblShortcuts);

        return panel;
    }

    private void SetupGridColumns()
    {
        _gridActive.Columns.Clear();
        _gridActive.Columns.Add(new DataGridViewTextBoxColumn { Name = "Ticket", HeaderText = "Ticket", MinimumWidth = 90, FillWeight = 90 });
        _gridActive.Columns.Add(new DataGridViewTextBoxColumn { Name = "Placa", HeaderText = "Placa", MinimumWidth = 100, FillWeight = 100 });
        _gridActive.Columns.Add(new DataGridViewTextBoxColumn { Name = "Tipo", HeaderText = "Tipo", MinimumWidth = 100, FillWeight = 100 });
        _gridActive.Columns.Add(new DataGridViewTextBoxColumn { Name = "Modelo", HeaderText = "Modelo", MinimumWidth = 140, FillWeight = 140 });
        _gridActive.Columns.Add(new DataGridViewTextBoxColumn { Name = "Cliente", HeaderText = "Cliente", MinimumWidth = 150, FillWeight = 150 });
        _gridActive.Columns.Add(new DataGridViewTextBoxColumn { Name = "Telefone", HeaderText = "Telefone", MinimumWidth = 130, FillWeight = 130 });
        _gridActive.Columns.Add(new DataGridViewTextBoxColumn { Name = "Entrada", HeaderText = "Horário de Entrada", MinimumWidth = 160, FillWeight = 160 });
        _gridActive.Columns.Add(new DataGridViewTextBoxColumn { Name = "Tempo", HeaderText = "Permanência", MinimumWidth = 120, FillWeight = 120 });
        _gridActive.Columns.Add(new DataGridViewTextBoxColumn { Name = "ValorEstimado", HeaderText = "Valor Atual", MinimumWidth = 120, FillWeight = 120 });
        _gridActive.Columns.Add(new DataGridViewTextBoxColumn { Name = "SessionId", Visible = false });
    }

    protected override async void OnShown(EventArgs e)
    {
        base.OnShown(e);
        await LoadDataAsync();
    }

    private async Task LoadDataAsync()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IParkingService>();

            // Load Dashboard metrics
            var dashData = await service.GetDashboardDataAsync();
            _lblOccupiedSpaces.Text = $"{dashData.OccupiedSpaces} / {dashData.TotalSpaces}";
            _lblAvailableSpaces.Text = Math.Max(0, dashData.TotalSpaces - dashData.OccupiedSpaces).ToString();

            var brCulture = CultureInfo.GetCultureInfo("pt-BR");
            _lblTodayRevenue.Text = dashData.TodayRevenue.ToString("C2", brCulture);

            // Load Active Sessions
            if (string.IsNullOrWhiteSpace(_txtSearch.Text))
            {
                _activeSessions = await service.GetActiveSessionsAsync();
            }
            else
            {
                _activeSessions = await service.SearchActiveSessionsAsync(_txtSearch.Text.Trim());
            }

            PopulateGrid();
            _lblStatusText.Text = $"Última atualização às {DateTime.Now:HH:mm:ss} — {dashData.ActiveVehicles} veículo(s) no pátio.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao carregar dados do painel principal");
            _lblStatusText.Text = "Erro ao atualizar dados do painel.";
        }
    }

    private async Task FilterActiveSessionsAsync()
    {
        var query = _txtSearch.Text.Trim();
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IParkingService>();

            if (string.IsNullOrWhiteSpace(query))
            {
                _activeSessions = await service.GetActiveSessionsAsync();
            }
            else
            {
                _activeSessions = await service.SearchActiveSessionsAsync(query);
            }

            PopulateGrid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao filtrar sessões ativas");
        }
    }

    private void PopulateGrid()
    {
        _gridActive.Rows.Clear();
        var now = DateTime.Now;
        var brCulture = CultureInfo.GetCultureInfo("pt-BR");

        foreach (var session in _activeSessions)
        {
            var elapsed = now - session.EntryDateTime;
            var fee = _feeCalculator.CalculateFee(session.EntryDateTime, now, session.VehicleType);

            _gridActive.Rows.Add(
                session.TicketNumber,
                session.Plate,
                session.VehicleType.ToDisplayName(),
                session.VehicleModel ?? "—",
                session.CustomerName ?? "—",
                session.CustomerPhone ?? "—",
                session.EntryDateTime.ToString("dd/MM/yyyy HH:mm"),
                $"{(int)elapsed.TotalHours:D2}:{elapsed.Minutes:D2}:{elapsed.Seconds:D2}",
                fee.ToString("C2", brCulture),
                session.Id
            );
        }
    }

    private void OpenEntryForm()
    {
        using var scope = _serviceProvider.CreateScope();
        var entryForm = scope.ServiceProvider.GetRequiredService<EntryForm>();
        if (entryForm.ShowDialog(this) == DialogResult.OK)
        {
            _ = LoadDataAsync();
        }
    }

    private void OpenCheckoutForSelected()
    {
        if (_gridActive.CurrentRow is null || _gridActive.CurrentRow.Index < 0)
        {
            MessageBox.Show("Selecione um veículo na lista para registrar a saída.", "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (_gridActive.CurrentRow.Cells["SessionId"].Value is not long sessionId) return;

        using var scope = _serviceProvider.CreateScope();
        var checkoutForm = scope.ServiceProvider.GetRequiredService<CheckoutForm>();
        checkoutForm.SessionId = sessionId;

        if (checkoutForm.ShowDialog(this) == DialogResult.OK)
        {
            _ = LoadDataAsync();
        }
    }

    private void OpenHistoryForm()
    {
        using var scope = _serviceProvider.CreateScope();
        var historyForm = scope.ServiceProvider.GetRequiredService<HistoryForm>();
        historyForm.ShowDialog(this);
    }

    private async Task TestPrinterAsync()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var printerService = scope.ServiceProvider.GetRequiredService<IPrinterService>();
            await printerService.TestPrinterAsync();

            MessageBox.Show("Teste de impressora enviado com sucesso!", "Teste de Impressão", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao testar impressora");
            MessageBox.Show($"Falha ao enviar teste para a impressora.\n\nDetalhes: {ex.Message}", "Erro de Impressão", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void DoBackup()
    {
        try
        {
            var dbPath = Path.Combine(AppContext.BaseDirectory, "parking.db");
            if (!File.Exists(dbPath))
            {
                // Fallback to project root directory
                dbPath = Path.Combine(Directory.GetCurrentDirectory(), "parking.db");
            }

            if (!File.Exists(dbPath))
            {
                MessageBox.Show("Arquivo de banco de dados 'parking.db' não foi encontrado para backup.", "Backup", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var backupDir = Path.Combine(AppContext.BaseDirectory, "Backups");
            Directory.CreateDirectory(backupDir);

            var fileName = $"parking_backup_{DateTime.Now:yyyy-MM-dd_HHmmss}.db";
            var destPath = Path.Combine(backupDir, fileName);

            File.Copy(dbPath, destPath, true);

            MessageBox.Show($"Backup realizado com sucesso!\n\nSalvo em:\n{destPath}", "Backup Concluído", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao realizar backup do banco de dados");
            MessageBox.Show($"Falha ao criar arquivo de backup.\n\nDetalhes: {ex.Message}", "Erro de Backup", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OpenBackupsFolder()
    {
        try
        {
            var backupDir = Path.Combine(AppContext.BaseDirectory, "Backups");
            Directory.CreateDirectory(backupDir);

            Process.Start(new ProcessStartInfo
            {
                FileName = backupDir,
                UseShellExecute = true,
                Verb = "open"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao abrir pasta de backups");
            MessageBox.Show("Não foi possível abrir a pasta de backups.", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ShowAboutDialog()
    {
        MessageBox.Show(
            "ParkEasy v1.0.0\n\nSistema Desktop de Gerenciamento de Estacionamento.\nDesenvolvido em .NET 10 & WinForms.\nSuporte a impressoras térmicas Bematech e banco de dados SQLite.",
            "Sobre o ParkEasy",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private void GridActive_CellDoubleClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex >= 0)
        {
            OpenCheckoutForSelected();
        }
    }

    private void GridActive_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter)
        {
            e.SuppressKeyPress = true;
            OpenCheckoutForSelected();
        }
    }

    private void MainForm_KeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.KeyCode)
        {
            case Keys.F2:
                OpenEntryForm();
                break;
            case Keys.F4:
                _txtSearch.Focus();
                _txtSearch.SelectAll();
                break;
            case Keys.F5:
                _ = LoadDataAsync();
                break;
            case Keys.F6:
                OpenHistoryForm();
                break;
        }
    }
}

/// <summary>
/// Custom Renderer for Dark MenuStrip with pure white text and crisp dark purple menu highlights.
/// </summary>
internal class DarkMenuRenderer : ToolStripProfessionalRenderer
{
    public DarkMenuRenderer() : base(new DarkColorTable()) { }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = Color.White;
        base.OnRenderItemText(e);
    }

    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        if (!e.Item.Enabled)
        {
            base.OnRenderMenuItemBackground(e);
            return;
        }

        var rect = new Rectangle(Point.Empty, e.Item.Size);
        if (e.Item.Selected || e.Item.Pressed)
        {
            using var brush = new SolidBrush(Theme.Primary);
            e.Graphics.FillRectangle(brush, rect);
        }
        else
        {
            using var brush = new SolidBrush(Theme.Surface);
            e.Graphics.FillRectangle(brush, rect);
        }
    }

    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
    {
        var rect = new Rectangle(Point.Empty, e.Item.Size);
        using (var bgBrush = new SolidBrush(Theme.Surface))
        {
            e.Graphics.FillRectangle(bgBrush, rect);
        }

        using var pen = new Pen(Theme.SurfaceLight, 1);
        int y = e.Item.ContentRectangle.Height / 2;
        e.Graphics.DrawLine(pen, 8, y, e.Item.ContentRectangle.Width - 8, y);
    }
}

internal class DarkColorTable : ProfessionalColorTable
{
    public override Color MenuItemSelected => Theme.Primary;
    public override Color MenuItemSelectedGradientBegin => Theme.Primary;
    public override Color MenuItemSelectedGradientEnd => Theme.Primary;
    public override Color MenuItemPressedGradientBegin => Theme.PrimaryHover;
    public override Color MenuItemPressedGradientMiddle => Theme.PrimaryHover;
    public override Color MenuItemPressedGradientEnd => Theme.PrimaryHover;
    public override Color MenuBorder => Theme.SurfaceLight;
    public override Color MenuItemBorder => Color.Transparent;
    public override Color ToolStripDropDownBackground => Theme.Surface;
    public override Color ImageMarginGradientBegin => Theme.Surface;
    public override Color ImageMarginGradientMiddle => Theme.Surface;
    public override Color ImageMarginGradientEnd => Theme.Surface;
}