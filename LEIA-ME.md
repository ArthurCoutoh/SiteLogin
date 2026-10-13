# Portfólio de Arthur Couto

## Abrir

Extraia o ZIP inteiro e abra `index.html` no navegador, ou abra a pasta no VS Code e use Live Server. Nenhuma instalação de pacotes é necessária.

## Navegação e imagem

A abertura apresenta sua foto e botões para cada parte. As opções permanecem fixas no topo em todas as telas. Cada tela possui botões **Voltar ao menu** no início e no final.

O fundo é preto. Somente a região perto do mouse revela as mãos feitas em linhas vermelhas de constelação, com o interior transparente. As mãos permanecem fixas no fundo durante a rolagem de qualquer tela. Não há fumaça. As ondulações respondem ao movimento e param quando o mouse fica parado. Ao sair da janela, a imagem fica oculta novamente.

## Organização do código

| Pasta / arquivo | Conteúdo |
| --- | --- |
| `index.html` | Tela inicial e menu. |
| `menu/` | Estilo e interações da abertura. |
| `sobre/` | Minha vida: HTML e CSS próprios. |
| `foco/` | Back-end e observabilidade: HTML, CSS e JavaScript próprios. |
| `alem-do-codigo/` | Música e boxe: HTML, CSS e JavaScript próprios. |
| `contato/` | Contatos: HTML, CSS e JavaScript próprios. |
| `exercicios/` | Área dos alunos, cartões, busca e páginas individuais dos três exercícios. |
| `apoio/` | Menu limpo e quatro páginas de fundamentos de lógica em C#. |
| `exercicios/atividades.js` | Lista de atividades que você irá cadastrar. |
| `exercicios/materiais/` | PDFs, imagens e outros materiais dos alunos. |
| `shared/css/base.css` | Estilos compartilhados e navegação fixa. |
| `shared/js/background.js` | Fundo preto e revelação com o mouse. |
| `shared/js/common.js` | Idade e contatos. |
| `assets/` | Sua foto, capa da música e mãos em constelação. |
| `config.js` | Contatos e identificação da música. |

## Exercícios

Os três enunciados enviados estão completos nas páginas:

- `exercicios/exercicio-1/index.html`: Conversor e Analisador de Temperatura — fácil.
- `exercicios/exercicio-2/index.html`: Loja de Produtos - Gerenciamento de Estoque — médio.
- `exercicios/exercicio-3/index.html`: Sistema de Reservas de Cinema — difícil.

Cada página apresenta o enunciado, o exemplo de interação e as dicas, sem soluções. Apenas os botões **Menu principal** e **Exercícios** aparecem no topo à esquerda. Os números 1, 2 e 3 ficam fixos no centro inferior e permitem trocar de exercício durante a leitura.

