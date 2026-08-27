using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ParkEasy.Application.Configuration;
using ParkEasy.Application.DTOs;
using ParkEasy.Application.Interfaces;
using ParkEasy.Application.Services;
using ParkEasy.Domain.Entities;
using ParkEasy.Domain.Enums;

namespace ParkEasy.UI.Forms;

public class WashForm : Form
{
    private static readonly CultureInfo BrCulture = CultureInfo.GetCultureInfo("pt-BR");

    private const string PersonalizadaLabel = "Personalizada (valor específico)";

    private const int SharedSectionTopModeA = 88;
    private const int SharedSectionTopModeB = 420;
    private const int FormHeightModeA = 500;
    private const int FormHeightModeB = 840;

    private readonly IServiceProvider _serviceProvider;
    private readonly IPrinterService _printerService;
    private readonly WashPricingSettings _washPricing;
    private readonly ILogger<WashForm> _logger;
    private readonly List<string> _washTypeKeys;

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public long SessionId { get; set; }

    private ParkingSession? _session;
    private bool _loadingSession;
    private string? _lastLookedUpPlate;
    private int _currentSharedTop = SharedSectionTopModeB;

    private readonly List<Control> _sharedSectionControls = [];
    private readonly List<Control> _newVisitorControls = [];

    // Modo edição (SessionId != 0)
    private Label _lblTicketPlate = null!;

    // Modo nova visita (SessionId == 0) — igual ao EntryForm
    private TextBox _txtPlate = null!;
    private Label _lblPlateHint = null!;
    private ComboBox _cmbVehicleType = null!;
    private TextBox _txtModel = null!;
    private TextBox _txtCustomer = null!;
    private TextBox _txtPhone = null!;

    // Seção compartilhada (tipo/valor/observação de lavagem)
    private ComboBox _cmbType = null!;
    private Label _lblAverageTime = null!;
    private TextBox _txtAmount = null!;
    private TextBox _txtNotes = null!;
    private Button _btnSave = null!;
    private Button _btnRemove = null!;
    private Button _btnCancel = null!;

    public WashForm(
        IServiceProvider serviceProvider,
        IPrinterService printerService,
        IOptions<WashPricingSettings> washPricingOptions,
        ILogger<WashForm> logger)
    {
        _serviceProvider = serviceProvider;
        _printerService = printerService;
        _washPricing = washPricingOptions.Value;
        _washTypeKeys = _washPricing.Keys.ToList();
        _logger = logger;

        InitializeComponent();
    }

