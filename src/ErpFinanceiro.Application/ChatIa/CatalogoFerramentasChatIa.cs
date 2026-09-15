namespace ErpFinanceiro.Application.ChatIa;

/// <summary>
/// Um parâmetro de ferramenta. Só tipos primitivos (string/number/boolean)
/// — nunca objeto aninhado — de propósito: é o que faz um modelo fraco
/// conseguir preencher os argumentos direito. "Tipo" aqui é o nosso
/// vocabulário interno (string/number/boolean); cada adapter de provider
/// traduz pro formato de schema que o provider espera (ex.: Gemini usa
/// "STRING"/"NUMBER"/"BOOLEAN" maiúsculo).
/// </summary>
public sealed record ParametroFerramentaChatIa(string Nome, string Tipo, string Descricao);

/// <summary>Uma ferramenta que o agente de chat pode chamar.</summary>
public sealed record FerramentaChatIa(string Nome, string Descricao, IReadOnlyList<ParametroFerramentaChatIa> Parametros);

/// <summary>
/// Catálogo FIXO de ferramentas somente-leitura do chat — o núcleo do
/// design "funciona bem mesmo com modelo fraco": em vez de dar ao modelo
/// acesso livre a consulta (SQL, filtros arbitrários, IDs que ele teria
/// que adivinhar), cada ferramenta é um caso de uso pequeno e concreto,
/// mapeado 1:1 a um método ListarAsync/ObterAsync que a própria interface
/// do sistema já usa — sem bypass de RBAC, sem consulta que a UI normal
/// não faria. Nenhuma ferramenta aqui cria, edita, aprova, rejeita ou
/// exclui nada — isso é reforçado estruturalmente: quem executa
/// (ExecutorFerramentasChatIa, na Infrastructure) só injeta métodos de
/// leitura das interfaces já existentes.
///
/// Descrições são deliberadamente explícitas e concretas — um modelo fraco
/// se apoia muito no texto da descrição pra decidir SE e QUAL ferramenta
/// chamar; descrição vaga ("consulta dados") produz escolha errada com
/// mais frequência que descrição específica com exemplos de uso.
/// </summary>
public static class CatalogoFerramentasChatIa
{
    public static readonly IReadOnlyList<FerramentaChatIa> Todas = new List<FerramentaChatIa>
    {
        new(
            "consultar_indicadores_financeiros",
            "Retorna os indicadores gerais do painel financeiro: total em aberto, total vencido, total a " +
            "pagar hoje, total a pagar nos próximos 7 dias, total a pagar no mês, total pago no mês e no " +
            "ano, gasto em cartão no ano, pagamentos não identificados, e a lista dos próximos vencimentos. " +
            "Use para perguntas gerais como 'quanto está vencido?', 'o que vence essa semana?', 'quanto já " +
            "pagamos esse mês?'. Não tem parâmetros.",
            Array.Empty<ParametroFerramentaChatIa>()),

        new(
            "listar_contas_a_pagar",
            "Lista contas a pagar cadastradas no ERP, com filtros opcionais. Use quando o usuário perguntar " +
            "sobre despesas, contas específicas, vencimentos de um fornecedor específico, ou contas com " +
            "determinado status (ex.: 'quais contas do fornecedor X estão vencidas?', 'mostra as contas " +
            "aguardando aprovação').",
            new[]
            {
                new ParametroFerramentaChatIa("fornecedorNome", "string",
                    "Nome ou parte do nome do fornecedor, para filtrar por ele. Omita para não filtrar por fornecedor."),
                new ParametroFerramentaChatIa("statusFinanceiro", "string",
                    "Situação financeira da conta. Um destes valores exatos: Agendada, EmAberto, AVencer, " +
                    "Vencida, Paga, Cancelada, PagamentoNaoIdentificado, PagamentoRecusadoEstornado. Omita para não filtrar."),
                new ParametroFerramentaChatIa("statusAprovacao", "string",
                    "Situação de aprovação da conta. Um destes valores exatos: Cadastrada, AguardandoAprovacao, " +
                    "Aprovada, Rejeitada. Omita para não filtrar."),
                new ParametroFerramentaChatIa("vencimentoInicial", "string",
                    "Data inicial do período de vencimento a considerar, formato AAAA-MM-DD. Omita se não houver filtro de período."),
                new ParametroFerramentaChatIa("vencimentoFinal", "string",
                    "Data final do período de vencimento a considerar, formato AAAA-MM-DD. Omita se não houver filtro de período."),
            }),

        new(
            "listar_fornecedores",
            "Lista fornecedores cadastrados no ERP. Use para perguntas como 'temos algum fornecedor chamado " +
            "X?', 'qual a forma de pagamento padrão do fornecedor Y?', 'quantos fornecedores temos cadastrados?'.",
            new[]
            {
                new ParametroFerramentaChatIa("nomeContem", "string",
                    "Texto que deve aparecer na razão social ou nome fantasia do fornecedor. Omita para listar todos."),
            }),

        new(
            "listar_documentos_pendentes_revisao",
            "Lista documentos enviados à Central de Documentos que a IA já processou e que estão " +
            "aguardando um Administrador revisar e aprovar (ainda não viraram lançamento oficial). Use para " +
            "perguntas como 'tem documento pendente de revisão?', 'quantos documentos estão esperando aprovação?'.",
            new[]
            {
                new ParametroFerramentaChatIa("apenasDoUsuarioAtual", "boolean",
                    "Se true, mostra só os documentos que o próprio usuário que está perguntando enviou. Se " +
                    "omitido ou false, mostra de todos os usuários."),
            }),

        new(
            "consultar_documento_importado",
            "Retorna o detalhe completo de UM documento importado específico — todos os campos que a IA " +
            "extraiu, o nível de confiança de cada um, e avisos. Use depois de já ter encontrado o ID do " +
            "documento numa chamada anterior (ex.: via listar_documentos_pendentes_revisao), quando o " +
            "usuário perguntar detalhes sobre um documento específico (ex.: 'por que esse documento está " +
            "com confiança baixa?', 'o que a IA leu nesse comprovante?').",
            new[]
            {
                new ParametroFerramentaChatIa("documentoId", "string",
                    "O ID (GUID) do documento importado, obtido de uma chamada anterior a listar_documentos_pendentes_revisao."),
            }),

        new(
            "listar_notas_fiscais",
            "Lista notas fiscais cadastradas no ERP, com filtros opcionais. Use para perguntas sobre notas " +
            "fiscais de um fornecedor, de um período, ou por número.",
            new[]
            {
                new ParametroFerramentaChatIa("numero", "string", "Número da nota fiscal. Omita para não filtrar por número."),
                new ParametroFerramentaChatIa("fornecedorNome", "string", "Nome ou parte do nome do fornecedor. Omita para não filtrar."),
                new ParametroFerramentaChatIa("emissaoInicial", "string", "Data inicial de emissão, formato AAAA-MM-DD. Omita se não houver filtro."),
                new ParametroFerramentaChatIa("emissaoFinal", "string", "Data final de emissão, formato AAAA-MM-DD. Omita se não houver filtro."),
            }),

        new(
            "listar_boletos",
            "Lista boletos cadastrados no ERP, com filtros opcionais. Use para perguntas sobre boletos de " +
            "um fornecedor, vencimento, ou status (em aberto, pago, vencido, cancelado).",
            new[]
            {
                new ParametroFerramentaChatIa("fornecedorNome", "string", "Nome ou parte do nome do fornecedor. Omita para não filtrar."),
                new ParametroFerramentaChatIa("status", "string",
                    "Situação do boleto. Um destes valores exatos: EmAberto, Pago, Vencido, Cancelado. Omita para não filtrar."),
                new ParametroFerramentaChatIa("vencimentoInicial", "string", "Data inicial de vencimento, formato AAAA-MM-DD. Omita se não houver filtro."),
                new ParametroFerramentaChatIa("vencimentoFinal", "string", "Data final de vencimento, formato AAAA-MM-DD. Omita se não houver filtro."),
            }),
    };
}
