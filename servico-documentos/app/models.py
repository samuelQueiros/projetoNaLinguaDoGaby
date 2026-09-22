"""Modelos de dados do serviço.

O formato de `RespostaExtracao` é o **contrato HTTP** com o backend .NET
(`LeitorDocumentosHttp`) — ver `docs/modulo-ia-documentos.md`, seção
"Contrato HTTP com o serviço Python". Mudança aqui = mudança no .NET.
"""
from __future__ import annotations

import enum
from typing import Optional

from pydantic import BaseModel, Field


class TipoDocumento(str, enum.Enum):
    """Espelha `TipoDocumentoDetectado` no .NET."""
    NAO_IDENTIFICADO = "NaoIdentificado"
    COMPROVANTE_PAGAMENTO = "ComprovantePagamento"
    CONTRATO = "Contrato"
    NOTA_FISCAL = "NotaFiscal"
    PEDIDO_COMPRA = "PedidoCompra"
    BOLETO = "Boleto"


class StatusCampo(str, enum.Enum):
    EXTRAIDO = "extraido"
    NAO_ENCONTRADO = "nao_encontrado"
    AMBIGUO = "ambiguo"          # mais de um candidato plausível
    BAIXA_CONFIANCA = "baixa_confianca"


class CampoExtraido(BaseModel):
    nome: str = Field(..., description="Nome canônico do campo (ver schemas/tipos.py).")
    valor: Optional[str] = Field(None, description="Sempre string; conversão é na revisão do .NET.")
    confianca: float = Field(0.0, ge=0.0, le=1.0)
    status: StatusCampo = StatusCampo.NAO_ENCONTRADO
    origem: Optional[str] = Field(
        None, description="Trecho de texto bruto de onde o valor veio (para o revisor conferir)."
    )
    candidatos: list[str] = Field(default_factory=list, description="Outros valores plausíveis, se ambíguo.")


class InfoArquivo(BaseModel):
    nome: str
    formato: str                 # pdf | docx | xlsx | jpg | png | txt
    ocr_usado: bool = False
    paginas: Optional[int] = None


class RespostaExtracao(BaseModel):
    lote_id: str = Field(..., alias="loteId")
    tipo_detectado: TipoDocumento = Field(..., alias="tipoDetectado")
    tipo_origem: str = Field(..., alias="tipoOrigem", description="informado | classificacao")
    confianca_geral: float = Field(..., alias="confiancaGeral", ge=0.0, le=1.0)
    arquivo: InfoArquivo
    campos: list[CampoExtraido]
    campos_obrigatorios_pendentes: list[str] = Field(default_factory=list, alias="camposObrigatoriosPendentes")
    avisos: list[str] = Field(default_factory=list)
    texto: str = Field(
        "", description="Texto integral extraído do documento pelo sistema (sem IA) — "
        "pypdf/OCR conforme o formato. Truncado em TEXTO_MAX_CHARS. O .NET persiste "
        "isso em DocumentoImportado.TextoExtraido para consulta posterior (revisão, "
        "agente de chat)."
    )

    model_config = {"populate_by_name": True}


class DocumentoBruto(BaseModel):
    """O que um `LeitorArquivo` devolve: texto já normalizado + metadados."""
    texto: str
    paginas: int = 1
    ocr_usado: bool = False
    metadados: dict = Field(default_factory=dict)
