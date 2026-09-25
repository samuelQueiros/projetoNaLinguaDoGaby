# Deploy do ERP no Portainer

Esta stack é para **Docker Standalone, em um único servidor**, com uma instância
web. Ela sobe Blazor/.NET 8, PostgreSQL 16, serviço Python de documentos e Caddy
para HTTPS. O Compose da raiz continua sendo o ambiente de desenvolvimento.

## 1. Identificar o servidor

No Portainer, abra **Environments** e selecione o ambiente Docker. Verifique se
aparece Standalone ou Swarm. No terminal do servidor, também pode usar:

```bash
docker info --format '{{.Swarm.LocalNodeState}}'
```

`inactive` indica que o Docker não participa de Swarm. Se aparecer `active`,
esta configuração precisa ser adaptada antes do deploy: redes, armazenamento,
execução única das migrações e ordem de inicialização diferem em Swarm.

O caminho abaixo pressupõe um servidor Linux com Docker (e Compose v2 para
os comandos de terminal), domínio como
`financeiro.suaempresa.com.br` apontando para seu IP e portas TCP 80/443 livres
e acessíveis. Se existir um registro IPv6 (AAAA), ele também deve apontar para
o servidor correto. Caddy emite e renova o certificado automaticamente.
Não informe `https://` ou caminhos na variável `ERP_DOMAIN`.

Se as portas já estiverem ocupadas por Traefik ou Nginx Proxy Manager, consulte
a seção de proxy existente antes de implantar.

## 2. Construir as imagens

No **servidor Docker gerenciado pelo Portainer**, copie/clone o projeto e entre
na pasta raiz do repositório (onde estão `frontend/`, `backend/` e o `Dockerfile`):

```bash
docker build --pull -t erp-financeiro-web:1.0.0 .
docker build --pull -t erp-financeiro-documentos:1.0.0 backend/servico-documentos
```

O Web editor recebe somente o YAML: as imagens precisam existir previamente
nesse servidor. O build local no computador de desenvolvimento não as envia
para o servidor. Alternativamente, construa para a arquitetura do servidor,
publique em um registry e preencha `ERP_WEB_IMAGE`/`ERP_DOCUMENTOS_IMAGE` com
os nomes completos. Cadastre as credenciais do registry no Portainer se privado.
As imagens deste repositório ainda não estão publicadas em nenhum registry.

Use tags novas para cada atualização, como `1.0.1`, nos builds e nas variáveis.

## 3. Preparar as variáveis

```bash
cp deploy/portainer-https.env.example deploy/portainer-https.env
chmod 600 deploy/portainer-https.env
```

Edite a cópia e preencha:

| Variável | Valor |
|---|---|
| `ERP_DOMAIN` | Domínio apontando para o servidor, sem protocolo |
| `POSTGRES_PASSWORD` | Segredo gerado com `openssl rand -hex 32` |
| `AES_KEY_BASE64` | Chave gerada com `openssl rand -base64 32` |
| `ADMIN_EMAIL` | E-mail do primeiro administrador |
| `ADMIN_PASSWORD` | Senha forte com pelo menos 8 caracteres, maiúscula, minúscula, número e símbolo |
| `INTERNAL_TOKEN` | Outro segredo gerado com `openssl rand -hex 32` |
| `ERP_WEB_IMAGE` / `ERP_DOCUMENTOS_IMAGE` | Tags construídas no passo anterior |

Execute os comandos de geração separadamente e guarde os valores em local
seguro. Não publique o arquivo preenchido. Use a senha hexadecimal indicada
para PostgreSQL: evita problemas de interpolação e de connection string.
Se uma senha contiver `$`, prefira inseri-la diretamente no campo da variável
no Portainer; em arquivos `.env` do Compose use aspas simples para preservar
valores literais.

**Banco existente:** preserve a chave AES original ao transferir dados. Gerar
outra chave torna os dados bancários e chaves de IA já cifrados ilegíveis.
Esta stack cria volumes novos; ela não importa automaticamente o banco ou os
arquivos locais. Para migrar, restaure um dump PostgreSQL e copie `storage/`
para o volume de anexos antes de iniciar a web; mantenha os arquivos graváveis
pelo UID 1654, usuário `app` da imagem .NET.

Se `172.30.80.0/24` conflitar com uma rede do servidor, ajuste `PROXY_SUBNET`,
`PROXY_IP` e `WEB_IP` juntos. Os dois IPs devem ser diferentes, dentro da
sub-rede e fora do gateway. Os endereços fixos impedem que a web ocupe o IP
reservado para o proxy durante a inicialização.

Validação opcional no terminal (não imprime os segredos):

```bash
docker compose --env-file deploy/portainer-https.env -f deploy/portainer-https-stack.yml config --quiet
```

## 4. Criar a stack

1. Abra o ambiente **Docker Standalone → Stacks → Add stack**.
2. Nomeie a stack `erp-financeiro`. Mantenha esse nome nos próximos deploys.
3. Escolha **Web editor** e cole o conteúdo de `deploy/portainer-https-stack.yml`.
4. Em **Environment variables**, carregue `deploy/portainer-https.env` usando
   **Load variables from .env file**, ou preencha as variáveis manualmente.
5. Clique em **Deploy the stack**. Para imagens construídas no próprio servidor,
   não force novo pull das imagens locais durante atualizações.
