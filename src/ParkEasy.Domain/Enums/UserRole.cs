namespace ParkEasy.Domain.Enums;

/// <summary>
/// Ordem crescente de autoridade: Colaborador é o nível mais baixo, Desenvolvedor o mais alto.
/// </summary>
public enum UserRole
{
    Colaborador = 0,
    Gerente = 1,
    Administrador = 2,
    Desenvolvedor = 3
}
