#!/usr/bin/env bash
# Wrapper para rodar dotnet-ef dentro do container do SDK (ver seção 3.1 do CLAUDE.md).
# Uso: scripts/ef.sh migrations add NomeDaMigration
#      scripts/ef.sh database update
set -euo pipefail
cd "$(dirname "$0")/.."
docker run --rm --network host \
  -v "$PWD":/repo -w /repo \
  -e ASPNETCORE_ENVIRONMENT=Development \
  mcr.microsoft.com/dotnet/sdk:8.0 \
  bash -c "dotnet tool restore >/dev/null && dotnet dotnet-ef $* \
    -p src/ErpFinanceiro.Infrastructure -s src/ErpFinanceiro.Web"
docker run --rm -v "$PWD":/repo mcr.microsoft.com/dotnet/sdk:8.0 \
  chown -R "$(id -u):$(id -g)" /repo/src >/dev/null 2>&1 || true
