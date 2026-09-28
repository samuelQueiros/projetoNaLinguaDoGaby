using ErpFinanceiro.Application;
using ErpFinanceiro.Application.Auditoria;
using ErpFinanceiro.Application.ContasAPagar;
using ErpFinanceiro.Domain;
using ErpFinanceiro.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ErpFinanceiro.Infrastructure.ContasAPagar;

public sealed class FluxoAprovacao(AppDbContext db, UserManager<Usuario> userManager, IRegistradorAuditoria auditoria)
    : IFluxoAprovacao
{
    public Task<ResultadoOperacao> AprovarAsync(Guid contaPagarId, Guid usuarioId) =>
        RegistrarAsync(contaPagarId, usuarioId, AcaoAprovacao.Aprovada, StatusAprovacao.Aprovada, motivo: null);

    public Task<ResultadoOperacao> RetornarParaCadastradaAsync(Guid contaPagarId, Guid usuarioId) =>
        RegistrarAsync(contaPagarId, usuarioId, AcaoAprovacao.RetornadaCadastro, StatusAprovacao.Cadastrada, motivo: null);

    public Task<ResultadoOperacao> RejeitarAsync(Guid contaPagarId, Guid usuarioId, string motivo)
    {
        if (string.IsNullOrWhiteSpace(motivo))
        {
            return Task.FromResult(ResultadoOperacao.Falha("Informe o motivo da rejeição."));
        }

        return RegistrarAsync(contaPagarId, usuarioId, AcaoAprovacao.Rejeitada, StatusAprovacao.Rejeitada, motivo);
    }

    private async Task<ResultadoOperacao> RegistrarAsync(Guid contaPagarId, Guid usuarioId, AcaoAprovacao acao,
        StatusAprovacao statusDestino, string? motivo)
    {
        var usuario = await userManager.FindByIdAsync(usuarioId.ToString());
        if (usuario is null)
        {
            return ResultadoOperacao.Falha("Usuário não encontrado.");
        }

        var papeis = await userManager.GetRolesAsync(usuario);
        if (!papeis.Contains(nameof(PerfilUsuario.Gestor)) && !papeis.Contains(nameof(PerfilUsuario.Administrador)))
        {
            return ResultadoOperacao.Falha("Só usuários com perfil Gestor ou Administrador podem aprovar/rejeitar contas.");
        }

        var conta = await db.ContasPagar.FirstOrDefaultAsync(c => c.Id == contaPagarId && c.ExcluidoEm == null);
        if (conta is null)
        {
            return ResultadoOperacao.Falha("Conta a pagar não encontrada.");
        }

        var transicaoPermitida = statusDestino == StatusAprovacao.Cadastrada
            ? conta.StatusAprovacao == StatusAprovacao.Aprovada
            : conta.StatusAprovacao is StatusAprovacao.Cadastrada or StatusAprovacao.AguardandoAprovacao;

        if (!transicaoPermitida)
        {
            return ResultadoOperacao.Falha(statusDestino == StatusAprovacao.Cadastrada
                ? "Só contas aprovadas podem retornar para cadastrada."
                : $"Conta já está no status de aprovação '{conta.StatusAprovacao}' — não pode ser aprovada/rejeitada novamente.");
        }

        var statusAnterior = conta.StatusAprovacao;
        conta.StatusAprovacao = statusDestino;
        if (acao == AcaoAprovacao.Rejeitada)
        {
            conta.MotivoCancelamentoRejeicao = motivo;
        }
        else if (acao == AcaoAprovacao.RetornadaCadastro)
        {
            conta.MotivoCancelamentoRejeicao = null;
        }

        db.AprovacoesConta.Add(new AprovacaoConta
        {
            Id = Guid.NewGuid(),
            ContaPagarId = contaPagarId,
            UsuarioId = usuarioId,
            Acao = acao,
            Motivo = motivo,
        });

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            // ContaPagar usa xmin como token de concorrência (Passo 14) —
            // outro usuário alterou a mesma conta entre a leitura e a
            // gravação (ex.: dois Gestores aprovando ao mesmo tempo).
            // Cenário realista, não um edge case raro — recomendação do
            // security-auditor no Passo 18.
            return ResultadoOperacao.Falha("Esta conta foi alterada por outro usuário. Recarregue e tente novamente.");
        }

        await auditoria.RegistrarAsync(usuarioId, acao.ToString(), nameof(ContaPagar), contaPagarId,
            new { StatusAprovacao = statusAnterior }, new { conta.StatusAprovacao });

        return ResultadoOperacao.Ok();
    }
}
