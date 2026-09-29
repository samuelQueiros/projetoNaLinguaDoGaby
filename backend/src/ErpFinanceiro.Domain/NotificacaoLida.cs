namespace ErpFinanceiro.Domain;

/// <summary>Registra que um usuário dispensou um alerta de vencimento específico.</summary>
public class NotificacaoLida
{
    public Guid UsuarioId { get; set; }

    public Usuario? Usuario { get; set; }

    public TipoNotificacao Tipo { get; set; }

    public Guid ReferenciaId { get; set; }

    public DateOnly Vencimento { get; set; }

    public DateTime LidaEm { get; set; }
}
