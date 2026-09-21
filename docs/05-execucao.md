# Plano de execução e critérios de aceite

## 1. Resultado e decisões confirmadas

Construir um sistema para materiais consumíveis e itens emprestados, com local físico configurável, operação multiusuário e instalação em rede local ou na internet. Stack solicitada: C# Minimal API, PostgreSQL e React com shadcn/ui. Este planejamento especifica a entrega; ainda não há aplicação implementada.

O primeiro piloto usa uma organização com mais de um local e pelo menos dois operadores para validar concorrência e permissões. A separação por organização já existe no modelo, mesmo que a administração comercial de vários clientes venha depois.

Decisões de produto ainda a refinar: segmento e necessidade de lote/validade; volumes; servidor disponível; quem aprova ajustes; necessidade de comprovação de entrega; custo gerencial; perda tolerável de dados e tempo de recuperação. Não impedem construir a base, mas afetam escopo e critérios de produção.

## 2. Fases e dependências

| Fase | Entrega | Dependências | Evidência para encerrar |
| --- | --- | --- | --- |
| F0 — Contratos e piloto | Exemplos reais, escopo do piloto, wireframes, decisões de domínio e contrato inicial | Planejamento atual e amostra do processo | Fluxos de consumo, empréstimo, retorno e local físico compreendidos e registrados |
| F1 — Fundação | Solução .NET, React, banco, CI, login, organização, escopos, catálogo, locais e fotos | F0 | Usuários diferentes acessam apenas seus locais; cadastro acessível pela LAN/ambiente web de teste |
| F2 — Motor de estoque | Ledger, saldos, reservas, entrada, consumo, estorno, idempotência e auditoria | F1 | Disputa por saldo e retries passam em testes com PostgreSQL real |
| F3 — Operação completa | Requisição, aprovação, entrega parcial, seriais, empréstimos, retornos, trânsito, inventário e baixas | F2 | Jornada operacional completa com divergências e quantidades parciais |
| F4 — Liberação V1 | Importação/exportação, dashboard, avisos, backup/restore, instalação e piloto assistido | F1–F3, critérios de dados e recuperação | Aceite operacional, segurança, conciliação e restauração comprovados |
| F5 — Gestão avançada | Lotes/validade/FEFO, reposição, compras, custos, manutenção e relatórios avançados | V1 e prioridade validada pelo uso | Aceite específico de cada módulo sem regressão no motor |
| F6 — Escala e integrações | Hospedagem compartilhada, SSO, APIs externas, quotas, réplicas e isolamento adicional | Métricas e necessidade real | Carga, isolamento e operação atendem aos objetivos definidos |

F1 inicia a automação de backup e logs; F4 verifica recuperação e fecha a operação de produção. Alertas e importação podem ser desenvolvidos em paralelo após estabilizar seus contratos, mas só a V1 completa é declarada pronta para uso. Se o piloto exigir validade, F5-lotes é antecipado para F3 e seus testes tornam-se bloqueantes.

Não fechar prazo em calendário sem tamanho de equipe, disponibilidade e recorte do piloto. Ao concluir F0, estimar as histórias, identificar dependências e planejar ciclos curtos de entrega; revisar a previsão com velocidade observada. Etapas são marcos verificáveis, não promessa de prazo.

## 3. Orquestração das frentes

| Frente | Responsabilidade | Pode trabalhar em paralelo com |
| --- | --- | --- |
| Produto/domínio | Estados, exemplos, políticas e aceite com operadores | Contratos, UX e desenho de testes |
| Backend/dados | Casos de uso, invariantes, migrations, transações e autorização | Frontend após DTOs/erros acordados |
| Frontend/UX | Navegação, tabelas, formulários, acessibilidade e fluxos com scanner | Backend usando contrato/mock claramente identificado |
| Infraestrutura | Instalação, certificados, segredos, CI, backup e monitoramento | Todas as fases desde F1 |
| Qualidade | Casos de concorrência, permissão, recuperação e jornadas | Especificação e implementação de cada fluxo |

Uma pessoa responsável pelo domínio revisa qualquer alteração em saldo, reserva, movimento ou unidade. Uma pessoa coordena migrations para evitar mudanças incompatíveis. Frontend não inventa estados de documento que a API não suporta. Cada entrega inclui seu exemplo operacional, contrato, teste proporcional e documentação atualizada.

Usar branch/PR pequeno por fatia funcional, checks de build e integração. Não criar repositórios separados por módulo inicialmente. O mesmo item de backlog deve produzir comportamento utilizável de ponta a ponta, evitando meses de backend sem uma jornada demonstrável.

## 4. Backlog priorizado

