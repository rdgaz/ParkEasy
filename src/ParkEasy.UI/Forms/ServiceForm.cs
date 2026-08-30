using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ParkEasy.Application.Configuration;
using ParkEasy.Application.Interfaces;
using ParkEasy.Domain.Entities;
using ParkEasy.Domain.Enums;

namespace ParkEasy.UI.Forms;

/// <summary>
/// Edita o serviço de uma sessão já criada (aberta pela fila de lavagens ou pela grid
/// principal) — a criação de uma nova visita, com seu Tipo Serviço, acontece no EntryForm.
/// </summary>
public class ServiceForm : Form
{
    private static readonly CultureInfo BrCulture = CultureInfo.GetCultureInfo("pt-BR");

    private const string PersonalizadaLabel = "Personalizada (valor específico)";

    private readonly IServiceProvider _serviceProvider;
    private readonly WashPricingSettings _washPricing;
    private readonly ILogger<ServiceForm> _logger;
    private readonly List<string> _washTypeKeys;

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public long SessionId { get; set; }

    private ParkingSession? _session;
    private bool _loadingSession;

    private Label _lblTicketPlate = null!;
    private ComboBox _cmbType = null!;
    private Label _lblAverageTime = null!;
    private TextBox _txtAmount = null!;
    private TextBox _txtNotes = null!;
    private Button _btnSave = null!;
    private Button _btnRemove = null!;
    private Button _btnCancel = null!;

    public ServiceForm(
        IServiceProvider serviceProvider,
        IOptions<WashPricingSettings> washPricingOptions,
        ILogger<ServiceForm> logger)
    {
        _serviceProvider = serviceProvider;
        _washPricing = washPricingOptions.Value;
        _washTypeKeys = _washPricing.Keys.ToList();
        _logger = logger;

        InitializeComponent();
    }

    private void InitializeComponent()
    {
        SuspendLayout();

        Text = "Serviço";
        Size = new Size(460, 500);
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

        var lblTitle = Theme.CreateLabel("EDITAR SERVIÇO", Theme.FontTitle, Theme.Primary);
        lblTitle.Location = new Point(24, top);
        panel.Controls.Add(lblTitle);
        top += 32;

        _lblTicketPlate = Theme.CreateLabel("Ticket — Placa", Theme.FontMedium, Theme.TextSecondary);
        _lblTicketPlate.Location = new Point(24, top);
        panel.Controls.Add(_lblTicketPlate);
        top += 40;

        var lblType = Theme.CreateLabel("Tipo de Lavagem:", Theme.FontMedium);
        lblType.Location = new Point(24, top);
        panel.Controls.Add(lblType);
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
        panel.Controls.Add(_cmbType);
        top += 40;

        _lblAverageTime = Theme.CreateLabel(string.Empty, Theme.FontGrid, Theme.TextMuted);
        _lblAverageTime.Location = new Point(24, top);
        _lblAverageTime.AutoSize = true;
        panel.Controls.Add(_lblAverageTime);
        top += 24;

        var lblAmount = Theme.CreateLabel("Valor (R$):", Theme.FontMedium);
        lblAmount.Location = new Point(24, top);
        panel.Controls.Add(lblAmount);
        top += 24;

        _txtAmount = Theme.CreateInput(390);
        _txtAmount.Location = new Point(24, top);
        panel.Controls.Add(_txtAmount);
        top += 44;

        var lblNotes = Theme.CreateLabel("Observação (Opcional):", Theme.FontMedium);
        lblNotes.Location = new Point(24, top);
        panel.Controls.Add(lblNotes);
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
        panel.Controls.Add(_txtNotes);
        top += 84;

        _btnSave = Theme.CreateSuccessButton("SALVAR", 140);
        _btnSave.Location = new Point(24, top);
        _btnSave.Click += BtnSave_Click;
        panel.Controls.Add(_btnSave);

        _btnRemove = Theme.CreateDangerButton("REMOVER SERVIÇO", 200);
        _btnRemove.Location = new Point(174, top);
        _btnRemove.Click += BtnRemove_Click;
        panel.Controls.Add(_btnRemove);

        _btnCancel = Theme.CreateSecondaryButton("CANCELAR", 130);
        _btnCancel.Location = new Point(24, top + 50);
        _btnCancel.Click += (_, _) => DialogResult = DialogResult.Cancel;
        panel.Controls.Add(_btnCancel);

        Controls.Add(panel);

        KeyDown += ServiceForm_KeyDown;
        AcceptButton = _btnSave;
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

            _lblTicketPlate.Text = $"Ticket {_session.TicketNumber} — Placa {_session.Plate}";

            _loadingSession = true;

            if (!string.IsNullOrWhiteSpace(_session.ServiceType))
            {
                var index = _washTypeKeys.IndexOf(_session.ServiceType);
                _cmbType.SelectedIndex = index >= 0 ? index : _washTypeKeys.Count; // não encontrado -> cai em "Personalizada"
                _txtAmount.Text = (_session.ServiceAmount ?? 0).ToString("N2", BrCulture);
                _txtNotes.Text = _session.ServiceNotes ?? string.Empty;
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
            _logger.LogError(ex, "Erro ao carregar sessão para serviço");
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
        if (_session is null) return;

        if (!decimal.TryParse(_txtAmount.Text, NumberStyles.Number, BrCulture, out var amount) || amount <= 0)
        {
            MessageBox.Show("Informe um valor válido, maior que zero.", "Validação", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _txtAmount.Focus();
            return;
        }

        var index = _cmbType.SelectedIndex;
        var serviceTypeName = (index < 0 || index >= _washTypeKeys.Count) ? "Personalizada" : _washTypeKeys[index];

        _btnSave.Enabled = false;
        _btnRemove.Enabled = false;
        _btnCancel.Enabled = false;

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var parkingService = scope.ServiceProvider.GetRequiredService<IParkingService>();

            await parkingService.AddOrUpdateWashServiceAsync(_session.Id, serviceTypeName, amount, _txtNotes.Text);

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
            _logger.LogError(ex, "Erro ao salvar serviço");
            MessageBox.Show($"Não foi possível salvar o serviço.\n\nDetalhes: {ex.Message}", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
            _btnSave.Enabled = true;
            _btnRemove.Enabled = true;
            _btnCancel.Enabled = true;
        }
    }

    private async void BtnRemove_Click(object? sender, EventArgs e)
    {
        if (_session is null) return;

        var confirm = MessageBox.Show(
            "Deseja realmente remover o serviço deste veículo?",
            "Remover Serviço",
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
            _logger.LogError(ex, "Erro ao remover serviço");
            MessageBox.Show($"Não foi possível remover o serviço.\n\nDetalhes: {ex.Message}", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
            _btnSave.Enabled = true;
            _btnRemove.Enabled = true;
            _btnCancel.Enabled = true;
        }
    }

    private void ServiceForm_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape)
        {
            DialogResult = DialogResult.Cancel;
        }
    }
}
