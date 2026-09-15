using ErpFinanceiro.Application;
using ErpFinanceiro.Application.Auditoria;
using ErpFinanceiro.Application.Cartoes;
using ErpFinanceiro.Domain;
using ErpFinanceiro.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ErpFinanceiro.Infrastructure.Cartoes;

public sealed class GerenciadorCartoes(AppDbContext db, IRegistradorAuditoria auditoria, UserManager<Usuario> userManager)
    : IGerenciadorCartoes
{
    public async Task<Cartao> CriarAsync(CartaoInput input, Guid usuarioId)
    {
        await ExigirPermissaoAsync(usuarioId);

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

        await auditoria.RegistrarAsync(usuarioId, "Criar", nameof(Cartao), cartao.Id, null,
            new { cartao.Apelido, cartao.InstituicaoFinanceira, cartao.Bandeira, cartao.Limite });

        return cartao;
    }

    public async Task<ResultadoOperacao> EditarAsync(Guid id, CartaoInput input, Guid usuarioId)
    {
        var erroPermissao = await ValidarPermissaoAsync(usuarioId);
        if (erroPermissao is not null)
        {
            return erroPermissao;
        }

        var cartao = await db.Cartoes.FirstOrDefaultAsync(c => c.Id == id);
        if (cartao is null)
        {
            return ResultadoOperacao.Falha("Cartão não encontrado.");
        }

        var anterior = new { cartao.Apelido, cartao.Limite, cartao.ResponsavelId };

        cartao.InstituicaoFinanceira = input.InstituicaoFinanceira;
        cartao.Bandeira = input.Bandeira;
        cartao.Apelido = input.Apelido;
        cartao.UltimosQuatroDigitos = input.UltimosQuatroDigitos;
        cartao.Limite = input.Limite;
        cartao.DiaFechamento = input.DiaFechamento;
        cartao.DiaVencimento = input.DiaVencimento;
        cartao.ResponsavelId = input.ResponsavelId;

        await db.SaveChangesAsync();

        await auditoria.RegistrarAsync(usuarioId, "Editar", nameof(Cartao), cartao.Id, anterior,
            new { cartao.Apelido, cartao.Limite, cartao.ResponsavelId });

        return ResultadoOperacao.Ok();
    }

    public async Task<ResultadoOperacao> AlterarStatusAsync(Guid id, StatusCartao novoStatus, Guid usuarioId)
    {
        var erroPermissao = await ValidarPermissaoAsync(usuarioId);
        if (erroPermissao is not null)
        {
            return erroPermissao;
        }

        var cartao = await db.Cartoes.FirstOrDefaultAsync(c => c.Id == id);
        if (cartao is null)
        {
            return ResultadoOperacao.Falha("Cartão não encontrado.");
        }

        var statusAnterior = cartao.Status;
        cartao.Status = novoStatus;
        await db.SaveChangesAsync();

        await auditoria.RegistrarAsync(usuarioId, "AlterarStatus", nameof(Cartao), cartao.Id,
            new { Status = statusAnterior }, new { Status = novoStatus });

        return ResultadoOperacao.Ok();
    }

    public async Task<IReadOnlyList<Cartao>> ListarAsync() =>
        await db.Cartoes.AsNoTracking().Include(c => c.Responsavel).OrderBy(c => c.Apelido).ToListAsync();

    /// <summary>
    /// Cadastro/edição/status de cartão é atribuição de Financeiro/Administrador
    /// — mesma convenção do resto do módulo ContasAPagar. Achado crítico da
    /// auditoria de segurança: nenhum método validava papel nem registrava
    /// auditoria antes.
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
            return ResultadoOperacao.Falha("Só usuários com perfil Financeiro ou Administrador podem alterar cartões.");
        }

        return null;
    }

    private async Task ExigirPermissaoAsync(Guid usuarioId)
    {
        var erro = await ValidarPermissaoAsync(usuarioId);
        if (erro is not null)
        {
            throw new InvalidOperationException(erro.Erros[0]);
        }
    }
}
