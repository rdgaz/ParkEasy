using Microsoft.Extensions.Logging;
using ParkEasy.Application.Interfaces;

namespace ParkEasy.UI.Forms;

public class LoginForm : Form
{
    private readonly IAuthService _authService;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly ILogger<LoginForm> _logger;

    private TextBox _txtUsername = null!;
    private TextBox _txtPassword = null!;
    private Label _lblError = null!;
    private Button _btnLogin = null!;
    private Button _btnExit = null!;

    public LoginForm(
        IAuthService authService,
        ICurrentUserContext currentUserContext,
        ILogger<LoginForm> logger)
    {
        _authService = authService;
        _currentUserContext = currentUserContext;
        _logger = logger;

        InitializeComponent();
    }

    private void InitializeComponent()
    {
        SuspendLayout();

        Text = "ParkEasy — Login";
        Size = new Size(420, 380);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        Theme.ApplyTo(this);
        KeyPreview = true;

        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(28),
            BackColor = Theme.Background
        };

        int top = 20;

        var lblTitle = Theme.CreateLabel("PARKEASY", Theme.FontHuge, Theme.Primary);
        lblTitle.Location = new Point(28, top);
        panel.Controls.Add(lblTitle);
        top += 40;

        var lblSubtitle = Theme.CreateLabel("Entre com seu usuário e senha", Theme.FontNormal, Theme.TextSecondary);
        lblSubtitle.Location = new Point(28, top);
        panel.Controls.Add(lblSubtitle);
        top += 40;

        var lblUser = Theme.CreateLabel("Usuário:", Theme.FontMedium);
        lblUser.Location = new Point(28, top);
        panel.Controls.Add(lblUser);
        top += 24;

        _txtUsername = Theme.CreateInput(360);
        _txtUsername.Location = new Point(28, top);
        panel.Controls.Add(_txtUsername);
        top += 44;

        var lblPassword = Theme.CreateLabel("Senha:", Theme.FontMedium);
        lblPassword.Location = new Point(28, top);
        panel.Controls.Add(lblPassword);
        top += 24;

        _txtPassword = Theme.CreateInput(360);
        _txtPassword.Location = new Point(28, top);
        _txtPassword.UseSystemPasswordChar = true;
        panel.Controls.Add(_txtPassword);
        top += 40;

        _lblError = Theme.CreateLabel(string.Empty, Theme.FontGrid, Theme.Danger);
        _lblError.Location = new Point(28, top);
        _lblError.AutoSize = false;
        _lblError.Size = new Size(360, 32);
        panel.Controls.Add(_lblError);
        top += 36;

        _btnLogin = Theme.CreateSuccessButton("ENTRAR", 170);
        _btnLogin.Location = new Point(28, top);
        _btnLogin.Click += async (_, _) => await AttemptLoginAsync();
        panel.Controls.Add(_btnLogin);

        _btnExit = Theme.CreateSecondaryButton("SAIR", 150);
        _btnExit.Location = new Point(208, top);
        _btnExit.Click += (_, _) => DialogResult = DialogResult.Cancel;
        panel.Controls.Add(_btnExit);

        Controls.Add(panel);

        KeyDown += LoginForm_KeyDown;
        AcceptButton = _btnLogin;
        CancelButton = _btnExit;

        ResumeLayout(false);
    }

    private async Task AttemptLoginAsync()
    {
        var username = _txtUsername.Text.Trim();
        var password = _txtPassword.Text;

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            _lblError.Text = "Informe usuário e senha.";
            return;
        }

        _btnLogin.Enabled = false;
        _btnExit.Enabled = false;
        _lblError.Text = string.Empty;

        try
        {
            var user = await _authService.AuthenticateAsync(username, password);

            if (user is null)
            {
                _lblError.Text = "Usuário ou senha inválidos.";
                _txtPassword.Clear();
                _txtPassword.Focus();
                return;
            }

            _currentUserContext.SignIn(user.Id, user.Username, user.Role);
            DialogResult = DialogResult.OK;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao autenticar usuário {Username}", username);
            _lblError.Text = "Erro ao tentar entrar. Tente novamente.";
        }
        finally
        {
            _btnLogin.Enabled = true;
            _btnExit.Enabled = true;
        }
    }

    private void LoginForm_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape)
        {
            DialogResult = DialogResult.Cancel;
        }
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        _txtUsername.Focus();
    }
}
