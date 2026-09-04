using ErpFinanceiro.Application;
using ErpFinanceiro.Application.Auditoria;
using ErpFinanceiro.Application.ContasAPagar;
using ErpFinanceiro.Domain;
using ErpFinanceiro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpFinanceiro.Infrastructure.ContasAPagar;

public sealed class GerenciadorContasPagar(AppDbContext db, IRegistradorAuditoria auditoria) : IGerenciadorContasPagar
{
    public async Task<ResultadoContaPagar> CriarAsync(ContaPagarInput input, Guid usuarioId)
    {
        var erros = await ValidarAsync(input);
        if (erros.Count > 0)
        {
            return ResultadoContaPagar.Falha(erros.ToArray());
        }

        var conta = new ContaPagar
        {
            Id = Guid.NewGuid(),
            FornecedorId = input.FornecedorId,
            Descricao = input.Descricao,
            CategoriaId = input.CategoriaId,
            CentroCustoId = input.CentroCustoId,
            Vencimento = input.Vencimento,
            ValorOriginal = input.ValorOriginal,
            Desconto = input.Desconto,
            Juros = input.Juros,
            Multa = input.Multa,
            ValorFinal = CalculadoraValorFinal.Calcular(input.ValorOriginal, input.Desconto, input.Juros, input.Multa),
            FormaPagamentoId = input.FormaPagamentoId,
            StatusAprovacao = StatusAprovacao.Cadastrada,
            StatusFinanceiro = StatusFinanceiro.EmAberto,
            CriadoPorId = usuarioId,
        };

        db.ContasPagar.Add(conta);
        await db.SaveChangesAsync();

        await auditoria.RegistrarAsync(usuarioId, "Criar", nameof(ContaPagar), conta.Id, null, conta);

        return ResultadoContaPagar.Ok(conta);
    }

    public async Task<ResultadoOperacao> EditarAsync(Guid id, ContaPagarInput input, Guid usuarioId)
    {
        var conta = await db.ContasPagar.FirstOrDefaultAsync(c => c.Id == id && c.ExcluidoEm == null);
        if (conta is null)
        {
            return ResultadoOperacao.Falha("Conta a pagar não encontrada.");
        }

        var erros = await ValidarAsync(input);
        if (erros.Count > 0)
        {
            return ResultadoOperacao.Falha(erros.ToArray());
        }

        var valorAnterior = new { conta.FornecedorId, conta.ValorFinal, conta.Vencimento };

        conta.FornecedorId = input.FornecedorId;
        conta.Descricao = input.Descricao;
        conta.CategoriaId = input.CategoriaId;
        conta.CentroCustoId = input.CentroCustoId;
        conta.Vencimento = input.Vencimento;
        conta.ValorOriginal = input.ValorOriginal;
        conta.Desconto = input.Desconto;
        conta.Juros = input.Juros;
        conta.Multa = input.Multa;
        conta.ValorFinal = CalculadoraValorFinal.Calcular(input.ValorOriginal, input.Desconto, input.Juros, input.Multa);
        conta.FormaPagamentoId = input.FormaPagamentoId;

        await db.SaveChangesAsync();

        await auditoria.RegistrarAsync(usuarioId, "Editar", nameof(ContaPagar), conta.Id, valorAnterior,
            new { conta.FornecedorId, conta.ValorFinal, conta.Vencimento });

        return ResultadoOperacao.Ok();
    }

    public async Task<ResultadoOperacao> ExcluirAsync(Guid id, Guid usuarioId)
    {
        var conta = await db.ContasPagar.FirstOrDefaultAsync(c => c.Id == id && c.ExcluidoEm == null);
        if (conta is null)
        {
            return ResultadoOperacao.Falha("Conta a pagar não encontrada.");
        }

        conta.ExcluidoEm = DateTime.UtcNow;
        await db.SaveChangesAsync();

        await auditoria.RegistrarAsync(usuarioId, "Excluir", nameof(ContaPagar), conta.Id, null, null);

        return ResultadoOperacao.Ok();
    }

    public async Task<ContaPagar?> ObterAsync(Guid id) =>
        await db.ContasPagar
            .Include(c => c.Fornecedor)
            .Include(c => c.Categoria)
            .Include(c => c.CentroCusto)
            .Include(c => c.FormaPagamento)
            .Include(c => c.CriadoPor)
            .FirstOrDefaultAsync(c => c.Id == id);

    public async Task<IReadOnlyList<ContaPagar>> ListarAsync(FiltroContasPagar filtro)
    {
        var query = db.ContasPagar.AsNoTracking()
            .Include(c => c.Fornecedor)
            .Include(c => c.Categoria)
            .Include(c => c.CentroCusto)
            .AsQueryable();

        if (!filtro.IncluirExcluidas)
        {
            query = query.Where(c => c.ExcluidoEm == null);
        }

        if (filtro.FornecedorId is Guid fornecedorId)
        {
            query = query.Where(c => c.FornecedorId == fornecedorId);
        }

        if (filtro.StatusFinanceiro is StatusFinanceiro statusFinanceiro)
        {
            query = query.Where(c => c.StatusFinanceiro == statusFinanceiro);
        }

        if (filtro.StatusAprovacao is StatusAprovacao statusAprovacao)
        {
            query = query.Where(c => c.StatusAprovacao == statusAprovacao);
        }

        if (filtro.VencimentoInicial is DateOnly vencimentoInicial)
        {
            query = query.Where(c => c.Vencimento >= vencimentoInicial);
        }

        if (filtro.VencimentoFinal is DateOnly vencimentoFinal)
        {
            query = query.Where(c => c.Vencimento <= vencimentoFinal);
        }

        return await query.OrderBy(c => c.Vencimento).ToListAsync();
    }

    private async Task<List<string>> ValidarAsync(ContaPagarInput input)
    {
        var erros = new List<string>();

        if (string.IsNullOrWhiteSpace(input.Descricao))
        {
            erros.Add("Informe a descrição da despesa.");
        }

        if (input.ValorOriginal < 0)
        {
            erros.Add("Valor original não pode ser negativo.");
        }

        if (input.Desconto < 0 || input.Juros < 0 || input.Multa < 0)
        {
            erros.Add("Desconto, juros e multa não podem ser negativos.");
        }

        if (input.Desconto > input.ValorOriginal)
        {
            erros.Add("Desconto não pode ser maior que o valor original.");
        }

        var fornecedorValido = await db.Fornecedores.AnyAsync(f => f.Id == input.FornecedorId && f.ExcluidoEm == null);
        if (!fornecedorValido)
        {
            erros.Add("Fornecedor não encontrado ou excluído.");
        }

        return erros;
    }
}
