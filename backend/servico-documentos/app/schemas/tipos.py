"""Schema de campos POR TIPO DE DOCUMENTO.

>>> ESTE É O PONTO DE EXTENSÃO PRINCIPAL <<<
Para suportar um novo tipo de documento, adicione uma entrada em `SCHEMAS`.
Nenhum outro arquivo precisa mudar — pipeline, extractors e endpoint leem
tudo daqui.

`kind` controla como o `regex_extractor` procura e como o valor é normalizado:
  - "valor"  : monetário BR (R$ 1.234,56 -> "1234.56")
  - "data"   : datas BR/ISO -> "AAAA-MM-DD"
  - "cnpj"   : 14 dígitos -> só dígitos
  - "cpf"    : 11 dígitos
  - "texto"  : captura a linha/segmento após um dos `rotulos`
  - "numero" : sequência alfanumérica (nº nota, nº pedido)
  - "linha_digitavel" : 47/48 dígitos do boleto

`rotulos` são as expressões que aparecem ANTES do valor no documento
(usadas por regex e como dica para o LLM).
"""
from __future__ import annotations

from dataclasses import dataclass, field

from app.models import TipoDocumento


@dataclass(frozen=True)
class CampoSpec:
    nome: str                               # nome canônico (contrato com o .NET)
    kind: str
    obrigatorio: bool = False
    rotulos: tuple[str, ...] = ()            # textos que precedem o valor no doc
    dica_llm: str = ""                       # instrução extra para o LLM
    confianca_minima: float = 0.55          # abaixo disso -> status "baixa_confianca"


@dataclass(frozen=True)
class SchemaDocumento:
    tipo: TipoDocumento
    descricao: str
    campos: tuple[CampoSpec, ...]
    endpoint_erp: str = ""                   # informativo: rota no ERP .NET que consome este tipo
    campos_por_nome: dict = field(default_factory=dict)

    def __post_init__(self):
        object.__setattr__(
            self, "campos_por_nome", {c.nome: c for c in self.campos}
        )


# --------------------------------------------------------------------------- #
# Campos reutilizados entre tipos                                             #
# --------------------------------------------------------------------------- #
_FORNECEDOR = CampoSpec(
    "fornecedor", "texto", obrigatorio=True,
    rotulos=("fornecedor", "razão social", "emitente", "prestador", "cedente",
             "nome/razão social", "beneficiário"),
    dica_llm="Nome da empresa que emitiu o documento / vai receber o pagamento.",
)
_CNPJ = CampoSpec(
    "cnpj", "cnpj", obrigatorio=False,
    rotulos=("cnpj", "cnpj/cpf", "c.n.p.j."),
    dica_llm="CNPJ do emitente (14 dígitos). Só dígitos.",
)
_VALOR = CampoSpec(
    "valor", "valor", obrigatorio=True,
    rotulos=("valor total da nota", "valor total", "valor do documento",
             "valor a pagar", "valor pago", "total geral", "total a pagar",
             "vlr total", "total", "valor"),
    dica_llm="Valor monetário total do documento, em reais.",
)


