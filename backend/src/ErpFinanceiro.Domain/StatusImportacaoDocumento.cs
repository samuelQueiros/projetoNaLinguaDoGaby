namespace ErpFinanceiro.Domain;

/// <summary>
/// Ciclo de vida de um documento enviado à Central de Documentos (seção 9
/// do escopo — Assistente de IA; ver docs/modulo-ia-documentos.md).
/// Guardado como string no banco, igual aos outros enums de status do
/// schema (StatusPagamento, StatusBoleto etc.).
/// </summary>
public enum StatusImportacaoDocumento
{
    /// <summary>Arquivo salvo no storage, aguardando processamento na fila.</summary>
    Recebido,

    /// <summary>Em leitura pelo serviço de extração.</summary>
    Processando,

    /// <summary>Extração concluída; aguardando um Administrador revisar.</summary>
    AguardandoRevisao,

    /// <summary>Revisado e convertido em ContaPagar oficial.</summary>
    Aprovado,

    /// <summary>Descartado por um Administrador — não vira lançamento.</summary>
    Rejeitado,

    /// <summary>Falha no processamento (arquivo ilegível, serviço fora do ar).</summary>
    Falha,
}
