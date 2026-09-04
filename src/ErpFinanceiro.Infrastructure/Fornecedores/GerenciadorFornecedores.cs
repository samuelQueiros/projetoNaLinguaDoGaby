using ErpFinanceiro.Application;
using ErpFinanceiro.Application.Fornecedores;
using ErpFinanceiro.Domain;
using ErpFinanceiro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpFinanceiro.Infrastructure.Fornecedores;

public sealed class GerenciadorFornecedores(AppDbContext db) : IGerenciadorFornecedores
{
    public async Task<Fornecedor> CriarAsync(CriarFornecedorInput input)
    {
        var fornecedor = new Fornecedor
        {
            Id = Guid.NewGuid(),
            RazaoSocial = input.RazaoSocial,
            NomeFantasia = input.NomeFantasia,
            CnpjCpf = SomenteDigitos(input.CnpjCpf),
            InscricaoEstadual = input.InscricaoEstadual,
            Endereco = input.Endereco,
            Telefone = input.Telefone,
            Email = input.Email,
            ContatoResponsavel = input.ContatoResponsavel,
            FormaPagamentoPadraoId = input.FormaPagamentoPadraoId,
            Observacoes = input.Observacoes,
        };

        db.Fornecedores.Add(fornecedor);
        await SalvarOuLancarCnpjDuplicadoAsync();

        return fornecedor;
    }

    public async Task<ResultadoOperacao> EditarAsync(Guid id, CriarFornecedorInput input)
    {
        var fornecedor = await db.Fornecedores.FirstOrDefaultAsync(f => f.Id == id && f.ExcluidoEm == null);
        if (fornecedor is null)
        {
            return ResultadoOperacao.Falha("Fornecedor não encontrado.");
        }

        fornecedor.RazaoSocial = input.RazaoSocial;
        fornecedor.NomeFantasia = input.NomeFantasia;
        fornecedor.CnpjCpf = SomenteDigitos(input.CnpjCpf);
        fornecedor.InscricaoEstadual = input.InscricaoEstadual;
        fornecedor.Endereco = input.Endereco;
        fornecedor.Telefone = input.Telefone;
        fornecedor.Email = input.Email;
        fornecedor.ContatoResponsavel = input.ContatoResponsavel;
        fornecedor.FormaPagamentoPadraoId = input.FormaPagamentoPadraoId;
        fornecedor.Observacoes = input.Observacoes;

        try
        {
            await SalvarOuLancarCnpjDuplicadoAsync();
        }
        catch (InvalidOperationException ex)
        {
            return ResultadoOperacao.Falha(ex.Message);
        }

        return ResultadoOperacao.Ok();
    }

    public async Task<ResultadoOperacao> ExcluirAsync(Guid id)
    {
        var fornecedor = await db.Fornecedores.FirstOrDefaultAsync(f => f.Id == id && f.ExcluidoEm == null);
        if (fornecedor is null)
        {
            return ResultadoOperacao.Falha("Fornecedor não encontrado.");
        }

        fornecedor.ExcluidoEm = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return ResultadoOperacao.Ok();
    }

    public async Task<Fornecedor?> ObterAsync(Guid id) =>
        await db.Fornecedores
            .Include(f => f.DadosBancarios)
            .Include(f => f.FormaPagamentoPadrao)
            .FirstOrDefaultAsync(f => f.Id == id);

    public async Task<IReadOnlyList<Fornecedor>> ListarAsync(bool incluirExcluidos = false)
    {
        var query = db.Fornecedores.AsNoTracking().Include(f => f.FormaPagamentoPadrao).AsQueryable();
        if (!incluirExcluidos)
        {
            query = query.Where(f => f.ExcluidoEm == null);
        }

        return await query.OrderBy(f => f.RazaoSocial).ToListAsync();
    }

    public async Task<ResultadoOperacao> AdicionarDadosBancariosAsync(Guid fornecedorId, DadosBancariosInput input)
    {
        var fornecedorExiste = await db.Fornecedores.AnyAsync(f => f.Id == fornecedorId && f.ExcluidoEm == null);
        if (!fornecedorExiste)
        {
            return ResultadoOperacao.Falha("Fornecedor não encontrado.");
        }

        if (input.Principal)
        {
            await DesmarcarPrincipalAtualAsync(fornecedorId);
        }

        db.DadosBancariosFornecedores.Add(new DadosBancariosFornecedor
        {
            Id = Guid.NewGuid(),
            FornecedorId = fornecedorId,
            Banco = input.Banco,
            Agencia = input.Agencia,
            Conta = input.Conta,
            Tipo = input.Tipo,
            ChavePix = input.ChavePix,
            Principal = input.Principal,
        });

        await db.SaveChangesAsync();
        return ResultadoOperacao.Ok();
    }

    public async Task<ResultadoOperacao> EditarDadosBancariosAsync(Guid dadosBancariosId, DadosBancariosInput input)
    {
        var dados = await db.DadosBancariosFornecedores.FirstOrDefaultAsync(d => d.Id == dadosBancariosId);
        if (dados is null)
        {
            return ResultadoOperacao.Falha("Registro de dados bancários não encontrado.");
        }

        if (input.Principal && !dados.Principal)
        {
            await DesmarcarPrincipalAtualAsync(dados.FornecedorId);
        }

        dados.Banco = input.Banco;
        dados.Agencia = input.Agencia;
        dados.Conta = input.Conta;
        dados.Tipo = input.Tipo;
        dados.ChavePix = input.ChavePix;
        dados.Principal = input.Principal;

        await db.SaveChangesAsync();
        return ResultadoOperacao.Ok();
    }

    public async Task<ResultadoOperacao> RemoverDadosBancariosAsync(Guid dadosBancariosId)
    {
        var dados = await db.DadosBancariosFornecedores.FirstOrDefaultAsync(d => d.Id == dadosBancariosId);
        if (dados is null)
        {
            return ResultadoOperacao.Falha("Registro de dados bancários não encontrado.");
        }

        // Dados bancários não são referenciados por ContaPagar/Pagamento (ao
        // contrário de Fornecedor) — remoção física aqui é aceitável, não é
        // um registro financeiro em si, só um cadastro de referência.
        db.DadosBancariosFornecedores.Remove(dados);
        await db.SaveChangesAsync();
        return ResultadoOperacao.Ok();
    }

    private async Task DesmarcarPrincipalAtualAsync(Guid fornecedorId)
    {
        var atuais = await db.DadosBancariosFornecedores
            .Where(d => d.FornecedorId == fornecedorId && d.Principal)
            .ToListAsync();

        foreach (var atual in atuais)
        {
            atual.Principal = false;
        }
    }

    private async Task SalvarOuLancarCnpjDuplicadoAsync()
    {
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("CnpjCpf", StringComparison.Ordinal) == true)
        {
            throw new InvalidOperationException("Já existe um fornecedor ativo com esse CNPJ/CPF.", ex);
        }
    }

    /// <summary>
    /// Normaliza CNPJ/CPF removendo pontuação antes de gravar/comparar —
    /// evita que "12.345.678/0001-99" e "12345678000199" coexistam como
    /// fornecedores "diferentes" no índice único.
    /// </summary>
    private static string SomenteDigitos(string valor) => new(valor.Where(char.IsDigit).ToArray());
}
