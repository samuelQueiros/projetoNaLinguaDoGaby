using ErpFinanceiro.Application;
using ErpFinanceiro.Application.Cartoes;
using ErpFinanceiro.Domain;
using ErpFinanceiro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpFinanceiro.Infrastructure.Cartoes;

public sealed class GerenciadorCartoes(AppDbContext db) : IGerenciadorCartoes
{
    public async Task<Cartao> CriarAsync(CartaoInput input)
    {
        var cartao = new Cartao
        {
            Id = Guid.NewGuid(),
            InstituicaoFinanceira = input.InstituicaoFinanceira,
            Bandeira = input.Bandeira,
            Apelido = input.Apelido,
            UltimosQuatroDigitos = input.UltimosQuatroDigitos,
            Limite = input.Limite,
            DiaFechamento = input.DiaFechamento,
            DiaVencimento = input.DiaVencimento,
            ResponsavelId = input.ResponsavelId,
            Status = StatusCartao.Ativo,
        };

        db.Cartoes.Add(cartao);
        await db.SaveChangesAsync();
        return cartao;
    }

    public async Task<ResultadoOperacao> EditarAsync(Guid id, CartaoInput input)
    {
        var cartao = await db.Cartoes.FirstOrDefaultAsync(c => c.Id == id);
        if (cartao is null)
        {
            return ResultadoOperacao.Falha("Cartão não encontrado.");
        }

        cartao.InstituicaoFinanceira = input.InstituicaoFinanceira;
        cartao.Bandeira = input.Bandeira;
        cartao.Apelido = input.Apelido;
        cartao.UltimosQuatroDigitos = input.UltimosQuatroDigitos;
        cartao.Limite = input.Limite;
        cartao.DiaFechamento = input.DiaFechamento;
        cartao.DiaVencimento = input.DiaVencimento;
        cartao.ResponsavelId = input.ResponsavelId;

        await db.SaveChangesAsync();
        return ResultadoOperacao.Ok();
    }

    public async Task<ResultadoOperacao> AlterarStatusAsync(Guid id, StatusCartao novoStatus)
    {
        var cartao = await db.Cartoes.FirstOrDefaultAsync(c => c.Id == id);
        if (cartao is null)
        {
            return ResultadoOperacao.Falha("Cartão não encontrado.");
        }

        cartao.Status = novoStatus;
        await db.SaveChangesAsync();
        return ResultadoOperacao.Ok();
    }

    public async Task<IReadOnlyList<Cartao>> ListarAsync() =>
        await db.Cartoes.AsNoTracking().Include(c => c.Responsavel).OrderBy(c => c.Apelido).ToListAsync();
}
