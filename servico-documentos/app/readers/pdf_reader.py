from __future__ import annotations

import io

from app.config import get_config
from app.logging_config import get_logger
from app.models import DocumentoBruto
from app.readers.base import DocumentoIlegivel, ErroLeitura, LeitorArquivo, _normalizar_texto
from app.readers.image_reader import ocr_imagem

log = get_logger(__name__)


class PdfReader(LeitorArquivo):
    formatos = ("pdf",)

    def ler(self, dados: bytes, nome_arquivo: str) -> DocumentoBruto:
        try:
            from pypdf import PdfReader as _PdfReader
        except ImportError as e:  # pragma: no cover
            raise ErroLeitura("pypdf não instalado (pip install pypdf).") from e

        try:
            reader = _PdfReader(io.BytesIO(dados))
        except Exception as e:
            raise ErroLeitura(f"PDF inválido: {e}") from e

        paginas = len(reader.pages)
        texto_nativo = _normalizar_texto(
            "\n".join((p.extract_text() or "") for p in reader.pages)
        )

        cfg = get_config()
        # PDF escaneado: pouco/nenhum texto nativo -> cai para OCR página a página.
        if len(texto_nativo) >= cfg.ocr_limiar_caracteres or not cfg.ocr_habilitado:
            if not texto_nativo:
                raise DocumentoIlegivel("PDF sem texto e OCR desabilitado.")
            return DocumentoBruto(texto=texto_nativo, paginas=paginas, ocr_usado=False)

        log.info("PDF com pouco texto nativo (%d chars) — tentando OCR.", len(texto_nativo))
        texto_ocr = self._ocr_pdf(dados, cfg.ocr_idioma, paginas, cfg.ocr_max_paginas)
        texto = _normalizar_texto(texto_ocr) or texto_nativo
        if len(texto) < 3:
            raise DocumentoIlegivel("PDF escaneado ilegível mesmo com OCR.")
        return DocumentoBruto(texto=texto, paginas=paginas, ocr_usado=True)

    @staticmethod
    def _ocr_pdf(dados: bytes, idioma: str, total_paginas: int, max_paginas: int) -> str:
        try:
            from pdf2image import convert_from_bytes
        except ImportError as e:  # pragma: no cover
            raise ErroLeitura(
                "pdf2image não instalado — necessário para OCR de PDF escaneado."
            ) from e

        # first_page/last_page fazem o poppler rasterizar só o necessário —
        # sem isso, um PDF de poucas dezenas de MB mas milhares de páginas
        # (dentro do limite de tamanho, que é por byte, não por página)
        # rasteriza tudo a 200dpi de uma vez: CPU/memória sem teto (achado
        # da auditoria de segurança).
        ultima_pagina = min(total_paginas, max_paginas)
        if total_paginas > max_paginas:
            log.warning(
                "PDF com %d páginas excede o teto de OCR (%d) — só as %d primeiras serão lidas.",
                total_paginas, max_paginas, max_paginas,
            )

        try:
            imagens = convert_from_bytes(dados, dpi=200, fmt="png", first_page=1, last_page=ultima_pagina)
        except Exception as e:
            raise ErroLeitura(
                f"Falha ao rasterizar PDF (poppler instalado?): {e}"
            ) from e

        partes: list[str] = []
        for img in imagens:
            buf = io.BytesIO()
            img.save(buf, format="PNG")
            partes.append(ocr_imagem(buf.getvalue(), idioma))
        return "\n".join(partes)
