using System.Security.Claims;
using ErpFinanceiro.Application.Anexos;
using ErpFinanceiro.Application.Auditoria;
using ErpFinanceiro.Application.Boletos;
using ErpFinanceiro.Application.Cartoes;
using ErpFinanceiro.Application.Categorias;
using ErpFinanceiro.Application.ChatIa;
using ErpFinanceiro.Application.ConfiguracoesIa;
using ErpFinanceiro.Application.ContasAPagar;
using ErpFinanceiro.Application.Dashboard;
using ErpFinanceiro.Application.DocumentosIa;
using ErpFinanceiro.Application.Fornecedores;
using ErpFinanceiro.Application.NotasFiscais;
using ErpFinanceiro.Application.Usuarios;
using ErpFinanceiro.Domain;
using Microsoft.AspNetCore.Identity;

namespace ErpFinanceiro.Web;

public static class ReactApiEndpoints
{
    public sealed record LoginRequest(string Email, string Password, bool RememberMe);
    public sealed record MotivoRequest(string Motivo);
    public sealed record PerfilRequest(PerfilUsuario Perfil);
    public sealed record CadastroRequest(string Nome, string? Descricao);
    public sealed record ChatRequest(IReadOnlyList<MensagemChatIa> Historico, string Mensagem);

    public static void MapReactApi(this WebApplication app)
    {
        var auth = app.MapGroup("/api/auth");
        auth.MapPost("/login", Login);
        auth.MapGet("/me", Me).RequireAuthorization();
        auth.MapPost("/logout", async (SignInManager<Usuario> s) => { await s.SignOutAsync(); return Results.NoContent(); }).RequireAuthorization();

        var api = app.MapGroup("/api").RequireAuthorization();
        api.MapGet("/dashboard", (IPainelFinanceiro g) => g.ObterAsync());
        Cadastros(api); Fornecedores(api); Contas(api); BancosCartoes(api);
        NotasBoletos(api); Documentos(api); Anexos(api); Administracao(api);
    }

    static async Task<IResult> Login(LoginRequest r, SignInManager<Usuario> sm, UserManager<Usuario> um)
    {
        var login = await sm.PasswordSignInAsync(r.Email, r.Password, r.RememberMe, true);
        if (!login.Succeeded) return Results.Json(new { mensagem = login.IsLockedOut ? "Usuário bloqueado ou inativo." : "E-mail ou senha inválidos." }, statusCode: 401);
        var u = await um.FindByEmailAsync(r.Email);
        if (u is not null) { u.UltimoLogin = DateTime.UtcNow; await um.UpdateAsync(u); }
        if (u is null) return Results.Unauthorized();
        var roles = await um.GetRolesAsync(u);
        return Results.Ok(new { u.Id, u.Nome, u.Email, Papeis = roles, EhAdministrador = roles.Contains(nameof(PerfilUsuario.Administrador)) });
    }

    static async Task<IResult> Me(ClaimsPrincipal p, UserManager<Usuario> um)
    {
        var u = await um.GetUserAsync(p);
        if (u is null || !u.Ativo) return Results.Unauthorized();
        var roles = await um.GetRolesAsync(u);
        return Results.Ok(new { u.Id, u.Nome, u.Email, Papeis = roles, EhAdministrador = roles.Contains(nameof(PerfilUsuario.Administrador)) });
    }

