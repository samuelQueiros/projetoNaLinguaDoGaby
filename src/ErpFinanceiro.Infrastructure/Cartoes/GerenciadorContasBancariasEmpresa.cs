using ErpFinanceiro.Application;
using ErpFinanceiro.Application.Cartoes;
using ErpFinanceiro.Domain;
using ErpFinanceiro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpFinanceiro.Infrastructure.Cartoes;

public sealed class GerenciadorContasBancariasEmpresa(AppDbContext db) : IGerenciadorContasBancariasEmpresa
{
    public async Task<ContaBancariaEmpresa> CriarAsync(ContaBancariaEmpresaInput input)
    {
        var conta = new ContaBancariaEmpresa
        {
            Id = Guid.NewGuid(),
            Banco = input.Banco,
            Agencia = input.Agencia,
            Conta = input.Conta,
            Tipo = input.Tipo,
            Apelido = input.Apelido,
            Ativo = true,
        };

        db.ContasBancariasEmpresa.Add(conta);
        await db.SaveChangesAsync();
        return conta;
    }

    public async Task<ResultadoOperacao> EditarAsync(Guid id, ContaBancariaEmpresaInput input)
    {
        var conta = await db.ContasBancariasEmpresa.FirstOrDefaultAsync(c => c.Id == id);
        if (conta is null)
        {
            return ResultadoOperacao.Falha("Conta bancária não encontrada.");
        }

        conta.Banco = input.Banco;
        conta.Agencia = input.Agencia;
        conta.Conta = input.Conta;
        conta.Tipo = input.Tipo;
        conta.Apelido = input.Apelido;

        await db.SaveChangesAsync();
        return ResultadoOperacao.Ok();
    }

    public Task<ResultadoOperacao> InativarAsync(Guid id) => AlterarAtivoAsync(id, false);

    public Task<ResultadoOperacao> ReativarAsync(Guid id) => AlterarAtivoAsync(id, true);

    public async Task<IReadOnlyList<ContaBancariaEmpresa>> ListarAsync(bool apenasAtivas = false)
    {
        var query = db.ContasBancariasEmpresa.AsNoTracking().AsQueryable();
        if (apenasAtivas)
        {
            query = query.Where(c => c.Ativo);
        }

        return await query.OrderBy(c => c.Apelido).ToListAsync();
    }

    private async Task<ResultadoOperacao> AlterarAtivoAsync(Guid id, bool ativo)
    {
        var conta = await db.ContasBancariasEmpresa.FirstOrDefaultAsync(c => c.Id == id);
        if (conta is null)
        {
            return ResultadoOperacao.Falha("Conta bancária não encontrada.");
        }

        conta.Ativo = ativo;
        await db.SaveChangesAsync();
        return ResultadoOperacao.Ok();
    }
}
