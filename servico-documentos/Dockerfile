FROM python:3.12-slim

# Binários de OCR e conversão de PDF -> imagem.
# Remova esta linha se nunca for processar imagem / PDF escaneado.
RUN apt-get update && apt-get install -y --no-install-recommends \
        tesseract-ocr tesseract-ocr-por tesseract-ocr-eng poppler-utils libmagic1 curl \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app

COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

COPY app ./app

RUN useradd --create-home --uid 10001 documentos
ENV PYTHONDONTWRITEBYTECODE=1 PYTHONUNBUFFERED=1
USER documentos

EXPOSE 8000
# 1 worker: a carga é CPU-bound (OCR); escale por réplicas, não por workers.
CMD ["uvicorn", "app.main:app", "--host", "0.0.0.0", "--port", "8000"]
