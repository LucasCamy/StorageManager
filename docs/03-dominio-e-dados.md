# Modelo de estoque e dados

## 1. Vocabulário e quantidades

O catálogo descreve o material; o documento descreve a operação; o movimento registra um fato efetivado; o saldo permite consultar a posição atual rapidamente. Auditoria administrativa registra alterações e acessos relevantes, enquanto o extrato de estoque registra variações de quantidade. São históricos complementares.

Para uma posição armazenada, liberada e elegível:

```text
disponível = físico da posição elegível - reservado ativo nessa posição
```

Não subtrair quarentena ou trânsito novamente: eles já estão fora desse físico elegível. O saldo no depósito inclui posições armazenadas de todas as condições, apresentadas separadamente. Quantidade própria sob controle inclui depósito, trânsito e custódia. Quantidade consumida, devolvida ao fornecedor ou baixada não integra esse total.

Pedido de compra previsto não é físico. Solicitação aprovada sem estoque não é reserva. Reserva reduz disponibilidade, mas não reduz físico. Empréstimo reduz o disponível no depósito, mas não reduz o total próprio sob controle.

## 2. Contas de estoque e posições

Usar contas de estoque como agrupadores internos do motor, sem expor terminologia contábil ao operador:

| Tipo de conta | Vínculo | Significado |
| --- | --- | --- |
| Warehouse | Endereço armazenável | Material fisicamente guardado em sala, armário, gaveta, depósito etc. |
| Transit | Documento de transferência | Material expedido e ainda não resolvido no destino |
| Custody | Linha de empréstimo/cautela | Material entregue sob responsabilidade, ainda pertencente à organização |
| External | Origem/destino e motivo | Contrapartida de fornecedor, consumo, perda, descarte ou saldo de abertura |

Cada conta possui organização e exatamente o vínculo correspondente ao tipo. Posições são identificadas por organização + conta + SKU + lote opcional + série opcional + condição. Uma mudança de condição transfere quantidade entre posições. Condições armazenadas: liberado, inspeção, avariado e manutenção. Vencimento é uma restrição temporal calculada do lote, mesmo que uma tarefa de bloqueio ainda não tenha executado.

Contas externas encerram ou iniciam a responsabilidade da organização; suas linhas existem no histórico, mas não têm disponibilidade e não recebem reservas. `StockBalance` materializa saldos das contas internas. Todo movimento tem contrapartida externa ou interna identificada, evitando entradas e saídas sem causa.

Custódia e localização física são dimensões diferentes: um notebook sob responsabilidade de Ana pode ter como localização informada “Sala 04”. A localização informada fica no vínculo de custódia/ativo; não cria uma segunda posição Warehouse. Quando houver conferência física, registrar quem verificou e quando. Material emprestado por terceiros/consignado fica fora do primeiro escopo; suportá-lo exige acrescentar proprietário e regras próprias.

## 3. Entidades propostas

Os nomes são orientativos para implementação, não um schema já aplicado. Todo relacionamento entre entidades de negócio verifica a organização.

| Grupo | Entidades | Campos e vínculos essenciais |
| --- | --- | --- |
| Organização | Organization, OrganizationalUnit | Nome, fuso, moeda de referência, configurações e unidade/filial |
| Identidade | User, UserSession | Credenciais pelo Identity, sessão ativa, expiração e revogação |
| Acesso | Membership, Role, Permission, RolePermission, MembershipRole, ScopeGrant | Usuário-organização, papéis e raízes/locais permitidos |
| Localização | StockSite, Location, LocationType | Raiz operacional, pai, nome, tipo cadastrável, código, armazenável e ativo |
| Catálogo | Product, Sku, Category, UnitOfMeasure, SkuUnitConversion, Barcode | Variantes, unidade-base, precisão, comportamento e rastreio |
| Políticas | StockPolicy | SKU + raiz operacional, mínimo, reposição, alvo, prazo e endereço preferencial |
| Rastreio | Lot, SerializedItem | SKU, lote, validade, série/patrimônio e posição interna atual quando existente |
| Destinatários | Party, Department, CostCenter, Project | Pessoa/empresa, fornecedor/destinatário, área e uso do material |
| Motor | InventoryAccount, StockPosition, StockBalance | Dimensões de posição, físico, reservado e versão |
| Histórico | StockMovement, StockEntry | Tipo, documento, ator, instantes, motivo e linhas com variação em unidade-base |
| Reserva | Reservation, ReservationAllocation | Documento demandante, prazo, posição alocada, quantidade ativa/consumida/liberada |
| Entrada | Receipt, ReceiptLine | Fornecedor, referência, SKU, quantidade, custo informado e endereço/condição |
| Distribuição | MaterialRequest, RequestLine, Issue, IssueLine | Solicitante, aprovação, reservas, entregas e consumo efetivo |
| Empréstimo | Loan, LoanLine, LoanIssue, LoanReturn, LoanReturnLine | Responsável, vencimento, quantidade entregue/pendente, série e condição recebida |
| Transferência | Transfer, TransferLine, Dispatch, DispatchLine, TransferReceipt, TransferReceiptLine | Origem/destino, expedições, recebimentos e pendências por linha |
| Remanejamento | Relocation, RelocationLine | Endereços da mesma raiz, condição mantida e confirmação conjunta |
| Retornos externos | SupplierReturn, SupplierReturnLine, ConsumptionReturn | Devolução ao fornecedor ou retorno de material não utilizado, ligados à origem |
| Inventário | StockCount, CountScope, CountLine, CountObservation, StockAdjustment | Locais bloqueados, corte, observações, recontagem, aprovação e diferença aplicada |
| Aprovações | ApprovalDecision | Documento/versão, aprovador, decisão, motivo e horário |
| Arquivos | StoredFile, Attachment | Chave privada, hash, tamanho, tipo, estado de validação e vínculo autorizado |
| Intercâmbio | ImportJob, ImportRowResult, ExportJob | Arquivo, mapeamento, filtros, versão, progresso e resultado |
| Avisos | AlertRule, AlertInstance, Notification, NotificationDelivery | Regra, deduplicação, destinatários, estado e tentativas |
| Infraestrutura | AuditEvent, OutboxMessage, JobLease, IdempotencyRecord | Rastreabilidade, processamento recuperável e deduplicação |

