from __future__ import annotations

import io

from app.config import get_config
from app.logging_config import get_logger
from app.models import DocumentoBruto
from app.readers.base import DocumentoIlegivel, ErroLeitura, LeitorArquivo, _normalizar_texto

log = get_logger(__name__)


class XlsxReader(LeitorArquivo):
    formatos = ("xlsx",)

    #: além do texto corrido, exponho as células como "chave: valor" quando a
    #: planilha tem cara de formulário (coluna A = rótulo, coluna B = valor).
    def ler(self, dados: bytes, nome_arquivo: str) -> DocumentoBruto:
        try:
            import openpyxl
        except ImportError as e:  # pragma: no cover
            raise ErroLeitura("openpyxl não instalado (pip install openpyxl).") from e

        try:
            wb = openpyxl.load_workbook(io.BytesIO(dados), read_only=True, data_only=True)
        except Exception as e:
            raise ErroLeitura(f"XLSX inválido: {e}") from e

        cfg = get_config()
        n_planilhas = len(wb.sheetnames)
        linhas_texto: list[str] = []
        total_linhas = 0
        # Teto de linhas — um XLSX pequeno em disco pode conter uma
        # quantidade desproporcional de linhas depois de descompactado (é
        # um arquivo zip); sem teto, isso vira um vetor de DoS de
        # CPU/memória processando linha por linha (achado da auditoria de
        # segurança).
        limite_atingido = False
        for ws in wb.worksheets:
            linhas_texto.append(f"# Planilha: {ws.title}")
            for linha in ws.iter_rows(values_only=True):
                if total_linhas >= cfg.xlsx_max_linhas:
                    limite_atingido = True
                    break
                total_linhas += 1
                valores = [str(v).strip() for v in linha if v is not None and str(v).strip()]
                if valores:
                    linhas_texto.append(" | ".join(valores))
            if limite_atingido:
                log.warning(
                    "XLSX %s excede o teto de %d linhas — leitura interrompida.",
                    nome_arquivo, cfg.xlsx_max_linhas,
                )
                break
        wb.close()

        texto = _normalizar_texto("\n".join(linhas_texto))
        if not texto:
            raise DocumentoIlegivel("XLSX sem dados.")
        return DocumentoBruto(texto=texto, paginas=max(n_planilhas, 1))
