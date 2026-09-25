using ErpFinanceiro.Application;
using ErpFinanceiro.Application.Auditoria;
using ErpFinanceiro.Application.Fornecedores;
using ErpFinanceiro.Domain;
using ErpFinanceiro.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ErpFinanceiro.Infrastructure.Fornecedores;

public sealed class GerenciadorFornecedores(AppDbContext db, IRegistradorAuditoria auditoria, UserManager<Usuario> userManager)
    : IGerenciadorFornecedores
{
    public async Task<ResultadoCriacao<Fornecedor>> CriarAsync(CriarFornecedorInput input)
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
            Ativo = input.Ativo,
        };

        db.Fornecedores.Add(fornecedor);

        try
        {
            await SalvarOuLancarCnpjDuplicadoAsync();
        }
        catch (InvalidOperationException ex)
        {
            return ResultadoCriacao<Fornecedor>.Falha(ex.Message);
        }

        return ResultadoCriacao<Fornecedor>.Ok(fornecedor);
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
        fornecedor.Ativo = input.Ativo;

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

    public async Task<ResultadoOperacao> AdicionarDadosBancariosAsync(Guid fornecedorId, DadosBancariosInput input, Guid usuarioId)
    {
        var erroPermissao = await ValidarPermissaoDadosBancariosAsync(usuarioId);
        if (erroPermissao is not null)
        {
            return erroPermissao;
        }

        var fornecedorExiste = await db.Fornecedores.AnyAsync(f => f.Id == fornecedorId && f.ExcluidoEm == null);
        if (!fornecedorExiste)
        {
            return ResultadoOperacao.Falha("Fornecedor não encontrado.");
        }

        if (input.Principal)
        {
            await DesmarcarPrincipalAtualAsync(fornecedorId);
        }

        var dadosBancarios = new DadosBancariosFornecedor
        {
            Id = Guid.NewGuid(),
            FornecedorId = fornecedorId,
            Banco = input.Banco,
            Agencia = input.Agencia,
            Conta = input.Conta,
            Tipo = input.Tipo,
            ChavePix = input.ChavePix,
            Principal = input.Principal,
        };
        db.DadosBancariosFornecedores.Add(dadosBancarios);

        await db.SaveChangesAsync();

        await auditoria.RegistrarAsync(usuarioId, "AdicionarDadosBancarios", nameof(DadosBancariosFornecedor), dadosBancarios.Id,
            null, DescricaoAuditavel(dadosBancarios));

        return ResultadoOperacao.Ok();
    }

    public async Task<ResultadoOperacao> EditarDadosBancariosAsync(Guid dadosBancariosId, DadosBancariosInput input, Guid usuarioId)
    {
        var erroPermissao = await ValidarPermissaoDadosBancariosAsync(usuarioId);
        if (erroPermissao is not null)
        {
            return erroPermissao;
        }

        var dados = await db.DadosBancariosFornecedores.FirstOrDefaultAsync(d => d.Id == dadosBancariosId);
        if (dados is null)
        {
            return ResultadoOperacao.Falha("Registro de dados bancários não encontrado.");
        }

        var anterior = DescricaoAuditavel(dados);

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

        await auditoria.RegistrarAsync(usuarioId, "EditarDadosBancarios", nameof(DadosBancariosFornecedor), dados.Id,
            anterior, DescricaoAuditavel(dados));

        return ResultadoOperacao.Ok();
    }

    public async Task<ResultadoOperacao> RemoverDadosBancariosAsync(Guid dadosBancariosId, Guid usuarioId)
    {
        var erroPermissao = await ValidarPermissaoDadosBancariosAsync(usuarioId);
        if (erroPermissao is not null)
        {
            return erroPermissao;
        }

        var dados = await db.DadosBancariosFornecedores.FirstOrDefaultAsync(d => d.Id == dadosBancariosId);
        if (dados is null)
        {
            return ResultadoOperacao.Falha("Registro de dados bancários não encontrado.");
        }

        var anterior = DescricaoAuditavel(dados);

        // Dados bancários não são referenciados por ContaPagar/Pagamento (ao
        // contrário de Fornecedor) — remoção física aqui é aceitável, não é
        // um registro financeiro em si, só um cadastro de referência.
        db.DadosBancariosFornecedores.Remove(dados);
        await db.SaveChangesAsync();

        await auditoria.RegistrarAsync(usuarioId, "RemoverDadosBancarios", nameof(DadosBancariosFornecedor), dadosBancariosId,
            anterior, null);

        return ResultadoOperacao.Ok();
    }

    /// <summary>
    /// Só campos não sensíveis — nunca Conta/ChavePix, que são cifrados em
    /// repouso (AES-256-GCM) especificamente para não circular em texto
    /// plano; gravar o valor decifrado no JSONB da auditoria anularia essa
    /// proteção. "Alterada: sim/não" basta pra rastrear que a mudança
    /// aconteceu, sem duplicar o dado sensível em outra tabela.
    /// </summary>
    private static object DescricaoAuditavel(DadosBancariosFornecedor dados) => new
    {
        dados.FornecedorId,
        dados.Banco,
        dados.Agencia,
        dados.Tipo,
        dados.Principal,
        ContaPreenchida = !string.IsNullOrEmpty(dados.Conta),
        ChavePixPreenchida = !string.IsNullOrEmpty(dados.ChavePix),
    };

    /// <summary>
    /// Cadastrar/editar/remover dados bancários (conta e chave PIX) de
    /// fornecedor é atribuição de Financeiro/Administrador — mesma convenção
    /// de GerenciadorContasPagar/GerenciadorPagamentos. Achado crítico da
    /// auditoria de segurança: nenhum dos três métodos validava papel nem
    /// registrava auditoria antes, então qualquer usuário autenticado
    /// (inclusive Consulta) podia redirecionar o PIX de um fornecedor sem
    /// deixar rastro.
    /// </summary>
    private async Task<ResultadoOperacao?> ValidarPermissaoDadosBancariosAsync(Guid usuarioId)
    {
        var usuario = await userManager.FindByIdAsync(usuarioId.ToString());
        if (usuario is null)
        {
            return ResultadoOperacao.Falha("Usuário não encontrado.");
        }

        var papeis = await userManager.GetRolesAsync(usuario);
        if (!papeis.Contains(nameof(PerfilUsuario.Financeiro)) && !papeis.Contains(nameof(PerfilUsuario.Administrador)))
        {
            return ResultadoOperacao.Falha("Só usuários com perfil Financeiro ou Administrador podem alterar dados bancários de fornecedor.");
        }

        return null;
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