    static void Cadastros(RouteGroupBuilder a)
    {
        a.MapGet("/categorias", (bool apenasAtivos, IGerenciadorCadastroSimples<Categoria> g) => g.ListarAsync(apenasAtivos));
        a.MapPost("/categorias", (CadastroRequest r, IGerenciadorCadastroSimples<Categoria> g) => g.CriarAsync(r.Nome, r.Descricao));
        a.MapPut("/categorias/{id:guid}", (Guid id, CadastroRequest r, IGerenciadorCadastroSimples<Categoria> g) => g.EditarAsync(id, r.Nome, r.Descricao));
        a.MapPost("/categorias/{id:guid}/inativar", (Guid id, IGerenciadorCadastroSimples<Categoria> g) => g.InativarAsync(id));
        a.MapPost("/categorias/{id:guid}/reativar", (Guid id, IGerenciadorCadastroSimples<Categoria> g) => g.ReativarAsync(id));
        a.MapGet("/centros-custo", (bool apenasAtivos, IGerenciadorCadastroSimples<CentroCusto> g) => g.ListarAsync(apenasAtivos));
        a.MapPost("/centros-custo", (CadastroRequest r, IGerenciadorCadastroSimples<CentroCusto> g) => g.CriarAsync(r.Nome, r.Descricao));
        a.MapPut("/centros-custo/{id:guid}", (Guid id, CadastroRequest r, IGerenciadorCadastroSimples<CentroCusto> g) => g.EditarAsync(id, r.Nome, r.Descricao));
        a.MapPost("/centros-custo/{id:guid}/inativar", (Guid id, IGerenciadorCadastroSimples<CentroCusto> g) => g.InativarAsync(id));
        a.MapPost("/centros-custo/{id:guid}/reativar", (Guid id, IGerenciadorCadastroSimples<CentroCusto> g) => g.ReativarAsync(id));
        a.MapGet("/formas-pagamento", (bool apenasAtivos, IGerenciadorCadastroSimples<FormaPagamento> g) => g.ListarAsync(apenasAtivos));
    }

    static void Fornecedores(RouteGroupBuilder a)
    {
        a.MapGet("/fornecedores", (bool incluirExcluidos, IGerenciadorFornecedores g) => g.ListarAsync(incluirExcluidos));
        a.MapGet("/fornecedores/{id:guid}", async (Guid id, IGerenciadorFornecedores g) => await g.ObterAsync(id) is { } x ? Results.Ok(x) : Results.NotFound());
        a.MapPost("/fornecedores", (CriarFornecedorInput r, IGerenciadorFornecedores g) => g.CriarAsync(r));
        a.MapPut("/fornecedores/{id:guid}", (Guid id, CriarFornecedorInput r, IGerenciadorFornecedores g) => g.EditarAsync(id, r));
        a.MapDelete("/fornecedores/{id:guid}", (Guid id, IGerenciadorFornecedores g) => g.ExcluirAsync(id));
        a.MapPost("/fornecedores/{id:guid}/dados-bancarios", async (Guid id, DadosBancariosInput r, ClaimsPrincipal p, UserManager<Usuario> um, IGerenciadorFornecedores g) => await g.AdicionarDadosBancariosAsync(id, r, await Uid(p, um)));
        a.MapPut("/fornecedores/dados-bancarios/{id:guid}", async (Guid id, DadosBancariosInput r, ClaimsPrincipal p, UserManager<Usuario> um, IGerenciadorFornecedores g) => await g.EditarDadosBancariosAsync(id, r, await Uid(p, um)));
        a.MapDelete("/fornecedores/dados-bancarios/{id:guid}", async (Guid id, ClaimsPrincipal p, UserManager<Usuario> um, IGerenciadorFornecedores g) => await g.RemoverDadosBancariosAsync(id, await Uid(p, um)));
    }

