using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ParkEasy.Application.Interfaces;
using ParkEasy.Domain.Entities;
using ParkEasy.Domain.Enums;

namespace ParkEasy.UI.Forms;

public class ManageUsersForm : Form
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IAuthService _authService;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly ILogger<ManageUsersForm> _logger;

    private List<User> _users = [];

    private DataGridView _grid = null!;
    private Button _btnResetPassword = null!;

    private TextBox _txtUsername = null!;
    private TextBox _txtPassword = null!;
    private TextBox _txtConfirmPassword = null!;
    private ComboBox _cmbRole = null!;
    private Button _btnCreate = null!;
    private Label _lblError = null!;

    public ManageUsersForm(
        IServiceProvider serviceProvider,
        IAuthService authService,
        ICurrentUserContext currentUserContext,
        ILogger<ManageUsersForm> logger)
    {
        _serviceProvider = serviceProvider;
        _authService = authService;
        _currentUserContext = currentUserContext;
        _logger = logger;

        InitializeComponent();
    }

    private void InitializeComponent()
    {
        SuspendLayout();

        Text = "Gerenciar Usuários";
        Size = new Size(640, 780);
        MinimumSize = new Size(560, 700);
        StartPosition = FormStartPosition.CenterParent;
        Theme.ApplyTo(this);
        KeyPreview = true;

        var mainPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(Theme.Padding),
            BackColor = Theme.Background
        };

        var lblTitle = Theme.CreateLabel("USUÁRIOS DO SISTEMA", Theme.FontTitle, Theme.Primary);
        lblTitle.Dock = DockStyle.Top;
        lblTitle.Height = 32;
        mainPanel.Controls.Add(lblTitle);

        // Grid de usuários existentes
        _grid = new DataGridView
        {
            Dock = DockStyle.Top,
            Height = 220
        };
        Theme.StyleDataGridView(_grid);
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Username", HeaderText = "Usuário", FillWeight = 150 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Role", HeaderText = "Cargo", FillWeight = 120 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "CreatedAt", HeaderText = "Criado em", FillWeight = 130 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "UserId", Visible = false });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "RoleValue", Visible = false });
        _grid.SelectionChanged += (_, _) => UpdateResetButtonState();
        mainPanel.Controls.Add(_grid);

        var actionRow = new Panel { Dock = DockStyle.Top, Height = 48, Padding = new Padding(0, 8, 0, 8) };
        _btnResetPassword = Theme.CreateSecondaryButton("REDEFINIR SENHA DO SELECIONADO", 320);
        _btnResetPassword.Location = new Point(0, 8);
        _btnResetPassword.Enabled = false;
        _btnResetPassword.Click += (_, _) => OpenResetPasswordForSelected();
        actionRow.Controls.Add(_btnResetPassword);
        mainPanel.Controls.Add(actionRow);

        var divider = new Panel { Dock = DockStyle.Top, Height = 1, BackColor = Theme.SurfaceLight, Margin = new Padding(0, 8, 0, 8) };
        mainPanel.Controls.Add(divider);

        // Seção "Criar novo usuário"
        var createPanel = new Panel { Dock = DockStyle.Top, Height = 400, Padding = new Padding(0, 16, 0, 0) };

        var lblCreateTitle = Theme.CreateLabel("CRIAR NOVO USUÁRIO", Theme.FontLarge, Theme.TextPrimary);
        lblCreateTitle.Location = new Point(0, 0);
        createPanel.Controls.Add(lblCreateTitle);

        int top = 36;

        var lblUsername = Theme.CreateLabel("Usuário:", Theme.FontMedium);
        lblUsername.Location = new Point(0, top);
        createPanel.Controls.Add(lblUsername);
        top += 24;

        _txtUsername = Theme.CreateInput(390);
        _txtUsername.Location = new Point(0, top);
        createPanel.Controls.Add(_txtUsername);
        top += 44;

        var lblPassword = Theme.CreateLabel("Senha (mín. 6 caracteres):", Theme.FontMedium);
        lblPassword.Location = new Point(0, top);
        createPanel.Controls.Add(lblPassword);
        top += 24;

        _txtPassword = Theme.CreateInput(390);
        _txtPassword.Location = new Point(0, top);
        _txtPassword.UseSystemPasswordChar = true;
        createPanel.Controls.Add(_txtPassword);
        top += 44;

        var lblConfirm = Theme.CreateLabel("Confirmar senha:", Theme.FontMedium);
        lblConfirm.Location = new Point(0, top);
        createPanel.Controls.Add(lblConfirm);
        top += 24;

        _txtConfirmPassword = Theme.CreateInput(390);
        _txtConfirmPassword.Location = new Point(0, top);
        _txtConfirmPassword.UseSystemPasswordChar = true;
        createPanel.Controls.Add(_txtConfirmPassword);
        top += 44;

        var lblRole = Theme.CreateLabel("Cargo:", Theme.FontMedium);
        lblRole.Location = new Point(0, top);
        createPanel.Controls.Add(lblRole);
        top += 24;

        _cmbRole = new ComboBox
        {
            Location = new Point(0, top),
            Size = new Size(390, Theme.InputHeight),
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = Theme.SurfaceLight,
            ForeColor = Theme.TextPrimary,
            Font = Theme.FontNormal,
            FlatStyle = FlatStyle.Flat,
            FormattingEnabled = true
        };
        _cmbRole.Format += (_, args) =>
        {
            if (args.ListItem is UserRole role)
                args.Value = role.ToDisplayName();
        };
        createPanel.Controls.Add(_cmbRole);
        top += 44;

        _lblError = Theme.CreateLabel(string.Empty, Theme.FontGrid, Theme.Danger);
        _lblError.Location = new Point(0, top);
        _lblError.AutoSize = false;
        _lblError.Size = new Size(390, 32);
        createPanel.Controls.Add(_lblError);
        top += 36;

        _btnCreate = Theme.CreateSuccessButton("CRIAR USUÁRIO", 200);
        _btnCreate.Location = new Point(0, top);
        _btnCreate.Click += async (_, _) => await BtnCreate_ClickAsync();
        createPanel.Controls.Add(_btnCreate);

        mainPanel.Controls.Add(createPanel);

        // Ordem de empilhamento (Dock.Top adiciona de baixo pra cima na renderização)
        mainPanel.Controls.SetChildIndex(createPanel, 0);
        mainPanel.Controls.SetChildIndex(divider, 1);
        mainPanel.Controls.SetChildIndex(actionRow, 2);
        mainPanel.Controls.SetChildIndex(_grid, 3);
        mainPanel.Controls.SetChildIndex(lblTitle, 4);

        Controls.Add(mainPanel);

        KeyDown += ManageUsersForm_KeyDown;
        AcceptButton = _btnCreate;

        ResumeLayout(false);
    }

    protected override async void OnShown(EventArgs e)
    {
        base.OnShown(e);
        PopulateRoleOptions();
        await LoadUsersAsync();
    }

    private void PopulateRoleOptions()
    {
        _cmbRole.Items.Clear();

        var maxAssignableRole = _currentUserContext.Role ?? UserRole.Colaborador;

        foreach (UserRole role in Enum.GetValues<UserRole>())
        {
            if (role <= maxAssignableRole)
                _cmbRole.Items.Add(role);
        }

        if (_cmbRole.Items.Count > 0)
            _cmbRole.SelectedIndex = 0;
    }

    private async Task LoadUsersAsync()
    {
        try
        {
            _users = await _authService.GetAllUsersAsync();
            PopulateGrid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao carregar lista de usuários");
            MessageBox.Show("Erro ao carregar a lista de usuários.", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void PopulateGrid()
    {
        _grid.Rows.Clear();

        foreach (var user in _users)
        {
            _grid.Rows.Add(
                user.Username,
                user.Role.ToDisplayName(),
                user.CreatedAt.ToString("dd/MM/yyyy"),
                user.Id,
                (int)user.Role);
        }

        UpdateResetButtonState();
    }

    private void UpdateResetButtonState()
    {
        _btnResetPassword.Enabled = TryGetSelectedUser(out var userId, out _, out var role)
            && userId != _currentUserContext.UserId
            && (_currentUserContext.Role ?? UserRole.Colaborador) > role;
    }

    private bool TryGetSelectedUser(out long userId, out string username, out UserRole role)
    {
        userId = 0;
        username = string.Empty;
        role = UserRole.Colaborador;

        if (_grid.CurrentRow is null || _grid.CurrentRow.Index < 0)
            return false;

        if (_grid.CurrentRow.Cells["UserId"].Value is not long id)
            return false;

        userId = id;
        username = _grid.CurrentRow.Cells["Username"].Value?.ToString() ?? string.Empty;
        role = (UserRole)(int)_grid.CurrentRow.Cells["RoleValue"].Value!;

        return true;
    }

    private void OpenResetPasswordForSelected()
    {
        if (!TryGetSelectedUser(out var userId, out var username, out _))
            return;

        using var scope = _serviceProvider.CreateScope();
        var changePasswordForm = scope.ServiceProvider.GetRequiredService<ChangePasswordForm>();
        changePasswordForm.TargetUserId = userId;
        changePasswordForm.TargetUsername = username;

        if (changePasswordForm.ShowDialog(this) == DialogResult.OK)
        {
            _ = LoadUsersAsync();
        }
    }

    private async Task BtnCreate_ClickAsync()
    {
        if (_currentUserContext.UserId is not long actingUserId)
            return;

        _lblError.Text = string.Empty;

        if (_txtPassword.Text != _txtConfirmPassword.Text)
        {
            _lblError.Text = "A confirmação não corresponde à senha.";
            return;
        }

        if (_cmbRole.SelectedItem is not UserRole role)
        {
            _lblError.Text = "Selecione um cargo.";
            return;
        }

        _btnCreate.Enabled = false;

        try
        {
            await _authService.CreateUserAsync(actingUserId, _txtUsername.Text, _txtPassword.Text, role);

            _txtUsername.Clear();
            _txtPassword.Clear();
            _txtConfirmPassword.Clear();

            await LoadUsersAsync();

            MessageBox.Show("Usuário criado com sucesso!", "Sucesso", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (InvalidOperationException ex)
        {
            _lblError.Text = ex.Message;
        }
        catch (ArgumentException ex)
        {
            _lblError.Text = ex.Message;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao criar usuário");
            MessageBox.Show($"Não foi possível criar o usuário.\n\nDetalhes: {ex.Message}", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _btnCreate.Enabled = true;
        }
    }

    private void ManageUsersForm_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape)
        {
            Close();
        }
    }
}
