using System.Text.Json;
using ErpFinanceiro.Application.Boletos;
using ErpFinanceiro.Application.ContasAPagar;
using ErpFinanceiro.Application.Dashboard;
using ErpFinanceiro.Application.DocumentosIa;
using ErpFinanceiro.Application.Fornecedores;
using ErpFinanceiro.Application.NotasFiscais;
using ErpFinanceiro.Domain;
using Microsoft.AspNetCore.Identity;

namespace ErpFinanceiro.Infrastructure.ChatIa;

/// <summary>
/// Executa uma ferramenta do <see cref="ErpFinanceiro.Application.ChatIa.CatalogoFerramentasChatIa"/>
/// dado o nome + argumentos que o LLM mandou. Só injeta métodos de LEITURA
/// das interfaces já existentes (ListarAsync/ObterAsync) — nenhum
/// CriarAsync/EditarAsync/AprovarAsync/ExcluirAsync é alcançável a partir
/// daqui, então dá pra auditar "o chat não escreve nada" só olhando o
/// construtor desta classe.
///
/// Os resultados são projetados em DTOs próprios (records privados abaixo),
/// nunca a entidade de domínio crua — evita que um campo sensível ou uma
/// navegação de EF grande demais vaze pro prompt do LLM (mesma disciplina
/// já usada em IRegistradorAuditoria, que bloqueia passar entidade crua).
/// </summary>
public sealed class ExecutorFerramentasChatIa(
    IPainelFinanceiro painel,
    IGerenciadorContasPagar contasPagar,
    IGerenciadorFornecedores fornecedores,
    IGerenciadorDocumentosImportados documentos,
    IGerenciadorNotasFiscais notasFiscais,
    IGerenciadorBoletos boletos,
    UserManager<Usuario> userManager)
{
    public async Task<object> ExecutarAsync(string nomeFerramenta, JsonElement argumentos, Guid usuarioId, CancellationToken ct)
    {
        return nomeFerramenta switch
        {
            "consultar_indicadores_financeiros" => await ConsultarIndicadoresAsync(),
            "listar_contas_a_pagar" => await ListarContasAPagarAsync(argumentos),
            "listar_fornecedores" => await ListarFornecedoresAsync(argumentos),
            "listar_documentos_pendentes_revisao" => await ListarDocumentosPendentesAsync(argumentos, usuarioId),
            "consultar_documento_importado" => await ConsultarDocumentoAsync(argumentos, usuarioId),
            "listar_notas_fiscais" => await ListarNotasFiscaisAsync(argumentos),
            "listar_boletos" => await ListarBoletosAsync(argumentos),
            _ => new { erro = $"Ferramenta desconhecida: '{nomeFerramenta}'." },
        };
    }

    /// <summary>
    /// Documento importado ainda não revisado é visível só pra quem enviou
    /// ou pra Administrador — mesma regra aplicada em CentralDocumentos.razor
    /// e no endpoint de download (achado da auditoria de segurança: o chat
    /// era um segundo caminho pro mesmo vazamento, "apenasDoUsuarioAtual"
    /// era opcional e listava/mostrava documento de qualquer usuário por
    /// padrão).
    /// </summary>
    private async Task<bool> EhAdministradorAsync(Guid usuarioId)
    {
        var usuario = await userManager.FindByIdAsync(usuarioId.ToString());
        return usuario is not null && await userManager.IsInRoleAsync(usuario, nameof(PerfilUsuario.Administrador));
    }

    private async Task<object> ConsultarIndicadoresAsync()
    {
        var i = await painel.ObterAsync();
        return new
        {
            totalEmAberto = i.TotalEmAberto,
            quantidadeEmAberto = i.QuantidadeEmAberto,
            totalVencidas = i.TotalVencidas,
            quantidadeVencidas = i.QuantidadeVencidas,
            totalAPagarHoje = i.TotalAPagarHoje,
            totalAPagarProximos7Dias = i.TotalAPagarProximos7Dias,
            totalAPagarNoMes = i.TotalAPagarNoMes,
            totalPagoNoMes = i.TotalPagoNoMes,
            totalPagoNoAno = i.TotalPagoNoAno,
            totalDespesasCartaoNoAno = i.TotalDespesasCartaoNoAno,
            totalPagamentoNaoIdentificado = i.TotalPagamentoNaoIdentificado,
            quantidadePagamentoNaoIdentificado = i.QuantidadePagamentoNaoIdentificado,
            proximosVencimentos = i.ProximosVencimentos.Select(v => new
            {
                fornecedor = v.Fornecedor,
                descricao = v.Descricao,
                vencimento = v.Vencimento.ToString("yyyy-MM-dd"),
                valorFinal = v.ValorFinal,
            }),
        };
    }

    private async Task<object> ListarContasAPagarAsync(JsonElement args)
    {
        var resolucao = await ResolverFornecedorIdAsync(ObterString(args, "fornecedorNome"));
        if (resolucao.NomeNaoEncontrado)
        {
            return new { erro = "Nenhum fornecedor encontrado com esse nome." };
        }
        var fornecedorId = resolucao.FornecedorId;

        StatusFinanceiro? statusFinanceiro = null;
        if (ObterString(args, "statusFinanceiro") is { } sf && Enum.TryParse<StatusFinanceiro>(sf, true, out var parsedSf))
        {
            statusFinanceiro = parsedSf;
        }

        StatusAprovacao? statusAprovacao = null;
        if (ObterString(args, "statusAprovacao") is { } sa && Enum.TryParse<StatusAprovacao>(sa, true, out var parsedSa))
        {
            statusAprovacao = parsedSa;
        }

        var filtro = new FiltroContasPagar(
            FornecedorId: fornecedorId,
            StatusFinanceiro: statusFinanceiro,
            StatusAprovacao: statusAprovacao,
            VencimentoInicial: ObterData(args, "vencimentoInicial"),
            VencimentoFinal: ObterData(args, "vencimentoFinal"));

        var contas = await contasPagar.ListarAsync(filtro);
        return new
        {
            total = contas.Count,
            contas = contas.Take(50).Select(c => new
            {
                id = c.Id,
                fornecedor = c.Fornecedor?.RazaoSocial,
                descricao = c.Descricao,
                vencimento = c.Vencimento.ToString("yyyy-MM-dd"),
                valorFinal = c.ValorFinal,
                statusFinanceiro = c.StatusFinanceiro.ToString(),
                statusAprovacao = c.StatusAprovacao.ToString(),
            }),
        };
    }

    private async Task<object> ListarFornecedoresAsync(JsonElement args)
    {
        var todos = await fornecedores.ListarAsync();
        var filtro = ObterString(args, "nomeContem");
        var filtrados = string.IsNullOrWhiteSpace(filtro)
            ? todos
            : todos.Where(f =>
                f.RazaoSocial.Contains(filtro, StringComparison.OrdinalIgnoreCase) ||
                (f.NomeFantasia?.Contains(filtro, StringComparison.OrdinalIgnoreCase) ?? false)).ToList();

        return new
        {
            total = filtrados.Count,
            fornecedores = filtrados.Take(50).Select(f => new
            {
                id = f.Id,
                razaoSocial = f.RazaoSocial,
                nomeFantasia = f.NomeFantasia,
                formaPagamentoPadrao = f.FormaPagamentoPadrao?.Nome,
            }),
        };
    }

    private async Task<object> ListarDocumentosPendentesAsync(JsonElement args, Guid usuarioId)
    {
        var apenasDoUsuario = ObterBool(args, "apenasDoUsuarioAtual") ?? false;
        var ehAdministrador = await EhAdministradorAsync(usuarioId);
        var filtro = new FiltroDocumentosImportados(
            Status: StatusImportacaoDocumento.AguardandoRevisao,
            EnviadoPorId: (apenasDoUsuario || !ehAdministrador) ? usuarioId : null);

        var lista = await documentos.ListarAsync(filtro);
        return new
        {
            total = lista.Count,
            documentos = lista.Take(50).Select(d => new
            {
                id = d.Id,
                nomeArquivo = d.NomeArquivo,
                tipoDetectado = d.TipoDetectado.ToString(),
                confiancaGeral = d.ConfiancaGeral,
                enviadoPor = d.EnviadoPor?.Nome,
                criadoEm = d.CriadoEm.ToString("yyyy-MM-dd HH:mm"),
            }),
        };
    }

    private async Task<object> ConsultarDocumentoAsync(JsonElement args, Guid usuarioId)
    {
        var idTexto = ObterString(args, "documentoId");
        if (idTexto is null || !Guid.TryParse(idTexto, out var id))
        {
            return new { erro = "documentoId inválido ou não informado." };
        }

        var doc = await documentos.ObterAsync(id);
        if (doc is null)
        {
            return new { erro = "Documento não encontrado." };
        }

        if (doc.EnviadoPorId != usuarioId && !await EhAdministradorAsync(usuarioId))
        {
            return new { erro = "Documento não encontrado." };
        }

        return new
        {
            id = doc.Id,
            nomeArquivo = doc.NomeArquivo,
            status = doc.Status.ToString(),
            tipoDetectado = doc.TipoDetectado.ToString(),
            confiancaGeral = doc.ConfiancaGeral,
            campos = doc.Campos.Select(c => new { nome = c.Nome, valor = c.Valor, confianca = c.Confianca }),
            resumo = doc.Resumo,
            // Trecho, não o texto inteiro — isto é consulta sob demanda de UM
            // documento (o usuário já pediu por ele), não o contexto padrão
            // de toda mensagem do chat; ainda assim não faz sentido mandar
            // dezenas de milhares de caracteres pro prompt de uma vez.
            textoExtraidoResumido = doc.TextoExtraido is { Length: > 0 } texto
                ? texto[..Math.Min(texto.Length, 4000)]
                : null,
            mensagemErro = doc.MensagemErro,
            motivoRejeicao = doc.MotivoRejeicao,
        };
    }

    private async Task<object> ListarNotasFiscaisAsync(JsonElement args)
    {
        var resolucao = await ResolverFornecedorIdAsync(ObterString(args, "fornecedorNome"));
        if (resolucao.NomeNaoEncontrado)
        {
            return new { erro = "Nenhum fornecedor encontrado com esse nome." };
        }
        var fornecedorId = resolucao.FornecedorId;

        var filtro = new FiltroNotasFiscais(
            Numero: ObterString(args, "numero"),
            FornecedorId: fornecedorId,
            EmissaoInicial: ObterData(args, "emissaoInicial"),
            EmissaoFinal: ObterData(args, "emissaoFinal"));

        var lista = await notasFiscais.ListarAsync(filtro);
        return new
        {
            total = lista.Count,
            notasFiscais = lista.Take(50).Select(n => new
            {
                id = n.Id,
                numero = n.Numero,
                fornecedor = n.Fornecedor?.RazaoSocial,
                emissao = n.Emissao.ToString("yyyy-MM-dd"),
                valor = n.Valor,
            }),
        };
    }

    private async Task<object> ListarBoletosAsync(JsonElement args)
    {
        var resolucao = await ResolverFornecedorIdAsync(ObterString(args, "fornecedorNome"));
        if (resolucao.NomeNaoEncontrado)
        {
            return new { erro = "Nenhum fornecedor encontrado com esse nome." };
        }
        var fornecedorId = resolucao.FornecedorId;

        StatusBoleto? status = null;
        if (ObterString(args, "status") is { } s && Enum.TryParse<StatusBoleto>(s, true, out var parsedStatus))
        {
            status = parsedStatus;
        }

        var filtro = new FiltroBoletos(
            FornecedorId: fornecedorId,
            Status: status,
            VencimentoInicial: ObterData(args, "vencimentoInicial"),
            VencimentoFinal: ObterData(args, "vencimentoFinal"));

        var lista = await boletos.ListarAsync(filtro);
        return new
        {
            total = lista.Count,
            boletos = lista.Take(50).Select(b => new
            {
                id = b.Id,
                fornecedor = b.Fornecedor?.RazaoSocial,
                numero = b.Numero,
                vencimento = b.Vencimento.ToString("yyyy-MM-dd"),
                valor = b.Valor,
                status = b.Status.ToString(),
            }),
        };
    }

    /// <summary>
    /// Distingue "nome não informado" (<see cref="FornecedorId"/> null,
    /// <see cref="NomeNaoEncontrado"/> false — não filtra por fornecedor)
    /// de "nome informado mas não achou nenhum" (ambos null/true — deve dar
    /// erro claro). Antes era um <c>Guid?</c> com <c>Guid.Empty</c> como
    /// sentinela pro segundo caso — reaproveitava o mesmo valor usado em
    /// vários outros lugares do código pra "usuário não autenticado",
    /// coincidência frágil (achado da auditoria de qualidade).
    /// </summary>
    private readonly record struct ResolucaoFornecedor(Guid? FornecedorId, bool NomeNaoEncontrado);

    private async Task<ResolucaoFornecedor> ResolverFornecedorIdAsync(string? nomeParcial)
    {
        if (string.IsNullOrWhiteSpace(nomeParcial))
        {
            return new ResolucaoFornecedor(null, false);
        }

        var todos = await fornecedores.ListarAsync();
        var encontrado = todos.FirstOrDefault(f =>
            f.RazaoSocial.Contains(nomeParcial, StringComparison.OrdinalIgnoreCase) ||
            (f.NomeFantasia?.Contains(nomeParcial, StringComparison.OrdinalIgnoreCase) ?? false));

        return encontrado is null
            ? new ResolucaoFornecedor(null, true)
            : new ResolucaoFornecedor(encontrado.Id, false);
    }

    private static string? ObterString(JsonElement args, string nome)
    {
        if (args.ValueKind != JsonValueKind.Object || !args.TryGetProperty(nome, out var prop))
        {
            return null;
        }

        var valor = prop.ValueKind == JsonValueKind.String ? prop.GetString() : null;
        return string.IsNullOrWhiteSpace(valor) ? null : valor;
    }

    private static bool? ObterBool(JsonElement args, string nome)
    {
        if (args.ValueKind != JsonValueKind.Object || !args.TryGetProperty(nome, out var prop))
        {
            return null;
        }

        return prop.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null,
        };
    }

    private static DateOnly? ObterData(JsonElement args, string nome)
    {
        var texto = ObterString(args, nome);
        return texto is not null && DateOnly.TryParse(texto, out var data) ? data : null;
    }
}