    static void Contas(RouteGroupBuilder a)
    {
        a.MapGet("/contas-a-pagar", (Guid? fornecedorId, StatusFinanceiro? statusFinanceiro, StatusAprovacao? statusAprovacao, DateOnly? vencimentoInicial, DateOnly? vencimentoFinal, IGerenciadorContasPagar g) => g.ListarAsync(new(fornecedorId, statusFinanceiro, statusAprovacao, vencimentoInicial, vencimentoFinal)));
        a.MapGet("/contas-a-pagar/{id:guid}", async (Guid id, IGerenciadorContasPagar g) => await g.ObterAsync(id) is { } x ? Results.Ok(x) : Results.NotFound());
        a.MapPost("/contas-a-pagar", async (ContaPagarInput r, ClaimsPrincipal p, UserManager<Usuario> um, IGerenciadorContasPagar g) => await g.CriarAsync(r, await Uid(p, um)));
        a.MapPut("/contas-a-pagar/{id:guid}", async (Guid id, ContaPagarInput r, ClaimsPrincipal p, UserManager<Usuario> um, IGerenciadorContasPagar g) => await g.EditarAsync(id, r, await Uid(p, um)));
        a.MapDelete("/contas-a-pagar/{id:guid}", async (Guid id, ClaimsPrincipal p, UserManager<Usuario> um, IGerenciadorContasPagar g) => await g.ExcluirAsync(id, await Uid(p, um)));
        a.MapPost("/contas-a-pagar/{id:guid}/aprovar", async (Guid id, ClaimsPrincipal p, UserManager<Usuario> um, IFluxoAprovacao g) => await g.AprovarAsync(id, await Uid(p, um)));
        a.MapPost("/contas-a-pagar/{id:guid}/rejeitar", async (Guid id, MotivoRequest r, ClaimsPrincipal p, UserManager<Usuario> um, IFluxoAprovacao g) => await g.RejeitarAsync(id, await Uid(p, um), r.Motivo));
        a.MapGet("/contas-a-pagar/{id:guid}/pagamentos", (Guid id, IGerenciadorPagamentos g) => g.ListarPorContaAsync(id));
        a.MapPost("/contas-a-pagar/{id:guid}/pagamentos", async (Guid id, RegistrarPagamentoInput r, ClaimsPrincipal p, UserManager<Usuario> um, IGerenciadorPagamentos g) => await g.RegistrarAsync(id, r, await Uid(p, um)));
        a.MapPost("/pagamentos/{id:guid}/estornar", async (Guid id, MotivoRequest r, ClaimsPrincipal p, UserManager<Usuario> um, IGerenciadorPagamentos g) => await g.EstornarAsync(id, r.Motivo, await Uid(p, um)));
        a.MapGet("/contas-a-pagar/{id:guid}/timeline", (Guid id, ITimelineConsulta g) => g.ObterAsync(nameof(ContaPagar), id));
    }

    static void BancosCartoes(RouteGroupBuilder a)
    {
        a.MapGet("/contas-bancarias", (bool apenasAtivas, IGerenciadorContasBancariasEmpresa g) => g.ListarAsync(apenasAtivas));
        a.MapPost("/contas-bancarias", async (ContaBancariaEmpresaInput r, ClaimsPrincipal p, UserManager<Usuario> um, IGerenciadorContasBancariasEmpresa g) => await g.CriarAsync(r, await Uid(p, um)));
        a.MapPut("/contas-bancarias/{id:guid}", async (Guid id, ContaBancariaEmpresaInput r, ClaimsPrincipal p, UserManager<Usuario> um, IGerenciadorContasBancariasEmpresa g) => await g.EditarAsync(id, r, await Uid(p, um)));
        a.MapPost("/contas-bancarias/{id:guid}/{acao}", async (Guid id, string acao, ClaimsPrincipal p, UserManager<Usuario> um, IGerenciadorContasBancariasEmpresa g) => acao == "reativar" ? await g.ReativarAsync(id, await Uid(p, um)) : await g.InativarAsync(id, await Uid(p, um)));
        a.MapGet("/cartoes", (IGerenciadorCartoes g) => g.ListarAsync());
        a.MapPost("/cartoes", async (CartaoInput r, ClaimsPrincipal p, UserManager<Usuario> um, IGerenciadorCartoes g) => await g.CriarAsync(r, await Uid(p, um)));
        a.MapPut("/cartoes/{id:guid}", async (Guid id, CartaoInput r, ClaimsPrincipal p, UserManager<Usuario> um, IGerenciadorCartoes g) => await g.EditarAsync(id, r, await Uid(p, um)));
        a.MapPost("/cartoes/{id:guid}/status/{status}", async (Guid id, StatusCartao status, ClaimsPrincipal p, UserManager<Usuario> um, IGerenciadorCartoes g) => await g.AlterarStatusAsync(id, status, await Uid(p, um)));
    }

