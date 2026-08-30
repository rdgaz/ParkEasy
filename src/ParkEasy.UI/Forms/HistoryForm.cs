using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ParkEasy.Application.DTOs;
using ParkEasy.Application.Interfaces;
using ParkEasy.Domain.Entities;
using ParkEasy.Domain.Enums;

namespace ParkEasy.UI.Forms;

public class HistoryForm : Form
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<HistoryForm> _logger;

    private DateTimePicker _dtpStart = null!;
    private DateTimePicker _dtpEnd = null!;
    private CheckBox _chkUseDateFilter = null!;
    private TextBox _txtPlate = null!;
    private TextBox _txtTicket = null!;
    private TextBox _txtCustomer = null!;
    private ComboBox _cmbVehicleType = null!;
    private DataGridView _grid = null!;
    private Label _lblTotalVehicles = null!;
    private Label _lblTotalRevenue = null!;

    private List<ParkingSession> _history = [];

    public HistoryForm(
        IServiceProvider serviceProvider,
        ILogger<HistoryForm> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;

        InitializeComponent();
    }

    private void InitializeComponent()
    {
        SuspendLayout();

        Text = "Histórico de Estacionamentos";
        Size = new Size(1000, 680);
        MinimumSize = new Size(850, 550);
        StartPosition = FormStartPosition.CenterParent;
        Theme.ApplyTo(this);
        KeyPreview = true;

        var mainPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(Theme.Padding),
            BackColor = Theme.Background
        };

        // Title
        var lblTitle = Theme.CreateLabel("HISTÓRICO DE PERMANÊNCIAS ENCERRADAS", Theme.FontTitle, Theme.Primary);
        lblTitle.Dock = DockStyle.Top;
        lblTitle.Height = 36;
        mainPanel.Controls.Add(lblTitle);

        // Filter Panel
        var filterPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 150,
            BackColor = Theme.Surface,
            Padding = new Padding(16)
        };

        int top = 12;

        // Date row
        _chkUseDateFilter = new CheckBox
        {
            Text = "Filtrar Período (Saída):",
            Location = new Point(16, top),
            AutoSize = true,
            Font = Theme.FontMedium,
            ForeColor = Theme.TextPrimary,
            Checked = true
        };
        _chkUseDateFilter.CheckedChanged += (s, e) =>
        {
            _dtpStart.Enabled = _chkUseDateFilter.Checked;
            _dtpEnd.Enabled = _chkUseDateFilter.Checked;
        };
        filterPanel.Controls.Add(_chkUseDateFilter);

        var lblStart = Theme.CreateLabel("De:", Theme.FontNormal, Theme.TextSecondary);
        lblStart.Location = new Point(154, top);
        filterPanel.Controls.Add(lblStart);

        _dtpStart = new DateTimePicker
        {
            Format = DateTimePickerFormat.Short,
            Location = new Point(189, top - 2),
            Size = new Size(120, 26),
            Value = DateTime.Today.AddDays(-7),
            Font = Theme.FontNormal
        };
        filterPanel.Controls.Add(_dtpStart);

        var lblEnd = Theme.CreateLabel("Até:", Theme.FontNormal, Theme.TextSecondary);
        lblEnd.Location = new Point(316, top);
        filterPanel.Controls.Add(lblEnd);

        _dtpEnd = new DateTimePicker
        {
            Format = DateTimePickerFormat.Short,
            Location = new Point(369, top - 2),
            Size = new Size(120, 26),
            Value = DateTime.Today,
            Font = Theme.FontNormal
        };
        filterPanel.Controls.Add(_dtpEnd);

        top += 38;

        // Inputs row: Plate, Ticket, Customer
        var lblPlate = Theme.CreateLabel("Placa:", Theme.FontNormal, Theme.TextSecondary);
        lblPlate.Location = new Point(16, top);
        filterPanel.Controls.Add(lblPlate);

        _txtPlate = Theme.CreateInput(100);
        _txtPlate.Location = new Point(70, top - 2);
        _txtPlate.CharacterCasing = CharacterCasing.Upper;
        filterPanel.Controls.Add(_txtPlate);

        var lblTicket = Theme.CreateLabel("Ticket:", Theme.FontNormal, Theme.TextSecondary);
        lblTicket.Location = new Point(180, top);
        filterPanel.Controls.Add(lblTicket);

        _txtTicket = Theme.CreateInput(100);
        _txtTicket.Location = new Point(240, top - 2);
        filterPanel.Controls.Add(_txtTicket);

        var lblCust = Theme.CreateLabel("Cliente:", Theme.FontNormal, Theme.TextSecondary);
        lblCust.Location = new Point(350, top);
        filterPanel.Controls.Add(lblCust);

        _txtCustomer = Theme.CreateInput(150);
        _txtCustomer.Location = new Point(420, top - 2);
        filterPanel.Controls.Add(_txtCustomer);

        top += 38;

        // Vehicle type filter row
        var lblType = Theme.CreateLabel("Tipo:", Theme.FontNormal, Theme.TextSecondary);
        lblType.Location = new Point(16, top);
        filterPanel.Controls.Add(lblType);

        _cmbVehicleType = new ComboBox
        {
            Location = new Point(70, top - 2),
            Size = new Size(140, Theme.InputHeight),
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = Theme.SurfaceLight,
            ForeColor = Theme.TextPrimary,
            Font = Theme.FontNormal,
            FlatStyle = FlatStyle.Flat
        };
        _cmbVehicleType.Items.Add("Todos");
        _cmbVehicleType.Items.Add("Moto");
        _cmbVehicleType.Items.Add("Carro");
        _cmbVehicleType.Items.Add("Vaga Dupla");
        _cmbVehicleType.SelectedIndex = 0;
        filterPanel.Controls.Add(_cmbVehicleType);

        var btnFilter = Theme.CreatePrimaryButton("FILTRAR", 120);
        btnFilter.Location = new Point(580, top - 6);
        btnFilter.Height = 36;
        btnFilter.Click += async (_, _) => await LoadHistoryAsync();
        filterPanel.Controls.Add(btnFilter);

        var btnClear = Theme.CreateSecondaryButton("LIMPAR", 100);
        btnClear.Location = new Point(710, top - 6);
        btnClear.Height = 36;
        btnClear.Click += async (_, _) =>
        {
            _chkUseDateFilter.Checked = false;
            _txtPlate.Clear();
            _txtTicket.Clear();
            _txtCustomer.Clear();
            _cmbVehicleType.SelectedIndex = 0;
            await LoadHistoryAsync();
        };
        filterPanel.Controls.Add(btnClear);

        mainPanel.Controls.Add(filterPanel);

        // Summary footer panel
        var summaryPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 55,
            BackColor = Theme.Surface,
            Padding = new Padding(20, 10, 20, 10)
        };

        var lblVehHead = Theme.CreateLabel("TOTAL DE VEÍCULOS:", Theme.FontMedium, Theme.TextSecondary);
        lblVehHead.Location = new Point(20, 16);
        summaryPanel.Controls.Add(lblVehHead);

        _lblTotalVehicles = Theme.CreateLabel("0", Theme.FontTitle, Theme.TextPrimary);
        _lblTotalVehicles.Location = new Point(170, 12);
        summaryPanel.Controls.Add(_lblTotalVehicles);

        var lblRevHead = Theme.CreateLabel("TOTAL ARRECADADO:", Theme.FontMedium, Theme.TextSecondary);
        lblRevHead.Location = new Point(350, 16);
        summaryPanel.Controls.Add(lblRevHead);

        _lblTotalRevenue = Theme.CreateLabel("R$ 0,00", Theme.FontTitle, Theme.Success);
        _lblTotalRevenue.Location = new Point(520, 12);
        summaryPanel.Controls.Add(_lblTotalRevenue);

        var btnReprint = Theme.CreateSecondaryButton("REIMPRIMIR COMPROVANTE", 210);
        btnReprint.Dock = DockStyle.Right;
        btnReprint.Height = 36;
        btnReprint.Click += BtnReprint_Click;
        summaryPanel.Controls.Add(btnReprint);

        mainPanel.Controls.Add(summaryPanel);

        // DataGridView
        _grid = new DataGridView
        {
            Dock = DockStyle.Fill
        };
        Theme.StyleDataGridView(_grid);
        SetupGridColumns();
        mainPanel.Controls.Add(_grid);

        Controls.Add(mainPanel);

        // Fix dock stack order
        mainPanel.Controls.SetChildIndex(_grid, 0);
        mainPanel.Controls.SetChildIndex(summaryPanel, 1);
        mainPanel.Controls.SetChildIndex(filterPanel, 2);
        mainPanel.Controls.SetChildIndex(lblTitle, 3);

        KeyDown += HistoryForm_KeyDown;

        ResumeLayout(false);
    }

    private void SetupGridColumns()
    {
        _grid.Columns.Clear();
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Ticket", HeaderText = "Ticket", Width = 90 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Placa", HeaderText = "Placa", Width = 100 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Tipo", HeaderText = "Tipo", Width = 90 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Modelo", HeaderText = "Modelo", Width = 120 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Cliente", HeaderText = "Cliente", Width = 130 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Telefone", HeaderText = "Telefone", Width = 120 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Servico", HeaderText = "Serviço", Width = 90 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Entrada", HeaderText = "Entrada", Width = 130 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Saida", HeaderText = "Saída", Width = 130 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Tempo", HeaderText = "Tempo", Width = 80 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Valor", HeaderText = "Valor Pago", Width = 100 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Atendente", HeaderText = "Atendente (Entrada)", Width = 130 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Caixa", HeaderText = "Caixa (Pagamento)", Width = 130 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "SessionId", Visible = false });
    }

    protected override async void OnShown(EventArgs e)
    {
        base.OnShown(e);
        await LoadHistoryAsync();
    }

    private async Task LoadHistoryAsync()
    {
        try
        {
            var filter = new HistoryFilter
            {
                StartDate = _chkUseDateFilter.Checked ? _dtpStart.Value.Date : null,
                EndDate = _chkUseDateFilter.Checked ? _dtpEnd.Value.Date : null,
                Plate = string.IsNullOrWhiteSpace(_txtPlate.Text) ? null : _txtPlate.Text.Trim(),
                TicketNumber = string.IsNullOrWhiteSpace(_txtTicket.Text) ? null : _txtTicket.Text.Trim(),
                CustomerName = string.IsNullOrWhiteSpace(_txtCustomer.Text) ? null : _txtCustomer.Text.Trim(),
                VehicleType = _cmbVehicleType.SelectedIndex > 0 ? (VehicleType)(_cmbVehicleType.SelectedIndex - 1) : null
            };

            using var scope = _serviceProvider.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IParkingService>();

            _history = await service.GetHistoryAsync(filter);
            PopulateGrid();

            var (totalRevenue, totalVehicles) = await service.GetHistorySummaryAsync(filter);
            _lblTotalVehicles.Text = totalVehicles.ToString();

            var brCulture = CultureInfo.GetCultureInfo("pt-BR");
            _lblTotalRevenue.Text = totalRevenue.ToString("C2", brCulture);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao carregar histórico");
            MessageBox.Show("Erro ao carregar histórico de estacionamento.", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void PopulateGrid()
    {
        _grid.Rows.Clear();
        var brCulture = CultureInfo.GetCultureInfo("pt-BR");

        foreach (var session in _history)
        {
            var duration = session.ExitDateTime.HasValue
                ? session.ExitDateTime.Value - session.EntryDateTime
                : TimeSpan.Zero;

            _grid.Rows.Add(
                session.TicketNumber,
                session.Plate,
                session.VehicleType.ToDisplayName(),
                session.VehicleModel ?? "—",
                session.CustomerName ?? "—",
                session.CustomerPhone ?? "—",
                session.ServiceType ?? "—",
                session.EntryDateTime.ToString("dd/MM/yyyy HH:mm"),
                session.ExitDateTime?.ToString("dd/MM/yyyy HH:mm") ?? "—",
                $"{(int)duration.TotalHours:D2}:{duration.Minutes:D2}",
                (session.FinalAmount ?? 0m).ToString("C2", brCulture),
                session.EntryUsername ?? "—",
                session.CheckoutUsername ?? "—",
                session.Id
            );
        }
    }

    private async void BtnReprint_Click(object? sender, EventArgs e)
    {
        if (_grid.CurrentRow is null || _grid.CurrentRow.Index < 0)
        {
            MessageBox.Show("Selecione um registro no histórico para reimprimir.", "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (_grid.CurrentRow.Cells["SessionId"].Value is not long sessionId) return;
        var session = _history.FirstOrDefault(s => s.Id == sessionId);

        if (session is null) return;

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var parkingService = scope.ServiceProvider.GetRequiredService<IParkingService>();
            var printerService = scope.ServiceProvider.GetRequiredService<IPrinterService>();

            var receipt = await parkingService.BuildReceiptAsync(session);
            await printerService.PrintExitReceiptAsync(receipt);

            MessageBox.Show("Comprovante enviado para a impressora com sucesso!", "Reimpressão", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Falha ao reimprimir comprovante.\n\n{ex.Message}", "Erro de Impressão", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void HistoryForm_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape)
        {
            Close();
        }
    }
}
