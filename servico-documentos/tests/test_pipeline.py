"""Testes do pipeline de extração (offline, sem LLM, sem OCR).

    pip install pytest
    pytest
"""
from __future__ import annotations

import pytest

from app.models import StatusCampo, TipoDocumento
from app.pipeline import processar
from app.readers.base import DocumentoIlegivel
from app.readers.deteccao import detectar_formato

NF = """\
DANFE NOTA FISCAL ELETRONICA
NF-e Nº 12345
NATUREZA DA OPERACAO: VENDA
EMITENTE Razão Social: ACME COMERCIO LTDA
CNPJ: 11.222.333/0001-81
DATA DE EMISSÃO: 03/02/2026
VALOR TOTAL DA NOTA R$ 2.480,90
"""

BOLETO = """\
FICHA DE COMPENSACAO
Local de pagamento: pagável em qualquer banco
Cedente: FORNECEDOR AGUA E LUZ LTDA
Vencimento: 20/03/2026
Linha digitável: 34191.79001 01043.510047 91020.150008 5 91230000248090
Valor do documento: 248,09
"""


def test_detecta_formato_por_magic_bytes():
    assert detectar_formato(b"%PDF-1.7\n...", "x") == "pdf"
    assert detectar_formato(b"\x89PNG\r\n\x1a\n", "x") == "png"
    assert detectar_formato("boa noite".encode(), "nota.txt") == "txt"


def test_extrai_nota_fiscal():
    r = processar(NF.encode(), "nf.txt")
    assert r.tipo_detectado == TipoDocumento.NOTA_FISCAL
    campos = {c.nome: c for c in r.campos}
    assert campos["valor"].valor == "2480.90"
    assert campos["cnpj"].valor == "11222333000181"
    assert campos["data"].valor == "2026-02-03"
    assert campos["numeroNota"].valor == "12345"
    assert "ACME" in campos["fornecedor"].valor
    assert r.campos_obrigatorios_pendentes == []


def test_extrai_boleto_e_linha_digitavel():
    r = processar(BOLETO.encode(), "boleto.txt")
    assert r.tipo_detectado == TipoDocumento.BOLETO
    campos = {c.nome: c for c in r.campos}
    assert campos["vencimento"].valor == "2026-03-20"
    assert len(campos["linhaDigitavel"].valor) in (47, 48)
    assert campos["valor"].valor == "248.09"


def test_tipo_informado_sobrepoe_classificacao():
    r = processar(b"documento qualquer sem pistas\nvalor: 10,00", "x.txt",
                  tipo_informado=TipoDocumento.PEDIDO_COMPRA)
    assert r.tipo_detectado == TipoDocumento.PEDIDO_COMPRA
    assert r.tipo_origem == "informado"


def test_campo_obrigatorio_ausente_e_sinalizado():
    r = processar(b"NOTA FISCAL ELETRONICA DANFE\nsem mais nada util", "x.txt")
    assert "valor" in r.campos_obrigatorios_pendentes
    campos = {c.nome: c for c in r.campos}
    assert campos["valor"].status == StatusCampo.NAO_ENCONTRADO


def test_documento_ilegivel_levanta_erro():
    with pytest.raises(DocumentoIlegivel):
        processar(b"   \n  \n", "vazio.txt")


CONTRATO = """\
CONTRATO DE PRESTACAO DE SERVICOS
CONTRATANTE: ERP LTDA  CONTRATADA: CLEAN MAX SERVICOS LTDA - CNPJ: 98.765.432/0001-10
CLAUSULA SEGUNDA - VIGENCIA: inicio da vigencia em 01/10/2026, com termino em 30/09/2027.
CLAUSULA TERCEIRA: valor mensal de R$ 4.250,00.
Foro da comarca de Sao Paulo.
"""


def test_contrato_rotulos_sem_acento_e_nome_sem_cnpj():
    r = processar(CONTRATO.encode(), "contrato.txt")
    assert r.tipo_detectado == TipoDocumento.CONTRATO
    campos = {c.nome: c for c in r.campos}
    # rótulos do schema são acentuados; o documento não é
    assert campos["vigenciaInicio"].valor == "2026-10-01"
    assert campos["vigenciaFim"].valor == "2027-09-30"
    # o CNPJ colado na mesma linha não deve contaminar o nome
    assert campos["fornecedor"].valor == "CLEAN MAX SERVICOS LTDA"
    assert campos["valor"].valor == "4250.00"
    assert r.campos_obrigatorios_pendentes == []


def test_comprovante_valor_rotulo_curto():
    txt = b"Comprovante de Pix\nData da transacao: 02/09/2026\nValor: R$ 890,45\nFavorecido: JOAO ME\n"
    r = processar(txt, "comp.txt")
    campos = {c.nome: c for c in r.campos}
    assert campos["valor"].valor == "890.45"
    assert campos["valor"].status == StatusCampo.EXTRAIDO


def test_nao_identificado_gera_aviso():
    r = processar(b"lorem ipsum dolor sit amet\ntotal: 5,00", "x.txt")
    assert r.tipo_detectado == TipoDocumento.NAO_IDENTIFICADO
    assert any("não identificado" in a.lower() for a in r.avisos)


def test_cnpj_com_digito_verificador_errado_derruba_confianca_e_avisa():
    # Mesmo CNPJ da NF de exemplo, mas com o último dígito trocado (dígito
    # verificador passa a não bater) — o regex extrai igual (é só regex),
    # mas a validação determinística (app/validacao.py) tem que pegar isso
    # e não deixar passar como se estivesse tudo certo.
    txt = NF.replace("11.222.333/0001-81", "11.222.333/0001-82")
    r = processar(txt.encode(), "nf.txt")
    campos = {c.nome: c for c in r.campos}
    assert campos["cnpj"].status == StatusCampo.BAIXA_CONFIANCA
    assert any("dígito verificador" in a for a in r.avisos)


def test_cnpj_valido_tem_confianca_reforcada_pela_validacao():
    r = processar(NF.encode(), "nf.txt")
    campos = {c.nome: c for c in r.campos}
    assert campos["cnpj"].status == StatusCampo.EXTRAIDO
    assert not any("dígito verificador" in a for a in r.avisos)


def test_formato_nao_suportado_propaga_sem_virar_erro_extracao():
    # Antes, detectar_formato levantava FormatoNaoSuportado e o pipeline
    # reembalava em ErroExtracao — o handler de 415 em app/main.py nunca
    # era alcançado, e todo upload de formato não suportado virava 500
    # genérico (achado da auditoria de segurança). Precisa propagar como
    # FormatoNaoSuportado mesmo, não como ErroExtracao.
    from unittest.mock import patch

    from app.readers.deteccao import FormatoNaoSuportado

    with patch("app.pipeline.detectar_formato", side_effect=FormatoNaoSuportado("formato x")):
        with pytest.raises(FormatoNaoSuportado):
            processar(b"conteudo qualquer", "arquivo.xyz")
