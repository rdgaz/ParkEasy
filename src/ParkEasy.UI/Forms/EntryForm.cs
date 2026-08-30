using System.Globalization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ParkEasy.Application;
using ParkEasy.Application.Configuration;
using ParkEasy.Application.Interfaces;
using ParkEasy.Application.Services;
using ParkEasy.Domain.Enums;

namespace ParkEasy.UI.Forms;

public class EntryForm : Form
{
    private static readonly CultureInfo BrCulture = CultureInfo.GetCultureInfo("pt-BR");

    private const int OptionalBlockHeight = 152;

    private readonly IParkingService _parkingService;
    private readonly IPrinterService _printerService;
    private readonly IParkingFeeCalculator _feeCalculator;
    private readonly WashPricingSettings _washPricing;
    private readonly List<string> _washTypeKeys;
    private readonly ILogger<EntryForm> _logger;

    private TextBox _txtPlate = null!;
    private Label _lblPlateHint = null!;
    private ComboBox _cmbVehicleType = null!;
    private ComboBox _cmbServiceType = null!;
    private TextBox _txtAmount = null!;
    private TextBox _txtNotes = null!;
    private TextBox _txtModel = null!;
    private TextBox _txtCustomer = null!;
    private TextBox _txtPhone = null!;
    private Button _btnSave = null!;
    private Button _btnCancel = null!;

    private readonly List<Control> _optionalServiceControls = [];
    private readonly List<Control> _trailingControls = [];

    private int _trailingTopCollapsed;
    private int _trailingTopExpanded;
    private int _formHeightCollapsed;
    private int _formHeightExpanded;

    private string? _lastLookedUpPlate;
    private bool _loading;

    public EntryForm(
        IParkingService parkingService,
        IPrinterService printerService,
        IParkingFeeCalculator feeCalculator,
        IOptions<WashPricingSettings> washPricingOptions,
        ILogger<EntryForm> logger)
    {
        _parkingService = parkingService;
        _printerService = printerService;
        _feeCalculator = feeCalculator;
        _washPricing = washPricingOptions.Value;
        _washTypeKeys = _washPricing.Keys.ToList();
        _logger = logger;

        InitializeComponent();
    }

