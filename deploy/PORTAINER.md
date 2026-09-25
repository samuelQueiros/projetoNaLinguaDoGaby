# ERP local no Portainer

Configuração para **Docker Standalone**, acessível em **http://localhost:8080**
na máquina onde o Docker roda. Não precisa de domínio ou certificado. A stack
inicia a aplicação web, o PostgreSQL e o serviço Python de documentos.

## 1. Construir as imagens

`deploy/portainer-stack.yml` tem `build:` apontando para a raiz do
repositório e para `servico-documentos/`, com `pull_policy: build` — ou
seja, o Compose sempre constrói a imagem a partir do código-fonte, nunca
tenta baixar de um registry. Use **Build method: Repository** ao criar a
stack (passo 3): o Portainer clona o repositório e builda as duas imagens
sozinho a cada deploy (inicial e nos seguintes, inclusive pelo botão **Pull
and redeploy**) — não precisa rodar `docker build` manualmente. Isso exige
apenas que o token/autenticação Git usado tenha acesso de leitura ao
repositório (veja o passo 3 para a ressalva sobre GitOps updates).

Por causa do `pull_policy: build`, os métodos **Web editor**/**Upload** não
funcionam com este arquivo — eles não clonam o repositório, então não há
código-fonte disponível para o Compose buildar (`docker build` manual antes
não ajuda: mesmo com a imagem já existindo, `pull_policy: build` força uma
tentativa de rebuild). Para deploy sem repositório Git, use uma cópia do
YAML sem essas duas diretivas (`build:`/`pull_policy:`) e construa as
imagens manualmente antes, com as tags de `ERP_WEB_IMAGE`/
`ERP_DOCUMENTOS_IMAGE`.

## 2. Configurar acesso e senhas

```bash
cp deploy/portainer.env.example deploy/portainer.env
chmod 600 deploy/portainer.env
```

Edite a cópia preenchendo:

| Variável | Valor |
|---|---|
| `ERP_BIND_IP` | `0.0.0.0` (padrão) para aceitar acesso por qualquer interface de rede da máquina — inclusive por hostname/IP, não só `localhost`. Use `127.0.0.1` só se quiser restringir ao acesso local via `localhost`/`127.0.0.1` |
| `ERP_PORT` | `8080`, ou outra porta livre |
| `POSTGRES_PASSWORD` | Gere com `openssl rand -hex 32` |
| `AES_KEY_BASE64` | Gere com `openssl rand -base64 32` |
| `ADMIN_EMAIL` | E-mail do administrador |
| `ADMIN_PASSWORD` | Pelo menos 8 caracteres, incluindo maiúscula, minúscula, número e símbolo |
| `INTERNAL_TOKEN` | Gere outro segredo com `openssl rand -hex 32` |

Execute cada comando de geração separadamente. Guarde os valores e não
publique o arquivo preenchido. Para senhas contendo `$`, use aspas simples em
arquivos `.env` do Compose ou insira o valor literal no campo do Portainer.

Com `ERP_BIND_IP=0.0.0.0` (padrão), acesse por `http://localhost:8080`,
`http://IP_DA_MAQUINA:8080` ou pelo hostname da máquina, o que for mais
conveniente. Libere essa porta somente para sua rede confiável no firewall.
HTTP transmite dados sem criptografia; para acesso pela Internet, use a
[configuração HTTPS](HTTPS.md).

## 3. Criar a stack no Portainer

1. Selecione o ambiente Docker **Standalone** e abra **Stacks → Add stack**.
2. Nomeie a stack `erp-financeiro`.
3. Escolha **Web editor** e cole `deploy/portainer-stack.yml`.
4. Em **Environment variables**, carregue `deploy/portainer.env` ou preencha
   as variáveis manualmente.
5. Clique em **Deploy the stack** e aguarde os três serviços ficarem saudáveis.
6. Abra **http://localhost:8080** e entre com o e-mail/senha configurados.

`localhost` refere-se à máquina do navegador. Se o Docker estiver em outro
computador, use a configuração de rede local descrita acima.

Para identificar o ambiente, veja **Environments** no Portainer. No terminal,
`docker info --format '{{.Swarm.LocalNodeState}}'` retorna `inactive` quando o
Docker não participa de Swarm. Esta stack não foi preparada para Swarm.

Alternativamente, com Docker Compose v2, pode iniciar pelo terminal:

```bash
docker compose -p erp-financeiro --env-file deploy/portainer.env -f deploy/portainer-stack.yml up -d --wait
```

Escolha um dos caminhos para gerenciar a stack. Imagens locais não precisam
ser baixadas de um registry; não force pull delas na atualização do Portainer.

## Comportamento e persistência

- As migrations são aplicadas automaticamente antes de criar o administrador.
- O ambiente continua `Production`, com erros detalhados desabilitados. Apenas
  esta stack habilita `Seguranca__PermitirHttpLocal`, permitindo cookies de login
  por HTTP e desativando o redirecionamento HTTPS/HSTS.
- PostgreSQL e Python não publicam portas no computador.
- Banco, anexos e chaves dos cookies ficam nos volumes `postgres_data`, `anexos`
  e `chaves`, prefixados pelo nome da stack (`erp-financeiro_`).
- Mantenha uma instância web, devido à fila em memória de processamento de documentos.
- Configure o provedor de IA na tela **Configurações de IA**. Sem essa
  configuração, a leitura usa OCR e regras.
- `http://localhost:8080/health` deve responder `Healthy`.

O administrador é criado apenas se o e-mail não existir. Alterar a variável
`ADMIN_PASSWORD` não muda a senha de uma conta já criada. Se a senha inicial
for recusada, o motivo aparece nos logs da web. Depois de criar a conta, pode
remover a linha `SeedAdministrador__SenhaInicial` do YAML e a variável
`ADMIN_PASSWORD` do Portainer para desabilitar o provisionamento inicial.

## Dados existentes, backup e atualizações

A stack cria um banco novo. Ela não importa automaticamente o banco nem a
pasta `storage/` usados no desenvolvimento. Se migrar dados existentes,
**preserve a chave AES original**, restaure o dump PostgreSQL e copie os
arquivos para o volume de anexos antes de iniciar a web. Os anexos devem ser
graváveis pelo UID 1654 (`app`).

Antes de atualizar, pare a web para interromper gravações e faça um dump do
PostgreSQL (`pg_dump -U erp -d erp_financeiro -Fc`), além de uma cópia dos
volumes de anexos e chaves. Guarde também a chave AES e as variáveis em local
seguro. Teste a restauração em ambiente separado.

Use novas tags de imagem a cada atualização, ajuste `ERP_WEB_IMAGE` e
`ERP_DOCUMENTOS_IMAGE` e atualize a stack. Mantenha o mesmo nome de stack e
não exclua seus volumes. Reverter uma migration pode exigir restaurar backup;
voltar apenas a imagem não desfaz a alteração de schema.

A [validação registrada](VALIDACAO.md) descreve os testes realizados.