# --------------------------------------------------------------------------- #
# SCHEMAS — adicione novos tipos aqui                                         #
# --------------------------------------------------------------------------- #
SCHEMAS: dict[TipoDocumento, SchemaDocumento] = {
    TipoDocumento.NOTA_FISCAL: SchemaDocumento(
        tipo=TipoDocumento.NOTA_FISCAL,
        descricao="Nota fiscal de produto ou serviço (NF-e / NFS-e).",
        endpoint_erp="/notas-fiscais",
        campos=(
            _VALOR,
            _FORNECEDOR,
            _CNPJ,
            CampoSpec("numeroNota", "numero", obrigatorio=True,
                      rotulos=("nº", "numero", "número", "nota nº", "nf-e nº",
                               "nfs-e", "número da nota"),
                      dica_llm="Número da nota fiscal."),
            CampoSpec("data", "data", obrigatorio=True,
                      rotulos=("data de emissão", "data emissão", "emitida em",
                               "data/hora emissão"),
                      dica_llm="Data de emissão da nota."),
            CampoSpec("vencimento", "data", obrigatorio=False,
                      rotulos=("vencimento", "data de vencimento", "vencto")),
        ),
    ),
    TipoDocumento.BOLETO: SchemaDocumento(
        tipo=TipoDocumento.BOLETO,
        descricao="Boleto bancário / ficha de compensação.",
        endpoint_erp="/boletos",
        campos=(
            _VALOR,
            CampoSpec("fornecedor", "texto", obrigatorio=True,
                      rotulos=("beneficiário", "cedente", "nome do beneficiário")),
            _CNPJ,
            CampoSpec("vencimento", "data", obrigatorio=True,
                      rotulos=("vencimento", "data de vencimento", "vencto")),
            CampoSpec("linhaDigitavel", "linha_digitavel", obrigatorio=True,
                      rotulos=("linha digitável", "linha digitavel", "código de barras"),
                      dica_llm="Linha digitável do boleto (47-48 dígitos, pode vir com pontos e espaços)."),
            CampoSpec("banco", "texto", obrigatorio=False,
                      rotulos=("banco", "instituição", "local de pagamento")),
        ),
    ),
    TipoDocumento.PEDIDO_COMPRA: SchemaDocumento(
        tipo=TipoDocumento.PEDIDO_COMPRA,
        descricao="Pedido / ordem de compra emitido para um fornecedor.",
        endpoint_erp="/pedidos",
        campos=(
            CampoSpec("numeroPedido", "numero", obrigatorio=True,
                      rotulos=("pedido nº", "pedido de compra nº", "oc nº",
                               "ordem de compra", "nº do pedido", "pedido"),
                      dica_llm="Número do pedido / ordem de compra."),
            _FORNECEDOR,
            _CNPJ,
            _VALOR,
            CampoSpec("data", "data", obrigatorio=True,
                      rotulos=("data do pedido", "data de emissão", "emitido em")),
            CampoSpec("previsaoEntrega", "data", obrigatorio=False,
                      rotulos=("previsão de entrega", "prazo de entrega", "entrega")),
        ),
    ),
    TipoDocumento.CONTRATO: SchemaDocumento(
        tipo=TipoDocumento.CONTRATO,
        descricao="Contrato de prestação de serviço / fornecimento.",
        endpoint_erp="/contratos",
        campos=(
            CampoSpec("fornecedor", "texto", obrigatorio=True,
                      rotulos=("contratada", "contratado", "prestadora", "fornecedora")),
            _CNPJ,
            CampoSpec("valor", "valor", obrigatorio=False,
                      rotulos=("valor do contrato", "valor global", "valor mensal",
                               "preço", "remuneração")),
            CampoSpec("vigenciaInicio", "data", obrigatorio=True,
                      rotulos=("vigência", "início da vigência", "data de início",
                               "vigência a partir de", "início")),
            CampoSpec("vigenciaFim", "data", obrigatorio=False,
                      rotulos=("término", "fim da vigência", "vigência até",
                               "data de término", "encerramento")),
            CampoSpec("objeto", "texto", obrigatorio=False,
                      rotulos=("objeto", "objeto do contrato", "cláusula primeira"),
                      dica_llm="Descrição resumida do objeto do contrato."),
        ),
    ),
    TipoDocumento.COMPROVANTE_PAGAMENTO: SchemaDocumento(
        tipo=TipoDocumento.COMPROVANTE_PAGAMENTO,
        descricao="Comprovante de pagamento / transferência / PIX.",
        endpoint_erp="/despesas/comprovante",
        campos=(
            _VALOR,
            CampoSpec("data", "data", obrigatorio=True,
                      rotulos=("data do pagamento", "data da transação", "data/hora",
                               "efetuado em", "data")),
            CampoSpec("fornecedor", "texto", obrigatorio=False,
                      rotulos=("favorecido", "beneficiário", "destinatário",
                               "quem recebeu", "recebedor")),
            CampoSpec("cnpj", "cnpj", obrigatorio=False, rotulos=("cnpj", "cpf/cnpj")),
            CampoSpec("formaPagamento", "texto", obrigatorio=False,
                      rotulos=("forma de pagamento", "tipo de transação", "meio de pagamento"),
                      dica_llm="Ex.: PIX, TED, DOC, boleto, débito."),
            CampoSpec("banco", "texto", obrigatorio=False,
                      rotulos=("banco", "instituição", "banco de origem")),
        ),
    ),
    # Tipo "guarda-chuva": documento não reconhecido -> extrai só o básico.
    TipoDocumento.NAO_IDENTIFICADO: SchemaDocumento(
        tipo=TipoDocumento.NAO_IDENTIFICADO,
        descricao="Tipo não identificado — extração genérica de valor/data/fornecedor.",
        campos=(
            CampoSpec("valor", "valor", obrigatorio=False, rotulos=_VALOR.rotulos),
            CampoSpec("data", "data", obrigatorio=False,
                      rotulos=("data", "emissão", "vencimento")),
            CampoSpec("fornecedor", "texto", obrigatorio=False, rotulos=_FORNECEDOR.rotulos),
            CampoSpec("cnpj", "cnpj", obrigatorio=False, rotulos=("cnpj",)),
        ),
    ),
}


def obter_schema(tipo: TipoDocumento) -> SchemaDocumento:
    return SCHEMAS.get(tipo, SCHEMAS[TipoDocumento.NAO_IDENTIFICADO])
