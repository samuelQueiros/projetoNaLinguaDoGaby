using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ErpFinanceiro.Application;
using ErpFinanceiro.Application.Anexos;
using ErpFinanceiro.Application.Auditoria;
using ErpFinanceiro.Application.Fornecedores;
using ErpFinanceiro.Domain;
using ErpFinanceiro.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpFinanceiro.Infrastructure.Fornecedores;

public sealed class GerenciadorFornecedores(AppDbContext db, IRegistradorAuditoria auditoria, UserManager<Usuario> userManager,
    IArmazenamentoAnexos storage, ILogger<GerenciadorFornecedores> logger)
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

        var erroValidacao = ValidarDadosBancarios(input);
        if (erroValidacao is not null)
        {
            return erroValidacao;
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
            NomeTitular = input.NomeTitular,
            CpfCnpjTitular = SomenteDigitos(input.CpfCnpjTitular),
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

        var erroValidacao = ValidarDadosBancarios(input);
        if (erroValidacao is not null)
        {
            return erroValidacao;
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
        dados.NomeTitular = input.NomeTitular;
        dados.CpfCnpjTitular = SomenteDigitos(input.CpfCnpjTitular);
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

    public async Task<ResultadoCriacao<ContratoFornecedor>> AdicionarContratoAsync(Guid fornecedorId, NovoContratoInput input, Guid usuarioId)
    {
        if (input.VigenciaFim < input.VigenciaInicio)
        {
            return ResultadoCriacao<ContratoFornecedor>.Falha("A vigência final não pode ser anterior à inicial.");
        }

        var fornecedor = await db.Fornecedores.FirstOrDefaultAsync(f => f.Id == fornecedorId && f.ExcluidoEm == null);
        if (fornecedor is null)
        {
            return ResultadoCriacao<ContratoFornecedor>.Falha("Fornecedor não encontrado.");
        }

        // Pasta legível por fornecedor/ano de vigência (pedido de negócio,
        // facilita achar o contrato certo navegando o disco); o nome físico
        // do arquivo continua gerado pelo servidor (Guid), nunca o nome
        // original do usuário — mesma blindagem de ArmazenamentoAnexosDisco.
        var pasta = $"onrtdpj/fornecedores/{input.VigenciaInicio.Year}/{PastaDoFornecedor(fornecedor)}";
        var nomeFisico = $"{Guid.NewGuid():N}{Path.GetExtension(input.NomeOriginal)}";

        ArquivoArmazenado armazenado;
        try
        {
            armazenado = await storage.SalvarEmAsync(pasta, nomeFisico, input.Conteudo, input.TipoConteudo);
        }
        catch (InvalidOperationException ex)
        {
            return ResultadoCriacao<ContratoFornecedor>.Falha(ex.Message);
        }

        var contrato = new ContratoFornecedor
        {
            Id = Guid.NewGuid(),
            FornecedorId = fornecedorId,
            Nome = input.Nome,
            VigenciaInicio = input.VigenciaInicio,
            VigenciaFim = input.VigenciaFim,
            NomeArquivo = Path.GetFileName(input.NomeOriginal),
            CaminhoArmazenamento = armazenado.CaminhoRelativo,
            TamanhoBytes = armazenado.TamanhoBytes,
            TipoConteudo = armazenado.TipoConteudo,
            EnviadoPorId = usuarioId,
        };

        db.ContratosFornecedor.Add(contrato);
        await db.SaveChangesAsync();

        await auditoria.RegistrarAsync(usuarioId, "AnexarContratoFornecedor", nameof(Fornecedor), fornecedorId,
            null, new { contrato.Nome, contrato.VigenciaInicio, contrato.VigenciaFim, contrato.NomeArquivo });

        return ResultadoCriacao<ContratoFornecedor>.Ok(contrato);
    }

    public async Task<IReadOnlyList<ContratoFornecedor>> ListarContratosAsync(Guid fornecedorId) =>
        await db.ContratosFornecedor.AsNoTracking()
            .Where(c => c.FornecedorId == fornecedorId)
            .OrderByDescending(c => c.VigenciaFim)
            .ToListAsync();

    public async Task<ContratoParaDownload?> BaixarContratoAsync(Guid contratoId)
    {
        var contrato = await db.ContratosFornecedor.AsNoTracking().FirstOrDefaultAsync(c => c.Id == contratoId);
        if (contrato is null)
        {
            return null;
        }

        var conteudo = await storage.AbrirAsync(contrato.CaminhoArmazenamento);
        return new ContratoParaDownload(conteudo, contrato.NomeArquivo, contrato.TipoConteudo);
    }

    public async Task<ResultadoOperacao> RemoverContratoAsync(Guid contratoId, Guid usuarioId)
    {
        var contrato = await db.ContratosFornecedor.FirstOrDefaultAsync(c => c.Id == contratoId);
        if (contrato is null)
        {
            return ResultadoOperacao.Falha("Contrato não encontrado.");
        }

        // Mesma ordem de DadosBancarios/Anexo: apaga o arquivo físico ANTES
        // de remover o registro do banco — se o delete falhar, a operação
        // inteira falha e nada muda, evitando arquivo órfão em disco.
        try
        {
            await storage.ExcluirAsync(contrato.CaminhoArmazenamento);
        }
        catch (IOException ex)
        {
            logger.LogError(ex, "Falha ao excluir o arquivo físico do contrato {ContratoId} ({Caminho}).", contrato.Id, contrato.CaminhoArmazenamento);
            return ResultadoOperacao.Falha("Não consegui excluir o arquivo — tente de novo em instantes.");
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.LogError(ex, "Falha ao excluir o arquivo físico do contrato {ContratoId} ({Caminho}).", contrato.Id, contrato.CaminhoArmazenamento);
            return ResultadoOperacao.Falha("Não consegui excluir o arquivo — tente de novo em instantes.");
        }

        db.ContratosFornecedor.Remove(contrato);
        await db.SaveChangesAsync();

        await auditoria.RegistrarAsync(usuarioId, "ExcluirContratoFornecedor", nameof(Fornecedor), contrato.FornecedorId,
            new { contrato.Nome, contrato.NomeArquivo }, null);

        return ResultadoOperacao.Ok();
    }

    /// <summary>
    /// Só campos não sensíveis — nunca Conta/ChavePix/CpfCnpjTitular, que são
    /// cifrados em repouso (AES-256-GCM) especificamente para não circular em
    /// texto plano; gravar o valor decifrado no JSONB da auditoria anularia
    /// essa proteção. "Preenchida: sim/não" basta pra rastrear que a mudança
    /// aconteceu, sem duplicar o dado sensível em outra tabela. NomeTitular
    /// não é um documento/credencial, então vai direto (mesmo tratamento de
    /// Banco/Agência).
    /// </summary>
    private static object DescricaoAuditavel(DadosBancariosFornecedor dados) => new
    {
        dados.FornecedorId,
        dados.Banco,
        dados.Agencia,
        dados.Tipo,
        dados.NomeTitular,
        dados.Principal,
        ContaPreenchida = !string.IsNullOrEmpty(dados.Conta),
        ChavePixPreenchida = !string.IsNullOrEmpty(dados.ChavePix),
        CpfCnpjTitularPreenchido = !string.IsNullOrEmpty(dados.CpfCnpjTitular),
    };

    private static ResultadoOperacao? ValidarDadosBancarios(DadosBancariosInput input)
    {
        if (string.IsNullOrWhiteSpace(input.NomeTitular))
        {
            return ResultadoOperacao.Falha("Informe o nome do titular da conta.");
        }

        if (string.IsNullOrWhiteSpace(input.CpfCnpjTitular))
        {
            return ResultadoOperacao.Falha("Informe o CPF/CNPJ do titular da conta.");
        }

        return null;
    }

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

    /// <summary>
    /// Segmento de pasta legível para o fornecedor — nome fantasia (ou
    /// razão social) reduzido a [a-z0-9-], com o começo do Id como sufixo
    /// pra garantir que dois fornecedores com nome parecido/igual nunca
    /// caiam na mesma pasta.
    /// </summary>
    private static string PastaDoFornecedor(Fornecedor fornecedor)
    {
        var slug = Slugificar(fornecedor.NomeFantasia ?? fornecedor.RazaoSocial);
        var sufixo = fornecedor.Id.ToString("N")[..8];
        return string.IsNullOrEmpty(slug) ? sufixo : $"{slug}-{sufixo}";
    }

    /// <summary>
    /// Converte texto livre num segmento de pasta seguro: minúsculo, sem
    /// acentos, só [a-z0-9-]. Path traversal já é bloqueado pelo storage
    /// de qualquer forma (ResolverDentroDaRaiz) — isso aqui é só pra pasta
    /// ficar legível e não esbarrar em caracteres inválidos no disco.
    /// </summary>
    private static string Slugificar(string texto)
    {
        var normalizado = texto.Normalize(NormalizationForm.FormD);
        var semAcento = new string(normalizado.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray());
        var slug = Regex.Replace(semAcento.ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
        return slug.Length > 80 ? slug[..80].Trim('-') : slug;
    }
}