    private void InitializeComponent()
    {
        SuspendLayout();

        Text = "Lavagem";
        Size = new Size(460, FormHeightModeB);
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
        var lblTitle = Theme.CreateLabel("SERVIÇO DE LAVAGEM", Theme.FontTitle, Theme.Primary);
        lblTitle.Location = new Point(24, top);
        panel.Controls.Add(lblTitle);
        top += 32;

        var groupTop = top;

        // --- Modo edição: ticket/placa (somente leitura) ---
        _lblTicketPlate = Theme.CreateLabel("Ticket — Placa", Theme.FontMedium, Theme.TextSecondary);
        _lblTicketPlate.Location = new Point(24, groupTop);
        _lblTicketPlate.Visible = false;
        panel.Controls.Add(_lblTicketPlate);

        // --- Modo nova visita: mesmos campos do "Nova Entrada" ---
        top = groupTop;

        var lblPlate = Theme.CreateLabel("Placa (Obrigatório):", Theme.FontMedium);
        lblPlate.Location = new Point(24, top);
        _newVisitorControls.Add(lblPlate);
        top += 24;

        _txtPlate = Theme.CreateInput(390);
        _txtPlate.Location = new Point(24, top);
        _txtPlate.CharacterCasing = CharacterCasing.Upper;
        _txtPlate.PlaceholderText = "ABC1D23";
        _txtPlate.TextChanged += TxtPlate_TextChanged;
        _newVisitorControls.Add(_txtPlate);
        top += 40;

        _lblPlateHint = Theme.CreateLabel(string.Empty, Theme.FontGrid, Theme.Success);
        _lblPlateHint.Location = new Point(24, top);
        _lblPlateHint.AutoSize = true;
        _newVisitorControls.Add(_lblPlateHint);
        top += 24;

        var lblVehicleType = Theme.CreateLabel("Tipo de Veículo (Obrigatório):", Theme.FontMedium);
        lblVehicleType.Location = new Point(24, top);
        _newVisitorControls.Add(lblVehicleType);
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
        _newVisitorControls.Add(_cmbVehicleType);
        top += 44;

        var lblModel = Theme.CreateLabel("Modelo do veículo (Opcional):", Theme.FontMedium);
        lblModel.Location = new Point(24, top);
        _newVisitorControls.Add(lblModel);
        top += 24;

        _txtModel = Theme.CreateInput(390);
        _txtModel.Location = new Point(24, top);
        _txtModel.PlaceholderText = "Ex: Toyota Corolla";
        _newVisitorControls.Add(_txtModel);
        top += 44;

        var lblCustomer = Theme.CreateLabel("Nome do cliente (Opcional):", Theme.FontMedium);
        lblCustomer.Location = new Point(24, top);
        _newVisitorControls.Add(lblCustomer);
        top += 24;

        _txtCustomer = Theme.CreateInput(390);
        _txtCustomer.Location = new Point(24, top);
        _txtCustomer.PlaceholderText = "Ex: João da Silva";
        _newVisitorControls.Add(_txtCustomer);
        top += 44;

        var lblPhone = Theme.CreateLabel("Telefone do cliente (Opcional):", Theme.FontMedium);
        lblPhone.Location = new Point(24, top);
        _newVisitorControls.Add(lblPhone);
        top += 24;

        _txtPhone = Theme.CreateInput(390);
        _txtPhone.Location = new Point(24, top);
        _txtPhone.PlaceholderText = "Ex: (53) 99999-9999";
        _newVisitorControls.Add(_txtPhone);
        top += 44;

        foreach (var control in _newVisitorControls)
        {
            control.Visible = false;
            panel.Controls.Add(control);
        }

        // --- Seção compartilhada: tipo/valor/observação de lavagem ---
        top = SharedSectionTopModeB;

        var lblType = Theme.CreateLabel("Tipo de Lavagem:", Theme.FontMedium);
        lblType.Location = new Point(24, top);
        _sharedSectionControls.Add(lblType);
        top += 24;

        _cmbType = new ComboBox
        {
            Location = new Point(24, top),
            Size = new Size(390, Theme.InputHeight),
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = Theme.SurfaceLight,
            ForeColor = Theme.TextPrimary,
            Font = Theme.FontNormal,
            FlatStyle = FlatStyle.Flat
        };
        foreach (var washType in _washTypeKeys)
        {
            _cmbType.Items.Add(washType);
        }
        _cmbType.Items.Add(PersonalizadaLabel);
        _cmbType.SelectedIndexChanged += CmbType_SelectedIndexChanged;
        _sharedSectionControls.Add(_cmbType);
        top += 40;

        _lblAverageTime = Theme.CreateLabel(string.Empty, Theme.FontGrid, Theme.TextMuted);
        _lblAverageTime.Location = new Point(24, top);
        _lblAverageTime.AutoSize = true;
        _sharedSectionControls.Add(_lblAverageTime);
        top += 24;

        var lblAmount = Theme.CreateLabel("Valor (R$):", Theme.FontMedium);
        lblAmount.Location = new Point(24, top);
        _sharedSectionControls.Add(lblAmount);
        top += 24;

        _txtAmount = Theme.CreateInput(390);
        _txtAmount.Location = new Point(24, top);
        _sharedSectionControls.Add(_txtAmount);
        top += 44;

        var lblNotes = Theme.CreateLabel("Observação (Opcional):", Theme.FontMedium);
        lblNotes.Location = new Point(24, top);
        _sharedSectionControls.Add(lblNotes);
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
        _sharedSectionControls.Add(_txtNotes);
        top += 84;

        _btnSave = Theme.CreateSuccessButton("SALVAR", 140);
        _btnSave.Location = new Point(24, top);
        _btnSave.Click += BtnSave_Click;
        _sharedSectionControls.Add(_btnSave);

        _btnRemove = Theme.CreateDangerButton("REMOVER LAVAGEM", 200);
        _btnRemove.Location = new Point(174, top);
        _btnRemove.Click += BtnRemove_Click;
        _sharedSectionControls.Add(_btnRemove);

        _btnCancel = Theme.CreateSecondaryButton("CANCELAR", 130);
        _btnCancel.Location = new Point(24, top + 50);
        _btnCancel.Click += (_, _) => DialogResult = DialogResult.Cancel;
        _sharedSectionControls.Add(_btnCancel);

        foreach (var control in _sharedSectionControls)
        {
            panel.Controls.Add(control);
        }

        Controls.Add(panel);

        KeyDown += WashForm_KeyDown;
        AcceptButton = _btnSave;
        CancelButton = _btnCancel;

        ResumeLayout(false);
    }