Product/Sku simplificam-se em uma tela quando não há variantes. Lot e os fluxos avançados só ganham interface ao entrar no escopo. A lista representa o domínio planejado; criar tabelas e abstrações apenas conforme as fases de entrega, preservando as fronteiras definidas.

Cada documento tem identificador técnico, número legível único na organização/tipo, estado, versão e datas. Lacunas na numeração são aceitáveis; não prometer sequência fiscal. Linhas preservam descrição, unidade e conversão aplicadas, impedindo que editar o catálogo reescreva documentos antigos.

## 4. Movimento e rastreabilidade

`StockMovement`: organização, tipo, documento de origem, chave do efeito, motivo, operador, instante de registro, data operacional e vínculo de reversão quando aplicável.

`StockEntry`: movimento, posição, quantidade-base com sinal, linha de documento e valores de custo quando o módulo de valorização estiver ativo. Cada par transfere a mesma quantidade do mesmo SKU/lote/série; condição e conta podem mudar. A soma das linhas por SKU dentro do movimento é zero incluindo contrapartidas externas. Conversões ocorrem antes, para unidade-base.

| Evento | Origem | Destino |
| --- | --- | --- |
| Recebimento | Fornecedor externo | Endereço liberado ou inspeção |
| Saldo inicial | Contrapartida de abertura | Endereço informado; na migração autorizada, custódia ou trânsito documentados |
| Consumo | Endereço elegível | Destinatário/finalidade externa de consumo |
| Empréstimo | Endereço elegível | Custódia da linha de empréstimo |
| Retorno de empréstimo | Custódia original | Endereço/condição de recebimento |
| Expedição | Origem | Trânsito da transferência |
| Remanejamento imediato | Endereço de origem | Endereço de destino na mesma raiz, mantendo rastreio e condição |
| Recebimento de transferência | Trânsito | Destino/condição recebida |
| Reclassificação | Posição liberada | Posição avariada/inspeção/manutenção, ou inverso autorizado |
| Perda/baixa | Posição interna | Contrapartida externa com causa |
| Ajuste | Posição interna ou contrapartida de ajuste | Contrapartida de ajuste ou posição interna |

Não permitir edição ou exclusão de `StockMovement` e `StockEntry` efetivados por operações da aplicação. O papel runtime pode inserir e consultar esses fatos, mas não alterá-los. Isso protege contra edição comum; não significa que um administrador irrestrito do banco seja incapaz de modificar dados. Evidência externa adicional pode ser prevista se o negócio exigir.

Estorno é novo movimento autorizado, com vínculo e justificativa, sujeito às mesmas restrições de saldo e aos efeitos posteriores. Entrada já consumida não pode ser estornada produzindo estoque negativo. Retornar fisicamente um empréstimo ou uma transferência usa o fluxo correspondente, preservando também as pendências do documento.

## 5. Invariantes e restrições

