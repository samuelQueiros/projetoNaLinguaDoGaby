namespace ErpFinanceiro.Domain;

/// <summary>
/// Contrato comum aos cadastros simples de nome/descrição/ativo
/// (Categoria, CentroCusto) — permite um único caso de uso genérico em
/// Application em vez de duplicar Criar/Editar/Listar/Inativar para cada
/// entidade estruturalmente idêntica.
/// </summary>
public interface ICadastroSimples
{
    Guid Id { get; set; }

    string Nome { get; set; }

    string? Descricao { get; set; }

    bool Ativo { get; set; }
}
