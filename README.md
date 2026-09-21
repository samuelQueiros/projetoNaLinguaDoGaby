# ERP Financeiro

Sistema financeiro interno para gestão de obrigações e movimentações de uma
empresa: contas a pagar, fornecedores, boletos, notas fiscais, cartões,
conciliação bancária, relatórios, alertas e controle de usuários.

Objetivo: ser o painel central do setor financeiro, eliminando planilhas
soltas, documentos perdidos e divergências entre sistema e banco.

## Índice

- [Stack](#stack)
- [Arquitetura](#arquitetura)
- [Módulos do sistema](#módulos-do-sistema)
- [Assistente de IA para documentos](#assistente-de-ia-para-documentos)
- [Perfis de acesso](#perfis-de-acesso)
- [Rodando localmente](#rodando-localmente)
- [Testes](#testes)
- [Deploy em produção (Portainer)](#deploy-em-produção-portainer)
- [Estrutura do repositório](#estrutura-do-repositório)

## Stack

| Camada | Tecnologia |
|---|---|
| Backend + Frontend | ASP.NET Core 8 com Blazor Server |
| Banco de dados | PostgreSQL 16 (via Npgsql) |
| ORM / migrações | Entity Framework Core |
| Autenticação | ASP.NET Core Identity (cookies, roles) |
| Exportações | ClosedXML (Excel), QuestPDF (PDF), CsvHelper (CSV) |
| Leitura de documentos | Serviço Python (FastAPI) separado, com OCR e LLM opcional |
| Testes | xUnit (backend), pytest (serviço de documentos) |

## Arquitetura

O sistema tem dois processos: a aplicação principal em .NET e um serviço
Python auxiliar, stateless, dedicado à leitura de documentos.

```
┌─────────────────────────────┐        ┌──────────────────────────┐
│   ErpFinanceiro.Web          │        │   servico-documentos      │
│   (Blazor Server, porta 8080)│──HTTP─▶│   (FastAPI, porta 8000)   │
│                               │  X-Internal-Token              │
│   Application → Domain        │        │   OCR + regex + LLM opc.  │
│   Infrastructure (EF Core)    │        └──────────────────────────┘
└──────────────┬────────────────┘
               │ Npgsql
               ▼
        ┌─────────────┐
        │ PostgreSQL 16 │
        └─────────────┘
```

O backend .NET segue arquitetura em camadas, sem dependências circulares:

```
src/
├── ErpFinanceiro.Domain/          # entidades e enums, sem dependências externas
├── ErpFinanceiro.Application/     # regras de negócio / casos de uso (interfaces + serviços)
├── ErpFinanceiro.Infrastructure/  # EF Core, storage em disco, exportação, integrações
└── ErpFinanceiro.Web/             # Blazor Server (UI), Program.cs, endpoints HTTP
tests/
└── ErpFinanceiro.Tests/           # testes xUnit (165 testes)
servico-documentos/                # serviço Python separado — ver seção própria
```

`Domain` não depende de nada do projeto; `Application` depende só de
`Domain`; `Infrastructure` implementa as interfaces de `Application`; `Web`
monta tudo via injeção de dependência em `Program.cs`.

O serviço Python (`servico-documentos/`) não acessa o banco nem conhece o
ERP: recebe um arquivo, devolve um JSON com os campos extraídos e a
confiança de cada um. Toda a fila, estado de revisão e persistência ficam no
backend .NET.

## Módulos do sistema

- **Contas a pagar** — cadastro, fluxo de aprovação (`StatusAprovacao`) e
  situação financeira (`StatusFinanceiro`: agendada, em aberto, a vencer,
  vencida, paga, cancelada, pagamento não identificado, pagamento
  recusado/estornado) tratadas como dimensões independentes.
- **Fornecedores** — cadastro, dados bancários e histórico financeiro
  completo por fornecedor.
- **Boletos e notas fiscais** — cadastro dedicado, vinculável a uma conta a
  pagar.
- **Cartões e contas bancárias da empresa** — cadastro de meios de
  pagamento internos.
- **Categorias e centros de custo** — classificação das contas a pagar.
- **Pagamentos** — registro do pagamento efetivo de uma conta, com timeline
  e reconciliação.
- **Anexos** — anexação polimórfica de arquivo (nota fiscal, boleto, XML,
  PDF, comprovante, contrato, orçamento, recibo, observação, outros) a
  qualquer entidade do sistema, armazenados em disco.
- **Central de Documentos** — fila de documentos importados via IA
  aguardando revisão antes de virarem lançamentos.
- **Dashboard** — painel financeiro com indicadores agregados.
- **Relatórios** — relatório de contas a pagar com exportação em Excel, CSV
  e PDF, com os mesmos filtros da tela.
- **Auditoria** — log de auditoria de todas as ações relevantes, com tela de
  consulta dedicada.
- **Usuários** — CRUD de usuários e atribuição de perfil (role).
- **Configurações de IA** — tela (somente leitura, Administrador) que mostra
  provedor/modelo/status do LLM usado na leitura de documentos e no chat, e
  testa a conexão. Provedor/modelo/chave são configurados por variável de
  ambiente (`Ia__Documentos__*` / `Ia__Chat__*`), não pela tela — ver seção
  de variáveis de ambiente abaixo.
- **Assistente de chat (IA)** — chat com ferramentas somente-leitura sobre
  os dados do ERP (hoje com adapter para Gemini), configurado por variável
  de ambiente (`Ia__Chat__*`).

## Assistente de IA para documentos

Fluxo: o usuário sobe um arquivo (nota fiscal, boleto, comprovante etc.) na
Central de Documentos → o backend .NET enfileira o processamento
(`IFilaProcessamentoDocumentos`, fila em memória + worker hospedado) → o
worker chama o serviço Python via HTTP (`POST /extrair`, autenticado por um
token interno compartilhado) → o Python detecta o tipo de arquivo, extrai
texto (com OCR se necessário), classifica o tipo de documento e extrai os
campos (regex/heurística, mais LLM opcional) → devolve um JSON com cada
campo, sua confiança e os campos obrigatórios pendentes → o documento fica
com status "pendente de revisão" até um usuário confirmar os dados e gerar o
lançamento correspondente (nota fiscal, boleto etc.).

O leitor é trocável por configuração (`Ia:Leitor:Modo`):

- `Stub` (padrão) — não chama o serviço Python; útil em desenvolvimento sem
  subir o serviço de documentos.
- `Http` — chama o serviço Python real em `Ia:Leitor:BaseUrl`.

Sem uma chave de LLM configurada, o serviço Python funciona só com extração
por regex/heurística e OCR (Tesseract) — sem custo e offline. Provedores
suportados quando configurado: Anthropic, OpenAI e Gemini. Veja
[`servico-documentos/README.md`](servico-documentos/README.md) para detalhes
do pipeline, endpoints e pontos de extensão (novo tipo de documento, novo
formato de arquivo, etc.).

## Perfis de acesso

Quatro perfis (roles do ASP.NET Core Identity), sem coluna própria em
`Usuario`:

| Perfil | Uso típico |
|---|---|
| `Administrador` | acesso completo, inclusive Usuários e Configurações de IA |
| `Financeiro` | operação do dia a dia: contas, pagamentos, fornecedores |
| `Gestor` | aprovação de contas a pagar |
| `Consulta` | leitura, sem permissão de alterar dados |

O primeiro Administrador é criado no startup a partir de configuração
(`SeedAdministrador:Email` / `SeedAdministrador:SenhaInicial`) — nunca
hardcoded no código. Sem essas chaves configuradas, nenhum usuário é criado
automaticamente (só os quatro papéis).

## Rodando localmente

Pré-requisitos: .NET 8 SDK, Docker (para o PostgreSQL e, opcionalmente, o
serviço de documentos).

```bash
# 1. Suba o banco
cp .env.example .env
docker compose up -d postgres

# 2. Configure a connection string local
cp src/ErpFinanceiro.Web/appsettings.Development.json.example \
   src/ErpFinanceiro.Web/appsettings.Development.json

# 3. Aplique as migrations
dotnet tool install --global dotnet-ef
dotnet ef database update \
  -p src/ErpFinanceiro.Infrastructure/ErpFinanceiro.Infrastructure.csproj \
  -s src/ErpFinanceiro.Web/ErpFinanceiro.Web.csproj

# 4. Rode a aplicação
dotnet run --project src/ErpFinanceiro.Web
```

Por padrão (`Ia:Leitor:Modo = Stub`) não é preciso subir o serviço Python
para desenvolver. Para testar o fluxo completo de leitura de documentos,
suba também `servico-documentos` (`docker compose up -d
servico-documentos`) e mude `IA_LEITOR_MODO=Http` no `.env`. Veja
[`servico-documentos/README.md`](servico-documentos/README.md) para rodá-lo
fora do Docker.

## Testes

```bash
# Backend .NET (165 testes)
dotnet test tests/ErpFinanceiro.Tests/ErpFinanceiro.Tests.csproj

# Serviço de documentos (Python)
cd servico-documentos && pytest
```

## Deploy em produção (Portainer)

A stack de produção sobe aplicação web, PostgreSQL, serviço de documentos e
volumes persistentes para banco, anexos e chaves de cookie. Duas variantes
prontas, cada uma com seu guia:

- **[`deploy/PORTAINER.md`](deploy/PORTAINER.md)** — acesso local por HTTP
  em `http://localhost:8080`, sem domínio nem certificado. Stack:
  [`deploy/portainer-stack.yml`](deploy/portainer-stack.yml).
- **[`deploy/HTTPS.md`](deploy/HTTPS.md)** — acesso pela Internet com
  domínio próprio e HTTPS automático via Caddy. Stack:
  [`deploy/portainer-https-stack.yml`](deploy/portainer-https-stack.yml).

Resumo do caminho HTTP local:

```bash
# Na máquina gerenciada pelo Portainer, na pasta que contém ErpFinanceiro.sln:
docker build -t erp-financeiro-web:1.0.0 .
docker build -t erp-financeiro-documentos:1.0.0 servico-documentos

cp deploy/portainer.env.example deploy/portainer.env
chmod 600 deploy/portainer.env
# preencha POSTGRES_PASSWORD, AES_KEY_BASE64, ADMIN_EMAIL, ADMIN_PASSWORD, INTERNAL_TOKEN
```

Depois, no Portainer: **Stacks → Add stack**, ambiente **Docker
Standalone**, cole `deploy/portainer-stack.yml` no Web editor, carregue as
variáveis de `deploy/portainer.env` e clique em **Deploy the stack**. O
[guia completo](deploy/PORTAINER.md) cobre geração de segredos, primeiro
acesso, backup e atualização; a [validação registrada](deploy/VALIDACAO.md)
descreve os testes já realizados sobre essa configuração (build, 165 testes
.NET, subida com banco vazio, HTTPS via Caddy, exportações, recriação de
container preservando dados).

Pontos importantes:

- As migrations do banco são aplicadas automaticamente no startup da web
  (`Banco:AplicarMigracoes`), antes do seed do administrador.
- Mantenha **uma única instância** da web: o sistema usa fila em memória e
  Blazor Server, não está preparado para múltiplas réplicas.
- `GET /health` verifica a conexão com o banco e deve responder `Healthy`.
- Depois do primeiro acesso, remova `ADMIN_PASSWORD`/
  `SeedAdministrador__SenhaInicial` para desativar o provisionamento
  inicial — isso não afeta a conta já criada.
- Configure o provedor de IA pelas variáveis de ambiente `Ia__Documentos__*`
  e `Ia__Chat__*` (Provedor/Modelo/ApiKey — ver `.env.example` ou
  `deploy/portainer.env.example`) antes do deploy; sem isso, a leitura de
  documentos usa só OCR e regras, e o chat fica indisponível. A tela
  **Configurações de IA** só mostra o que está configurado e testa a
  conexão — trocar exige reiniciar o container.

## Estrutura do repositório

```
├── src/                        # backend + frontend .NET (ver Arquitetura)
├── tests/ErpFinanceiro.Tests/  # testes xUnit
├── servico-documentos/         # serviço Python de leitura de documentos
├── storage/                    # anexos em disco (dev local)
├── deploy/                     # stacks e guias de deploy no Portainer
├── scripts/ef.sh               # atalho para comandos dotnet-ef
├── Dockerfile                  # imagem de produção da aplicação web
└── docker-compose.yml          # ambiente de desenvolvimento local
```