    static void NotasBoletos(RouteGroupBuilder a)
    {
        a.MapGet("/notas-fiscais", (string? numero, Guid? fornecedorId, string? cnpjCpf, DateOnly? emissaoInicial, DateOnly? emissaoFinal, int? mes, int? ano, Guid? contaPagarId, IGerenciadorNotasFiscais g) => g.ListarAsync(new(numero, fornecedorId, cnpjCpf, emissaoInicial, emissaoFinal, mes, ano, contaPagarId)));
        a.MapPost("/notas-fiscais", async (NotaFiscalInput r, ClaimsPrincipal p, UserManager<Usuario> um, IGerenciadorNotasFiscais g) => await g.CriarAsync(r, await Uid(p, um)));
        a.MapPut("/notas-fiscais/{id:guid}", async (Guid id, NotaFiscalInput r, ClaimsPrincipal p, UserManager<Usuario> um, IGerenciadorNotasFiscais g) => await g.EditarAsync(id, r, await Uid(p, um)));
        a.MapDelete("/notas-fiscais/{id:guid}", async (Guid id, ClaimsPrincipal p, UserManager<Usuario> um, IGerenciadorNotasFiscais g) => await g.ExcluirAsync(id, await Uid(p, um)));
        a.MapGet("/boletos", (Guid? fornecedorId, Guid? contaPagarId, StatusBoleto? status, DateOnly? vencimentoInicial, DateOnly? vencimentoFinal, int? mes, int? ano, IGerenciadorBoletos g) => g.ListarAsync(new(fornecedorId, contaPagarId, status, vencimentoInicial, vencimentoFinal, mes, ano)));
        a.MapPost("/boletos", async (BoletoInput r, ClaimsPrincipal p, UserManager<Usuario> um, IGerenciadorBoletos g) => await g.CriarAsync(r, await Uid(p, um)));
        a.MapPut("/boletos/{id:guid}", async (Guid id, BoletoInput r, ClaimsPrincipal p, UserManager<Usuario> um, IGerenciadorBoletos g) => await g.EditarAsync(id, r, await Uid(p, um)));
        a.MapDelete("/boletos/{id:guid}", async (Guid id, ClaimsPrincipal p, UserManager<Usuario> um, IGerenciadorBoletos g) => await g.ExcluirAsync(id, await Uid(p, um)));
    }

    static void Documentos(RouteGroupBuilder a)
    {
        a.MapGet("/documentos", async (StatusImportacaoDocumento? status, string? busca, DateOnly? dataInicial, DateOnly? dataFinal, TipoDocumentoDetectado? tipoDetectado, bool? comErro, int pagina, int tamanhoPagina, ClaimsPrincipal p, UserManager<Usuario> um, IGerenciadorDocumentosImportados g) => {
            var u = await um.GetUserAsync(p); var admin = u is not null && await um.IsInRoleAsync(u, nameof(PerfilUsuario.Administrador));
            return await g.ListarPaginadoAsync(new(status, admin ? null : u?.Id, busca, dataInicial, dataFinal, tipoDetectado, comErro, Math.Max(1, pagina), tamanhoPagina <= 0 ? 20 : tamanhoPagina));
        });
        a.MapGet("/documentos/indicadores", async (ClaimsPrincipal p, UserManager<Usuario> um, IGerenciadorDocumentosImportados g) => { var u = await um.GetUserAsync(p); return await g.ObterIndicadoresAsync(u is not null && await um.IsInRoleAsync(u, nameof(PerfilUsuario.Administrador)) ? null : u?.Id); });
        a.MapGet("/documentos/{id:guid}", async (Guid id, IGerenciadorDocumentosImportados g) => await g.ObterAsync(id) is { } x ? Results.Ok(x) : Results.NotFound());
        a.MapPost("/documentos", async (IFormFileCollection arquivos, ClaimsPrincipal p, UserManager<Usuario> um, IGerenciadorDocumentosImportados g) => { var xs = arquivos.Select(x => new ArquivoEnviado(x.OpenReadStream(), x.FileName, x.ContentType)).ToList(); try { return Results.Ok(await g.EnviarAsync(xs, await Uid(p, um))); } finally { xs.ForEach(x => x.Conteudo.Dispose()); } }).DisableAntiforgery();
        a.MapPost("/documentos/{id:guid}/reprocessar", async (Guid id, ClaimsPrincipal p, UserManager<Usuario> um, IGerenciadorDocumentosImportados g) => await g.ReprocessarAsync(id, await Uid(p, um)));
        a.MapPost("/documentos/{id:guid}/aprovar", async (Guid id, RevisaoDocumentoInput r, ClaimsPrincipal p, UserManager<Usuario> um, IGerenciadorDocumentosImportados g) => await g.AprovarAsync(id, r, await Uid(p, um)));
        a.MapPost("/documentos/{id:guid}/rejeitar", async (Guid id, MotivoRequest r, ClaimsPrincipal p, UserManager<Usuario> um, IGerenciadorDocumentosImportados g) => await g.RejeitarAsync(id, r.Motivo, await Uid(p, um)));
        a.MapDelete("/documentos/{id:guid}", async (Guid id, ClaimsPrincipal p, UserManager<Usuario> um, IGerenciadorDocumentosImportados g) => await g.ExcluirAsync(id, await Uid(p, um)));
    }

