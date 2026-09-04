# ERP Financeiro

Sistema financeiro interno para gestão de obrigações e movimentações de uma
empresa: contas a pagar, fornecedores, boletos, notas fiscais, cartões,
conciliação bancária, relatórios, alertas e controle de usuários.

Objetivo: ser o painel central do setor financeiro, eliminando planilhas
soltas, documentos perdidos e divergências entre sistema e banco.

## Stack

| Camada | Tecnologia |
|---|---|
| Backend + Frontend | ASP.NET Core 8 com Blazor Server |
| Banco de dados | PostgreSQL (via Npgsql) |
| ORM / migrações | Entity Framework Core |
| Autenticação | ASP.NET Core Identity |
| Exportações | ClosedXML (Excel), QuestPDF (PDF), CsvHelper (CSV) |
| Testes | xUnit, bUnit, Playwright for .NET |

## Estrutura do projeto

```
src/
├── ErpFinanceiro.Web/             # Blazor Server (UI)
├── ErpFinanceiro.Application/     # regras de negócio / casos de uso
├── ErpFinanceiro.Domain/          # entidades e enums
└── ErpFinanceiro.Infrastructure/  # EF Core, storage, exportação
tests/
└── ErpFinanceiro.Tests/           # testes xUnit
```

Arquitetura em camadas: `Domain` (sem dependências) → `Application` →
`Infrastructure` → `Web`.

## Rodando localmente

Pré-requisitos: .NET 8 SDK, Docker (para o PostgreSQL).

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

## Testes

```bash
dotnet test tests/ErpFinanceiro.Tests/ErpFinanceiro.Tests.csproj
```