Na área inicial de exercícios, três cartões grandes ficam centralizados, com número, título, dificuldade e resumo sempre visíveis. O exercício fácil usa verde (#1ED760), o médio usa azul (#4285F4) e o difícil usa o vermelho do site (#FF354A). Apenas o cartão Exercícios do menu principal usa fundo vermelho e letras pretas. Clique ou toque em qualquer parte de um cartão para abrir o enunciado completo. No celular, os cartões ficam empilhados para facilitar a leitura.

## Editar ou adicionar exercícios

Os resumos, títulos, dificuldades e destinos ficam em `exercicios/atividades.js`. O campo `pagina` aponta para a página completa; as dificuldades aceitas são `"facil"`, `"medio"` e `"dificil"`.

Exemplo de cadastro:

```js
{
  titulo: "Título da atividade",
  disciplina: "Nome da disciplina",
  dificuldade: "facil",
  enunciado: "Resumo para o cartão.",
  pagina: "exercicio-4/index.html"
}
```

Para um novo exercício, crie uma pasta `exercicios/exercicio-4/`, copie o HTML de outro exercício e substitua o título e o conteúdo dentro de `exercise-content`. Adicione o cadastro em `atividades.js` e o link do novo número nos menus inferiores das páginas completas. A busca e o filtro de disciplina usam os cadastros em `atividades.js`; a numeração permanece estável ao filtrar.

Materiais complementares podem ficar em `exercicios/materiais/`. Este é um site estático: alterações são feitas nos arquivos e precisam ser reenviadas à hospedagem quando o site estiver online. Não há login, envio de respostas ou banco de dados.

## Fotos

A foto da abertura é a mesma foto de perfil fornecida anteriormente, em `assets/perfil.jpg`. Não há controles para trocar imagens na interface. Para trocar definitivamente, substitua os arquivos na pasta `assets`.

## Asset das mãos

Imagem criada com a ferramenta integrada de geração de imagens para este projeto. Prompt: duas mãos robóticas diagonais com indicadores encostando, somente contornos finos e pontos vermelhos de constelação, sem preenchimento e com transparência real. Arquivo final: `assets/maos-constelacao.png`.

## Apoio

Apoio aparece no menu principal e na navegação, sempre em azul (#4285F4). Os links para Exercícios usam vermelho (#FF354A) com letras pretas. No topo da listagem de exercícios, aparecem somente Menu e Apoio. O menu de Apoio mostra apenas Menu e Exercícios no topo e quatro cartões centrais em verde menta, azul, coral e lilás, sem busca, rodapé ou textos extras.

Os fundamentos estão em `apoio/variaveis/`, `apoio/condicoes/`, `apoio/switch-case/` e `apoio/lacos/`, cada um com `index.html`. Os exemplos são em C#, com variáveis explícitas, entrada e saída no console, validação e explicações passo a passo. Cada trecho é independente; não cole todos de uma vez no mesmo programa. Os cartões e artigos compartilham `apoio/style.css`.

Referências para consulta: Microsoft Learn, [tipos em C#](https://learn.microsoft.com/pt-br/dotnet/csharp/language-reference/builtin-types/built-in-types), [if e switch](https://learn.microsoft.com/pt-br/dotnet/csharp/language-reference/statements/selection-statements), [laços](https://learn.microsoft.com/pt-br/dotnet/csharp/language-reference/statements/iteration-statements).

Contato usa o verde das questões fáceis (#1ED760), com letras pretas, no cartão do menu principal e nos botões do topo. As quatro cores dos temas de Apoio ficam em `apoio/style.css` e não alteram o azul dos links para a área Apoio.

O botão Ouvir no Spotify, com a logo à esquerda, abre Rolê na City no Spotify. O destino pode ser editado em `config.js`, no campo `linkMusica`.

## Lista com paginação

Há 10 exercícios: quatro fáceis, três médios, dois difíceis (cinema e notas) e um muito difícil (banco). A listagem mostra no máximo seis cartões por página, em duas linhas de três colunas no computador. O filtro por dificuldade usa os nomes em português; os cartões exibem easy, medium, hard e very hard. Busca e filtro retornam à primeira página. Os números das atividades não mudam ao filtrar. Os enunciados ficam em `exercicios/exercicio-1/` até `exercicios/exercicio-10/`.


## Temas Light e Dark
Use Light / Dark no topo de qualquer página. Light usa fundo branco e mãos pretas; Dark mantém fundo preto e mãos vermelhas. A preferência permanece ao navegar e reabrir o site. Os cartões mantêm suas cores. As mãos continuam sendo reveladas perto do mouse ou do toque, e a água só se move quando o ponteiro se move.

As páginas individuais dos exercícios não possuem mais a barra inferior de números. Os exemplos em caixas usam fundo preto e texto branco nos dois temas.

Fórmulas, trechos de código e exemplos, em Exercícios e Apoio: preto com letras brancas no Light; branco com letras pretas no Dark.

## Catálogo de 30 exercícios
São 30 propostas em C#, com seis cartões por página (cinco páginas). Os cartões mostram dificuldade em inglês e todas as estruturas trabalhadas. Busca, dificuldade e tipo de exercício funcionam juntos; trocar um filtro volta à primeira página. Cada atividade tem requisitos, exemplo e orientação explícita das funções e condições de validação. Um exercício pode aparecer em vários tipos.

## Login com MySQL
Esta versão precisa do servidor C# para proteger Exercícios e Apoio. Siga LOGIN-E-HOSPEDAGEM.md. Não publique todas as pastas como arquivos estáticos no Netlify; a pasta deploy-netlify serve somente como proxy para o servidor já hospedado.
