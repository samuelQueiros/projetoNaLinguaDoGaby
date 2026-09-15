# Validação da preparação para deploy

Executada em 15/09/2026, localmente, com Docker Linux amd64:

- 165 testes .NET passaram (`dotnet test ... --configuration Release`).
- Build das imagens `erp-financeiro-web:1.0.0` e
  `erp-financeiro-documentos:1.0.0` concluído.
- Stack validada pelo Docker Compose 2.39.4.
- Inicialização em volumes isolados e banco vazio: migrations e administrador
  criados; banco, Python e web saudáveis.
- HTTPS via Caddy com certificado interno exclusivo do teste, na porta local
  18443; login com cookie Secure e exportações PDF/Excel funcionaram.
- Negociação Blazor anunciou suporte a WebSocket.
- Recriação do container web preservou o arquivo no volume e a autenticação
  com o cookie emitido antes da recriação.
- Serviço Python respondeu ao healthcheck e recusou extração sem token (401).

A emissão de certificado público depende do domínio/DNS e do acesso às portas
no servidor de destino. Não houve deploy no Portainer remoto, teste visual de
navegação, sessão WebSocket completa ou chamada aos provedores externos de IA.

## Ajuste para execução local

Após a confirmação de uso local pelo usuário, a stack padrão passou a servir
HTTP em `127.0.0.1:8080`, e a versão HTTPS foi preservada em arquivo separado.
A imagem web foi reconstruída e a nova stack validada pelo Compose. Um teste
isolado na porta 18080 confirmou inicialização com banco vazio, healthcheck,
login HTTP, cookie compatível, acesso autenticado e exportação PDF, sem
redirecionamento HTTPS nem HSTS. Os containers e volumes de teste foram removidos.
