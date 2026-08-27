namespace ParkEasy.Domain.Enums;

public static class UserRoleExtensions
{
    public static string ToDisplayName(this UserRole role) => role switch
    {
        UserRole.Desenvolvedor => "Desenvolvedor",
        UserRole.Administrador => "Administrador",
        UserRole.Gerente => "Gerente",
        UserRole.Colaborador => "Colaborador",
        _ => role.ToString()
    };
}
