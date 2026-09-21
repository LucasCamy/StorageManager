# Contrato do primeiro incremento

Escopo implementado nesta etapa: provisionamento de organização/admin, sessão, usuários básicos, locais hierárquicos, SKUs, entrada/consumo, saldo, dashboard e extrato. Reservas, empréstimos, imagens, importação e recuperação automatizada permanecem no roadmap. Retornáveis podem ser cadastrados/recebidos, mas não consumidos: saída por empréstimo virá na próxima etapa.

Origem única via proxy Vite no desenvolvimento. API em `http://localhost:5080`, frontend em `http://localhost:5178`, PostgreSQL em `localhost:55432`. Essas portas evitam serviços já existentes na máquina. Prefixo `/api/v1`. JSON camelCase. Identificadores GUID como string; quantidade como string decimal invariante. Erros ProblemDetails com `status`, `title`, `detail`, `code` e, quando necessário, `errors`.

## Sessão e instalação

- `GET /setup/status` → `{ required: boolean }`.
- `GET /auth/csrf` → `{ token: string }` e cookie antiforgery. Enviar `X-CSRF-TOKEN` em todo POST autenticado e em login/setup; renovar depois do login.
- `POST /setup` → body `{ organizationName, name, email, password, setupToken }`; cria uma organização/admin uma única vez; responde `201`. Token vem de `Setup__Token` no ambiente, obrigatório, nunca exposto pelo status. Setup não autentica automaticamente; frontend faz login depois.
- `POST /auth/login` → `{ email, password }`; responde usuário e cookie de sessão.
- `GET /me` → `{ id, name, email, role, organizationId, organizationName }`.
- `POST /auth/logout` → `204`.
- Roles iniciais: `Admin`, `Operator`, `Viewer`. Admin gerencia tudo; Operator lê e movimenta, mas não cria usuário; Viewer apenas lê. Este incremento tem escopo organizacional, ainda sem grants por local. Não expor como multiempresa SaaS pronto.
- `GET /users` → array `{ id, name, email, role, isActive }`, admin somente.
- `POST /users` → `{ name, email, password, role }`, admin somente, `201`.
- `PATCH /users/{id}/status` → `{ isActive: boolean }`, admin somente; impedir auto-desativação/remoção do último administrador. Revalidar sessão ativa/usuário em cada request.

## Cadastros

- `GET /locations` → array `{ id, name, code, type, parentId, path, canStore }`.
- `POST /locations` → `{ name, code, type, parentId: string|null, canStore: boolean }`; `201` retorna o recurso. Todos os nós pertencem à organização; não há alteração de árvore neste incremento.
- `GET /products?search=&page=1&pageSize=25` → `{ items, total, page, pageSize }`.
- Produto neste contrato representa um SKU simples: `{ id, code, name, category, unit, kind, minimumStock, quantityScale, isActive }`.
- `kind`: `Consumable` ou `Returnable`; `minimumStock` string; `quantityScale` inteiro 0–6.
- `POST /products` → `{ code, name, category, unit, kind, minimumStock, quantityScale }`; `201` retorna produto. Código único por organização.

## Estoque e movimentações

- `GET /stock?search=&locationId=&lowStockOnly=false&page=1&pageSize=25` → `{ items, total, page, pageSize }`.
- Linha de saldo: `{ productId, productCode, productName, category, unit, kind, locationId, locationPath, quantity, reserved, available, minimumStock, status }`. Quantidades strings; `status` = `Healthy`, `Low`, `Empty`. Mínimo é referência do SKU na etapa inicial, a evoluir para política por local.
- `POST /movements` com `Idempotency-Key`: `{ type, productId, locationId, quantity, reference, notes, recipient }`.
- `type` = `Receipt` ou `Consumption`; `quantity` positiva em string. `recipient` é obrigatório no consumo, opcional na entrada. Somente locais `canStore`. Produto retornável rejeita `Consumption`.
- Resposta `201` ou resultado repetido `200`: `{ id, number, type, productId, productCode, productName, locationId, locationPath, quantity, unit, reference, notes, recipient, actorName, createdAt }`.
- `GET /movements?search=&type=&page=1&pageSize=25` → `{ items, total, page, pageSize }`, ordem mais recente primeiro.
- `GET /dashboard` → `{ productCount, locationCount, stockedPositionCount, lowStockCount, todayMovementCount, recentMovements, lowStockItems }`. RecentMovements até 6 e lowStockItems até 5, nos formatos acima. Nunca somar unidades incompatíveis como KPI.

Toda mutação exige autenticação e proteção CSRF, salvo setup/login que validam antiforgery e o mecanismo específico. Cadastros permitidos a Admin/Operator; escrita de usuários apenas Admin; Viewer não escreve. Paginação limitada a 100 itens. Estoque inicial zero surge quando existe posição de saldo; SKU sem posição aparece no catálogo, sem inventar sua localização.

## Integridade e limites do incremento

Saldo e ledger confirmam na mesma transação PostgreSQL. Criar saldo ausente com chave única e proteção concorrente; bloquear posição ao retirar. Persistir chave/hash/resultado da movimentação por organização/ator. Mesmo envio repete resultado; chave com payload diferente dá `409`. Histórico é append-only na aplicação. Isolamento de organização em todas as consultas e relações relevantes. Cookies Secure em produção; HTTP permitido apenas no desenvolvimento loopback.

Este contrato especifica o primeiro incremento e não declara todas as fases F1/F2 concluídas. Documentar explicitamente as funcionalidades posteriores e as decisões temporárias. Criar migrations EF; executar por comando `--migrate`, não implicitamente em todo startup de produção. `GET /health` é público e verifica dependências sem revelar segredos.
