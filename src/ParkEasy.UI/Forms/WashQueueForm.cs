using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ParkEasy.Application.Configuration;
using ParkEasy.Application.Interfaces;
using ParkEasy.Domain.Entities;
using ParkEasy.Domain.Enums;

namespace ParkEasy.UI.Forms;

public class WashQueueForm : Form
{
    private readonly IServiceProvider _serviceProvider;
    private readonly WashPricingSettings _washPricing;
    private readonly ILogger<WashQueueForm> _logger;

    private List<ParkingSession> _activeWashes = [];

    private DataGridView _gridPendente = null!;
    private DataGridView _gridLavando = null!;
    private DataGridView _gridConcluida = null!;

    private Label _lblPendenteHead = null!;
    private Label _lblLavandoHead = null!;
    private Label _lblConcluidaHead = null!;

    private Button _btnIniciar = null!;
    private Button _btnConcluir = null!;

    private System.Windows.Forms.Timer _refreshTimer = null!;

    public WashQueueForm(
        IServiceProvider serviceProvider,
        IOptions<WashPricingSettings> washPricingOptions,
        ILogger<WashQueueForm> logger)
    {
        _serviceProvider = serviceProvider;
        _washPricing = washPricingOptions.Value;
        _logger = logger;

        InitializeComponent();
    }

    private void InitializeComponent()
    {
        SuspendLayout();

        Text = "Fila de Lavagens";
        Size = new Size(1080, 680);
        MinimumSize = new Size(900, 560);
        StartPosition = FormStartPosition.CenterParent;
        Theme.ApplyTo(this);
        KeyPreview = true;

        var mainPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(Theme.Padding),
            BackColor = Theme.Background
        };

        var lblTitle = Theme.CreateLabel("FILA DE LAVAGENS", Theme.FontTitle, Theme.Primary);
        lblTitle.Dock = DockStyle.Top;
        lblTitle.Height = 36;
        mainPanel.Controls.Add(lblTitle);

        var topBar = new Panel { Dock = DockStyle.Top, Height = 48, Padding = new Padding(0, 8, 0, 8) };
        var btnNewWash = Theme.CreateSuccessButton("+ NOVA LAVAGEM", 200);
        btnNewWash.Location = new Point(0, 8);
        btnNewWash.Click += (_, _) => OpenNewWash();
        topBar.Controls.Add(btnNewWash);

        var btnRefresh = Theme.CreateSecondaryButton("ATUALIZAR", 140);
        btnRefresh.Location = new Point(210, 8);
        btnRefresh.Click += async (_, _) => await LoadDataAsync();
        topBar.Controls.Add(btnRefresh);

        mainPanel.Controls.Add(topBar);