6. Aguarde `postgres`, `servico-documentos` e `web` ficarem saudáveis. Veja os
   logs da web para migrações/seed e do Caddy para emissão do certificado.
7. Acesse `https://SEU_DOMINIO` e entre com o administrador configurado.

As migrations são aplicadas antes do seed e do processamento de documentos.
Por isso, mantenha **uma instância web**; este projeto usa fila em memória e
Blazor Server, e não está configurado para múltiplas réplicas. Falha de migração
impede a inicialização; consulte o log antes de tentar novamente.

A criação do administrador ocorre somente se o e-mail ainda não existe.
Alterar `ADMIN_PASSWORD` depois não redefine a senha de uma conta existente.
Uma senha recusada pelo Identity aparece nos logs; corrija e recrie o container
se a conta ainda não foi criada. Depois do primeiro acesso, retire a linha
`SeedAdministrador__SenhaInicial` da stack e remova a variável `ADMIN_PASSWORD`
do Portainer. Isso desativa o provisionamento inicial sem apagar a conta.

Configure o provedor/modelo/chave de IA na tela **Configurações de IA** do ERP.
Sem configuração, o serviço de documentos usa OCR e extração por regras.
Somente Caddy publica portas; PostgreSQL e Python são acessados pela rede Docker.

## 5. Verificações após o deploy

- Abrir login e navegar entre telas: Blazor depende de WebSocket.
- Cadastrar um registro de teste, anexar um arquivo e baixar novamente.
- Exportar um relatório PDF e Excel.
- Reiniciar a stack e confirmar que dados e anexos continuam disponíveis.
- `https://SEU_DOMINIO/health` deve retornar `Healthy`; verifica a conexão ao banco.

O healthcheck Python verifica o processo HTTP; não confirma acesso aos provedores
externos de IA. O healthcheck Docker informa falhas; `restart: unless-stopped`
reinicia processos encerrados, mas não reinicia automaticamente um processo
que continua executando e está apenas marcado como unhealthy.

## 6. Persistência, backup e atualização

Os volumes recebem o prefixo do nome da stack, por exemplo:

- `erp-financeiro_postgres_data`: banco;
- `erp-financeiro_anexos`: documentos e anexos;
- `erp-financeiro_chaves`: chaves de proteção dos cookies;
- `erp-financeiro_caddy_data` e `erp-financeiro_caddy_config`: estado do HTTPS.

Faça backup antes de atualizar. No terminal do servidor, com a cópia do YAML e
das variáveis usados no Portainer (nome da stack deve coincidir):

```bash
mkdir -p backups
chmod 700 backups
# Interrompe gravações enquanto banco e anexos são copiados.
docker compose -p erp-financeiro --env-file deploy/portainer-https.env -f deploy/portainer-https-stack.yml stop web
docker compose -p erp-financeiro --env-file deploy/portainer-https.env -f deploy/portainer-https-stack.yml exec -T postgres pg_dump -U erp -d erp_financeiro -Fc > backups/banco.dump
docker run --rm -v erp-financeiro_anexos:/origem:ro -v "$PWD/backups:/backup" alpine:3.22 tar czf /backup/anexos.tar.gz -C /origem .
docker run --rm -v erp-financeiro_chaves:/origem:ro -v "$PWD/backups:/backup" alpine:3.22 tar czf /backup/chaves.tar.gz -C /origem .
docker compose -p erp-financeiro --env-file deploy/portainer-https.env -f deploy/portainer-https-stack.yml start web
```

Use uma pasta nova por backup para não sobrescrever o anterior. Guarde também
a chave AES e as variáveis fora do servidor, com acesso restrito. Valide a
restauração em um ambiente separado. Para restaurar o banco vazio, mantenha web
parada e use `pg_restore -U erp -d erp_financeiro --no-owner` no container
PostgreSQL, passando o dump pela entrada padrão; restaure os arquivos nos
volumes correspondentes antes de iniciar a web.

Para atualizar, construa/publique novas tags, altere as duas variáveis de imagem
no Portainer e atualize a stack. Não remova os volumes. Migrações podem exigir
restauração do backup para voltar à versão anterior; trocar apenas a imagem
não desfaz alterações de schema.

## Proxy HTTPS já existente

Nesse caso, adapte uma cópia da stack:

1. Remova o serviço `caddy` e conecte `web` à rede Docker externa do seu proxy.
2. Configure o proxy para encaminhar o domínio para `http://web:8080`, com
   WebSocket habilitado e `X-Forwarded-Proto: https`.
3. Configure `Proxy__EnderecosConfiaveis` com o IP real e estável do proxy visto
   pela web (endereços separados por vírgula). Não libere todos os proxies.
4. Mantenha o domínio em `AllowedHosts`, a porta HTTPS externa correta e os
   volumes da web. Não publique a porta HTTP da web na Internet.

HTTPS é necessário para os cookies de login em Production. Acessar diretamente
um IP por HTTP não é um substituto para configurar o domínio/certificado.

## Referências

- [Stacks no Portainer](https://docs.portainer.io/2.33-lts/user/docker/stacks/add)
- [Proxy e HTTPS com Caddy](https://caddyserver.com/docs/quick-starts/reverse-proxy)
- [Proxies confiáveis no ASP.NET Core](https://learn.microsoft.com/en-us/dotnet/core/compatibility/aspnet-core/8.0/forwarded-headers-unknown-proxies)
- [Persistência de chaves ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/configuration/default-settings?view=aspnetcore-8.0)
