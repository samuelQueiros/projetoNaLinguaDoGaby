using ErpFinanceiro.Application;
using ErpFinanceiro.Application.Auditoria;
using ErpFinanceiro.Application.Cartoes;
using ErpFinanceiro.Domain;
using ErpFinanceiro.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ErpFinanceiro.Infrastructure.Cartoes;

public sealed class GerenciadorContasBancariasEmpresa(AppDbContext db, IRegistradorAuditoria auditoria, UserManager<Usuario> userManager)
    : IGerenciadorContasBancariasEmpresa
{
    public async Task<ContaBancariaEmpresa> CriarAsync(ContaBancariaEmpresaInput input, Guid usuarioId)
    {
        await ExigirPermissaoAsync(usuarioId);
        ExigirCamposNumericos(input);

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

        await auditoria.RegistrarAsync(usuarioId, "Criar", nameof(ContaBancariaEmpresa), conta.Id, null,
            new { conta.Banco, conta.Agencia, conta.Tipo, conta.Apelido });

        return conta;
    }

    public async Task<ResultadoOperacao> EditarAsync(Guid id, ContaBancariaEmpresaInput input, Guid usuarioId)
    {
        var erroPermissao = await ValidarPermissaoAsync(usuarioId);
        if (erroPermissao is not null)
        {
            return erroPermissao;
        }

        var erroValidacao = ValidarCamposNumericos(input);
        if (erroValidacao is not null)
        {
            return ResultadoOperacao.Falha(erroValidacao);
        }

        var conta = await db.ContasBancariasEmpresa.FirstOrDefaultAsync(c => c.Id == id);
        if (conta is null)
        {
            return ResultadoOperacao.Falha("Conta bancária não encontrada.");
        }

        var anterior = new { conta.Banco, conta.Agencia, conta.Tipo, conta.Apelido };

        conta.Banco = input.Banco;
        conta.Agencia = input.Agencia;
        conta.Conta = input.Conta;
        conta.Tipo = input.Tipo;
        conta.Apelido = input.Apelido;

        await db.SaveChangesAsync();

        await auditoria.RegistrarAsync(usuarioId, "Editar", nameof(ContaBancariaEmpresa), conta.Id, anterior,
            new { conta.Banco, conta.Agencia, conta.Tipo, conta.Apelido });

        return ResultadoOperacao.Ok();
    }

    public async Task<ResultadoOperacao> ExcluirAsync(Guid id, Guid usuarioId)
    {
        var erroPermissao = await ValidarPermissaoAsync(usuarioId);
        if (erroPermissao is not null)
        {
            return erroPermissao;
        }

        var conta = await db.ContasBancariasEmpresa.FirstOrDefaultAsync(c => c.Id == id);
        if (conta is null)
        {
            return ResultadoOperacao.Falha("Conta bancária não encontrada.");
        }

        if (await db.Pagamentos.AnyAsync(p => p.ContaBancariaEmpresaId == id))
        {
            return ResultadoOperacao.Falha(
                "Esta conta bancária possui pagamentos vinculados e não pode ser excluída.");
        }

        var anterior = new { conta.Banco, conta.Agencia, conta.Tipo, conta.Apelido, conta.Ativo };
        db.ContasBancariasEmpresa.Remove(conta);

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return ResultadoOperacao.Falha(
                "Esta conta bancária possui vínculos e não pode ser excluída.");
        }

        await auditoria.RegistrarAsync(usuarioId, "Excluir", nameof(ContaBancariaEmpresa), id, anterior, null);
        return ResultadoOperacao.Ok();
    }

    public Task<ResultadoOperacao> InativarAsync(Guid id, Guid usuarioId) => AlterarAtivoAsync(id, false, usuarioId);

    public Task<ResultadoOperacao> ReativarAsync(Guid id, Guid usuarioId) => AlterarAtivoAsync(id, true, usuarioId);

    public async Task<IReadOnlyList<ContaBancariaEmpresa>> ListarAsync(bool apenasAtivas = false)
    {
        var query = db.ContasBancariasEmpresa.AsNoTracking().AsQueryable();
        if (apenasAtivas)
        {
            query = query.Where(c => c.Ativo);
        }

        return await query.OrderBy(c => c.Apelido).ToListAsync();
    }

    private static string? ValidarCamposNumericos(ContaBancariaEmpresaInput input)
    {
        if (string.IsNullOrEmpty(input.Banco) || input.Banco.Any(c => !char.IsAsciiDigit(c)))
        {
            return "Banco deve conter somente números.";
        }

        if (string.IsNullOrEmpty(input.Agencia) || input.Agencia.Any(c => !char.IsAsciiDigit(c)))
        {
            return "Agência deve conter somente números.";
        }

        return string.IsNullOrEmpty(input.Conta) || input.Conta.Any(c => !char.IsAsciiDigit(c))
            ? "Conta deve conter somente números."
            : null;
    }

    private static void ExigirCamposNumericos(ContaBancariaEmpresaInput input)
    {
        var erro = ValidarCamposNumericos(input);
        if (erro is not null)
        {
            throw new InvalidOperationException(erro);
        }
    }

    private async Task<ResultadoOperacao> AlterarAtivoAsync(Guid id, bool ativo, Guid usuarioId)
    {
        var erroPermissao = await ValidarPermissaoAsync(usuarioId);
        if (erroPermissao is not null)
        {
            return erroPermissao;
        }

        var conta = await db.ContasBancariasEmpresa.FirstOrDefaultAsync(c => c.Id == id);
        if (conta is null)
        {
            return ResultadoOperacao.Falha("Conta bancária não encontrada.");
        }

        conta.Ativo = ativo;
        await db.SaveChangesAsync();

        await auditoria.RegistrarAsync(usuarioId, ativo ? "Reativar" : "Inativar", nameof(ContaBancariaEmpresa), conta.Id,
            new { AtivoAnterior = !ativo }, new { Ativo = ativo });

        return ResultadoOperacao.Ok();
    }

    /// <summary>
    /// Cadastro/edição/inativação de conta bancária da própria empresa —
    /// origem usada depois para registrar pagamentos — é atribuição de
    /// Financeiro/Administrador, mesma convenção do resto do módulo
    /// ContasAPagar. Achado crítico da auditoria de segurança: nenhum
    /// método validava papel nem registrava auditoria antes.
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
            return ResultadoOperacao.Falha("Só usuários com perfil Financeiro ou Administrador podem alterar contas bancárias da empresa.");
        }

        return null;
    }

    /// <summary>
    /// CriarAsync não retorna ResultadoOperacao (mesmo formato já usado por
    /// GerenciadorFornecedores.CriarAsync para CNPJ duplicado) — negar
    /// permissão aqui também lança, em vez de mudar o contrato de retorno
    /// só por causa desta checagem.
    /// </summary>
    private async Task ExigirPermissaoAsync(Guid usuarioId)
    {
        var erro = await ValidarPermissaoAsync(usuarioId);
        if (erro is not null)
        {
            throw new InvalidOperationException(erro.Erros[0]);
        }
    }
}
