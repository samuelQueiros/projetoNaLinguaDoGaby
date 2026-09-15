FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ErpFinanceiro.sln ./
COPY src/ErpFinanceiro.Domain/ErpFinanceiro.Domain.csproj src/ErpFinanceiro.Domain/
COPY src/ErpFinanceiro.Application/ErpFinanceiro.Application.csproj src/ErpFinanceiro.Application/
COPY src/ErpFinanceiro.Infrastructure/ErpFinanceiro.Infrastructure.csproj src/ErpFinanceiro.Infrastructure/
COPY src/ErpFinanceiro.Web/ErpFinanceiro.Web.csproj src/ErpFinanceiro.Web/
COPY tests/ErpFinanceiro.Tests/ErpFinanceiro.Tests.csproj tests/ErpFinanceiro.Tests/
RUN dotnet restore src/ErpFinanceiro.Web/ErpFinanceiro.Web.csproj

COPY src/ src/
RUN dotnet publish src/ErpFinanceiro.Web/ErpFinanceiro.Web.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "ErpFinanceiro.Web.dll"]