    private void InitializeComponent()
    {
        SuspendLayout();

        Text = "Registrar Entrada";
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
        var lblTitle = Theme.CreateLabel("REGISTRAR ENTRADA", Theme.FontTitle, Theme.Primary);
        lblTitle.Location = new Point(24, top);
        panel.Controls.Add(lblTitle);
        top += 40;

        // Placa (Required)
        var lblPlate = Theme.CreateLabel("Placa (Obrigatório):", Theme.FontMedium);
        lblPlate.Location = new Point(24, top);
        panel.Controls.Add(lblPlate);
        top += 24;

        _txtPlate = Theme.CreateInput(390);
        _txtPlate.Location = new Point(24, top);
        _txtPlate.CharacterCasing = CharacterCasing.Upper;
        _txtPlate.Font = Theme.FontTitle;
        _txtPlate.Height = 40;
        _txtPlate.PlaceholderText = "ABC1D23";
        _txtPlate.TextChanged += TxtPlate_TextChanged;
        panel.Controls.Add(_txtPlate);
        top += 40;

        _lblPlateHint = Theme.CreateLabel(string.Empty, Theme.FontGrid, Theme.Success);
        _lblPlateHint.Location = new Point(24, top);
        _lblPlateHint.AutoSize = true;
        panel.Controls.Add(_lblPlateHint);
        top += 24;

        // Tipo de Veículo (Required)
        var lblVehicleType = Theme.CreateLabel("Tipo de Veículo (Obrigatório):", Theme.FontMedium);
        lblVehicleType.Location = new Point(24, top);
        panel.Controls.Add(lblVehicleType);
        top += 24;

        _cmbVehicleType = new ComboBox
        {
            Location = new Point(24, top),
            Size = new Size(390, Theme.InputHeight),
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = Theme.SurfaceLight,
            ForeColor = Theme.TextPrimary,
            Font = Theme.FontNormal,
            FlatStyle = FlatStyle.Flat
        };
        _cmbVehicleType.Items.Add("Moto");
        _cmbVehicleType.Items.Add("Carro");
        _cmbVehicleType.Items.Add("Vaga Dupla");
        _cmbVehicleType.SelectedIndex = (int)VehicleType.Carro;
        _cmbVehicleType.SelectedIndexChanged += CmbVehicleType_SelectedIndexChanged;
        panel.Controls.Add(_cmbVehicleType);
        top += 44;

        // Tipo Serviço (Required) — sempre visível
        var lblServiceType = Theme.CreateLabel("Tipo Serviço (Obrigatório):", Theme.FontMedium);
        lblServiceType.Location = new Point(24, top);
        panel.Controls.Add(lblServiceType);
        top += 24;

        _cmbServiceType = new ComboBox
        {
            Location = new Point(24, top),
            Size = new Size(390, Theme.InputHeight),
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = Theme.SurfaceLight,
            ForeColor = Theme.TextPrimary,
            Font = Theme.FontNormal,
            FlatStyle = FlatStyle.Flat
        };
        _cmbServiceType.Items.Add(ServiceTypeNames.Hora);
        _cmbServiceType.Items.Add(ServiceTypeNames.Diaria);
        _cmbServiceType.Items.Add(ServiceTypeNames.Mensal);
        foreach (var washType in _washTypeKeys)
        {
            _cmbServiceType.Items.Add(washType);
        }
        _cmbServiceType.Items.Add(ServiceTypeNames.Personalizada);
        _cmbServiceType.SelectedIndexChanged += CmbServiceType_SelectedIndexChanged;
        panel.Controls.Add(_cmbServiceType);
        top += 44;

        var topBeforeOptional = top;

        // Valor + Observação — só aparecem quando Tipo Serviço != Hora
        var lblAmount = Theme.CreateLabel("Valor (R$):", Theme.FontMedium);
        lblAmount.Location = new Point(24, top);
        _optionalServiceControls.Add(lblAmount);
        top += 24;

        _txtAmount = Theme.CreateInput(390);
        _txtAmount.Location = new Point(24, top);
        _optionalServiceControls.Add(_txtAmount);
        top += 44;

        var lblNotes = Theme.CreateLabel("Observação (Opcional):", Theme.FontMedium);
        lblNotes.Location = new Point(24, top);
        _optionalServiceControls.Add(lblNotes);
        top += 24;

        _txtNotes = new TextBox
        {
            Location = new Point(24, top),
            Size = new Size(390, 70),
            Multiline = true,
            BackColor = Theme.SurfaceLight,
            ForeColor = Theme.TextPrimary,
            Font = Theme.FontNormal,
            BorderStyle = BorderStyle.FixedSingle
        };
        _optionalServiceControls.Add(_txtNotes);
        top += 84;

        foreach (var control in _optionalServiceControls)
        {
            panel.Controls.Add(control);
        }

        _trailingTopExpanded = topBeforeOptional + OptionalBlockHeight;
        _trailingTopCollapsed = topBeforeOptional;
        top = _trailingTopExpanded;

        // Modelo (Optional)
        var lblModel = Theme.CreateLabel("Modelo do veículo (Opcional):", Theme.FontMedium);
        lblModel.Location = new Point(24, top);
        _trailingControls.Add(lblModel);
        top += 24;

        _txtModel = Theme.CreateInput(390);
        _txtModel.Location = new Point(24, top);
        _txtModel.PlaceholderText = "Ex: Toyota Corolla";
        _trailingControls.Add(_txtModel);
        top += 44;

        // Cliente (Optional)
        var lblCustomer = Theme.CreateLabel("Nome do cliente (Opcional):", Theme.FontMedium);
        lblCustomer.Location = new Point(24, top);
        _trailingControls.Add(lblCustomer);
        top += 24;

        _txtCustomer = Theme.CreateInput(390);
        _txtCustomer.Location = new Point(24, top);
        _txtCustomer.PlaceholderText = "Ex: João da Silva";
        _trailingControls.Add(_txtCustomer);
        top += 44;

        // Telefone (Optional)
        var lblPhone = Theme.CreateLabel("Telefone do cliente (Opcional):", Theme.FontMedium);
        lblPhone.Location = new Point(24, top);
        _trailingControls.Add(lblPhone);
        top += 24;

        _txtPhone = Theme.CreateInput(390);
        _txtPhone.Location = new Point(24, top);
        _txtPhone.PlaceholderText = "Ex: (53) 99999-9999";
        _trailingControls.Add(_txtPhone);
        top += 54;

        // Buttons
        _btnSave = Theme.CreateSuccessButton("REGISTRAR ENTRADA", 220);
        _btnSave.Location = new Point(24, top);
        _btnSave.Click += BtnSave_Click;
        _trailingControls.Add(_btnSave);

        _btnCancel = Theme.CreateSecondaryButton("CANCELAR", 150);
        _btnCancel.Location = new Point(264, top);
        _btnCancel.Click += (_, _) => DialogResult = DialogResult.Cancel;
        _trailingControls.Add(_btnCancel);

        foreach (var control in _trailingControls)
        {
            panel.Controls.Add(control);
        }

        top += 60;
        _formHeightExpanded = top;
        _formHeightCollapsed = top - OptionalBlockHeight;

        Controls.Add(panel);

        KeyDown += EntryForm_KeyDown;
        AcceptButton = _btnSave;
        CancelButton = _btnCancel;

        ResumeLayout(false);

        _cmbServiceType.SelectedIndex = 0; // Hora — aplica o layout colapsado inicial
    }