| ID | Fase | Entrega concreta | Critério essencial |
| --- | --- | --- | --- |
| PLAN-01 | F0 | Dicionário e exemplos reais do piloto | Consumível, retornável, transferência e localização têm exemplos claros |
| PLAN-02 | F0 | Telas de entrada, estoque, entrega e retorno | Operador consegue explicar a sequência e localizar erros |
| BASE-01 | F1 | Backend/frontend/banco reproduzíveis | Instalação nova sobe com configuração documentada |
| IAM-01 | F1 | Login, logout, revogação, reset e MFA administrativo | Sessão revogada não confirma operação |
| IAM-02 | F1 | Papéis e permissões por organização/local | API, listas, totais e arquivos respeitam escopo |
| CAT-01 | F1 | Produto/SKU/unidade/códigos/foto | Código único e conversão preservada nos documentos |
| LOC-01 | F1 | Sala/armário/gaveta e outros tipos | Árvore sem ciclos, saldos agregados sem duplicação |
| OBS-01 | F1 | Logs, health checks e primeira rotina de backup | Falha detectável e dados persistentes entre reinícios |
| INV-01 | F2 | Posição, ledger e saldo transacional | Saldo concilia com histórico após sucesso e rollback |
| INV-02 | F2 | Idempotência e concorrência | Duplo envio não duplica; duas saídas não excedem saldo |
| OPS-01 | F2 | Recebimento e consumo | Entrada 10 e consumo 3 resultam em 7 com destinatário e ator |
| RES-01 | F2 | Alocar, usar, liberar e expirar reserva | Entrega concorrente com expiração não duplica efeito |
| AUD-01 | F2 | Auditoria e correção documentada | Histórico original permanece após estorno |
| REQ-01 | F3 | Requisição, aprovação e atendimento parcial | Cancelar restante não elimina entregas anteriores |
| SER-01 | F3 | Identificação individual de retornável | Um patrimônio não ocupa duas posições internas |
| LOAN-01 | F3 | Empréstimo, prazo e retorno parcial | Quantidade pendente e condição recebida ficam corretas |
| TRF-01 | F3 | Expedição/trânsito/recebimento | Receber 8 de 10 deixa 2 em trânsito |
| LOC-02 | F3 | Remanejamento entre armários/endereços | Origem e destino mudam juntos, preservando condição e permissões |
| RET-01 | F3 | Retorno de consumo e ao fornecedor | Vínculo com origem e limites de quantidade respeitados |
| CNT-01 | F3 | Inventário cego com bloqueio de endereço | Movimentos não atravessam o corte de contagem |
| ADJ-01 | F3 | Ajuste/baixa com motivo e aprovação | Alçada não é burlada pela API ou importação |
| DATA-01 | F4 | Importação com prévia, abertura e retomada | Erros por linha; custódias/trânsito anteriores representados; documentos não se repetem |
| DATA-02 | F4 | Exportação CSV/XLSX filtrada | Arquivo respeita acesso, códigos e snapshot |
| UX-01 | F4 | Pesquisa, filtros salvos e dashboard | Totais explicáveis e acesso aos registros de origem |
| NTF-01 | F4 | Central de avisos e worker | Aviso deduplicado, autorizado e resolvido pelo estado real |
| REC-01 | F4 | Restauração em ambiente isolado | Banco, arquivos, saldo, permissões e sessões tratados corretamente |
| REL-01 | F4 | Instalação e piloto assistido | Operadores completam tarefas e executam procedimento de contingência |

Desenvolver devolução de consumível não utilizado e ao fornecedor na V1 mesmo que tenham telas simples. Integração com contas a pagar, crédito de fornecedor ou documento fiscal não faz parte desses fluxos iniciais.

## 5. Testes de aceite que bloqueiam produção

