namespace ErpFinanceiro.Domain;

/// <summary>
/// Registra quem aprovou/rejeitou uma ContaPagar e quando (seção 16 do
/// escopo). Nunca editado após criado — é, em si, um registro de auditoria
/// pontual, por isso não implementa IEntidadeAuditavel (não há
/// "AtualizadoEm" fazendo sentido aqui).
/// </summary>
public class AprovacaoConta
{
    public Guid Id { get; set; }

    public Guid ContaPagarId { get; set; }

    public ContaPagar? ContaPagar { get; set; }

    public Guid UsuarioId { get; set; }

    public Usuario? Usuario { get; set; }

    public AcaoAprovacao Acao { get; set; }

    public string? Motivo { get; set; }

    public DateTime Data { get; set; }
}
