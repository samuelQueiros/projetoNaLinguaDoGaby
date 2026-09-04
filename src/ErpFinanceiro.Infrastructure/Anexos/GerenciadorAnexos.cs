using ErpFinanceiro.Application;
using ErpFinanceiro.Application.Anexos;
using ErpFinanceiro.Application.Auditoria;
using ErpFinanceiro.Domain;
using ErpFinanceiro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpFinanceiro.Infrastructure.Anexos;

public sealed class GerenciadorAnexos(AppDbContext db, IArmazenamentoAnexos storage, IRegistradorAuditoria auditoria)
    : IGerenciadorAnexos
{
    public async Task<Anexo> AnexarAsync(NovoAnexo novo, Guid usuarioId)
    {
        var armazenado = await storage.SalvarAsync(novo.Conteudo, novo.NomeOriginal, novo.TipoConteudo);

        var anexo = new Anexo
        {
            Id = Guid.NewGuid(),
            EntidadeTipo = novo.EntidadeTipo,
            EntidadeId = novo.EntidadeId,
            TipoDocumento = novo.TipoDocumento,
            NomeArquivo = Path.GetFileName(novo.NomeOriginal),
            CaminhoArmazenamento = armazenado.CaminhoRelativo,
            TamanhoBytes = armazenado.TamanhoBytes,
            TipoConteudo = armazenado.TipoConteudo,
            EnviadoPorId = usuarioId,
        };

        db.Anexos.Add(anexo);
        await db.SaveChangesAsync();

        await auditoria.RegistrarAsync(usuarioId, "AnexarDocumento", novo.EntidadeTipo.ToString(), novo.EntidadeId,
            null, new { anexo.TipoDocumento, anexo.NomeArquivo, anexo.TamanhoBytes });

        return anexo;
    }

    public async Task<IReadOnlyList<Anexo>> ListarAsync(EntidadeAnexo entidadeTipo, Guid entidadeId) =>
        await db.Anexos.AsNoTracking()
            .Include(a => a.EnviadoPor)
            .Where(a => a.EntidadeTipo == entidadeTipo && a.EntidadeId == entidadeId)
            .OrderByDescending(a => a.CriadoEm)
            .ToListAsync();

    public async Task<AnexoParaDownload?> BaixarAsync(Guid anexoId)
    {
        var anexo = await db.Anexos.AsNoTracking().FirstOrDefaultAsync(a => a.Id == anexoId);
        if (anexo is null)
        {
            return null;
        }

        var conteudo = await storage.AbrirAsync(anexo.CaminhoArmazenamento);
        return new AnexoParaDownload(conteudo, anexo.NomeArquivo, anexo.TipoConteudo);
    }

    public async Task<ResultadoOperacao> ExcluirAsync(Guid anexoId, Guid usuarioId)
    {
        var anexo = await db.Anexos.FirstOrDefaultAsync(a => a.Id == anexoId);
        if (anexo is null)
        {
            return ResultadoOperacao.Falha("Anexo não encontrado.");
        }

        db.Anexos.Remove(anexo);
        await db.SaveChangesAsync();
        await storage.ExcluirAsync(anexo.CaminhoArmazenamento);

        await auditoria.RegistrarAsync(usuarioId, "ExcluirDocumento", anexo.EntidadeTipo.ToString(), anexo.EntidadeId,
            new { anexo.TipoDocumento, anexo.NomeArquivo }, null);

        return ResultadoOperacao.Ok();
    }
}