    private void ShiftSharedSectionTo(int newTop)
    {
        var delta = newTop - _currentSharedTop;
        if (delta == 0) return;

        foreach (var control in _sharedSectionControls)
        {
            control.Location = new Point(control.Location.X, control.Location.Y + delta);
        }

        _currentSharedTop = newTop;
    }

    protected override async void OnShown(EventArgs e)
    {
        base.OnShown(e);

        if (SessionId == 0)
        {
            SetupNewVisitorMode();
        }
        else
        {
            await LoadSessionDataAsync();
        }
    }

    private void SetupNewVisitorMode()
    {
        _lblTicketPlate.Visible = false;
        foreach (var control in _newVisitorControls)
        {
            control.Visible = true;
        }

        _cmbVehicleType.SelectedIndex = (int)VehicleType.Carro;
        _btnRemove.Visible = false;

        // Sem sessão prévia pra proteger — deixa o evento disparar normalmente pra
        // preencher valor sugerido/tempo médio do tipo selecionado por padrão.
        _cmbType.SelectedIndex = _washTypeKeys.Count > 0 ? 0 : _cmbType.Items.Count - 1;

        ShiftSharedSectionTo(SharedSectionTopModeB);
        Size = new Size(460, FormHeightModeB);
        PositionCancelButton(sideBySideWithSave: true);

        _txtPlate.Focus();
    }

    /// <summary>
    /// "Remover Lavagem" só aparece no modo edição — quando ele está oculto (nova visita),
    /// dá pra colocar o Cancelar ao lado do Salvar em vez de numa segunda linha.
    /// </summary>
    private void PositionCancelButton(bool sideBySideWithSave)
    {
        _btnCancel.Location = sideBySideWithSave
            ? new Point(174, _btnSave.Location.Y)
            : new Point(24, _btnSave.Location.Y + 50);
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
            using var scope = _serviceProvider.CreateScope();
            var parkingService = scope.ServiceProvider.GetRequiredService<IParkingService>();
            var previous = await parkingService.FindMostRecentByPlateAsync(normalizedPlate);

            if (previous is null)
            {
                _lblPlateHint.Text = string.Empty;
                return;
            }

            _cmbVehicleType.SelectedIndex = (int)previous.VehicleType;
            _txtModel.Text = previous.VehicleModel ?? string.Empty;
            _txtCustomer.Text = previous.CustomerName ?? string.Empty;
            _txtPhone.Text = previous.CustomerPhone ?? string.Empty;

            _lblPlateHint.Text = "✓ Dados preenchidos a partir do cadastro anterior desta placa.";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Erro ao buscar histórico da placa {Plate}", normalizedPlate);
        }
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

            _lblTicketPlate.Text = $"Ticket {_session.TicketNumber} — Placa {_session.Plate}";
            _lblTicketPlate.Visible = true;
            foreach (var control in _newVisitorControls)
            {
                control.Visible = false;
            }

            ShiftSharedSectionTo(SharedSectionTopModeA);
            Size = new Size(460, FormHeightModeA);
            PositionCancelButton(sideBySideWithSave: false);

            _loadingSession = true;

            if (!string.IsNullOrWhiteSpace(_session.WashTypeName))
            {
                var index = _washTypeKeys.IndexOf(_session.WashTypeName);
                _cmbType.SelectedIndex = index >= 0 ? index : _washTypeKeys.Count; // não encontrado -> cai em "Personalizada"
                _txtAmount.Text = (_session.WashAmount ?? 0).ToString("N2", BrCulture);
                _txtNotes.Text = _session.WashNotes ?? string.Empty;
                _btnRemove.Visible = true;
            }
            else
            {
                _cmbType.SelectedIndex = _washTypeKeys.Count > 0 ? 0 : _cmbType.Items.Count - 1;
                _btnRemove.Visible = false;
            }

