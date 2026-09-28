namespace ErpFinanceiro.Domain;

/// <summary>
/// Cadastro simples que também guarda uma cor (hex) de identidade visual —
/// hoje só CentroCusto, usada no gráfico de pizza do painel. Interface
/// separada de <see cref="ICadastroSimples"/> para não obrigar Categoria
/// (que não tem cor) a carregar essa coluna.
/// </summary>
public interface ICadastroComCor
{
    string? Cor { get; set; }
}
