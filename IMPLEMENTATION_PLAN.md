# Implementação da landing page

## Inspeção

O frontend existente usa Vite, React 19, TypeScript, Tailwind 4, React Router, Lucide e componentes shadcn/ui. Antes da mudança, `/` mostrava o login para visitantes sem sessão e não havia página pública. O backend mantém autenticação por cookie e proteção CSRF. Os fluxos operacionais ficam preservados.

## Arquitetura entregue

- `/` renderiza a landing pública sem depender da disponibilidade da API.
- `/login` renderiza o acesso e o provisionamento já existentes. Rotas internas sem sessão redirecionam para `/login`.
- `src/frontend/src/pages/landing-page.tsx` contém a página e a prévia com dados fictícios locais.
- `src/frontend/src/styles/landing.css` contém tokens visuais escopados, composição e breakpoints; não altera as telas de estoque.
- Fontes self-hosted via Fontsource. Nenhuma biblioteca de animação ou de gráficos foi adicionada.

## Ordem de implementação

1. Inspecionar o produto, a stack e os limites reais do incremento.
2. Definir copy, direção visual, tokens e elemento central.
3. Criar rota pública e preservar login e aplicação autenticada.
4. Implementar seções e prévia interativa com filtro, seleção e estado vazio.
5. Validar build, responsividade, navegação, interação, acessibilidade básica e dependências.

## Estratégia de validação

Compilar com `npm run build`, rodar `npm test` e `npm audit` em `src/frontend`. Inspecionar a página no navegador em 375, 390, 768, 1024, 1280, 1440, 1920 e 2560 px. Conferir foco, menu móvel, âncoras, filtro, seleção de local, estado vazio e `prefers-reduced-motion`.

## Limite da prévia

A interação demonstrativa não grava dados, não lê estoque real e continua funcional mesmo sem o backend iniciado. O login depende da API; os testes de autenticação e provisionamento exigem o ambiente completo do projeto.
