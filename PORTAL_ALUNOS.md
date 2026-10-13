# Portal dos alunos

O portal usa o mesmo servidor C# e banco MySQL do login. As tabelas adicionais são criadas automaticamente no próximo início do servidor; contas e sessões existentes são preservadas. Cadastros antigos que aguardavam aprovação passam a ter acesso. Contas banidas continuam bloqueadas.

## Aluno

1. Abra `/login/index.html`, escolha **Criar conta** e aceite os termos.
2. Entre imediatamente com o e-mail e a senha escolhidos.
3. Em **Minhas entregas**, selecione o exercício, dê um título e cole seu código C# (ou carregue um arquivo `.cs`/`.txt`, até 20.000 caracteres).
4. Em **Privacidade das minhas entregas**, permita o acesso do professor se quiser receber feedback. Essa opção começa desmarcada e pode ser revogada.
5. As tentativas e o feedback ficam na sua própria conta. Cada envio cria uma tentativa nova. Você pode excluir uma entrega e seu feedback.

## Professor

Entre com sua conta de administrador e abra **Alunos**. O painel tem três abas:

- **Alunos**: nome, e-mail e situação da conta; banir e reativar. Banir encerra todas as sessões.
- **Entregas**: somente códigos de alunos com conta ativa que autorizaram a consulta. A permissão é conferida novamente ao enviar feedback.
- **Trocas de senha**: autorizar ou recusar solicitações. Antes de autorizar, confirme a identidade do aluno por um canal que você já conhece. A autorização mostra um código aleatório uma única vez, válido por 30 minutos. Entregue esse código manualmente ao aluno; ele define sua nova senha sozinho. Gerar outro código invalida o anterior.

## Recuperação sem e-mail

No login, o aluno escolhe **Esqueci ou quero mudar minha senha**, informa seu e-mail e pede autorização. Depois de falar com o professor e receber o código, abre **Já tenho o código de autorização**, informa e-mail, código e nova senha. O código só funciona uma vez. Todas as sessões anteriores são encerradas.

O professor nunca vê a senha antiga ou a nova senha. O banco mantém apenas hashes das senhas e dos códigos. Não há SMTP, envio de e-mail ou verificação de caixa de entrada. Use uma senha exclusiva para o site.

## Hospedagem e testes

O sistema funciona com o computador do professor desligado enquanto o servidor e o banco hospedados estiverem ativos. O deploy existente da branch `main` no Railway utiliza o Dockerfile atualizado. Não é necessário criar outra variável de e-mail ou serviço.

O workflow **Compilar e testar portal** compila o .NET 10 e verifica os fluxos com MySQL 8.4 real: termos, cadastro livre, papéis, proteção de arquivos, consentimento/revogação, feedback, isolamento entre alunos, exclusão de entrega, recuperação autorizada de uso único, CSRF e banimento.
