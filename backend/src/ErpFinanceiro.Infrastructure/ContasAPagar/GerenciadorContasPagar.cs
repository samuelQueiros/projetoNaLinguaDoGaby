using ErpFinanceiro.Application;
using ErpFinanceiro.Application.Auditoria;
using ErpFinanceiro.Application.ContasAPagar;
using ErpFinanceiro.Domain;
using ErpFinanceiro.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ErpFinanceiro.Infrastructure.ContasAPagar;

public sealed class GerenciadorContasPagar(AppDbContext db, IRegistradorAuditoria auditoria, UserManager<Usuario> userManager, IRelogio relogio)
    : IGerenciadorContasPagar
{
    public async Task<ResultadoContaPagar> CriarAsync(ContaPagarInput input, Guid usuarioId)
    {
        var erroPermissao = await ValidarPermissaoAsync(usuarioId);
        if (erroPermissao is not null)
        {
            return ResultadoContaPagar.Falha(erroPermissao.Erros.ToArray());
        }

        var erros = await ValidarAsync(input);
        // Só no cadastro — não na edição, que pode estar só ajustando um
        // valor de uma conta que legitimamente já venceu (StatusFinanceiro
        // Vencida). A regra é "não deixar nascer já vencida", não "nunca
        // ter vencimento no passado".
        if (input.Vencimento < relogio.Hoje())
        {
            erros.Add("O vencimento não pode ser anterior à data de hoje.");
        }

        if (erros.Count > 0)
        {
            return ResultadoContaPagar.Falha(erros.ToArray());
        }

        var conta = new ContaPagar
        {
            Id = Guid.NewGuid(),
            FornecedorId = input.FornecedorId,
            Descricao = input.Descricao,
            CategoriaId = input.CategoriaId,
            CentroCustoId = input.CentroCustoId,
            Vencimento = input.Vencimento,
            ValorOriginal = input.ValorOriginal,
            Desconto = input.Desconto,
            Juros = input.Juros,
            Multa = input.Multa,
            ValorFinal = CalculadoraValorFinal.Calcular(input.ValorOriginal, input.Desconto, input.Juros, input.Multa),
            FormaPagamentoId = input.FormaPagamentoId,
            StatusAprovacao = StatusAprovacao.Cadastrada,
            StatusFinanceiro = StatusFinanceiro.EmAberto,
            CriadoPorId = usuarioId,
        };

        db.ContasPagar.Add(conta);
        await db.SaveChangesAsync();

        // Nunca passar a entidade crua (conta) para a auditoria: em Blazor
        // Server o AppDbContext é scoped por circuito, então o EF Core pode
        // popular conta.Fornecedor via fixup de relacionamento se algum
        // outro ponto do mesmo circuito já tiver carregado esse Fornecedor
        // rastreado — vazando CnpjCpf (ou pior, dados bancários se a cadeia
        // de navegação chegar até lá) em texto plano no JSONB do log
        // (achado alto do security-auditor, Passo 21). Só campos explícitos.
        await auditoria.RegistrarAsync(usuarioId, "Criar", nameof(ContaPagar), conta.Id, null,
            new { conta.FornecedorId, conta.Descricao, conta.ValorFinal, conta.Vencimento, conta.StatusAprovacao, conta.StatusFinanceiro });

        return ResultadoContaPagar.Ok(conta);
    }

    public async Task<ResultadoOperacao> EditarAsync(Guid id, ContaPagarInput input, Guid usuarioId)
    {
        var erroPermissao = await ValidarPermissaoAsync(usuarioId);
        if (erroPermissao is not null)
        {
            return erroPermissao;
        }

        var conta = await db.ContasPagar.FirstOrDefaultAsync(c => c.Id == id && c.ExcluidoEm == null);
        if (conta is null)
        {
            return ResultadoOperacao.Falha("Conta a pagar não encontrada.");
        }

        var erros = await ValidarAsync(input);
        if (erros.Count > 0)
        {
            return ResultadoOperacao.Falha(erros.ToArray());
        }

        var valorAnterior = new { conta.FornecedorId, conta.ValorFinal, conta.Vencimento };

        conta.FornecedorId = input.FornecedorId;
        conta.Descricao = input.Descricao;
        conta.CategoriaId = input.CategoriaId;
        conta.CentroCustoId = input.CentroCustoId;
        conta.Vencimento = input.Vencimento;
        conta.ValorOriginal = input.ValorOriginal;
        conta.Desconto = input.Desconto;
        conta.Juros = input.Juros;
        conta.Multa = input.Multa;
        conta.ValorFinal = CalculadoraValorFinal.Calcular(input.ValorOriginal, input.Desconto, input.Juros, input.Multa);
        conta.FormaPagamentoId = input.FormaPagamentoId;

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            // Mesmo cenário e mesmo tratamento de FluxoAprovacao/GerenciadorPagamentos
            // (ContaPagar usa xmin como token de concorrência) — faltava aqui.
            return ResultadoOperacao.Falha("Esta conta foi alterada por outro usuário. Recarregue e tente novamente.");
        }

        await auditoria.RegistrarAsync(usuarioId, "Editar", nameof(ContaPagar), conta.Id, valorAnterior,
            new { conta.FornecedorId, conta.ValorFinal, conta.Vencimento });

        return ResultadoOperacao.Ok();
    }

    public async Task<ResultadoOperacao> ExcluirAsync(Guid id, Guid usuarioId)
    {
        var erroPermissao = await ValidarPermissaoAsync(usuarioId);
        if (erroPermissao is not null)
        {
            return erroPermissao;
        }

        var conta = await db.ContasPagar.FirstOrDefaultAsync(c => c.Id == id && c.ExcluidoEm == null);
        if (conta is null)
        {
            return ResultadoOperacao.Falha("Conta a pagar não encontrada.");
        }

        conta.ExcluidoEm = DateTime.UtcNow;

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return ResultadoOperacao.Falha("Esta conta foi alterada por outro usuário. Recarregue e tente novamente.");
        }

        await auditoria.RegistrarAsync(usuarioId, "Excluir", nameof(ContaPagar), conta.Id, null, null);

        return ResultadoOperacao.Ok();
    }

    /// <summary>
    /// Cadastro/edição/exclusão de conta a pagar é atribuição de Financeiro
    /// (Gestor só aprova — ver FluxoAprovacao — e Consulta só visualiza,
    /// mesma convenção de GerenciadorPagamentos.ValidarPermissaoAsync).
    /// Checado aqui na camada Application, não só na UI — achado crítico da
    /// auditoria de segurança: nenhum dos três métodos validava papel antes.
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
            return ResultadoOperacao.Falha("Só usuários com perfil Financeiro ou Administrador podem cadastrar, editar ou excluir contas a pagar.");
        }

        return null;
    }

    /// <summary>
    /// AsNoTracking() de propósito — sem isso, essa é a query que preenche
    /// o campo `conta` da tela de detalhe, e como todos os Gerenciadores
    /// compartilham a mesma instância de AppDbContext (escopo do circuito
    /// Blazor Server), o EF Core devolve a MESMA instância rastreada
    /// quando FluxoAprovacao/GerenciadorPagamentos buscam essa conta pelo
    /// Id de novo: aprovar/pagar mutava o objeto que a tela já tinha em
    /// mãos por baixo dos panos, antes mesmo do recarregamento explícito
    /// da tela rodar — o que fazia um componente filho (PainelPagamentos)
    /// montar cedo demais e disputar a mesma instância de DbContext com
    /// uma consulta ainda em andamento (achado descoberto testando de
    /// verdade o fluxo completo aprovar → pagar → estornar).
    /// </summary>
    public async Task<ContaPagar?> ObterAsync(Guid id) =>
        await db.ContasPagar
            .AsNoTracking()
            .Include(c => c.Fornecedor)
            .Include(c => c.Categoria)
            .Include(c => c.CentroCusto)
            .Include(c => c.FormaPagamento)
            .Include(c => c.CriadoPor)
            .FirstOrDefaultAsync(c => c.Id == id);

    public async Task<IReadOnlyList<ContaPagar>> ListarAsync(FiltroContasPagar filtro) =>
        await ConstruirQuery(filtro).OrderBy(c => c.Vencimento).ToListAsync();

    public async Task<ResultadoPaginado<ContaPagar>> ListarPaginadoAsync(FiltroContasPagar filtro)
    {
        var query = ConstruirQuery(filtro).OrderBy(c => c.Vencimento);
        var total = await query.CountAsync();

        var pagina = Math.Max(1, filtro.Pagina);
        var tamanho = filtro.TamanhoPagina <= 0 ? 20 : Math.Min(filtro.TamanhoPagina, 100);

        var itens = await query
            .Skip((pagina - 1) * tamanho)
            .Take(tamanho)
            .ToListAsync();

        return new ResultadoPaginado<ContaPagar>(itens, total, pagina, tamanho);
    }

    private IQueryable<ContaPagar> ConstruirQuery(FiltroContasPagar filtro)
    {
        var query = db.ContasPagar.AsNoTracking()
            .Include(c => c.Fornecedor)
            .Include(c => c.Categoria)
            .Include(c => c.CentroCusto)
            .AsQueryable();

        if (!filtro.IncluirExcluidas)
        {
            query = query.Where(c => c.ExcluidoEm == null);
        }

        if (filtro.FornecedorId is Guid fornecedorId)
        {
            query = query.Where(c => c.FornecedorId == fornecedorId);
        }

        if (filtro.StatusFinanceiro is StatusFinanceiro statusFinanceiro)
        {
            query = query.Where(c => c.StatusFinanceiro == statusFinanceiro);
        }

        if (filtro.StatusAprovacao is StatusAprovacao statusAprovacao)
        {
            query = query.Where(c => c.StatusAprovacao == statusAprovacao);
        }

        if (filtro.VencimentoInicial is DateOnly vencimentoInicial)
        {
            query = query.Where(c => c.Vencimento >= vencimentoInicial);
        }

        if (filtro.VencimentoFinal is DateOnly vencimentoFinal)
        {
            query = query.Where(c => c.Vencimento <= vencimentoFinal);
        }

        return query;
    }

    private async Task<List<string>> ValidarAsync(ContaPagarInput input)
    {
        var erros = new List<string>();

        if (string.IsNullOrWhiteSpace(input.Descricao))
        {
            erros.Add("Informe a descrição da despesa.");
        }

        if (input.ValorOriginal < 0)
        {
            erros.Add("Valor original não pode ser negativo.");
        }

        if (input.Desconto < 0 || input.Juros < 0 || input.Multa < 0)
        {
            erros.Add("Desconto, juros e multa não podem ser negativos.");
        }

        if (input.Desconto > input.ValorOriginal)
        {
            erros.Add("Desconto não pode ser maior que o valor original.");
        }

        var fornecedorValido = await db.Fornecedores.AnyAsync(f => f.Id == input.FornecedorId && f.ExcluidoEm == null);
        if (!fornecedorValido)
        {
            erros.Add("Fornecedor não encontrado ou excluído.");
        }

        return erros;
    }
}