1. Nenhum saldo interno negativo. Nas posições elegíveis, `0 <= reservado <= físico`.
2. Alteração de físico sempre acompanha lançamento; alteração de reserva acompanha evento/estado de alocação.
3. Quantidades usam `numeric(20,6)` no banco e `decimal` em C#, com limite e escala de cada SKU. Não usar float/double para quantidade ou custo.
4. Unidade serializada tem quantidade inteira 0 ou 1 em uma posição interna e não pode estar ativa em duas posições. A linha de SerializedItem é bloqueada e sua posição atual é atualizada junto do movimento. Lote e série devem pertencer ao SKU da posição; se a série tiver lote, ele deve coincidir. A posição atual do SerializedItem só referencia uma posição da própria série, com FKs compostas e validação transacional apropriadas.
5. Índice único da posição trata lote/série nulos como dimensões iguais, evitando duplicação de saldos “sem lote”. Unicidade e FKs incluem organização.
6. Código de SKU é único por organização; código de barras identifica SKU/unidade de forma não ambígua. Série é única por organização/SKU, e código patrimonial por organização quando informado.
7. Documento ou ação já efetivada não pode ser repetida. Hash do payload e chave de idempotência não substituem a unicidade permanente do efeito de negócio.
8. Uma transferência não cria quantidade. Pendência em trânsito é expedido menos recebido, retornado à origem e baixado, nunca negativa.
9. Pendência de empréstimo é entregue menos retornado fisicamente e baixado, nunca negativa. Inspeção de retorno não desconta a pendência outra vez.
10. Retorno de consumo não supera o que foi entregue menos retornos anteriores. Sem origem comprovável, usar procedimento excepcional autorizado, com justificativa explícita.
11. Reserva só se aloca em posição Warehouse elegível; solicitações sem saldo aguardam atendimento. Expedição/entrega reduz físico e reserva consumida na mesma transação.
12. Expiração de reserva disputa o mesmo lock da entrega. A validade é reavaliada ao confirmar, mesmo se o worker estiver atrasado.
13. Quarentena, bloqueio de qualidade ou vencimento impedem distribuição, empréstimo e reserva para uso. Remanejamento segregado, devolução ao fornecedor e baixa autorizada continuam possíveis, preservando a condição e o motivo. Uma reclassificação explícita precisa realocar ou liberar reservas afetadas na mesma transação e avisar os responsáveis. Ao vencer naturalmente, o lote deixa de ser elegível por consulta/regra; o worker regulariza reservas e avisos, sem ser a única proteção.
14. Inventário bloqueia entrada, saída, reserva nova e importação que afetem seus endereços, salvo o ajuste aplicado pela própria contagem. O bloqueio identifica sua contagem proprietária e rejeita sobreposição; só ela o libera. Finalização resolve reservas acima da quantidade encontrada antes do ajuste.
15. Movimento retrodatado registra também o instante real da inclusão. Na V1, saldo corrente segue a ordem de efetivação; não prometer reconstrução retroativa de custo médio ou fechamento sem módulo específico.
16. A árvore de locais não admite ciclos nem pai de outra organização/raiz. Escopos e agregações não contam duas vezes o mesmo estoque.

Checks, índices e FKs implementam as garantias locais. Regras entre várias linhas, como soma do movimento e exclusividade serial, exigem o serviço transacional e verificações no banco apropriadas; não presumir que um CHECK simples pode consultar toda a tabela. Reconciliação periódica detecta discrepâncias e alerta, sem “corrigir” saldos silenciosamente.

## 6. Exemplos de aceite numérico

| Situação | Resultado esperado |
| --- | --- |
| Sala A recebe 10, reserva 6 | Físico 10, reservado 6, disponível 4 |
| Terceiro tenta retirar 5 dessas 10 | Recusa; reserva existente continua 6 |
| Entregar 4 usando a reserva de 6 | Físico 6, reservado 2, disponível 4 |
| Transferir 10 e receber 8 | Origem -10, destino +8, trânsito 2 |
| Emprestar 5 e receber 2 avariados | Custódia 3, posição avariada +2, disponível não aumenta |
| Duas pessoas retiram 7 de um saldo 10 | Uma retirada confirma; outra é recusada após revalidação |
| Uma requisição confirmada é reenviada | Mesmo documento/resultado; nenhum segundo lançamento |

## 7. Consulta, índices e reconciliação

Começar com índices por organização/código, organização/SKU, posição única, documento/número, extrato por organização/SKU/instante/ID e empréstimo por responsável/prazo. Planejar índice por localização e fila por estado/próxima tentativa. Medir planos antes de introduzir índices adicionais ou particionamento.

Busca exata de SKU/código usa índice adequado; busca descritiva pode evoluir com recursos textuais do PostgreSQL. Saldo vem de StockBalance no primário. Histórico vem dos lançamentos, com paginação por cursor. Caminho de localização exibido em documento preserva o contexto da época; consultas de saldo usam a árvore atual.

Reconciliação: recomputar físico por posição a partir do ledger, comparar reservas ativas com o agregado reservado, conferir seriais e validar pendências de documentos. Diferença gera incidente e bloqueio seletivo se necessário, sem sobrescrever dados automaticamente. Reconstrução administrativa de projeções deve ter procedimento próprio, concorrência controlada e auditoria.

## 8. Valorização gerencial futura

Guardar custo informado no recebimento desde a V1, com moeda, origem e permissão específica. Custo desconhecido fica identificado, não é transformado em zero. O painel não apresenta valor total confiável antes de definir e implementar a política.

Proposta para etapa posterior: custo médio móvel por organização/SKU, com custo de saída preservado na linha. Transferências internas e empréstimos conservam valor; retorno rastreável recompõe pelo custo vinculado à saída original. Definir tratamento de frete, arredondamento, perdas, retorno a fornecedor, retroatividade e fechamento antes da liberação. FEFO de lotes para separação física não determina o método de valorização.
