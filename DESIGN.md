# StorageManager: direção da landing page

## Missão e público

Mostrar, de forma concreta, como uma equipe encontra material, confere saldo e registra movimentações. A página se dirige a responsáveis pelo estoque e a pessoas que operam ou consultam o sistema. O tom é direto, calmo e confiável.

O elemento central é uma prévia interativa de local e saldo. Ela usa dados fictícios, identificados na própria interface, e não consulta a API.

## Sistema visual

- Tema claro único. Superfícies em azul frio, sem inversão de tema entre seções.
- Acento único: azul `#3156d9`. Verde `#23624e` e âmbar `#9d5515` indicam estado com símbolo e texto.
- Fundo `#f6f8fb`, papel `#ffffff`, texto `#17243d`, texto secundário `#54647d`, metadados `#61718a`, linha `#dce3ed`.
- Contraste calculado: texto principal sobre fundo 14,55:1 e papel 15,48:1; secundário 5,65:1 e 6,01:1; metadados 4,66:1 e 4,96:1; branco sobre CTA 6,06:1; azul sobre seleção clara 5,24:1.
- Archivo Variable para títulos e corpo; JetBrains Mono Variable para dados e metadados. Ambas as fontes são servidas pelo próprio frontend.
- Títulos fluídos de 34 a 75 px, corpo entre 15 e 19 px, medidas e códigos em algarismos tabulares.
- Espaçamento base de 4 px. Raio 8 px para botões e controles, 14 px para painéis e 22 px para a moldura da prévia.
- Sombra azul discreta apenas para a janela de produto e superfícies elevadas. O resto usa bordas e espaçamento.

## Componentes e estados

- CTA principal azul com texto branco, altura mínima de 50 px na landing. Links secundários mantêm área clicável de ao menos 44 px.
- A navegação móvel abre abaixo do cabeçalho, informa `aria-expanded` e fecha com Escape ou após escolher um destino.
- Na prévia, o local e o filtro são controles de estado com `aria-pressed`. O painel de detalhes anuncia mudanças. Há estado vazio com ação para limpar filtro.
- Estados de estoque usam forma e texto: `● Regular` e `▲ Baixo`.
- O login e os fluxos internos continuam usando o sistema visual existente para evitar alterações inesperadas na operação.

## Responsividade e motion

- Hero em duas colunas no desktop; texto antes da prévia no celular. Lista de locais da prévia vira uma grade de três opções no celular.
- Grade de capacidades assimétrica no desktop e linear no celular. Fluxo operacional permanece uma lista vertical.
- A única entrada animada é a janela da prévia, em 760 ms; hover e pressão de botões duram 120 a 180 ms. Todas as transições são removidas com `prefers-reduced-motion`.
- Não há animação de rolagem, loops ou 3D. A prévia já é uma representação funcional do produto e não precisa de fallback WebGL.

## Conteúdo e limites

- Copys descrevem somente o primeiro incremento: locais hierárquicos, catálogo, entrada, consumo, saldo, histórico e três perfis.
- Não anunciar empréstimos, devoluções, QR code, alertas externos, importação ou escopos de permissão por local, que estão no roteiro futuro.
- Não usar métricas de desempenho, depoimentos ou clientes fictícios.

## Revisão final

- Um `h1`; links de âncora válidos; foco visível; skip link; alvos de toque; dados da demonstração identificados; ausência de overflow horizontal; headline em no máximo três linhas; CTA visível no primeiro quadro desktop; redução de movimento; build e auditoria de dependências sem erro.
