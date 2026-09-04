using ErpFinanceiro.Application;
using ErpFinanceiro.Application.Auditoria;
using ErpFinanceiro.Application.ContasAPagar;
using ErpFinanceiro.Domain;
using ErpFinanceiro.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ErpFinanceiro.Infrastructure.ContasAPagar;

public sealed class GerenciadorPagamentos(AppDbContext db, IRegistradorAuditoria auditoria, IRelogio relogio, UserManager<Usuario> userManager)
    : IGerenciadorPagamentos
{
    public async Task<ResultadoOperacao> RegistrarAsync(Guid contaPagarId, RegistrarPagamentoInput input, Guid usuarioId)
    {
        var erroPermissao = await ValidarPermissaoAsync(usuarioId);
        if (erroPermissao is not null)
        {
            return erroPermissao;
        }

        var contaBancariaPreenchida = input.ContaBancariaEmpresaId is not null;
        var cartaoPreenchido = input.CartaoId is not null;
        if (contaBancariaPreenchida == cartaoPreenchido) // ambos ou nenhum
        {
            return ResultadoOperacao.Falha("Informe exatamente uma conta bancária OU um cartão, nunca os dois nem nenhum.");
        }

        if (input.ValorPago <= 0)
        {
            return ResultadoOperacao.Falha("Valor pago deve ser maior que zero.");
        }

        var conta = await db.ContasPagar.FirstOrDefaultAsync(c => c.Id == contaPagarId && c.ExcluidoEm == null);
        if (conta is null)
        {
            return ResultadoOperacao.Falha("Conta a pagar não encontrada.");
        }

        if (conta.StatusAprovacao != StatusAprovacao.Aprovada)
        {
            return ResultadoOperacao.Falha("Só é possível registrar pagamento em uma conta aprovada.");
        }

        var totalJaPago = await db.Pagamentos
            .Where(p => p.ContaPagarId == contaPagarId && p.Status == StatusPagamento.Confirmado)
            .SumAsync(p => (decimal?)p.ValorPago) ?? 0m;

        if (totalJaPago + input.ValorPago > conta.ValorFinal)
        {
            return ResultadoOperacao.Falha(
                $"Valor pago excede o valor final da conta (já pago: {totalJaPago:C}, valor final: {conta.ValorFinal:C}).");
        }

        var pagamento = new Pagamento
        {
            Id = Guid.NewGuid(),
            ContaPagarId = contaPagarId,
            Data = input.Data,
            ValorPago = input.ValorPago,
            FormaPagamentoId = input.FormaPagamentoId,
            ContaBancariaEmpresaId = input.ContaBancariaEmpresaId,
            CartaoId = input.CartaoId,
            Status = StatusPagamento.Confirmado,
            RegistradoPorId = usuarioId,
        };

        db.Pagamentos.Add(pagamento);

        var statusAnterior = conta.StatusFinanceiro;
        RecalcularStatusFinanceiro(conta, totalJaPago + input.ValorPago);
        ForcarDeteccaoDeConcorrencia(conta);

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return ResultadoOperacao.Falha("Esta conta foi alterada por outro usuário. Recarregue e tente novamente.");
        }

        await auditoria.RegistrarAsync(usuarioId, "RegistrarPagamento", nameof(ContaPagar), contaPagarId,
            new { StatusFinanceiro = statusAnterior }, new { conta.StatusFinanceiro, pagamento.ValorPago });

        return ResultadoOperacao.Ok();
    }

    public async Task<ResultadoOperacao> EstornarAsync(Guid pagamentoId, string motivo, Guid usuarioId)
    {
        var erroPermissao = await ValidarPermissaoAsync(usuarioId);
        if (erroPermissao is not null)
        {
            return erroPermissao;
        }

        if (string.IsNullOrWhiteSpace(motivo))
        {
            return ResultadoOperacao.Falha("Informe o motivo do estorno.");
        }

        var pagamento = await db.Pagamentos.FirstOrDefaultAsync(p => p.Id == pagamentoId);
        if (pagamento is null)
        {
            return ResultadoOperacao.Falha("Pagamento não encontrado.");
        }

        if (pagamento.Status == StatusPagamento.Estornado)
        {
            return ResultadoOperacao.Falha("Pagamento já está estornado.");
        }

        var conta = await db.ContasPagar.FirstOrDefaultAsync(c => c.Id == pagamento.ContaPagarId);
        if (conta is null)
        {
            return ResultadoOperacao.Falha("Conta a pagar não encontrada.");
        }

        pagamento.Status = StatusPagamento.Estornado;
        pagamento.MotivoEstorno = motivo;

        var totalConfirmadoAposEstorno = await db.Pagamentos
            .Where(p => p.ContaPagarId == conta.Id && p.Status == StatusPagamento.Confirmado && p.Id != pagamentoId)
            .SumAsync(p => (decimal?)p.ValorPago) ?? 0m;

        var statusAnterior = conta.StatusFinanceiro;
        RecalcularStatusFinanceiro(conta, totalConfirmadoAposEstorno);
        ForcarDeteccaoDeConcorrencia(conta);

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return ResultadoOperacao.Falha("Esta conta foi alterada por outro usuário. Recarregue e tente novamente.");
        }

        await auditoria.RegistrarAsync(usuarioId, "EstornarPagamento", nameof(ContaPagar), conta.Id,
            new { StatusFinanceiro = statusAnterior }, new { conta.StatusFinanceiro });

        return ResultadoOperacao.Ok();
    }

    public async Task<IReadOnlyList<Pagamento>> ListarPorContaAsync(Guid contaPagarId) =>
        await db.Pagamentos.AsNoTracking()
            .Include(p => p.FormaPagamento)
            .Include(p => p.ContaBancariaEmpresa)
            .Include(p => p.Cartao)
            .Where(p => p.ContaPagarId == contaPagarId)
            .OrderByDescending(p => p.Data)
            .ToListAsync();

    /// <summary>
    /// Registrar/estornar pagamento é atribuição de Financeiro (seção 15 do
    /// escopo — Gestor só aprova, Consulta só visualiza), checado aqui na
    /// camada Application, não só na UI (achado crítico do security-auditor,
    /// Passo 19: nenhuma das duas operações validava perfil antes).
    /// </summary>
    private async Task<ResultadoOperacao?> ValidarPermissaoAsync(Guid usuarioId)
    {
        var usuario = await userManager.FindByIdAsync(usuarioId.ToString());
        if (usuario is null)
        {
            return ResultadoOperacao.Falha("Usuário não encontrado.");
        }

        var papeis = await userManager.GetRolesAsync(usuario);
        if (!papeis.Contains(nameof(PerfilUsuario.Financeiro)) && !papeis.Contains(nameof(PerfilUsuario.Administrador)))
        {
            return ResultadoOperacao.Falha("Só usuários com perfil Financeiro ou Administrador podem registrar/estornar pagamentos.");
        }

        return null;
    }

    /// <summary>
    /// O token de concorrência (xmin) só é comparado pelo EF Core quando a
    /// entidade é marcada como Modified — e isso não acontece se
    /// RecalcularStatusFinanceiro atribuir o MESMO valor que já estava
    /// carregado (cenário realista: dois pagamentos parciais que não mudam
    /// o enum de status). Sem isso, dois registros concorrentes poderiam
    /// somar acima do valor final sem que o segundo SaveChanges detectasse
    /// nada (achado médio/alto do security-auditor, Passo 19).
    /// </summary>
    private void ForcarDeteccaoDeConcorrencia(ContaPagar conta) =>
        db.Entry(conta).Property(c => c.StatusFinanceiro).IsModified = true;

    /// <summary>
    /// Pagamento parcial não muda o status para Paga (fica no que já
    /// estava); soma de pagamentos confirmados == valor final marca Paga.
    /// Fora isso, recalcula com base na data de vencimento vs. hoje —
    /// mesma lógica que o dashboard (Passo 28) vai usar.
    /// </summary>
    private void RecalcularStatusFinanceiro(ContaPagar conta, decimal totalPagoConfirmado)
    {
        if (conta.ValorFinal > 0 && totalPagoConfirmado >= conta.ValorFinal)
        {
            conta.StatusFinanceiro = StatusFinanceiro.Paga;
            return;
        }

        if (conta.StatusFinanceiro == StatusFinanceiro.Cancelada)
        {
            return;
        }

        var hoje = relogio.Hoje();
        conta.StatusFinanceiro = conta.Vencimento < hoje
            ? StatusFinanceiro.Vencida
            : conta.Vencimento <= hoje.AddDays(7)
                ? StatusFinanceiro.AVencer
                : StatusFinanceiro.EmAberto;
    }
}
