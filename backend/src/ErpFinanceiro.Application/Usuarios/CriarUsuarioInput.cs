using ErpFinanceiro.Domain;

namespace ErpFinanceiro.Application.Usuarios;

public sealed record CriarUsuarioInput(string Nome, string Email, string SenhaInicial, PerfilUsuario Perfil);
