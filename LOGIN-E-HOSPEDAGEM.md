# Login de alunos com C# e MySQL

O portfólio permanece público. Exercícios, Apoio e seus arquivos são servidos apenas após o servidor validar uma sessão de uma conta ativa. O cadastro de aluno começa pendente; o professor libera ou bloqueia em **Alunos**. O professor também pode estudar, alterar sua própria senha em **Minha conta** e sair.

## Funciona com meu computador desligado?

Sim. Hospede este aplicativo e o MySQL na nuvem. O navegador da aluna conversa com o servidor da hospedagem; seu computador não participa. Se executar apenas o Docker no seu computador, o acesso depende de ele estar ligado. Contas da hospedagem devem permanecer ativas e com recursos disponíveis; confirme o plano e a cobrança antes de contratar. Este ZIP não publica nem contrata serviços.

## Caminho recomendado: Railway (aplicação e MySQL)

1. Descompacte o ZIP. Envie o conteúdo da pasta `portfolio-arthur` a um repositório privado do GitHub. Inclua Dockerfile, backend e as pastas do site; não envie `.env`, senhas, `bin` ou `obj`.
2. No Railway, crie um projeto e use **+ New → Database → MySQL**. Mantenha o MySQL na rede privada; não é necessário habilitar acesso público ao banco.
3. No mesmo projeto, use **+ New → GitHub Repo** e escolha o repositório. O Dockerfile deve estar na raiz do repositório. Se você enviou a pasta completa em vez de seu conteúdo, configure **Root Directory** para `/portfolio-arthur`.
4. No serviço da aplicação, abra **Variables**. Use referências ao serviço MySQL para preencher as variáveis abaixo. Se o banco tiver outro nome no painel, troque `MySQL` pelo nome exato dele.

```text
MYSQLHOST=${{MySQL.MYSQLHOST}}
MYSQLPORT=${{MySQL.MYSQLPORT}}
MYSQLUSER=${{MySQL.MYSQLUSER}}
MYSQLPASSWORD=${{MySQL.MYSQLPASSWORD}}
MYSQLDATABASE=${{MySQL.MYSQLDATABASE}}
MYSQL_SSL_MODE=Required
ADMIN_EMAIL=seu-email-real
ADMIN_PASSWORD=sua-senha-unica-de-pelo-menos-12-caracteres
ASPNETCORE_ENVIRONMENT=Production
```

Esses valores ficam no servidor, nunca em config.js. `MYSQL_SSL_MODE=Required` exige conexão criptografada; verifique se o serviço aceita TLS. Para banco externo, prefira `VerifyFull` e o certificado/CA exigido pelo provedor. Se o serviço privado não suportar TLS, confirme a rede privada isolada antes de usar `Disabled`; não desative TLS para um banco acessado pela internet.

5. Publique o serviço. No primeiro início ele cria as tabelas de `backend/schema.sql` e a conta de professor. O banco precisa existir: o template Railway já o provisiona. Se o primeiro início falhar, confira as variáveis e o log do serviço; não poste senhas em prints.
6. Abra **Settings → Networking → Generate Domain**, apontando para a porta informada pelo Railway (a aplicação respeita `PORT`; o padrão é 8080). O endereço gerado usa HTTPS.
7. Abra `/login/index.html`, entre com ADMIN_EMAIL e ADMIN_PASSWORD, e teste o fluxo abaixo. A senha de bootstrap não redefine uma conta existente. Após o primeiro início bem-sucedido, remova ADMIN_PASSWORD das variáveis; guarde sua senha de login. Mantenha o banco persistente e configure backups no provedor.
8. A aluna abre o site, escolhe **Criar conta** e aguarda. Você entra como professor, abre **Alunos** e usa **Liberar acesso**. A aluna então consegue entrar. **Bloquear** revoga todas as sessões dessa aluna imediatamente para as próximas requisições.

Não há envio automático de e-mail, recuperação por link ou autenticação em duas etapas. A alteração de senha exige conhecer a senha atual. Para recuperar uma senha perdida, consulte um administrador do servidor; não guarde senhas em texto simples nem altere `password_hash` manualmente para uma senha legível.

## Manter o mesmo link do Netlify

A alternativa mais simples é usar o domínio HTTPS do aplicativo. Para continuar com o endereço atual do Netlify, ele pode atuar como proxy para o aplicativo:

1. Primeiro publique e teste o servidor no Railway.
2. Na pasta `deploy-netlify`, edite `_redirects`. Troque somente o domínio de exemplo pelo domínio HTTPS real da aplicação, mantendo `/:splat 200!`:

```text
/* https://SEU-DOMINIO-REAL.up.railway.app/:splat 200!
```