    private void SetOptionalServiceVisible(bool visible)
    {
        foreach (var control in _optionalServiceControls)
        {
            control.Visible = visible;
        }

        var delta = (visible ? _trailingTopExpanded : _trailingTopCollapsed)
            - _trailingControls[0].Location.Y;

        if (delta != 0)
        {
            foreach (var control in _trailingControls)
            {
                control.Location = new Point(control.Location.X, control.Location.Y + delta);
            }
        }

        Size = new Size(460, visible ? _formHeightExpanded : _formHeightCollapsed);
    }

    private void CmbServiceType_SelectedIndexChanged(object? sender, EventArgs e)
    {
        var serviceType = _cmbServiceType.SelectedItem as string ?? ServiceTypeNames.Hora;
        var isHora = serviceType == ServiceTypeNames.Hora;

        SetOptionalServiceVisible(!isHora);

        if (isHora || _loading)
            return;

        var vehicleType = (VehicleType)_cmbVehicleType.SelectedIndex;

        if (serviceType is ServiceTypeNames.Diaria or ServiceTypeNames.Mensal)
        {
            _txtAmount.Text = _feeCalculator.GetFlatRate(vehicleType, serviceType).ToString("N2", BrCulture);
        }
        else if (_washPricing.TryGetValue(serviceType, out var config))
        {
            _txtAmount.Text = config.Price.ToString("N2", BrCulture);
        }
        else
        {
            _txtAmount.Text = string.Empty;
        }
    }

