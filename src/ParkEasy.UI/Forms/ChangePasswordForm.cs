using Microsoft.Extensions.Logging;
using ParkEasy.Application.Interfaces;

namespace ParkEasy.UI.Forms;

public class ChangePasswordForm : Form
{
    private readonly IAuthService _authService;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly ILogger<ChangePasswordForm> _logger;

    /// <summary>
    /// Usuário cuja senha será trocada. Deixe null (padrão) para trocar a própria senha do
    /// usuário logado — nesse caso a senha atual é exigida. Quando definido para OUTRO usuário,
    /// é uma redefinição administrativa (sem pedir a senha atual do alvo); quem chama deve ter
    /// certeza de que o usuário logado tem autoridade — a checagem final acontece no serviço.
    /// </summary>
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public long? TargetUserId { get; set; }

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public string? TargetUsername { get; set; }

    private bool IsSelfService => TargetUserId is null || TargetUserId == _currentUserContext.UserId;

    private Label _lblTitle = null!;
    private Label _lblCurrentHead = null!;
    private TextBox _txtCurrentPassword = null!;
    private TextBox _txtNewPassword = null!;
    private TextBox _txtConfirmPassword = null!;
    private Button _btnSave = null!;
    private Button _btnCancel = null!;

    public ChangePasswordForm(
        IAuthService authService,
        ICurrentUserContext currentUserContext,
        ILogger<ChangePasswordForm> logger)
    {
        _authService = authService;
        _currentUserContext = currentUserContext;
        _logger = logger;

        InitializeComponent();
    }

    private void InitializeComponent()
    {
        SuspendLayout();

        Text = "Alterar Senha";
        Size = new Size(420, 420);
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

        _lblTitle = Theme.CreateLabel("ALTERAR MINHA SENHA", Theme.FontTitle, Theme.Primary);
        _lblTitle.Location = new Point(24, top);
        panel.Controls.Add(_lblTitle);
        top += 44;

        _lblCurrentHead = Theme.CreateLabel("Senha atual:", Theme.FontMedium);
        _lblCurrentHead.Location = new Point(24, top);
        panel.Controls.Add(_lblCurrentHead);
        top += 24;

        _txtCurrentPassword = Theme.CreateInput(360);
        _txtCurrentPassword.Location = new Point(24, top);
        _txtCurrentPassword.UseSystemPasswordChar = true;
        panel.Controls.Add(_txtCurrentPassword);
        top += 44;

        var lblNew = Theme.CreateLabel("Nova senha (mín. 6 caracteres):", Theme.FontMedium);
        lblNew.Location = new Point(24, top);
        panel.Controls.Add(lblNew);
        top += 24;

        _txtNewPassword = Theme.CreateInput(360);
        _txtNewPassword.Location = new Point(24, top);
        _txtNewPassword.UseSystemPasswordChar = true;
        panel.Controls.Add(_txtNewPassword);
        top += 44;

        var lblConfirm = Theme.CreateLabel("Confirmar nova senha:", Theme.FontMedium);
        lblConfirm.Location = new Point(24, top);
        panel.Controls.Add(lblConfirm);
        top += 24;

        _txtConfirmPassword = Theme.CreateInput(360);
        _txtConfirmPassword.Location = new Point(24, top);
        _txtConfirmPassword.UseSystemPasswordChar = true;
        panel.Controls.Add(_txtConfirmPassword);
        top += 54;

        _btnSave = Theme.CreateSuccessButton("SALVAR", 170);
        _btnSave.Location = new Point(24, top);
        _btnSave.Click += async (_, _) => await BtnSave_ClickAsync();
        panel.Controls.Add(_btnSave);

        _btnCancel = Theme.CreateSecondaryButton("CANCELAR", 150);
        _btnCancel.Location = new Point(204, top);
        _btnCancel.Click += (_, _) => DialogResult = DialogResult.Cancel;
        panel.Controls.Add(_btnCancel);

        Controls.Add(panel);

        KeyDown += ChangePasswordForm_KeyDown;
        AcceptButton = _btnSave;
        CancelButton = _btnCancel;

        ResumeLayout(false);
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);

        if (IsSelfService)
        {
            Text = "Alterar Minha Senha";
            _lblTitle.Text = "ALTERAR MINHA SENHA";
            _lblCurrentHead.Visible = true;
            _txtCurrentPassword.Visible = true;
        }
        else
        {
            Text = $"Redefinir Senha — {TargetUsername}";
            _lblTitle.Text = $"REDEFINIR SENHA: {TargetUsername?.ToUpperInvariant()}";
            _lblCurrentHead.Visible = false;
            _txtCurrentPassword.Visible = false;
        }
    }

    private async Task BtnSave_ClickAsync()
    {
        if (_currentUserContext.UserId is not long actingUserId)
        {
            MessageBox.Show("Nenhum usuário autenticado.", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
            DialogResult = DialogResult.Cancel;
            return;
        }

        var targetUserId = TargetUserId ?? actingUserId;

        if (_txtNewPassword.Text != _txtConfirmPassword.Text)
        {
            MessageBox.Show("A confirmação não corresponde à nova senha.", "Validação", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _btnSave.Enabled = false;
        _btnCancel.Enabled = false;

        try
        {
            var currentPassword = IsSelfService ? _txtCurrentPassword.Text : null;
            await _authService.ChangePasswordAsync(actingUserId, targetUserId, currentPassword, _txtNewPassword.Text);

            MessageBox.Show("Senha alterada com sucesso!", "Sucesso", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
            _logger.LogError(ex, "Erro ao trocar senha do usuário {UserId}", targetUserId);
            MessageBox.Show($"Não foi possível alterar a senha.\n\nDetalhes: {ex.Message}", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
            _btnSave.Enabled = true;
            _btnCancel.Enabled = true;
        }
    }

    private void ChangePasswordForm_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape)
        {
            DialogResult = DialogResult.Cancel;
        }
    }
}