    static void Anexos(RouteGroupBuilder a)
    {
        a.MapGet("/anexos", (EntidadeAnexo entidadeTipo, Guid entidadeId, IGerenciadorAnexos g) => g.ListarAsync(entidadeTipo, entidadeId));
        a.MapPost("/anexos", async (IFormFile arquivo, EntidadeAnexo entidadeTipo, Guid entidadeId, TipoDocumentoAnexo tipoDocumento, ClaimsPrincipal p, UserManager<Usuario> um, IGerenciadorAnexos g) => { await using var s = arquivo.OpenReadStream(); return Results.Ok(await g.AnexarAsync(new(entidadeTipo, entidadeId, tipoDocumento, s, arquivo.FileName, arquivo.ContentType), await Uid(p, um))); }).DisableAntiforgery();
        a.MapDelete("/anexos/{id:guid}", async (Guid id, ClaimsPrincipal p, UserManager<Usuario> um, IGerenciadorAnexos g) => await g.ExcluirAsync(id, await Uid(p, um)));
    }

    static void Administracao(RouteGroupBuilder a)
    {
        var admin = a.MapGroup("").RequireAuthorization(x => x.RequireRole(nameof(PerfilUsuario.Administrador)));
        admin.MapGet("/usuarios", async (IGerenciadorUsuarios g) => (await g.ListarComPapeisAsync()).Select(x => new { Usuario = x.Usuario, Papeis = x.Papeis }));
        admin.MapPost("/usuarios", async (CriarUsuarioInput r, ClaimsPrincipal p, UserManager<Usuario> um, IGerenciadorUsuarios g) => await g.CriarUsuarioAsync(r, await Uid(p, um)));
        admin.MapPost("/usuarios/{id:guid}/desativar", async (Guid id, ClaimsPrincipal p, UserManager<Usuario> um, IGerenciadorUsuarios g) => await g.DesativarUsuarioAsync(id, await Uid(p, um)));
        admin.MapPut("/usuarios/{id:guid}/perfil", async (Guid id, PerfilRequest r, ClaimsPrincipal p, UserManager<Usuario> um, IGerenciadorUsuarios g) => await g.TrocarPerfilAsync(id, r.Perfil, await Uid(p, um)));
        admin.MapGet("/auditoria/tipos", (IConsultaAuditoria g) => g.ListarTiposEntidadeAsync());
        admin.MapGet("/auditoria", (string? tipoEntidade, DateTime? dataInicial, DateTime? dataFinal, int limite, IConsultaAuditoria g) => g.ListarAsync(new(tipoEntidade, dataInicial, dataFinal), limite <= 0 ? 200 : limite));
        admin.MapGet("/configuracoes-ia/{finalidade}", (FinalidadeConfiguracaoIa finalidade, IGerenciadorConfiguracaoIa g) => g.ObterAsync(finalidade));
        admin.MapPost("/configuracoes-ia/{finalidade}/testar", (FinalidadeConfiguracaoIa finalidade, IGerenciadorConfiguracaoIa g) => g.TestarConexaoAsync(finalidade));
        a.MapPost("/chat", async (ChatRequest r, ClaimsPrincipal p, UserManager<Usuario> um, IAgenteChatIa g, CancellationToken ct) => await g.ResponderAsync(r.Historico, r.Mensagem, await Uid(p, um), ct));
    }

    static async Task<Guid> Uid(ClaimsPrincipal p, UserManager<Usuario> um) =>
        (await um.GetUserAsync(p))?.Id ?? throw new UnauthorizedAccessException();
}