            _loadingSession = false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao carregar sessão para lavagem");
            MessageBox.Show("Erro ao carregar dados do estacionamento.", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
            DialogResult = DialogResult.Cancel;
        }
    }

    private void CmbType_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_loadingSession) return;

        var index = _cmbType.SelectedIndex;
        var isPersonalizada = index < 0 || index >= _washTypeKeys.Count;

        if (isPersonalizada)
        {
            _txtAmount.Text = string.Empty;
            _lblAverageTime.Text = string.Empty;
        }
        else
        {
            var config = _washPricing[_washTypeKeys[index]];
            _txtAmount.Text = config.Price.ToString("N2", BrCulture);
            _lblAverageTime.Text = config.AverageMinutes > 0 ? $"Tempo médio estimado: {config.AverageMinutes} min" : string.Empty;
        }
    }

    private async void BtnSave_Click(object? sender, EventArgs e)
    {
        if (!decimal.TryParse(_txtAmount.Text, NumberStyles.Number, BrCulture, out var amount) || amount <= 0)
        {
            MessageBox.Show("Informe um valor de lavagem válido, maior que zero.", "Validação", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _txtAmount.Focus();
            return;
        }

        string? normalizedPlate = null;

        if (SessionId == 0)
        {
            var rawPlate = _txtPlate.Text.Trim();
            if (string.IsNullOrWhiteSpace(rawPlate))
            {
                MessageBox.Show("Informe a placa do veículo.", "Validação", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _txtPlate.Focus();
                return;
            }

            normalizedPlate = PlateNormalizer.Normalize(rawPlate);
            if (!PlateNormalizer.IsValid(normalizedPlate))
            {
                MessageBox.Show("A placa informada não é válida. Use o formato ABC1234 ou ABC1D23.", "Validação", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _txtPlate.Focus();
                return;
            }
        }

        var index = _cmbType.SelectedIndex;
        var washTypeName = (index < 0 || index >= _washTypeKeys.Count) ? "Personalizada" : _washTypeKeys[index];

        _btnSave.Enabled = false;
        _btnRemove.Enabled = false;
        _btnCancel.Enabled = false;

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var parkingService = scope.ServiceProvider.GetRequiredService<IParkingService>();

            ParkingSession session;

            if (SessionId == 0)
            {
                var existingActive = await parkingService.GetActiveSessionByPlateAsync(normalizedPlate!);

                if (existingActive is not null)
                {
                    if (existingActive.WashStatus is not null)
                    {
                        MessageBox.Show(
                            "Este veículo já tem uma lavagem em andamento ou pendente.",
                            "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        _btnSave.Enabled = true;
                        _btnRemove.Enabled = true;
                        _btnCancel.Enabled = true;
                        return;
                    }

                    session = existingActive;
                }
                else
                {
                    var vehicleType = (VehicleType)_cmbVehicleType.SelectedIndex;
                    session = await parkingService.RegisterEntryAsync(
                        normalizedPlate!, vehicleType, _txtModel.Text, _txtCustomer.Text, _txtPhone.Text);

                    var ticket = await parkingService.BuildTicketAsync(session);
                    await PrintTicketWithRetryAsync(ticket);
                }
            }
            else
            {
                session = _session!;
            }

            await parkingService.AddOrUpdateWashServiceAsync(session.Id, washTypeName, amount, _txtNotes.Text);

            DialogResult = DialogResult.OK;
        }
        catch (InvalidOperationException ex)
        {
            MessageBox.Show(ex.Message, "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _btnSave.Enabled = true;
            _btnRemove.Enabled = true;
            _btnCancel.Enabled = true;
        }
        catch (ArgumentException ex)
        {
            MessageBox.Show(ex.Message, "Validação", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _btnSave.Enabled = true;
            _btnRemove.Enabled = true;
            _btnCancel.Enabled = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao salvar lavagem");
            MessageBox.Show($"Não foi possível salvar a lavagem.\n\nDetalhes: {ex.Message}", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
            _btnSave.Enabled = true;
            _btnRemove.Enabled = true;
            _btnCancel.Enabled = true;
        }
    }

    private async Task PrintTicketWithRetryAsync(ParkingTicket ticket)
    {
        while (true)
        {
            try
            {
                await _printerService.PrintEntryTicketAsync(ticket);
                break;
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
                    break;
            }
        }
    }

    private async void BtnRemove_Click(object? sender, EventArgs e)
    {
        if (_session is null) return;

        var confirm = MessageBox.Show(
            "Deseja realmente remover a lavagem deste veículo?",
            "Remover Lavagem",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (confirm != DialogResult.Yes) return;

        _btnSave.Enabled = false;
        _btnRemove.Enabled = false;
        _btnCancel.Enabled = false;

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var parkingService = scope.ServiceProvider.GetRequiredService<IParkingService>();
            await parkingService.RemoveWashServiceAsync(_session.Id);

            DialogResult = DialogResult.OK;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao remover lavagem");
            MessageBox.Show($"Não foi possível remover a lavagem.\n\nDetalhes: {ex.Message}", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
            _btnSave.Enabled = true;
            _btnRemove.Enabled = true;
            _btnCancel.Enabled = true;
        }
    }

    private void WashForm_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape)
        {
            DialogResult = DialogResult.Cancel;
        }
    }
}
