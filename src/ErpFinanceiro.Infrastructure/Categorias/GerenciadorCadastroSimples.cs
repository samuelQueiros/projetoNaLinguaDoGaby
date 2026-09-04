using ErpFinanceiro.Application;
using ErpFinanceiro.Application.Categorias;
using ErpFinanceiro.Domain;
using ErpFinanceiro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpFinanceiro.Infrastructure.Categorias;

public sealed class GerenciadorCadastroSimples<TEntidade>(AppDbContext db) : IGerenciadorCadastroSimples<TEntidade>
    where TEntidade : class, ICadastroSimples, new()
{
    public async Task<TEntidade> CriarAsync(string nome, string? descricao)
    {
        var entidade = new TEntidade
        {
            Id = Guid.NewGuid(),
            Nome = nome,
            Descricao = descricao,
            Ativo = true,
        };

        db.Set<TEntidade>().Add(entidade);
        await SalvarOuLancarNomeDuplicadoAsync();

        return entidade;
    }

    public async Task<ResultadoOperacao> EditarAsync(Guid id, string nome, string? descricao)
    {
        var entidade = await db.Set<TEntidade>().FirstOrDefaultAsync(e => e.Id == id);
        if (entidade is null)
        {
            return ResultadoOperacao.Falha("Registro não encontrado.");
        }

        entidade.Nome = nome;
        entidade.Descricao = descricao;

        try
        {
            await SalvarOuLancarNomeDuplicadoAsync();
        }
        catch (InvalidOperationException ex)
        {
            return ResultadoOperacao.Falha(ex.Message);
        }

        return ResultadoOperacao.Ok();
    }

    public Task<ResultadoOperacao> InativarAsync(Guid id) => AlterarAtivoAsync(id, ativo: false);

    public Task<ResultadoOperacao> ReativarAsync(Guid id) => AlterarAtivoAsync(id, ativo: true);

    public async Task<IReadOnlyList<TEntidade>> ListarAsync(bool apenasAtivos = false)
    {
        var query = db.Set<TEntidade>().AsNoTracking().AsQueryable();
        if (apenasAtivos)
        {
            query = query.Where(e => e.Ativo);
        }

        return await query.OrderBy(e => e.Nome).ToListAsync();
    }

    private async Task<ResultadoOperacao> AlterarAtivoAsync(Guid id, bool ativo)
    {
        var entidade = await db.Set<TEntidade>().FirstOrDefaultAsync(e => e.Id == id);
        if (entidade is null)
        {
            return ResultadoOperacao.Falha("Registro não encontrado.");
        }

        entidade.Ativo = ativo;

        try
        {
            await SalvarOuLancarNomeDuplicadoAsync();
        }
        catch (InvalidOperationException ex)
        {
            // Só pode acontecer ao reativar (o índice único é parcial,
            // WHERE Ativo = true) e já existir outro registro ativo com o
            // mesmo nome.
            return ResultadoOperacao.Falha(ex.Message);
        }

        return ResultadoOperacao.Ok();
    }

    /// <summary>
    /// Traduz a violação do índice único parcial de Nome (entre os ativos)
    /// numa mensagem de negócio legível, em vez de vazar a exceção do
    /// Npgsql para quem chamou.
    /// </summary>
    private async Task SalvarOuLancarNomeDuplicadoAsync()
    {
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("_Nome", StringComparison.Ordinal) == true)
        {
            throw new InvalidOperationException("Já existe um registro ativo com esse nome.", ex);
        }
    }
}
