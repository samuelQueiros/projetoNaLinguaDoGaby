using ErpFinanceiro.Application;
using ErpFinanceiro.Application.Auditoria;
using ErpFinanceiro.Application.Boletos;
using ErpFinanceiro.Domain;
using ErpFinanceiro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpFinanceiro.Infrastructure.Boletos;

public sealed class GerenciadorBoletos(AppDbContext db, IRegistradorAuditoria auditoria) : IGerenciadorBoletos
{
    public async Task<ResultadoCriacao<Boleto>> CriarAsync(BoletoInput input, Guid usuarioId)
    {
        var erros = await ValidarAsync(input);
        if (erros.Count > 0)
        {
            return ResultadoCriacao<Boleto>.Falha(erros.ToArray());
        }

        var boleto = new Boleto
        {
            Id = Guid.NewGuid(),
            FornecedorId = input.FornecedorId,
            ContaPagarId = input.ContaPagarId,
            Numero = input.Numero.Trim(),
            LinhaDigitavel = input.LinhaDigitavel.Trim(),
            CodigoBarras = input.CodigoBarras?.Trim(),
            Valor = input.Valor,
            Vencimento = input.Vencimento,
            DataPagamento = input.DataPagamento,
            Banco = input.Banco?.Trim(),
            Status = input.Status,
            Observacoes = input.Observacoes,
        };

        db.Boletos.Add(boleto);
        await db.SaveChangesAsync();

        await auditoria.RegistrarAsync(usuarioId, "Criar", nameof(Boleto), boleto.Id, null,
            new { boleto.FornecedorId, boleto.ContaPagarId, boleto.Numero, boleto.Valor, boleto.Status });

        return ResultadoCriacao<Boleto>.Ok(boleto);
    }

    public async Task<ResultadoOperacao> EditarAsync(Guid id, BoletoInput input, Guid usuarioId)
    {
        var boleto = await db.Boletos.FirstOrDefaultAsync(b => b.Id == id);
        if (boleto is null)
        {
            return ResultadoOperacao.Falha("Boleto não encontrado.");
        }

        var erros = await ValidarAsync(input);
        if (erros.Count > 0)
        {
            return ResultadoOperacao.Falha(erros.ToArray());
        }

        var anterior = new { boleto.FornecedorId, boleto.ContaPagarId, boleto.Numero, boleto.Valor, boleto.Status };

        boleto.FornecedorId = input.FornecedorId;
        boleto.ContaPagarId = input.ContaPagarId;
        boleto.Numero = input.Numero.Trim();
        boleto.LinhaDigitavel = input.LinhaDigitavel.Trim();
        boleto.CodigoBarras = input.CodigoBarras?.Trim();
        boleto.Valor = input.Valor;
        boleto.Vencimento = input.Vencimento;
        boleto.DataPagamento = input.DataPagamento;
        boleto.Banco = input.Banco?.Trim();
        boleto.Status = input.Status;
        boleto.Observacoes = input.Observacoes;

        await db.SaveChangesAsync();

        await auditoria.RegistrarAsync(usuarioId, "Editar", nameof(Boleto), boleto.Id, anterior,
            new { boleto.FornecedorId, boleto.ContaPagarId, boleto.Numero, boleto.Valor, boleto.Status });

        return ResultadoOperacao.Ok();
    }

    public async Task<ResultadoOperacao> ExcluirAsync(Guid id, Guid usuarioId)
    {
        var boleto = await db.Boletos.FirstOrDefaultAsync(b => b.Id == id);
        if (boleto is null)
        {
            return ResultadoOperacao.Falha("Boleto não encontrado.");
        }

        db.Boletos.Remove(boleto);
        await db.SaveChangesAsync();

        await auditoria.RegistrarAsync(usuarioId, "Excluir", nameof(Boleto), id,
            new { boleto.FornecedorId, boleto.ContaPagarId, boleto.Numero, boleto.Valor }, null);

        return ResultadoOperacao.Ok();
    }

    public async Task<Boleto?> ObterAsync(Guid id) =>
        await db.Boletos.AsNoTracking()
            .Include(b => b.Fornecedor)
            .Include(b => b.ContaPagar)
            .FirstOrDefaultAsync(b => b.Id == id);

    public async Task<IReadOnlyList<Boleto>> ListarAsync(FiltroBoletos filtro)
    {
        var query = db.Boletos.AsNoTracking()
            .Include(b => b.Fornecedor)
            .Include(b => b.ContaPagar)
            .AsQueryable();

        if (filtro.FornecedorId is Guid fornecedorId)
        {
            query = query.Where(b => b.FornecedorId == fornecedorId);
        }

        if (filtro.ContaPagarId is Guid contaId)
        {
            query = query.Where(b => b.ContaPagarId == contaId);
        }

        if (filtro.Status is StatusBoleto status)
        {
            query = query.Where(b => b.Status == status);
        }

        if (filtro.VencimentoInicial is DateOnly ini)
        {
            query = query.Where(b => b.Vencimento >= ini);
        }

        if (filtro.VencimentoFinal is DateOnly fim)
        {
            query = query.Where(b => b.Vencimento <= fim);
        }

        if (filtro.Ano is int ano)
        {
            query = query.Where(b => b.Vencimento.Year == ano);
        }

        if (filtro.Mes is int mes)
        {
            query = query.Where(b => b.Vencimento.Month == mes);
        }

        return await query.OrderBy(b => b.Vencimento).ToListAsync();
    }

    private async Task<List<string>> ValidarAsync(BoletoInput input)
    {
        var erros = new List<string>();

        if (string.IsNullOrWhiteSpace(input.Numero))
        {
            erros.Add("Informe o número do boleto.");
        }

        if (string.IsNullOrWhiteSpace(input.LinhaDigitavel))
        {
            erros.Add("Informe a linha digitável do boleto.");
        }

        if (input.Valor < 0)
        {
            erros.Add("Valor do boleto não pode ser negativo.");
        }

        if (!await db.Fornecedores.AnyAsync(f => f.Id == input.FornecedorId && f.ExcluidoEm == null))
        {
            erros.Add("Fornecedor não encontrado ou excluído.");
        }

        if (!await db.ContasPagar.AnyAsync(c => c.Id == input.ContaPagarId && c.ExcluidoEm == null))
        {
            erros.Add("Conta a pagar vinculada não encontrada.");
        }

        return erros;
    }
}
