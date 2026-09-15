from __future__ import annotations

import io

from app.config import get_config
from app.logging_config import get_logger
from app.models import DocumentoBruto
from app.readers.base import DocumentoIlegivel, ErroLeitura, LeitorArquivo, _normalizar_texto

log = get_logger(__name__)


def ocr_imagem(imagem_bytes: bytes, idioma: str) -> str:
    """OCR de uma imagem via Tesseract. Precisa do binário `tesseract` +
    pacote de idioma no sistema (ver Dockerfile / README)."""
    try:
        import pytesseract
        from PIL import Image, ImageOps
    except ImportError as e:  # pragma: no cover
        raise ErroLeitura("pytesseract/Pillow não instalados.") from e

    try:
        img = Image.open(io.BytesIO(imagem_bytes))
        img = ImageOps.exif_transpose(img)          # corrige rotação de fotos
        img = ImageOps.grayscale(img)                # OCR vai melhor em cinza
        return pytesseract.image_to_string(img, lang=idioma)
    except pytesseract.TesseractNotFoundError as e:
        raise ErroLeitura(
            "Binário do Tesseract não encontrado. Instale tesseract-ocr "
            "(+ idioma) no sistema — ver README."
        ) from e
    except Exception as e:
        raise ErroLeitura(f"Falha no OCR: {e}") from e


class ImageReader(LeitorArquivo):
    formatos = ("jpg", "png")

    def ler(self, dados: bytes, nome_arquivo: str) -> DocumentoBruto:
        cfg = get_config()
        if not cfg.ocr_habilitado:
            raise DocumentoIlegivel("OCR desabilitado (OCR_HABILITADO=false).")

        texto = _normalizar_texto(ocr_imagem(dados, cfg.ocr_idioma))
        if len(texto) < 3:
            raise DocumentoIlegivel(
                "Não consegui extrair texto da imagem (foto ilegível ou sem texto)."
            )
        return DocumentoBruto(texto=texto, paginas=1, ocr_usado=True)
