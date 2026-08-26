using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ParkEasy.Application.Configuration;
using ParkEasy.Application.Interfaces;
using ParkEasy.Domain.Entities;

namespace ParkEasy.UI.Forms;

public class WashForm : Form
{
    private static readonly CultureInfo BrCulture = CultureInfo.GetCultureInfo("pt-BR");

    private readonly IServiceProvider _serviceProvider;
    private readonly WashPricingSettings _washPricing;
    private readonly ILogger<WashForm> _logger;

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public long SessionId { get; set; }

    private const string PersonalizadaLabel = "Personalizada (valor específico)";

    private ParkingSession? _session;
    private bool _loadingSession;
    private readonly List<string> _washTypeKeys;

    private Label _lblTicketPlate = null!;
    private ComboBox _cmbType = null!;
    private TextBox _txtAmount = null!;
    private TextBox _txtNotes = null!;
    private Button _btnSave = null!;
    private Button _btnRemove = null!;
    private Button _btnCancel = null!;

    public WashForm(
        IServiceProvider serviceProvider,
        IOptions<WashPricingSettings> washPricingOptions,
        ILogger<WashForm> logger)
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

        Text = "Lavagem";
        Size = new Size(460, 480);
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

        _lblTicketPlate = Theme.CreateLabel("Ticket — Placa", Theme.FontMedium, Theme.TextSecondary);
        _lblTicketPlate.Location = new Point(24, top);
        panel.Controls.Add(_lblTicketPlate);
        top += 40;

        // Tipo de Lavagem
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
        top += 44;

        // Valor
        var lblAmount = Theme.CreateLabel("Valor (R$):", Theme.FontMedium);
        lblAmount.Location = new Point(24, top);
        panel.Controls.Add(lblAmount);
        top += 24;

        _txtAmount = Theme.CreateInput(390);
        _txtAmount.Location = new Point(24, top);
        panel.Controls.Add(_txtAmount);
        top += 44;

        // Observação
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

        // Buttons
        _btnSave = Theme.CreateSuccessButton("SALVAR", 140);
        _btnSave.Location = new Point(24, top);
        _btnSave.Click += BtnSave_Click;
        panel.Controls.Add(_btnSave);

        _btnRemove = Theme.CreateDangerButton("REMOVER LAVAGEM", 200);
        _btnRemove.Location = new Point(174, top);
        _btnRemove.Click += BtnRemove_Click;
        panel.Controls.Add(_btnRemove);

        _btnCancel = Theme.CreateSecondaryButton("CANCELAR", 130);
        _btnCancel.Location = new Point(24, top + 50);
        _btnCancel.Click += (_, _) => DialogResult = DialogResult.Cancel;
        panel.Controls.Add(_btnCancel);

        Controls.Add(panel);

        KeyDown += WashForm_KeyDown;
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

        _txtAmount.Text = isPersonalizada
            ? string.Empty
            : _washPricing[_washTypeKeys[index]].ToString("N2", BrCulture);
    }

    private async void BtnSave_Click(object? sender, EventArgs e)
    {
        if (_session is null) return;

        if (!decimal.TryParse(_txtAmount.Text, NumberStyles.Number, BrCulture, out var amount) || amount <= 0)
        {
            MessageBox.Show("Informe um valor de lavagem válido, maior que zero.", "Validação", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _txtAmount.Focus();
            return;
        }

        _btnSave.Enabled = false;
        _btnRemove.Enabled = false;
        _btnCancel.Enabled = false;

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var parkingService = scope.ServiceProvider.GetRequiredService<IParkingService>();

            var index = _cmbType.SelectedIndex;
            var washTypeName = (index < 0 || index >= _washTypeKeys.Count) ? "Personalizada" : _washTypeKeys[index];
            await parkingService.AddOrUpdateWashServiceAsync(_session.Id, washTypeName, amount, _txtNotes.Text);

            DialogResult = DialogResult.OK;
        }
        catch (InvalidOperationException ex)
        {
            MessageBox.Show(ex.Message, "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
