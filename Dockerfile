FROM node:20-bookworm-slim AS frontend
WORKDIR /frontend
COPY frontend/package*.json ./
RUN npm ci
COPY frontend/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:8.0-bookworm-slim AS build
WORKDIR /src
COPY backend/src/ErpFinanceiro.Domain/*.csproj backend/src/ErpFinanceiro.Domain/
COPY backend/src/ErpFinanceiro.Application/*.csproj backend/src/ErpFinanceiro.Application/
COPY backend/src/ErpFinanceiro.Infrastructure/*.csproj backend/src/ErpFinanceiro.Infrastructure/
COPY backend/src/ErpFinanceiro.Web/*.csproj backend/src/ErpFinanceiro.Web/
RUN dotnet restore backend/src/ErpFinanceiro.Web/ErpFinanceiro.Web.csproj
COPY backend/src/ backend/src/
COPY --from=frontend /backend/src/ErpFinanceiro.Web/wwwroot/react/ backend/src/ErpFinanceiro.Web/wwwroot/react/
RUN dotnet publish backend/src/ErpFinanceiro.Web/ErpFinanceiro.Web.csproj \
    -c Release -o /out --no-restore /p:UseAppHost=false /p:BuildReact=false \
    && rm -f /out/appsettings.Development.json*

FROM mcr.microsoft.com/dotnet/aspnet:8.0-bookworm-slim AS runtime
RUN apt-get update && apt-get install -y --no-install-recommends \
    curl fontconfig fonts-dejavu-core \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /out/ ./
RUN mkdir -p /app/storage /app/keys && chown -R app:app /app/storage /app/keys
ENV ASPNETCORE_HTTP_PORTS=8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false
USER app
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=5s --start-period=90s --retries=3 \
    CMD curl --fail --silent http://localhost:8080/health || exit 1
ENTRYPOINT ["dotnet", "ErpFinanceiro.Web.dll"]
