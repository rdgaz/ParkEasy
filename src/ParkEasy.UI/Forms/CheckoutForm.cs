using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ParkEasy.Application.Interfaces;
using ParkEasy.Domain.Entities;
using ParkEasy.Domain.Enums;

namespace ParkEasy.UI.Forms;

public class CheckoutForm : Form
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IParkingFeeCalculator _feeCalculator;
    private readonly ILogger<CheckoutForm> _logger;

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public long SessionId { get; set; }

    private ParkingSession? _session;
    private Label _lblTicket = null!;
    private Label _lblPlate = null!;
    private Label _lblVehicleType = null!;
    private Label _lblModel = null!;
    private Label _lblCustomer = null!;
    private Label _lblEntry = null!;
    private Label _lblExit = null!;
    private Label _lblDuration = null!;
    private Label _lblWash = null!;
    private Label _lblAmount = null!;
    private Button _btnConfirm = null!;
    private Button _btnCancel = null!;

    public CheckoutForm(
        IServiceProvider serviceProvider,
        IParkingFeeCalculator feeCalculator,
        ILogger<CheckoutForm> logger)
    {
        _serviceProvider = serviceProvider;
        _feeCalculator = feeCalculator;
        _logger = logger;

        InitializeComponent();
    }

    private void InitializeComponent()
    {
        SuspendLayout();

        Text = "Finalizar Estacionamento";
        Size = new Size(460, 614);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        Theme.ApplyTo(this);
        KeyPreview = true;

        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(24),
            BackColor = Theme.Background
        };

        int top = 16;

        // Title
        var lblTitle = Theme.CreateLabel("FINALIZAR ESTACIONAMENTO", Theme.FontTitle, Theme.Warning);
        lblTitle.Location = new Point(24, top);
        panel.Controls.Add(lblTitle);
        top += 40;

        // Details card panel
        var card = new Panel
        {
            Location = new Point(24, top),
            Size = new Size(390, 312),
            BackColor = Theme.Surface,
            Padding = new Padding(16)
        };

        int cardTop = 16;

        // Ticket & Plate line
        var lblTicketHead = Theme.CreateLabel("Ticket:", Theme.FontNormal, Theme.TextSecondary);
        lblTicketHead.Location = new Point(16, cardTop);
        card.Controls.Add(lblTicketHead);

        _lblTicket = Theme.CreateLabel("000000", Theme.FontLarge, Theme.TextPrimary);
        _lblTicket.Location = new Point(70, cardTop - 3);
        card.Controls.Add(_lblTicket);

        var lblPlateHead = Theme.CreateLabel("Placa:", Theme.FontNormal, Theme.TextSecondary);
        lblPlateHead.Location = new Point(220, cardTop);
        card.Controls.Add(lblPlateHead);

        _lblPlate = Theme.CreateLabel("ABC1D23", Theme.FontLarge, Theme.Primary);
        _lblPlate.Location = new Point(270, cardTop - 3);
        card.Controls.Add(_lblPlate);

        cardTop += 34;

        // Vehicle type
        var lblTypeHead = Theme.CreateLabel("Tipo:", Theme.FontNormal, Theme.TextSecondary);
        lblTypeHead.Location = new Point(16, cardTop);
        card.Controls.Add(lblTypeHead);

        _lblVehicleType = Theme.CreateLabel("—", Theme.FontMedium, Theme.TextPrimary);
        _lblVehicleType.Location = new Point(104, cardTop);
        card.Controls.Add(_lblVehicleType);

        cardTop += 28;

        // Model & Customer
        var lblModelHead = Theme.CreateLabel("Modelo:", Theme.FontNormal, Theme.TextSecondary);
        lblModelHead.Location = new Point(16, cardTop);
        card.Controls.Add(lblModelHead);

        _lblModel = Theme.CreateLabel("—", Theme.FontMedium, Theme.TextPrimary);
        _lblModel.Location = new Point(104, cardTop);
        card.Controls.Add(_lblModel);

        cardTop += 28;

        var lblCustHead = Theme.CreateLabel("Cliente:", Theme.FontNormal, Theme.TextSecondary);
        lblCustHead.Location = new Point(16, cardTop);
        card.Controls.Add(lblCustHead);

        _lblCustomer = Theme.CreateLabel("—", Theme.FontMedium, Theme.TextPrimary);
        _lblCustomer.Location = new Point(104, cardTop);
        card.Controls.Add(_lblCustomer);

        cardTop += 34;

        // Divider
        var divider = new Panel
        {
            Location = new Point(16, cardTop),
            Size = new Size(358, 1),
            BackColor = Theme.SurfaceLight
        };
        card.Controls.Add(divider);
        cardTop += 12;

        // Entry, Exit & Duration
        var lblEntHead = Theme.CreateLabel("Entrada:", Theme.FontNormal, Theme.TextSecondary);
        lblEntHead.Location = new Point(16, cardTop);
        card.Controls.Add(lblEntHead);

        _lblEntry = Theme.CreateLabel("00/00/0000 00:00:00", Theme.FontNormal, Theme.TextPrimary);
        _lblEntry.Location = new Point(104, cardTop);
        card.Controls.Add(_lblEntry);

        cardTop += 24;

        var lblExitHead = Theme.CreateLabel("Saída:", Theme.FontNormal, Theme.TextSecondary);
        lblExitHead.Location = new Point(16, cardTop);
        card.Controls.Add(lblExitHead);

        _lblExit = Theme.CreateLabel("00/00/0000 00:00:00", Theme.FontNormal, Theme.TextPrimary);
        _lblExit.Location = new Point(104, cardTop);
        card.Controls.Add(_lblExit);

        cardTop += 24;

        var lblDurHead = Theme.CreateLabel("Tempo:", Theme.FontNormal, Theme.TextSecondary);
        lblDurHead.Location = new Point(16, cardTop);
        card.Controls.Add(lblDurHead);

        _lblDuration = Theme.CreateLabel("00:00:00", Theme.FontMedium, Theme.Info);
        _lblDuration.Location = new Point(104, cardTop);
        card.Controls.Add(_lblDuration);

        cardTop += 24;

        var lblWashHead = Theme.CreateLabel("Lavagem:", Theme.FontNormal, Theme.TextSecondary);
        lblWashHead.Location = new Point(16, cardTop);
        card.Controls.Add(lblWashHead);

        _lblWash = Theme.CreateLabel("—", Theme.FontMedium, Theme.Success);
        _lblWash.AutoSize = false;
        _lblWash.Size = new Size(270, 40);
        _lblWash.Location = new Point(104, cardTop);
        card.Controls.Add(_lblWash);

        panel.Controls.Add(card);
        top += 332;

        // Amount Box
        var amountPanel = new Panel
        {
            Location = new Point(24, top),
            Size = new Size(390, 80),
            BackColor = Color.FromArgb(20, 83, 45), // Dark green container
            Padding = new Padding(12)
        };

        var lblAmountTitle = Theme.CreateLabel("VALOR A PAGAR", Theme.FontNormal, Color.FromArgb(187, 247, 208));
        lblAmountTitle.Location = new Point(135, 10);
        amountPanel.Controls.Add(lblAmountTitle);

        _lblAmount = Theme.CreateLabel("R$ 0,00", Theme.FontHuge, Color.White);
        _lblAmount.AutoSize = false;
        _lblAmount.Size = new Size(366, 40);
        _lblAmount.Location = new Point(12, 32);
        _lblAmount.TextAlign = ContentAlignment.MiddleCenter;
        amountPanel.Controls.Add(_lblAmount);

        panel.Controls.Add(amountPanel);
        top += 96;

        // Buttons
        _btnConfirm = Theme.CreateSuccessButton("CONFIRMAR PAGAMENTO", 220);
        _btnConfirm.Location = new Point(24, top);
        _btnConfirm.Click += BtnConfirm_Click;
        panel.Controls.Add(_btnConfirm);

        _btnCancel = Theme.CreateSecondaryButton("CANCELAR", 150);
        _btnCancel.Location = new Point(264, top);
        _btnCancel.Click += (_, _) => DialogResult = DialogResult.Cancel;
        panel.Controls.Add(_btnCancel);

        Controls.Add(panel);

        KeyDown += CheckoutForm_KeyDown;
        AcceptButton = _btnConfirm;
        CancelButton = _btnCancel;

        ResumeLayout(false);
    }

    protected override async void OnShown(EventArgs e)
    {
        base.OnShown(e);
        await LoadSessionDataAsync();
    }

    private async Task LoadSessionDataAsync()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<Domain.Interfaces.IParkingSessionRepository>();
            _session = await repo.GetByIdAsync(SessionId);

            if (_session is null)
            {
                MessageBox.Show("Sessão de estacionamento não encontrada.", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
                DialogResult = DialogResult.Cancel;
                return;
            }

            var now = DateTime.Now;
            var elapsed = now - _session.EntryDateTime;
            var fee = _feeCalculator.CalculateFee(_session.EntryDateTime, now, _session.VehicleType, _session.WashAmount.HasValue);
            var brCulture = CultureInfo.GetCultureInfo("pt-BR");

            _lblTicket.Text = _session.TicketNumber;
            _lblPlate.Text = _session.Plate;
            _lblVehicleType.Text = _session.VehicleType.ToDisplayName();
            _lblModel.Text = _session.VehicleModel ?? "—";
            _lblCustomer.Text = !string.IsNullOrWhiteSpace(_session.CustomerPhone)
                ? $"{_session.CustomerName ?? "—"} ({_session.CustomerPhone})"
                : (_session.CustomerName ?? "—");
            _lblEntry.Text = _session.EntryDateTime.ToString("dd/MM/yyyy HH:mm:ss");
            _lblExit.Text = now.ToString("dd/MM/yyyy HH:mm:ss");
            _lblDuration.Text = $"{(int)elapsed.TotalHours:D2}:{elapsed.Minutes:D2}:{elapsed.Seconds:D2}";

            var washAmount = _session.WashAmount ?? 0;
            _lblWash.Text = !string.IsNullOrWhiteSpace(_session.WashTypeName)
                ? $"{_session.WashTypeName} — {washAmount.ToString("C2", brCulture)}"
                : "—";

            _lblAmount.Text = (fee + washAmount).ToString("C2", brCulture);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao carregar sessão para checkout");
            MessageBox.Show("Erro ao carregar dados do estacionamento.", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
            DialogResult = DialogResult.Cancel;
        }
    }

    private async void BtnConfirm_Click(object? sender, EventArgs e)
    {
        if (_session is null) return;

        _btnConfirm.Enabled = false;
        _btnCancel.Enabled = false;

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var parkingService = scope.ServiceProvider.GetRequiredService<IParkingService>();
            var printerService = scope.ServiceProvider.GetRequiredService<IPrinterService>();

            // 1. Finalize session in DB
            var completedSession = await parkingService.FinalizeSessionAsync(_session.Id);

            // 2. Build receipt DTO
            var receipt = await parkingService.BuildReceiptAsync(completedSession);

            // 3. Print receipt with retry handling
            await PrintReceiptWithRetryAsync(printerService, receipt);

            DialogResult = DialogResult.OK;
        }
        catch (InvalidOperationException ex)
        {
            MessageBox.Show(ex.Message, "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _btnConfirm.Enabled = true;
            _btnCancel.Enabled = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao finalizar estacionamento");
            MessageBox.Show($"Não foi possível finalizar a operação.\n\nDetalhes: {ex.Message}", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
            _btnConfirm.Enabled = true;
            _btnCancel.Enabled = true;
        }
    }

    private async Task PrintReceiptWithRetryAsync(IPrinterService printerService, Application.DTOs.ParkingReceipt receipt)
    {
        while (true)
        {
            try
            {
                await printerService.PrintExitReceiptAsync(receipt);
                break; // Successful
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Falha na impressão do comprovante {TicketNumber}", receipt.TicketNumber);

                var result = MessageBox.Show(
                    $"Pagamento confirmado e sessão encerrada!\n\nPorém não foi possível imprimir o comprovante de saída.\nVerifique a impressora.\n\nDetalhes: {ex.Message}",
                    "Falha de Impressão",
                    MessageBoxButtons.RetryCancel,
                    MessageBoxIcon.Warning);

                if (result == DialogResult.Cancel)
                {
                    break; // Continue without printing
                }
            }
        }
    }

    private void CheckoutForm_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape)
        {
            DialogResult = DialogResult.Cancel;
        }
    }
}
