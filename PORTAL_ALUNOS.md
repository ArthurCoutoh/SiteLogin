# Portal dos alunos

O portal usa o mesmo servidor C# e banco MySQL do login. As tabelas adicionais são criadas automaticamente no próximo início do servidor; contas e sessões existentes são preservadas. Cadastros antigos que aguardavam aprovação passam a ter acesso. Contas banidas continuam bloqueadas.

## Aluno

1. Abra `/login/index.html`, escolha **Criar conta** e aceite os termos.
2. Entre imediatamente com o e-mail e a senha escolhidos.
3. No menu ☰, abra **Minhas entregas**, selecione o exercício, dê um título e cole seu código C# (ou carregue um arquivo `.cs`/`.txt`, até 20.000 caracteres).
4. Nos três pontinhos ⋯ de **Minhas entregas**, abra **Preferências e privacidade**, permita o acesso do professor se quiser receber feedback. Essa opção começa desmarcada e pode ser revogada.
5. As tentativas e o feedback ficam na sua própria conta. Cada envio cria uma tentativa nova. Você pode excluir uma entrega e seu feedback.

## Professor

Entre com sua conta de administrador e abra **☰ → Painel do professor**. A conta de professor não usa o formulário de entregas nem as permissões de aluno. O painel tem três abas:

- **Alunos**: nome, e-mail e situação da conta; banir e reativar. Banir encerra todas as sessões.
- **Entregas**: somente códigos de alunos com conta ativa que autorizaram a consulta. A permissão é conferida novamente ao enviar feedback.
- **Trocas de senha**: autorizar ou recusar solicitações. Antes de autorizar, confirme a identidade do aluno por um canal que você já conhece. A autorização mostra um código aleatório uma única vez, válido por 30 minutos. Entregue esse código manualmente ao aluno; ele define sua nova senha sozinho. Gerar outro código invalida o anterior.

## Recuperação sem e-mail

No login, o aluno escolhe **Esqueci ou quero mudar minha senha**, informa seu e-mail e pede autorização. Depois de falar com o professor e receber o código, abre **Já tenho o código de autorização**, informa e-mail, código e nova senha. O código só funciona uma vez. Todas as sessões anteriores são encerradas.

O professor nunca vê a senha antiga ou a nova senha. O banco mantém apenas hashes das senhas e dos códigos. Não há SMTP, envio de e-mail ou verificação de caixa de entrada. Use uma senha exclusiva para o site.

## Hospedagem e testes

O sistema funciona com o computador do professor desligado enquanto o servidor e o banco hospedados estiverem ativos. O deploy existente da branch `main` no Railway utiliza o Dockerfile atualizado. Não é necessário criar outra variável de e-mail ou serviço.

O workflow **Compilar e testar portal** compila o .NET 10 e verifica os fluxos com MySQL 8.4 real: termos, cadastro livre, papéis, proteção de arquivos, consentimento/revogação, feedback, isolamento entre alunos, exclusão de entrega, recuperação autorizada de uso único, CSRF e banimento.


## Perfil e menus

A navegação completa fica em **☰**, no canto superior direito. Apenas os atalhos Menu / Apoio / Exercícios permanecem visíveis nas telas de estudo. A foto no canto superior esquerdo abre **Editar perfil**, **Preferências / Privacy** (para alunos), **Senha e acesso**, **Light / Dark** e **Sair**. A foto aparece no menu principal, na seleção de exercícios e nas páginas comuns; nas páginas de conteúdo, as configurações também ficam acessíveis por ☰.

Em **Editar perfil**, você pode mudar o nome, escrever uma biografia de até 600 caracteres e carregar uma foto JPEG, PNG ou WebP. O navegador recorta a foto em um quadrado de 256 px e a converte para JPEG antes de enviar. O servidor aceita imagens de até 80 KB e rejeita SVG e tipos não permitidos.

## Amigos e mensagens

1. Abra **☰ → Amigos**.
2. Para receber pedidos, abra **Editar perfil** e marque **Permitir que outras pessoas me encontrem pelo nome**. Essa opção começa desmarcada.
3. Busque alguém por pelo menos dois caracteres do nome. Somente pessoas que autorizaram a busca aparecem; e-mails e entregas não são exibidos.
4. Envie um pedido. A outra pessoa precisa aceitar em **Pedidos** antes que mensagens sejam permitidas.
5. Em **Amigos**, escolha **Conversar** para abrir a página **Mensagens**, ou abra **☰ → Mensagens** e selecione um amigo. Mensagens têm até 2.000 caracteres e atualizam automaticamente enquanto a página estiver visível. Use **Carregar mensagens anteriores** para consultar o histórico mais antigo.
6. Nos três pontinhos da conversa, você pode desfazer a amizade ou bloquear a pessoa. Bloquear impede novos pedidos e mensagens; **Bloqueados** permite desbloquear. Após recusar um pedido, o remetente precisa aguardar 24 horas para tentar novamente.

O histórico é mantido no MySQL e fica indisponível quando a amizade é desfeita ou bloqueada. Uma nova amizade aceita pode reabrir esse histórico. A interface e a API só permitem ler a própria conversa; nem o painel de professor tem acesso às conversas entre outros usuários. Não há criptografia ponta a ponta, e o administrador da infraestrutura tem acesso técnico ao banco. Os termos explicam essa diferença.

O teste **social_flow.py**, com MySQL real, verifica separação de professor/aluno, visibilidade por consentimento, perfil, tipos de avatar, pedidos e aceite de amizade, autoria das mensagens, isolamento de conversas, bloqueio, remoção de foto e banimento.
