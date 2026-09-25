using ErpFinanceiro.Application;
using ErpFinanceiro.Application.Auditoria;
using ErpFinanceiro.Application.NotasFiscais;
using ErpFinanceiro.Domain;
using ErpFinanceiro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpFinanceiro.Infrastructure.NotasFiscais;

public sealed class GerenciadorNotasFiscais(AppDbContext db, IRegistradorAuditoria auditoria) : IGerenciadorNotasFiscais
{
    public async Task<ResultadoCriacao<NotaFiscal>> CriarAsync(NotaFiscalInput input, Guid usuarioId)
    {
        var erros = await ValidarAsync(input);
        if (erros.Count > 0)
        {
            return ResultadoCriacao<NotaFiscal>.Falha(erros.ToArray());
        }

        var nf = new NotaFiscal
        {
            Id = Guid.NewGuid(),
            FornecedorId = input.FornecedorId,
            ContaPagarId = input.ContaPagarId,
            Numero = input.Numero.Trim(),
            Serie = input.Serie?.Trim(),
            Emissao = input.Emissao,
            Valor = input.Valor,
            Vencimento = input.Vencimento,
            CategoriaId = input.CategoriaId,
            CentroCustoId = input.CentroCustoId,
            Observacoes = input.Observacoes,
        };

        db.NotasFiscais.Add(nf);
        await db.SaveChangesAsync();

        await auditoria.RegistrarAsync(usuarioId, "Criar", nameof(NotaFiscal), nf.Id, null,
            new { nf.FornecedorId, nf.Numero, nf.Valor, nf.ContaPagarId });

        return ResultadoCriacao<NotaFiscal>.Ok(nf);
    }

    public async Task<ResultadoOperacao> EditarAsync(Guid id, NotaFiscalInput input, Guid usuarioId)
    {
        var nf = await db.NotasFiscais.FirstOrDefaultAsync(n => n.Id == id);
        if (nf is null)
        {
            return ResultadoOperacao.Falha("Nota fiscal não encontrada.");
        }

        var erros = await ValidarAsync(input);
        if (erros.Count > 0)
        {
            return ResultadoOperacao.Falha(erros.ToArray());
        }

        var anterior = new { nf.FornecedorId, nf.Numero, nf.Valor, nf.ContaPagarId };

        nf.FornecedorId = input.FornecedorId;
        nf.ContaPagarId = input.ContaPagarId;
        nf.Numero = input.Numero.Trim();
        nf.Serie = input.Serie?.Trim();
        nf.Emissao = input.Emissao;
        nf.Valor = input.Valor;
        nf.Vencimento = input.Vencimento;
        nf.CategoriaId = input.CategoriaId;
        nf.CentroCustoId = input.CentroCustoId;
        nf.Observacoes = input.Observacoes;

        await db.SaveChangesAsync();

        await auditoria.RegistrarAsync(usuarioId, "Editar", nameof(NotaFiscal), nf.Id, anterior,
            new { nf.FornecedorId, nf.Numero, nf.Valor, nf.ContaPagarId });

        return ResultadoOperacao.Ok();
    }

    public async Task<ResultadoOperacao> ExcluirAsync(Guid id, Guid usuarioId)
    {
        var nf = await db.NotasFiscais.FirstOrDefaultAsync(n => n.Id == id);
        if (nf is null)
        {
            return ResultadoOperacao.Falha("Nota fiscal não encontrada.");
        }

        db.NotasFiscais.Remove(nf);
        await db.SaveChangesAsync();

        await auditoria.RegistrarAsync(usuarioId, "Excluir", nameof(NotaFiscal), id,
            new { nf.FornecedorId, nf.Numero, nf.Valor }, null);

        return ResultadoOperacao.Ok();
    }

    public async Task<NotaFiscal?> ObterAsync(Guid id) =>
        await db.NotasFiscais.AsNoTracking()
            .Include(n => n.Fornecedor)
            .Include(n => n.Categoria)
            .Include(n => n.CentroCusto)
            .FirstOrDefaultAsync(n => n.Id == id);

    public async Task<IReadOnlyList<NotaFiscal>> ListarAsync(FiltroNotasFiscais filtro)
    {
        var query = db.NotasFiscais.AsNoTracking()
            .Include(n => n.Fornecedor)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(filtro.Numero))
        {
            var numero = filtro.Numero.Trim();
            query = query.Where(n => n.Numero == numero);
        }

        if (filtro.FornecedorId is Guid fornecedorId)
        {
            query = query.Where(n => n.FornecedorId == fornecedorId);
        }

        if (!string.IsNullOrWhiteSpace(filtro.CnpjCpf))
        {
            var digitos = new string(filtro.CnpjCpf.Where(char.IsDigit).ToArray());
            query = query.Where(n => n.Fornecedor!.CnpjCpf == digitos);
        }

        if (filtro.ContaPagarId is Guid contaId)
        {
            query = query.Where(n => n.ContaPagarId == contaId);
        }

        if (filtro.EmissaoInicial is DateOnly ini)
        {
            query = query.Where(n => n.Emissao >= ini);
        }

        if (filtro.EmissaoFinal is DateOnly fim)
        {
            query = query.Where(n => n.Emissao <= fim);
        }

        if (filtro.Ano is int ano)
        {
            query = query.Where(n => n.Emissao.Year == ano);
        }

        if (filtro.Mes is int mes)
        {
            query = query.Where(n => n.Emissao.Month == mes);
        }

        return await query.OrderByDescending(n => n.Emissao).ToListAsync();
    }

    private async Task<List<string>> ValidarAsync(NotaFiscalInput input)
    {
        var erros = new List<string>();

        if (string.IsNullOrWhiteSpace(input.Numero))
        {
            erros.Add("Informe o número da nota fiscal.");
        }

        if (input.Valor < 0)
        {
            erros.Add("Valor da nota fiscal não pode ser negativo.");
        }

        if (!await db.Fornecedores.AnyAsync(f => f.Id == input.FornecedorId && f.ExcluidoEm == null))
        {
            erros.Add("Fornecedor não encontrado ou excluído.");
        }

        if (input.ContaPagarId is Guid contaId &&
            !await db.ContasPagar.AnyAsync(c => c.Id == contaId && c.ExcluidoEm == null))
        {
            erros.Add("Conta a pagar vinculada não encontrada.");
        }

        return erros;
    }
}
