# Produto e operação

## 1. Objetivo e premissas

Responder com confiança: o que existe, quanto está disponível, onde está, com quem está, o que precisa retornar, o que está acabando e quem alterou cada informação.

Confirmado: estoque misto, locais de armazenamento cadastráveis e funcionamento em rede local ou pela internet. Premissas restantes: uma organização no primeiro piloto, vários usuários e locais; interface em português do Brasil. Uma instalação poderá atender outras organizações no futuro, mas os dados já nascerão separados por organização. Pessoa que recebe material não precisa necessariamente ter login.

“Local” no produto significa localização física do material. A implantação pode ser na empresa ou na nuvem. Sem internet, uma instalação na empresa mantém a operação principal pela LAN; uma instalação exclusivamente na nuvem depende da conectividade. Sem acesso ao servidor, o navegador não confirma movimentações.

## 2. Estrutura física e catálogo

Hierarquia flexível de locais, com nome, tipo e local pai cadastráveis. Exemplos: organização → escritório → sala de TI → armário 02 → gaveta 03; ou organização → filial → depósito → corredor → estante → prateleira. Não exigir todos esses níveis. “Depósito” nos demais documentos significa a raiz operacional que agrupa um estoque e suas permissões; ela pode se chamar “Escritório” ou “Almoxarifado”. A raiz pode armazenar diretamente se não tiver subdivisões. Cada endereço armazenável tem código único nessa raiz, status e QR/código de barras.

Nós usados apenas para agrupar não guardam saldo próprio. Pais mostram o total dos descendentes sem duplicar quantidades. Depois de ter estoque, transformar um endereço em agrupador exige redistribuir seu saldo. Na V1, reparentear só é permitido dentro da mesma raiz, validando permissões, ciclos, documentos e contagens abertas. Entre raízes, criar endereços de destino e transferir o estoque; não alterar a raiz de endereços já usados para mudar implicitamente o escopo do histórico.

Separar produto conceitual de SKU: camiseta é um produto; camiseta azul tamanho M é um SKU. Toda movimentação ocorre sobre um SKU. Na interface, um produto sem variantes pode mostrar uma única ficha, sem complexidade adicional.

| Cadastro | Informações úteis |
| --- | --- |
| Produto/SKU | Código interno, descrição, categoria, marca, variantes, unidade-base, códigos de barras, fotos, status e observações |
| Política do SKU | Consumível ou retornável; rastreio simples, série, lote ou lote+série; precisão de quantidade; exigência de inspeção no retorno |
| Unidade de medida | Unidade-base e conversões por SKU, como caixa de 12 unidades; conversão registrada no documento |
| Política por depósito | Estoque mínimo, ponto de reposição, estoque-alvo, prazo estimado e localização preferencial |
| Pessoas e destinos | Pessoa, setor, centro de custo, projeto, cliente ou unidade destinatária; vínculo com usuário é opcional |
| Fornecedores | Identificação, contatos, produtos fornecidos e referências de compra |
| Ativo individual | Número de série, patrimônio, condição, responsável, localização, garantia e histórico |

Unidade-base e modo de rastreio tornam-se protegidos após a primeira movimentação. Mudanças exigem migração explícita ou criação de novo SKU. Arquivar produtos, pessoas e endereços preserva o histórico; endereços com saldo ou operação aberta não podem ser desativados sem resolução.

## 3. Módulos e escopo

V1 significa primeira versão liberável para uso real, após todas as fases obrigatórias do piloto. Não corresponde ao primeiro protótipo.

