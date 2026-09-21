# Operação, dados e implantação

## 1. Importação utilizável e rastreável

Fluxo: escolher modelo → enviar arquivo → mapear colunas → validar → revisar prévia → confirmar → acompanhar → baixar resultado. Mostrar número de inclusões, alterações e erros antes de confirmar. Toda importação pertence a uma organização e a um usuário autorizado.

V1: CSV UTF-8 para produtos/SKUs, pessoas/fornecedores, locais e abertura de estoque. A implantação de uma operação existente inclui modelos específicos de custódias e transferências em trânsito: responsável/origem/destino, quantidade pendente, série, prazo e referência histórica. Gerar documentos de migração e movimentos da contrapartida de abertura para Custody/Transit, sem simular recebimento físico no armário ou repetir a expedição passada. A abertura usa apenas o saldo pendente no corte; o histórico anterior permanece referenciado no arquivo de origem.

Disponibilizar modelos com instruções, campos obrigatórios e exemplos. Importação XLSX entra depois, preservando o mesmo pipeline. Validar tamanho, cabeçalhos, tipo de arquivo e limites de linhas; arquivos muito grandes são processados em jobs.

| Aspecto | Regra |
| --- | --- |
| Identificação | SKU por código estável; descrição nunca é chave de atualização |
| Local | Código e caminho validados contra a árvore; prévia mostra o endereço resolvido |
| Formatação | Tratar separador decimal, datas, unidade e códigos com zeros à esquerda explicitamente |
| Referências | Informar categorias, locais, pessoas, lotes/séries inexistentes ou ambíguos |
| Duplicação | Detectar duplicatas dentro do arquivo e contra o banco; erro por linha/coluna |
| Atualização | Escolher incluir, atualizar ou ambos; não sobrescrever campo com vazio sem política declarada |
| Saldo | Cadastro não altera quantidade; abertura usa documento de movimento próprio |
| Segurança | Mesmas permissões, alçadas, bloqueios de inventário e regras das telas |
| Execução | Revalidar no commit o que pode ter mudado desde a prévia |
| Rastreio | Hash do arquivo, versão do modelo, mapeamento, ator e documento gerado por unidade de trabalho |

Na V1, erro de validação impede confirmar o lote apresentado. Na execução, cadastros independentes podem ser gravados por registro/bloco, com progresso e resultado exatos; essa possibilidade é informada antes da confirmação. Documentos operacionais, incluindo cada documento de abertura, são atômicos: suas linhas confirmam juntas ou nenhuma confirma. Nunca anunciar “tudo importado” quando houver falha parcial.

Uma abertura grande pode ser dividida explicitamente por raiz/endereço e em limites de tamanho documentados, com prévia dos documentos resultantes. Retomar o job não repete documentos já confirmados. Arquivo igual não significa automaticamente erro, pois uma mesma base pode servir para atualização legítima; deduplicação do efeito depende de identificador de importação e referência externa permanente por organização/tipo/origem.

Importação de saldo inicial não é ferramenta para substituir saldo atual. Exigir modo de implantação habilitado e impedir reabertura sobre posições já movimentadas, salvo migração excepcional autorizada com conciliação. Ajustar saldo existente usa inventário ou documento de ajuste.

## 2. Exportação e relatórios

V1: CSV/XLSX de catálogo, saldos, extrato, empréstimos pendentes e transferências, respeitando filtros, organização, locais e acesso a custo. Permitir escolher página atual ou todos os resultados. Registrar solicitante, filtros e horário de referência; exportação volumosa roda em segundo plano com link privado temporário.

Gerar o relatório a partir de snapshot consistente ou conjunto materializado com horário de referência, evitando misturar páginas de momentos diferentes. Para arquivos grandes, usar processamento/consulta apropriados sem manter uma transação de movimentação aberta. Paginação estável e ordenação determinística são obrigatórias.

Revalidar permissões ao executar e ao baixar. Escapar dados que possam ser interpretados como fórmulas de planilha; códigos e seriais são texto, preservando zeros. Em XLSX, definir tipos de célula e formatos, sem macros. Exportação de negócio não inclui credenciais, chaves ou dados de outra organização.

Comprovantes de entrega/devolução começam como página imprimível; PDF automatizado é evolução. Assinatura/aceite interno registra ator e evidência, sem alegar certificação jurídica. Relatórios gerenciais de custo só são disponibilizados com método e cobertura de dados identificados.

Exportar dados não equivale a fazer backup da instalação. A exportação serve à análise e portabilidade; o backup serve à recuperação integral.

## 3. Imagens e anexos