        var columns = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1
        };
        columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
        columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
        columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.34f));

        var (pendentePanel, gridPendente, headPendente, btnIniciar) =
            CreateColumn("PENDENTE", Theme.Warning, "INICIAR LAVAGEM", () => AdvanceSelectedAsync(_gridPendente, StartSelectedWashAsync));
        _gridPendente = gridPendente;
        _lblPendenteHead = headPendente;
        _btnIniciar = btnIniciar;
        columns.Controls.Add(pendentePanel, 0, 0);

        var (lavandoPanel, gridLavando, headLavando, btnConcluir) =
            CreateColumn("LAVANDO", Theme.Info, "CONCLUIR LAVAGEM", () => AdvanceSelectedAsync(_gridLavando, CompleteSelectedWashAsync));
        _gridLavando = gridLavando;
        _lblLavandoHead = headLavando;
        _btnConcluir = btnConcluir;
        columns.Controls.Add(lavandoPanel, 1, 0);

        var (concluidaPanel, gridConcluida, headConcluida, _) =
            CreateColumn("CONCLUÍDA", Theme.Success, null, null);
        _gridConcluida = gridConcluida;
        _lblConcluidaHead = headConcluida;
        columns.Controls.Add(concluidaPanel, 2, 0);

        mainPanel.Controls.Add(columns);

        mainPanel.Controls.SetChildIndex(columns, 0);
        mainPanel.Controls.SetChildIndex(topBar, 1);
        mainPanel.Controls.SetChildIndex(lblTitle, 2);

        Controls.Add(mainPanel);

        _refreshTimer = new System.Windows.Forms.Timer { Interval = 15000 };
        _refreshTimer.Tick += async (_, _) => await LoadDataAsync();
        _refreshTimer.Start();

        KeyDown += WashQueueForm_KeyDown;
        FormClosed += (_, _) => _refreshTimer.Stop();

        ResumeLayout(false);
    }

    private (Panel panel, DataGridView grid, Label head, Button? actionButton) CreateColumn(
        string title, Color accentColor, string? actionLabel, Func<Task>? onAction)
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(4),
            Padding = new Padding(0)
        };

        var head = Theme.CreateLabel($"{title} (0)", Theme.FontLarge, accentColor);
        head.Dock = DockStyle.Top;
        head.Height = 28;
        panel.Controls.Add(head);

        var grid = new DataGridView { Dock = DockStyle.Fill };
        Theme.StyleDataGridView(grid);
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Placa", HeaderText = "Placa", FillWeight = 100 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Tipo", HeaderText = "Tipo", FillWeight = 140 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Tempo", HeaderText = "Tempo", FillWeight = 110 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "SessionId", Visible = false });
        grid.CellDoubleClick += (_, e) =>
        {
            if (e.RowIndex >= 0)
                OpenEditWash(grid);
        };
        panel.Controls.Add(grid);

        Button? actionButton = null;
        if (actionLabel is not null && onAction is not null)
        {
            var actionRow = new Panel { Dock = DockStyle.Bottom, Height = 44, Padding = new Padding(0, 6, 0, 0) };
            actionButton = Theme.CreatePrimaryButton(actionLabel, 220);
            actionButton.Enabled = false;
            actionButton.Click += async (_, _) => await onAction();
            actionRow.Controls.Add(actionButton);
            panel.Controls.Add(actionRow);

            var capturedButton = actionButton;
            grid.SelectionChanged += (_, _) => capturedButton.Enabled = grid.CurrentRow is not null && grid.CurrentRow.Index >= 0;
        }

        // O controle com Dock=Fill precisa ser o mais "à frente" no z-order (índice 0) pra
        // que o WinForms calcule seu tamanho por último, ocupando o espaço que sobrou dos
        // controles ancorados nas bordas (Top/Bottom) — senão o grid não preenche a coluna
        // e os botões acabam grudados no rodapé da janela inteira, longe do grid.
        grid.BringToFront();

        return (panel, grid, head, actionButton);
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
            var parkingService = scope.ServiceProvider.GetRequiredService<IParkingService>();
            _activeWashes = await parkingService.GetActiveWashesAsync();

            Populate(_gridPendente, _lblPendenteHead, "PENDENTE", WashStatus.Pendente);
            Populate(_gridLavando, _lblLavandoHead, "LAVANDO", WashStatus.Lavando);
            Populate(_gridConcluida, _lblConcluidaHead, "CONCLUÍDA", WashStatus.Concluida);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao carregar fila de lavagens");
        }
    }

    private void Populate(DataGridView grid, Label head, string title, WashStatus status)
    {
        var selectedSessionId = grid.CurrentRow?.Cells["SessionId"].Value as long?;

        grid.Rows.Clear();

        var sessions = _activeWashes.Where(s => s.WashStatus == status).OrderBy(s => s.WashRequestedAt).ToList();
        head.Text = $"{title} ({sessions.Count})";

        var now = DateTime.Now;

        foreach (var session in sessions)
        {
            var elapsed = session.WashRequestedAt.HasValue ? now - session.WashRequestedAt.Value : TimeSpan.Zero;
            var elapsedText = $"{(int)elapsed.TotalMinutes} min";

            var averageMinutes = session.WashTypeName is not null && _washPricing.TryGetValue(session.WashTypeName, out var config)
                ? config.AverageMinutes
                : 0;

            var timeText = averageMinutes > 0 ? $"{elapsedText} / ~{averageMinutes} min" : elapsedText;

            var rowIndex = grid.Rows.Add(session.Plate, session.WashTypeName ?? "—", timeText, session.Id);

            if (selectedSessionId.HasValue && session.Id == selectedSessionId.Value)
                grid.Rows[rowIndex].Selected = true;
        }
    }

    private async Task AdvanceSelectedAsync(DataGridView grid, Func<long, Task> action)
    {
        if (grid.CurrentRow is null || grid.CurrentRow.Index < 0)
            return;

        if (grid.CurrentRow.Cells["SessionId"].Value is not long sessionId)
            return;

        try
        {
            await action(sessionId);
            await LoadDataAsync();
        }
        catch (InvalidOperationException ex)
        {
            MessageBox.Show(ex.Message, "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao avançar status da lavagem {SessionId}", sessionId);
            MessageBox.Show($"Não foi possível atualizar a lavagem.\n\nDetalhes: {ex.Message}", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task StartSelectedWashAsync(long sessionId)
    {
        using var scope = _serviceProvider.CreateScope();
        var parkingService = scope.ServiceProvider.GetRequiredService<IParkingService>();
        await parkingService.StartWashingAsync(sessionId);
    }

    private async Task CompleteSelectedWashAsync(long sessionId)
    {
        using var scope = _serviceProvider.CreateScope();
        var parkingService = scope.ServiceProvider.GetRequiredService<IParkingService>();
        await parkingService.CompleteWashingAsync(sessionId);
    }

    private void OpenEditWash(DataGridView grid)
    {
        if (grid.CurrentRow?.Cells["SessionId"].Value is not long sessionId)
            return;

        using var scope = _serviceProvider.CreateScope();
        var washForm = scope.ServiceProvider.GetRequiredService<WashForm>();
        washForm.SessionId = sessionId;

        if (washForm.ShowDialog(this) == DialogResult.OK)
        {
            _ = LoadDataAsync();
        }
    }

    private void OpenNewWash()
    {
        using var scope = _serviceProvider.CreateScope();
        var washForm = scope.ServiceProvider.GetRequiredService<WashForm>();

        if (washForm.ShowDialog(this) == DialogResult.OK)
        {
            _ = LoadDataAsync();
        }
    }

    private void WashQueueForm_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape)
        {
            Close();
        }
    }
}