| Módulo | V1 operacional | Evolução |
| --- | --- | --- |
| Acesso | Login, usuários, perfis, escopo por depósito, sessões, recuperação e auditoria | SSO corporativo, administração de vários clientes |
| Catálogo | SKU, categorias, unidades, códigos, fotos e seriais opcionais | Kits, atributos específicos por segmento e catálogos externos |
| Locais | Filiais, depósitos e endereços hierárquicos | Capacidade, ocupação e regras de armazenagem |
| Entradas | Recebimento, fornecedor, referência externa, conferência e saldo inicial | Ordem de compra, recebimento contra pedido, reposição assistida |
| Distribuição | Requisição, aprovação simples, reserva, separação, entrega parcial e comprovante | Alçadas por valor, múltiplos aprovadores e planejamento de atendimento |
| Retornáveis | Empréstimo, responsável, vencimento, devolução parcial, inspeção e perda | Manutenção, garantia, agenda e troca de responsável com aceite |
| Transferências | Remanejamento entre endereços, expedição, trânsito, recebimento parcial e divergências | Rotas, volumes, transportadora e integração logística |
| Inventário | Contagem cega por endereço, divergência, aprovação e ajuste | Inventário cíclico por classificação e contagem com movimentação concorrente |
| Consultas | Pesquisa, filtros, saldo por local, extrato e histórico do item | Relatórios agendados e análises de consumo/reposição |
| Arquivos | Fotos privadas e anexos de recebimento/entrega/devolução | Documentos adicionais e integrações documentais |
| Dados | Importação CSV de cadastros/saldo inicial; exportação CSV/XLSX de consultas | Importação XLSX avançada e integrações por API |
| Avisos | Falta/baixo estoque, atraso de devolução, aprovação pendente e falha operacional | Validade, resumos por e-mail e webhooks |
| Operação | Backup automático, restauração ensaiada, monitoramento e atualização documentada | PITR com menor perda tolerada e alta disponibilidade |
| Lotes/validade | Modelo preparado; ativação antecipada se necessária ao piloto | Lotes, validade, rastreabilidade de recolhimento e FEFO |
| Custos | Registro de custo informado no recebimento, acesso restrito | Valorização por custo médio, centros de custo e fechamento gerencial |

Alimentos, medicamentos e outros segmentos com validade ou rastreabilidade obrigatória precisam de lote/validade já na V1. A priorização atual pressupõe almoxarifado geral e equipamentos. Requisitos setoriais devem ser levantados antes do piloto; este plano não define obrigações fiscais ou regulatórias.

## 4. Fluxos de trabalho

### Entrada e conferência

Rascunho → em conferência → confirmado. Cancelamento é possível antes da confirmação. Quantidades recebidas e rejeitadas são registradas separadamente. Uma confirmação transfere quantidade da origem externa para o endereço e condição apropriados. Recebimento sujeito a inspeção entra bloqueado. Correção posterior usa documento vinculado de estorno/ajuste, preservando o original.

Recebimentos parciais de uma mesma compra terão documentos próprios, vinculados à referência de origem. Na evolução com pedidos de compra, o saldo pendente do pedido será acompanhado automaticamente.

### Requisição e distribuição de consumíveis

Rascunho → enviada → aprovada/rejeitada → parcialmente atendida → atendida. Cancelamento do restante preserva o que já foi entregue. Aprovar autoriza atender; reservar compromete quantidade; separar prepara a entrega; confirmar entrega reduz o saldo físico. Esses eventos são distintos.

Toda entrega identifica destinatário, finalidade/centro de custo, operador e horário. Uma requisição pode originar várias entregas. Reserva não utilizada é liberada ao cancelar ou expirar. O solicitante visualiza o progresso e a previsão disponível, sem acesso automático a dados de outras equipes.

### Empréstimo e retorno

Aberto → parcialmente devolvido → encerrado. “Atrasado” é uma condição calculada a partir da data prevista e do saldo pendente, não um estado que substitui a devolução parcial.

Ao emprestar, o item sai do depósito e permanece sob custódia do responsável. Na devolução, conferir série/quantidade e condição: liberado, aguardando inspeção, danificado ou manutenção. O recebimento físico reduz a pendência do responsável. Se a política exigir inspeção, somente sua aprovação libera o item para uso; se dispensar e a conferência aprovar a condição, receber diretamente como liberado.

