namespace ErpFinanceiro.Application.Auditoria;

public sealed record EventoTimeline(DateTime Data, string Acao, string NomeUsuario);