Metadados no PostgreSQL; bytes em armazenamento privado. Adaptador inicial de diretório persistente e opção compatível com S3 para nuvem. Nome de objeto gerado pelo sistema, isolado por organização, sem usar caminho arbitrário enviado pelo usuário.

Fotos de produto, comprovantes e anexos pertencem a recursos autorizados. Ler um arquivo exige permissão sobre o recurso vinculado. O navegador recebe conteúdo por endpoint autorizado ou URL de curta validade. Documentos privados não ficam em pasta pública do servidor.

Upload em área temporária → verificação de tamanho e assinatura/tipo → validação/inspeção aplicável → gravação imutável → publicação do vínculo no banco. Para imagens, gerar miniatura e validar decodificação; proteger contra dimensões excessivas. Definir formatos permitidos para cada finalidade. Arquivos ainda não validados não são apresentados como anexos disponíveis.

Um arquivo só pode ganhar referência ativa depois de seus bytes estarem duráveis. Se o commit do banco falhar, sobra objeto órfão que pode ser removido por rotina com prazo de segurança. Se o armazenamento falhar, não publicar o vínculo. Substituir imagem cria nova versão, preservando a antiga durante a janela necessária à restauração.

Limites iniciais propostos: 10 MB por imagem e 25 MB por anexo; ajustáveis após o piloto. Definir quota por organização e alertar antes de esgotar disco. A limpeza de arquivos órfãos/arquivados respeita retenção de backup e registros históricos. Não buscar automaticamente imagens em URLs arbitrárias informadas na importação na V1; upload controlado evita dependência e acessos inesperados.

## 4. Avisos e notificações

Regra → ocorrência deduplicada → destinatário autorizado → notificação → resolução. Central interna é o canal básico, disponível também em LAN. E-mail é opcional e depende de configuração e conectividade; falhar no envio não desfaz uma entrega ou bloqueia a operação principal.

| Regra | Destinatário | Disparo e resolução |
| --- | --- | --- |
| Estoque baixo/zerado | Gestor do local | Cruzar o limite; resolver após recomposição; evitar repetição por cada consulta |
| Empréstimo vencido | Responsável com login e gestor | Saldo pendente após prazo; resolver por retorno, baixa ou renovação autorizada |
| Transferência pendente | Origem e destino autorizados | Prazo de trânsito excedido; resolver quando saldo pendente for tratado |
| Aprovação aguardando | Aprovador com escopo | Documento enviado; resolver por decisão ou cancelamento |
| Reserva expirando/expirada | Solicitante/operador | Avisar e liberar conforme política; nunca retirar duas vezes |
| Validade próxima | Gestor do local | Quando lote/validade estiver ativo; limites configuráveis |
| Importação/exportação concluída | Solicitante | Resultado e erros; disponibilidade do arquivo por prazo definido |
| Falha de backup, pouco disco ou fila travada | Operador da instalação | Canal de saúde independente sempre que possível |

Chave de deduplicação inclui organização, regra, entidade e episódio. Registrar visto, resolvido e silenciado até uma data; resolver decorre do estado real, não apenas de clicar “li”. Permitir resumos e janela de lembrete. Não vazar o nome de item/local restrito em e-mail ou contagem de avisos.

## 5. Backup e recuperação

Definir antes do piloto: perda máxima tolerável de dados (RPO) e tempo desejado para recuperar operação (RTO). Metas propostas para discussão: produção com RPO de até 15 minutos e RTO de até 4 horas, sujeitas a infraestrutura, tamanho e ensaio. São objetivos, não garantias já medidas.