3. No **mesmo projeto Netlify existente**, atualize o deploy usando **somente o conteúdo de deploy-netlify**. Essa pasta contém index.html, _redirects e _headers; não publique o ZIP inteiro, backend ou os exercícios como arquivos estáticos.
4. Teste login, cadastro, liberação, acesso por link direto, alteração de senha e saída pelo endereço Netlify. Cookies são HttpOnly, Secure e sem Domain explícito, para funcionar no domínio que o navegador acessa. As chamadas da interface usam `/api`, no mesmo domínio, sem CORS.
5. Confira no deploy publicado que Exercícios/Apoio não existem como arquivos estáticos. Os arquivos novos e antigos devem passar pelo proxy. Deploys antigos do Netlify, cópias antigas, caches e materiais que já estavam públicos não são recolhidos por esta mudança; retire versões públicas antigas na administração da hospedagem se precisar impedir acesso a elas.

O proxy mantém o link do projeto, mas depende da API estar disponível. O Netlify possui limite de tempo para requisições por proxy; escolha hospedagem que permaneça ativa para evitar atraso de partida. Não hospede a API C# simplesmente arrastando arquivos no Netlify: ele não inicia esse servidor ASP.NET Core dessa forma.

## Teste local (Docker Desktop)

Instale Docker Desktop no Windows e inicie-o. Na pasta `portfolio-arthur`, copie `.env.example` para `.env`, preencha senhas próprias e seu e-mail, e execute no terminal:

```powershell
docker compose up --build
```

Abra http://localhost:8080. Este teste usa cookies sem Secure somente no ambiente Development. Não publique usando Development. Para parar:

```powershell
docker compose down
```

Esse comando preserva o volume do banco. `docker compose down -v` apaga o banco local e as contas; não execute se quiser preservar dados.

Alternativa para quem já tem MySQL e SDK .NET 10: crie um banco `portfolio`, configure `ConnectionStrings__MySql` como variável de ambiente com sua conexão, defina ADMIN_EMAIL/ADMIN_PASSWORD e execute `dotnet run --project backend --urls http://localhost:8080` com ASPNETCORE_ENVIRONMENT=Development. SiteRoot padrão aponta para a pasta acima de backend.

## Verificação funcional

Com o aplicativo e o banco em funcionamento, execute:

```powershell
python backend/tests/login_flow.py http://localhost:8080
```

O teste pede a senha do professor sem exibi-la e cria uma conta temporária de teste. Testa acesso direto bloqueado, cadastro pendente, aprovação, login, bloqueio, CSRF e saída. A conta de teste fica bloqueada no banco após o teste para preservar registros; pode ser removida por um administrador do banco. Nunca execute um teste com credenciais de terceiros.

## Organização e proteção

- `backend/`: API ASP.NET Core .NET 10, driver MySqlConnector 2.6.2, SQL parametrizado.
- `users`: nomes, e-mails únicos, hash de senha, papel, aprovação e bloqueio temporário de tentativas.
- `sessions`: somente o hash de um token aleatório e sua expiração, não a senha nem o token original. A aplicação deve ter uma única instância nesta configuração inicial; antiforgery usa chaves locais do processo. Para escalar para várias instâncias, compartilhe as chaves Data Protection entre elas. Em produção o serviço considera HTTPS terminado no proxy da hospedagem, portanto use obrigatoriamente um domínio HTTPS.
- Sessões duram oito horas e sobrevivem a reinícios do aplicativo se o banco for preservado.
- Senhas usam PasswordHasher do ASP.NET Core; não são salvas em texto simples.
- Cinco erros de senha bloqueiam a conta por 15 minutos; endpoints de cadastro/login também têm limite de requisições por IP. Atrás de proxy o limite pode agrupar usuários, pois a configuração não confia cegamente em X-Forwarded-For.
- Alterações usam proteção CSRF. Reiniciar o aplicativo pode exigir recarregar uma tela aberta para obter um token CSRF novo; as sessões de login continuam no MySQL.
- Conteúdo de Exercícios e Apoio, inclusive atividades.js, só sai do servidor com conta ativa. Nenhuma checagem apenas em JavaScript é usada como controle de acesso.
- O servidor só entrega arquivos de pastas permitidas; backend, SQL, .env, código de testes e documentos de configuração não são publicados.
- Quem já baixou material enquanto tinha acesso continua com aquela cópia.

## Fontes para configuração

- https://docs.railway.com/guides/docker-compose
- https://docs.railway.com/databases/mysql
- https://docs.railway.com/networking/public-networking
- https://docs.netlify.com/manage/routing/redirects/rewrites-proxies/
- https://mysqlconnector.net/
- https://learn.microsoft.com/aspnet/core/security/anti-request-forgery

## Validação desta entrega

A sintaxe JavaScript e a estrutura dos arquivos foram verificadas. A compilação C# e a execução integrada com MySQL precisam ser feitas no Docker ou na hospedagem: o ambiente de preparação não possui SDK .NET nem servidor MySQL. O teste acima está incluído para verificar o funcionamento real antes de liberar aos alunos.
