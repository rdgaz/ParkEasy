using Microsoft.Extensions.Logging;
using ParkEasy.Application.Interfaces;
using ParkEasy.Application.Services;

namespace ParkEasy.UI.Forms;

public class EntryForm : Form
{
    private readonly IParkingService _parkingService;
    private readonly IPrinterService _printerService;
    private readonly ILogger<EntryForm> _logger;

    private TextBox _txtPlate = null!;
    private TextBox _txtModel = null!;
    private TextBox _txtCustomer = null!;
    private TextBox _txtPhone = null!;
    private Button _btnSave = null!;
    private Button _btnCancel = null!;

    public EntryForm(
        IParkingService parkingService,
        IPrinterService printerService,
        ILogger<EntryForm> logger)
    {
        _parkingService = parkingService;
        _printerService = printerService;
        _logger = logger;

        InitializeComponent();
    }

    private void InitializeComponent()
    {
        SuspendLayout();

        Text = "Registrar Entrada";
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
        top += 48;

        // Modelo (Optional)
        var lblModel = Theme.CreateLabel("Modelo do veículo (Opcional):", Theme.FontMedium);
        lblModel.Location = new Point(24, top);
        panel.Controls.Add(lblModel);
        top += 24;

        _txtModel = Theme.CreateInput(390);
        _txtModel.Location = new Point(24, top);
        _txtModel.PlaceholderText = "Ex: Toyota Corolla";
        panel.Controls.Add(_txtModel);
        top += 44;

        // Cliente (Optional)
        var lblCustomer = Theme.CreateLabel("Nome do cliente (Opcional):", Theme.FontMedium);
        lblCustomer.Location = new Point(24, top);
        panel.Controls.Add(lblCustomer);
        top += 24;

        _txtCustomer = Theme.CreateInput(390);
        _txtCustomer.Location = new Point(24, top);
        _txtCustomer.PlaceholderText = "Ex: João da Silva";
        panel.Controls.Add(_txtCustomer);
        top += 44;

        // Telefone (Optional)
        var lblPhone = Theme.CreateLabel("Telefone do cliente (Opcional):", Theme.FontMedium);
        lblPhone.Location = new Point(24, top);
        panel.Controls.Add(lblPhone);
        top += 24;

        _txtPhone = Theme.CreateInput(390);
        _txtPhone.Location = new Point(24, top);
        _txtPhone.PlaceholderText = "Ex: (53) 99999-9999";
        panel.Controls.Add(_txtPhone);
        top += 54;

        // Buttons
        _btnSave = Theme.CreateSuccessButton("REGISTRAR ENTRADA", 220);
        _btnSave.Location = new Point(24, top);
        _btnSave.Click += BtnSave_Click;
        panel.Controls.Add(_btnSave);

        _btnCancel = Theme.CreateSecondaryButton("CANCELAR", 150);
        _btnCancel.Location = new Point(264, top);
        _btnCancel.Click += (_, _) => DialogResult = DialogResult.Cancel;
        panel.Controls.Add(_btnCancel);

        Controls.Add(panel);

        KeyDown += EntryForm_KeyDown;
        AcceptButton = _btnSave;
        CancelButton = _btnCancel;

        ResumeLayout(false);
    }

    private void TxtPlate_TextChanged(object? sender, EventArgs e)
    {
        // Auto format plate uppercase clean
        var cursor = _txtPlate.SelectionStart;
        var clean = PlateNormalizer.Normalize(_txtPlate.Text);
        // keep text reasonably un-mangled while typing
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

        _btnSave.Enabled = false;
        _btnCancel.Enabled = false;

        try
        {
            // 1. Save entry to database first
            var session = await _parkingService.RegisterEntryAsync(
                normalizedPlate,
                _txtModel.Text,
                _txtCustomer.Text,
                _txtPhone.Text
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