    private void CmbVehicleType_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_loading) return;

        var serviceType = _cmbServiceType.SelectedItem as string;
        if (serviceType is ServiceTypeNames.Diaria or ServiceTypeNames.Mensal)
        {
            var vehicleType = (VehicleType)_cmbVehicleType.SelectedIndex;
            _txtAmount.Text = _feeCalculator.GetFlatRate(vehicleType, serviceType).ToString("N2", BrCulture);
        }
    }

    private async void TxtPlate_TextChanged(object? sender, EventArgs e)
    {
        var normalized = PlateNormalizer.Normalize(_txtPlate.Text);

        if (normalized.Length != 7)
        {
            _lastLookedUpPlate = null;
            _lblPlateHint.Text = string.Empty;
            return;
        }

        if (!PlateNormalizer.IsValid(normalized) || normalized == _lastLookedUpPlate)
            return;

        await LookupPlateHistoryAsync(normalized);
    }

    private async Task LookupPlateHistoryAsync(string normalizedPlate)
    {
        _lastLookedUpPlate = normalizedPlate;

        try
        {
            var previous = await _parkingService.FindMostRecentByPlateAsync(normalizedPlate);

            if (previous is null)
            {
                _lblPlateHint.Text = string.Empty;
                return;
            }

            _loading = true;
            _cmbVehicleType.SelectedIndex = (int)previous.VehicleType;
            _loading = false;
            _txtModel.Text = previous.VehicleModel ?? string.Empty;
            _txtCustomer.Text = previous.CustomerName ?? string.Empty;
            _txtPhone.Text = previous.CustomerPhone ?? string.Empty;

            _lblPlateHint.Text = "✓ Registro anterior encontrado.";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Erro ao buscar histórico da placa {Plate}", normalizedPlate);
        }
    }

    private async void BtnSave_Click(object? sender, EventArgs e)
    {
        var rawPlate = _txtPlate.Text.Trim();
        if (string.IsNullOrWhiteSpace(rawPlate))
        {
            MessageBox.Show("Informe a placa do veículo.", "Validação", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _txtPlate.Focus();
            return;
        }

        var normalizedPlate = PlateNormalizer.Normalize(rawPlate);
        if (!PlateNormalizer.IsValid(normalizedPlate))
        {
            MessageBox.Show("A placa informada não é válida. Use o formato ABC1234 ou ABC1D23.", "Validação", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _txtPlate.Focus();
            return;
        }

        var serviceType = _cmbServiceType.SelectedItem as string ?? ServiceTypeNames.Hora;
        var isHora = serviceType == ServiceTypeNames.Hora;
        decimal? serviceAmount = null;

        if (!isHora)
        {
            if (!decimal.TryParse(_txtAmount.Text, NumberStyles.Number, BrCulture, out var amount) || amount <= 0)
            {
                MessageBox.Show("Informe um valor de serviço válido, maior que zero.", "Validação", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _txtAmount.Focus();
                return;
            }

            serviceAmount = amount;
        }

        _btnSave.Enabled = false;
        _btnCancel.Enabled = false;

        try
        {
            var vehicleType = (VehicleType)_cmbVehicleType.SelectedIndex;

            // 1. Save entry to database first
            var session = await _parkingService.RegisterEntryAsync(
                normalizedPlate,
                vehicleType,
                _txtModel.Text,
                _txtCustomer.Text,
                _txtPhone.Text,
                serviceType,
                serviceAmount,
                _txtNotes.Text
            );

            // 2. Build ticket DTO
            var ticket = await _parkingService.BuildTicketAsync(session);

            // 3. Print ticket (independent operation - failure does NOT rollback DB)
            await PrintTicketWithRetryAsync(ticket);

            DialogResult = DialogResult.OK;
        }
        catch (InvalidOperationException ex)
        {
            MessageBox.Show(ex.Message, "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _btnSave.Enabled = true;
            _btnCancel.Enabled = true;
        }
        catch (ArgumentException ex)
        {
            MessageBox.Show(ex.Message, "Validação", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _btnSave.Enabled = true;
            _btnCancel.Enabled = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro inesperado ao registrar entrada");
            MessageBox.Show($"Não foi possível salvar a operação.\n\nDetalhes: {ex.Message}", "Erro de Banco", MessageBoxButtons.OK, MessageBoxIcon.Error);
            _btnSave.Enabled = true;
            _btnCancel.Enabled = true;
        }
    }

    private async Task PrintTicketWithRetryAsync(Application.DTOs.ParkingTicket ticket)
    {
        while (true)
        {
            try
            {
                await _printerService.PrintEntryTicketAsync(ticket);
                break; // Print successful
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Falha na impressão do ticket {TicketNumber}", ticket.TicketNumber);

                var result = MessageBox.Show(
                    $"Entrada registrada com sucesso!\n\nPorém não foi possível imprimir o ticket.\nVerifique a impressora.\n\nDetalhes: {ex.Message}",
                    "Falha de Impressão",
                    MessageBoxButtons.RetryCancel,
                    MessageBoxIcon.Warning);

                if (result == DialogResult.Cancel)
                {
                    // User chose to continue without printing
                    break;
                }
                // Retry requested -> loop continues
            }
        }
    }

    private void EntryForm_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape)
        {
            DialogResult = DialogResult.Cancel;
        }
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        _txtPlate.Focus();
    }
}