Para essa meta, planejar backup físico do PostgreSQL com arquivamento contínuo de WAL e recuperação por ponto no tempo. O intervalo efetivo de arquivamento precisa sustentar o RPO, inclusive com pouco movimento. A documentação oficial descreve a combinação de base backup e WAL em [PITR](https://www.postgresql.org/docs/18/continuous-archiving.html). Verificar continuidade e recuperabilidade da cadeia, não só a existência de um arquivo.

Instalação pequena/piloto pode começar com dump consistente diário apenas se aceitar explicitamente a perda potencial de até um dia; sua rotina não satisfaz o objetivo de 15 minutos. `pg_dump` continua útil para exportação lógica e portabilidade; considerar também papéis e configurações da instalação, como explica a [documentação](https://www.postgresql.org/docs/18/app-pgdump.html).

Escopo recuperável: banco, imagens e anexos, manifesto das versões, configurações necessárias, chaves de criptografia/Data Protection, certificados ou procedimento de reemissão, e credenciais de recuperação protegidas separadamente. Cópia no mesmo disco não atende a perda do servidor. Manter cópia externa criptografada e ao menos uma cópia protegida contra exclusão pela credencial normal da aplicação. Backup integral usa credencial/procedimento próprios e verifica cobertura de todas as organizações, inclusive com RLS ativo; não reaproveitar exportação filtrada pelo contexto do usuário.

Política inicial sugerida: 7 diárias, 4 semanais e 6 mensais; ajustar a volume, finalidade e janela real de PITR. Retenção é diferente de RPO. O encadeamento base/WAL deve ser preservado para todos os pontos prometidos. Não remover objetos imutáveis enquanto qualquer backup ainda puder referenciá-los.

Para backup lógico coordenado simples: suspender publicações/remoções de anexos, concluir uploads em andamento, registrar checkpoint e manifestos, produzir cópia consistente do banco e dos objetos vinculados. Para operação com PITR: replicar/versionar objetos continuamente e definir o ponto recuperável comum ao banco e aos objetos. O RPO só é atingido se os dois lados atendem ao objetivo.

Runbook de restauração:

1. Selecionar ponto de recuperação, base, cadeia WAL e cópia de objetos compatíveis; obter as chaves pelo procedimento autorizado.
2. Provisionar ambiente isolado com versão compatível; impedir envio de notificações, jobs externos e integrações.
3. Restaurar banco, configurações e objetos; verificar checksums e referências de arquivos.
4. Conciliar ledger/saldos/reservas, checar séries e documentos pendentes; testar usuários, permissões e amostra de fotos/anexos.
5. Identificar o que se perdeu após o ponto restaurado. Conferir recebimentos, entregas e empréstimos físicos ocorridos nesse intervalo e reconstruí-los com origem e regularização documentadas: restaurar o banco não desfaz a entrega física. Manter locais afetados bloqueados até conciliar. Revalidar tarefas/outbox para evitar repetir efeitos já enviados antes da falha.
6. Revogar sessões recuperadas conforme política, validar operação, registrar tempos efetivos e liberar tráfego e worker de forma controlada.

Executar ensaio antes da primeira produção e depois periodicamente, inicialmente mensal, além de mudanças importantes de infraestrutura. O painel mostra último backup, última cópia externa, falhas e última restauração comprovada. Restaurar produção é ação de infraestrutura com autorização e registro próprios; não oferecer um botão irrestrito ao administrador comum da organização.

## 6. Observabilidade e atualização

Medir latência e erro da API, conflitos de estoque, tempo de lock, disponibilidade do banco, fila/idade do job mais antigo, tentativas de login, espaço livre, anexos sem referência e atraso de backup. Logs trazem IDs de organização, documento e correlação, mas não senhas, cookies ou conteúdo integral de arquivos.

Health checks distinguem processo vivo de pronto para receber tráfego. Falhas críticas de infraestrutura precisam ser detectáveis mesmo se a aplicação principal estiver fora do ar. Relatório de reconciliação alerta se ledger, saldos, reservas e pendências discordarem.

Atualização: artefato versionado → backup verificado → migration executada por job único → smoke test → liberação. Não executar migrations concorrentes no startup de todas as réplicas. Preferir mudanças compatíveis de adição e migração gradual; rollback de código não desfaz automaticamente uma alteração de schema. Registrar procedimento de recuperação antes de mudanças irreversíveis.

## 7. Perfis de implantação

| Perfil | Comportamento e requisitos |
| --- | --- |
| Rede local | Servidor central, DNS/HTTPS interno, banco e arquivos privados, clientes pelo navegador; recursos essenciais e login funcionam sem internet |
| Internet | Hospedagem central, HTTPS público, PostgreSQL privado, arquivos privados, backup externo e acesso autenticado |
| Local com acesso remoto | Mesma instância acessada pela LAN e por VPN/proxy configurado; uma única fonte de saldo |

Fontes, scripts e ícones essenciais devem acompanhar o pacote, permitindo LAN sem CDN. Relógio sincronizado é necessário para auditoria e MFA. Para começar os testes, considerar 4 vCPU, 8–16 GB RAM e SSD, com espaço calculado para banco, arquivos e backups; medir o consumo real antes de dimensionar produção. Isso é ponto de partida de laboratório, não promessa de capacidade.

Se o servidor estiver indisponível, mostrar conexão perdida e permitir preservar rascunho quando apropriado; não mostrar operação como confirmada. Ao reconectar, consultar o resultado da tentativa anterior e reenviar com a mesma chave quando necessário. Sincronizar gravações independentes em filiais desconectadas continua fora do escopo desta arquitetura.
