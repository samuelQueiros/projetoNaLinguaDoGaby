namespace ErpFinanceiro.Domain;

/// <summary>
/// Override de <see cref="ConfiguracaoIa"/> gravado pela tela Configurações
/// de IA (Administrador) — uma linha por <see cref="FinalidadeConfiguracaoIa"/>.
/// Quando existe uma linha aqui, ela prevalece sobre as variáveis de
/// ambiente Ia__Documentos__*/Ia__Chat__* (ver
/// ErpFinanceiro.Infrastructure.ConfiguracoesIa.GerenciadorConfiguracaoIa.ObterAsync).
/// Sem linha — nenhuma edição feita pela tela ainda, ou removida via
/// "Restaurar padrão do ambiente" — a configuração volta a vir só da env
/// var, como antes desta tela existir.
///
/// ApiKey é cifrada em repouso (AES-256-GCM — mesmo padrão de
/// DadosBancariosFornecedor/ContaBancariaEmpresa, ver AppDbContext) e nunca
/// é devolvida em texto claro pela API depois de salva: os endpoints só
/// expõem se uma chave está definida, não o valor (ver ConfiguracaoIaResumo).
/// </summary>
public sealed class ConfiguracaoIaSalva : IEntidadeAuditavel
{
    public Guid Id { get; set; }

    public FinalidadeConfiguracaoIa Finalidade { get; set; }

    public bool Ativo { get; set; } = true;

    public ProvedorIa Provedor { get; set; }

    public string Modelo { get; set; } = string.Empty;

    public string? ApiKey { get; set; }

    public int TimeoutSegundos { get; set; } = 90;

    public Guid? AtualizadoPorId { get; set; }

    public DateTime CriadoEm { get; set; }

    public DateTime? AtualizadoEm { get; set; }
}
