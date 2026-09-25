FROM node:20-bookworm-slim AS frontend
WORKDIR /frontend
COPY frontend/package*.json ./
RUN npm ci
COPY frontend/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY backend/ErpFinanceiro.sln ./
COPY backend/src/ErpFinanceiro.Domain/ErpFinanceiro.Domain.csproj backend/src/ErpFinanceiro.Domain/
COPY backend/src/ErpFinanceiro.Application/ErpFinanceiro.Application.csproj backend/src/ErpFinanceiro.Application/
COPY backend/src/ErpFinanceiro.Infrastructure/ErpFinanceiro.Infrastructure.csproj backend/src/ErpFinanceiro.Infrastructure/
COPY backend/src/ErpFinanceiro.Web/ErpFinanceiro.Web.csproj backend/src/ErpFinanceiro.Web/
RUN dotnet restore backend/src/ErpFinanceiro.Web/ErpFinanceiro.Web.csproj

COPY backend/src/ backend/src/
COPY --from=frontend /backend/src/ErpFinanceiro.Web/wwwroot/react/ backend/src/ErpFinanceiro.Web/wwwroot/react/
RUN dotnet publish backend/src/ErpFinanceiro.Web/ErpFinanceiro.Web.csproj -c Release -o /app/publish --no-restore /p:BuildReact=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "ErpFinanceiro.Web.dll"]
