namespace ErpFinanceiro.Application.Fornecedores;

public sealed record NovoContratoInput(
    Stream Conteudo,
    string NomeOriginal,
    string TipoConteudo,
    string Nome,
    DateOnly VigenciaInicio,
    DateOnly VigenciaFim);

public sealed record ContratoParaDownload(Stream Conteudo, string NomeArquivo, string TipoConteudo);