Perda, descarte e dano irreparável exigem motivo e autorização. Baixa pode encerrar uma pendência sem fingir que houve devolução. Pessoa com empréstimo aberto não perde o vínculo histórico se seu login for desativado. Retorno de consumível não utilizado é permitido com referência à entrega original e conferência; material consumido ou sem condição de uso não volta ao disponível.

### Remanejamento e transferência

Mover da gaveta A para o armário B da mesma raiz terá ação própria de remanejamento. Quando retirada e guarda são confirmadas juntas, efetivar um movimento atômico entre os endereços, com permissão em ambos. Se existir intervalo até a confirmação do recebimento, usar o fluxo com trânsito, inclusive dentro da mesma raiz. Material avariado pode ser remanejado mantendo sua condição e segregação.

Rascunho → aprovada → parcialmente expedida/expedida → parcialmente recebida → concluída. Cancelar antes da expedição libera reservas. Depois da expedição, resolver saldo por recebimento, retorno à origem ou perda autorizada.

Expedir retira da origem e coloca em trânsito. Receber retira do trânsito e coloca no destino; o destino não ganha saldo disponível na mera expedição. O documento controla solicitado, reservado, expedido, recebido, devolvido e perdido por linha. Quantidade não recebida continua visível como pendência. Dano muda a condição de entrada no destino. Devolução ao fornecedor é uma saída específica, vinculada ao recebimento e motivo, sem simular consumo ou empréstimo.

### Inventário e ajustes

Planejado → em contagem → em revisão → aprovado → aplicado. Na V1, bloquear movimentações apenas nos endereços em contagem e estabelecer um saldo de corte depois de concluir operações em andamento. A contagem é cega: o contador não vê o saldo esperado antes de registrar sua medida.

Diferenças acima da tolerância exigem recontagem. Ajuste aprovado gera movimentos com causa e vínculo ao inventário. Reserva incompatível com a quantidade encontrada deve ser realocada ou cancelada com aviso antes da aplicação. Cancelar a contagem libera os endereços e preserva a trilha de auditoria. A contagem simultânea com movimentação será uma evolução específica.

## 5. Perfis e permissões

Papéis são modelos iniciais configuráveis. Toda permissão combina ação, organização e escopo de recurso. Acesso a custo é independente de acesso a quantidade.

| Perfil | Escopo padrão |
| --- | --- |
| Administrador da organização | Usuários, configurações e políticas da sua organização; permissões operacionais atribuídas explicitamente |
| Gestor de estoque | Aprovação, inventário, ajustes, relatórios e políticas nos locais atribuídos |
| Operador/almoxarife | Receber, separar, entregar e devolver nos depósitos atribuídos |
| Solicitante | Criar e acompanhar suas requisições e pendências |
| Auditor/consulta | Ler histórico e relatórios autorizados, sem movimentar |
| Operador de infraestrutura | Backup, restauração e manutenção da instalação; separado do perfil administrativo da empresa |

Exemplos de permissões: `catalog.write`, `stock.read`, `receipts.post`, `issues.post`, `loans.manage`, `transfers.dispatch`, `transfers.receive`, `adjustments.approve`, `costs.read`, `reports.export`, `users.manage`. Ações sensíveis e importações de saldo não podem contornar alçadas.

Aprovação de ajuste por outra pessoa será a política padrão em operação com equipe. Uma instalação de uma só pessoa pode adotar exceção explícita e auditada. Administrar usuários não implica autorizar a si mesmo silenciosamente para aprovar tudo.

## 6. Experiência e telas

Menu principal: Visão geral, Estoque, Catálogo, Operações, Pessoas e destinos, Inventários, Relatórios e Administração. Em Operações: Entradas, Requisições, Entregas, Empréstimos, Devoluções e Transferências. Importações e exportações têm uma central de tarefas, com progresso e resultado.

O cabeçalho exibe organização/depósito selecionado, pesquisa e notificações. A mudança de contexto deve ficar evidente. Operadores usam tabelas densas no computador e fluxos simplificados no celular para conferir e ler códigos. A leitura inicial aceita scanners que funcionam como teclado; leitura por câmera depende de implementação e validação nos dispositivos reais.

