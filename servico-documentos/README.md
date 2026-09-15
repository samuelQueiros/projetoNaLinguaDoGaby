# serviço-documentos

Serviço Python **stateless** de leitura de documentos da Fase 4 do ERP
(Assistente de IA para Documentos). É a **etapa 2** descrita em
`docs/modulo-ia-documentos.md`: o backend .NET (`LeitorDocumentosHttp`) chama
`POST /extrair` com um arquivo e recebe de volta um JSON com os campos lidos e
a confiança de cada um.

Este serviço **não** persiste nada, **não** acessa o banco e **não** fala com
o ERP. Fila, estado "pendente de revisão", aprovação por Administrador e
criação da `ContaPagar` continuam no backend .NET — ver o diagrama em
`docs/modulo-ia-documentos.md`.

## Fluxo interno (`app/pipeline.py`)

```
arquivo recebido
  │  1. detecção do tipo de arquivo (extensão + magic bytes)   readers/deteccao.py
  ▼
  │  2. reader plugável extrai texto/estrutura + OCR se preciso  readers/*.py
  ▼
  │  3. classificação do TIPO DE DOCUMENTO (keywords) —          classificacao/regras.py
  │     ou o tipo vem no request (campo `tipo` do form)
  ▼
  │  4. schema de campos específico do tipo                      schemas/tipos.py
  ▼
  │  5a. extração por regex/heurística (rápida, offline)         extracao/regex_extractor.py
  │  5b. extração por LLM (opcional, provider por env)           extracao/llm_extractor.py
  ▼
  │  6. merge + marca campos obrigatórios ausentes / baixa conf.
  ▼
resposta JSON (contrato de docs/modulo-ia-documentos.md)
```

## Rodando

```bash
cd servico-documentos
python -m venv .venv && source .venv/bin/activate
pip install -r requirements.txt

# OCR (opcional, só para imagem / PDF escaneado):
#   Debian/Ubuntu:  sudo apt install tesseract-ocr tesseract-ocr-por poppler-utils
#   Arch:           sudo pacman -S tesseract tesseract-data-por poppler

cp .env.example .env      # ajuste LLM_PROVIDER / LLM_API_KEY se for usar LLM
uvicorn app.main:app --host 0.0.0.0 --port 8000
```

Sem chave de LLM o serviço funciona só com a extração por regex/heurística
(`LLM_PROVIDER=none`), útil para desenvolvimento.

### Docker

```bash
docker build -t erp-servico-documentos .
docker run --rm -p 8000:8000 --env-file .env erp-servico-documentos
```

No `docker-compose.yml` do projeto, adicionar (ver comentário no fim daquele
arquivo):

```yaml
  servico-documentos:
    build: ./servico-documentos
    environment:
      LLM_PROVIDER: ${LLM_PROVIDER:-none}
      LLM_API_KEY: ${LLM_API_KEY:-}
      LLM_MODEL: ${LLM_MODEL:-claude-sonnet-5}
    ports:
      - "127.0.0.1:8000:8000"
```

e no `appsettings`/env do backend .NET: `Ia:Leitor:Modo = Http` e
`Ia:Leitor:BaseUrl = http://servico-documentos:8000`.

## Endpoints

| Método | Rota | Descrição |
|---|---|---|
| `GET`  | `/health`   | liveness + libs disponíveis |
| `POST` | `/extrair`  | `multipart/form-data`: `arquivo` (obrigatório), `tipo` (opcional) |
| `GET`  | `/tipos`    | lista os tipos de documento e seus schemas |

### Resposta de `POST /extrair` (200)

```json
{
  "loteId": "8f3c...-uuid",
  "tipoDetectado": "NotaFiscal",
  "tipoOrigem": "classificacao",
  "confiancaGeral": 0.78,
  "arquivo": { "nome": "nf-123.pdf", "formato": "pdf", "ocrUsado": false },
  "campos": [
    { "nome": "valor", "valor": "1530.00", "confianca": 0.97,
      "status": "extraido", "origem": "VALOR TOTAL DA NOTA R$ 1.530,00" }
  ],
  "camposObrigatoriosPendentes": ["cnpj"],
  "avisos": []
}
```

`422` → documento ilegível (sem texto aproveitável). `5xx` → falha interna.
O backend .NET traduz ambos para `Status=Falha` com `MensagemErro`.

## Onde ajustar (pontos de extensão)

| Quero... | Mexo em... |
|---|---|
| Adicionar um novo **tipo de documento** | `app/schemas/tipos.py` — adiciona uma entrada no dict `SCHEMAS`. Nada mais. |
| Adicionar palavras-chave de **classificação** | `app/classificacao/regras.py` — dict `PALAVRAS_CHAVE`. |
| Suportar um novo **formato de arquivo** | cria `app/readers/xxx_reader.py` implementando `LeitorArquivo` e registra em `app/readers/__init__.py`. |
| Trocar/afinar a **extração por regex** | `app/extracao/regex_extractor.py`. |
| Trocar o **provider de LLM** ou o prompt | `app/extracao/llm_extractor.py` + env `LLM_*`. |
| Nomes canônicos de campo aceitos pelo .NET | mantidos em sincronia com `docs/modulo-ia-documentos.md` (secção "Contrato HTTP"). |
