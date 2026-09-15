FROM mcr.microsoft.com/dotnet/sdk:8.0-bookworm-slim AS build
WORKDIR /src
COPY src/ErpFinanceiro.Domain/*.csproj src/ErpFinanceiro.Domain/
COPY src/ErpFinanceiro.Application/*.csproj src/ErpFinanceiro.Application/
COPY src/ErpFinanceiro.Infrastructure/*.csproj src/ErpFinanceiro.Infrastructure/
COPY src/ErpFinanceiro.Web/*.csproj src/ErpFinanceiro.Web/
RUN dotnet restore src/ErpFinanceiro.Web/ErpFinanceiro.Web.csproj
COPY src/ src/
RUN dotnet publish src/ErpFinanceiro.Web/ErpFinanceiro.Web.csproj \
    -c Release -o /out --no-restore /p:UseAppHost=false \
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