| Caso | Cenário e resultado esperado |
| --- | --- |
| A01 — Conciliação | Entrada de 10, consumo de 3: físico 7, ledger equivalente e vínculo do destinatário |
| A02 — Concorrência | Duas saídas de 7 disputam saldo 10: exatamente uma confirma, saldo final 3 |
| A03 — Última unidade | Dois operadores disputam um item serializado: só uma entrega confirma |
| A04 — Reserva | Físico 10 e reservado 6 impedem saída independente de 5; entrega reservada atualiza ambos juntos |
| A05 — Expiração | Expiração e entrega simultâneas da mesma reserva deixam uma única resolução coerente |
| A06 — Retry | Conexão cai após commit; reenviar a mesma confirmação retorna o resultado sem novo movimento |
| A07 — Payload diferente | Mesma chave idempotente com outra quantidade gera conflito e não altera estoque |
| A08 — Atomicidade | Falha entre ledger e atualização do documento desfaz a transação inteira |
| A09 — Trânsito | Expedir 10 e receber 8 reduz origem em 10, aumenta destino em 8 e conserva trânsito 2 |
| A10 — Retorno | Emprestar 5 e receber 2 avariados deixa custódia 3 e avariados 2, sem disponível novo |
| A11 — Excesso de retorno | Devolução acima da pendência, inclusive concorrente, é recusada |
| A12 — Inventário | Começar contagem compete com uma saída em andamento: há corte inequívoco, sem movimento atravessado |
| A13 — Isolamento | Outra organização/local não aparece por ID direto, busca, totais, anexos, jobs ou exportação |
| A14 — Revogação | Remover acesso impede confirmar operação e baixar exportação previamente solicitada |
| A15 — Correção | Estornar preserva original; entrada consumida não é anulada com saldo negativo |
| A16 — Importação | Zeros à esquerda, decimal local, duplicação, linha inválida e retomada têm resultados explicáveis |
| A17 — Backup | Restaurar em ambiente separado recupera dados e bytes referenciados, com conciliação e tempo medidos |
| A18 — Segurança web | Escrita sem proteção CSRF/sessão ou fora da permissão não produz efeito; upload restrito não fica público |
| A19 — Local físico | Agregar Sala → Armário → Gaveta conta cada posição uma vez; reparentear não burla escopo |
| A20 — Falha de worker | Confirmação de estoque funciona com worker parado; após retomada, outbox é processada sem duplicar negócio |
| A21 — Ambientes | Instalação LAN funciona sem internet; implantação web oferece a mesma regra e acesso pelo navegador |
| A22 — Vencimento, se ativo | Lote vencido não é entregue mesmo com worker parado e reserva antiga |
| A23 — Remanejamento | Mover 3 itens da gaveta A ao armário B conserva total e não cria pendência de trânsito se a confirmação é conjunta |
| A24 — Contagens sobrepostas | Segunda contagem não toma o mesmo endereço e não pode liberar bloqueio da primeira |
| A25 — Rastreio consistente | Série/lote de outro SKU não pode ser associado à posição, mesmo na mesma organização |
| A26 — Migração existente | Abrir custódia pendente de 3 permite devolvê-los sem uma saída fictícia anterior do depósito |

Testes de integração usam PostgreSQL real, pois armazenamento em memória não reproduz bloqueios, unicidades e transações. Testes unitários cobrem cálculo de pendência, elegibilidade, conversão e máquina de estados. Testes de navegador cobrem as jornadas e a comunicação de falhas; não substituir testes concorrentes da API por cliques sequenciais.

## 6. Qualidade e capacidade

Objetivos iniciais propostos, a calibrar com dados do piloto: consultas paginadas e confirmações comuns com p95 abaixo de 1 segundo, excluindo arquivos e jobs; nenhuma perda de integridade sob concorrência. Medir experiência do navegador separadamente da duração do backend, declarando latência de rede e hardware.

Massa inicial sugerida para laboratório: 10 mil SKUs, 1 milhão de linhas de ledger e 50 sessões concorrentes com mistura declarada de leitura/escrita. São parâmetros de teste, não uma capacidade prometida. Incluir contenção deliberada sobre um mesmo SKU e carga distribuída por locais; registrar throughput, erros, tempo de lock e tamanho das filas.

Limites de consulta/exportação, paginação, cancelamento e uso de memória devem impedir que um relatório paralise operações. Revisar acessibilidade, teclado e scanner nos dispositivos usados pelo almoxarifado. Antes da internet pública, validar configuração de proxy/TLS, upload, sessão, rate limit e isolamento de recursos.

Definição de concluído por funcionalidade: comportamento utilizável, regra exercitada, autorização aplicada, erro compreensível, evento auditável, migration quando necessária e verificação proporcional ao risco. Documentação de API e tipos gerados ficam sincronizados no build.

## 7. Entrada em produção

Preparar cadastros e mapa de locais; revisar acessos; importar dados em ambiente de homologação; corrigir inconsistências; fazer contagem de abertura e registrar seu instante de corte. O inventário antigo e o novo sistema não podem registrar o mesmo estoque de forma independente durante a virada.

No corte: suspender lançamentos na fonte antiga, registrar pendências de empréstimo/trânsito, efetivar abertura conciliada e resolver a migração dessas responsabilidades com documentos explícitos. Não importar só o que está nas prateleiras e esquecer itens com pessoas. Para saldos migrados de custódia/trânsito, usar fluxo de migração dedicado com documentos de origem, sem simular expedições a partir de saldo que não existe.

Fazer piloto com operadores reais e situações de entrada, entrega, empréstimo, retorno e transferência. Conciliar saldos diariamente no início. Registrar erros e dúvidas, corrigir antes da expansão e manter referência da base anterior para investigação.

O marco de produção exige: testes A01–A21 e A23–A26 aprovados, A22 quando aplicável, backup restaurado com verificação das operações físicas posteriores ao ponto recuperado, responsáveis operacionais definidos, instruções de contingência e instalação reproduzível. A continuidade após a primeira versão prioriza problemas observados e métricas do uso.

## 8. Próxima fatia de implementação

Começar por BASE-01, IAM-01/IAM-02, CAT-01 e LOC-01, com uma fatia mínima de INV-01 e OPS-01 para provar o ciclo completo: login → cadastro → entrada → saldo → saída → extrato. Em paralelo, implementar backup de desenvolvimento, logs e o primeiro teste de duas retiradas concorrentes. Assim, o primeiro incremento demonstra a base técnica e a operação que os módulos seguintes reutilizarão.