| Tela | Conteúdo e ações |
| --- | --- |
| Visão geral | Falta/baixo estoque, empréstimos atrasados, transferências pendentes, aprovações e ações rápidas |
| Estoque | SKU, foto, localização, físico, reservado, disponível, condição e acesso ao extrato |
| Ficha do item | Dados, fotos, locais, movimentos, responsáveis, anexos e políticas |
| Nova operação | Cabeçalho, leitura/pesquisa do item, linhas, conferência e confirmação com resumo do efeito |
| Fila de trabalho | Receber, separar, aprovar, conferir retorno e resolver divergências |
| Empréstimos | Responsável, entregue, retornado, saldo pendente, vencimento e histórico |
| Inventário | Seleção dos endereços, contagem cega, divergências, recontagem e aprovação |
| Administração | Usuários, papéis, escopos, parâmetros, integrações e saúde operacional conforme permissão |

Usar shadcn/ui para tabela, formulário, combobox, diálogo, abas, drawer, calendário e notificações. Formulários longos precisam de rascunho e navegação clara. A confirmação de uma operação mostra origem, destino, quantidade e consequência; erros identificam a linha e como resolvê-la. Falha após envio não autoriza repetir a movimentação com uma chave nova: consultar o resultado original.

Prever navegação por teclado, foco visível, contraste, rótulos acessíveis, mensagens sem depender só de cor, estados vazios, carregamento, erro e falta de conexão. Exportações grandes não bloqueiam a tela. A interface não mostra ações inacessíveis como se estivessem disponíveis, mas a validação definitiva é sempre do backend.

## 7. Pesquisa, filtros e indicadores

Pesquisar por nome, SKU, código de barras, série/patrimônio, lote, documento ou responsável, respeitando permissões. Priorizar correspondência exata de código; permitir busca textual tolerante a acento para descrições. Consultas e paginação ocorrem no servidor.

Filtros combináveis: depósito/endereço, categoria, fornecedor, condição, status, disponibilidade, responsável, centro de custo, período, operador, vencimento e faixa de quantidade. Preservar filtros/ordenação na URL e permitir visões salvas privadas; compartilhamento só com escopo autorizado. Data e horário usam o fuso da organização. Períodos se convertem para intervalos UTC consistentes no backend.

| Indicador | Definição para evitar interpretação errada |
| --- | --- |
| Disponível | Quantidade liberada e elegível no depósito menos reservas ativas sobre essa quantidade |
| Ruptura | SKU ativo com política de reposição e disponível igual a zero no depósito |
| Baixo estoque | Disponível abaixo do ponto configurado por SKU/depósito |
| Em trânsito | Expedido que ainda não foi recebido, retornado ou baixado |
| Sob responsabilidade | Quantidade emprestada ainda não devolvida nem baixada |
| Consumo | Entregas definitivas menos retornos válidos, por período e unidade-base do SKU |
| Acuracidade | Posições contadas sem divergência / posições contadas, com tolerância declarada |
| Valor em estoque | Somente após existir política de valorização e cobertura de custos confiável |

Não somar litros, quilogramas e unidades como se fossem uma mesma quantidade. Para um total geral, mostrar número de SKUs/posições, ou valor com método declarado. Cada cartão tem data de atualização e permite abrir os registros que o compõem. Custos e totais gerenciais seguem as mesmas restrições de acesso das consultas.

## 8. Extensões planejadas e limites

Planejar etiquetas, termos de entrega, leitura QR, lotes/validade/FEFO, reposição, classificação ABC, kits, manutenção e integrações. FEFO significa sugerir primeiro os lotes elegíveis com vencimento mais próximo; a exceção exige motivo. Valorização gerencial terá método definido antes da implementação, separado da estratégia física de separação.

Ficam fora da primeira versão: emissão fiscal, contabilidade completa, folha de pagamento, roteirização avançada, faturamento SaaS, ERP completo e sincronização bidirecional de instalações desconectadas. Esses limites mantêm as regras de estoque verificáveis e a primeira entrega delimitada.
