# StorageManager

Sistema web de estoque para consumíveis e itens retornáveis, preparado para funcionar em uma rede local ou atrás de HTTPS na internet. O primeiro incremento executável usa ASP.NET Core Minimal API, PostgreSQL, React, TypeScript, Tailwind CSS e componentes shadcn/ui.

## O que já funciona

- instalação inicial segura, login por cookie e proteção CSRF;
- perfis Administrador, Operador e Consulta, com suspensão imediata de sessão;
- locais físicos hierárquicos, como sala, armário, prateleira e gaveta;
- catálogo de consumíveis e retornáveis, unidades e quantidades decimais;
- entrada e consumo com saldo transacional, histórico imutável e idempotência;
- bloqueio de estoque negativo mesmo com retiradas simultâneas;
- dashboard, estoque, filtros, pesquisa, paginação e histórico;
- isolamento dos dados por organização e trilha de auditoria no banco;
- interface responsiva e API/frontend publicáveis na mesma origem.

Itens retornáveis já podem ser cadastrados e recebidos. Empréstimo, devolução e cadeia de custódia entram no próximo incremento; eles não são tratados como consumo para preservar o modelo correto.

## Executar localmente

Pré-requisitos: PowerShell 7.4+, .NET SDK 10, Node.js 22+ e Docker Desktop.

```powershell
.\scripts\Start-Dev.ps1
```

Abra `http://localhost:5178`. Na primeira execução, o script mostra a chave usada para criar a organização e o administrador. Se o ambiente já estiver aberto e ainda não provisionado, consulte-a localmente com:

```powershell
.\scripts\Show-SetupToken.ps1
```

Segredos, chaves e logs de desenvolvimento ficam em `.local/`, que é ignorada pelo controle de versão. Para encerrar também o PostgreSQL:

```powershell
.\scripts\Stop-Dev.ps1 -StopDatabase
```

## Validar e publicar

```powershell
# Teste integrado em banco PostgreSQL efêmero
.\scripts\Test-Integration.ps1

# Pacote de release com frontend servido pela própria API
.\scripts\Publish.ps1
```

Ao executar o teste com `-KeepRunning` para inspeção manual, encerre depois com `.\scripts\Stop-Integration.ps1`. O comando remove somente o contêiner efêmero rotulado e os segredos daquele teste.

Se o servidor de desenvolvimento estiver aberto no Windows, use `Publish.ps1 -SkipInstall`; isso evita substituir módulos nativos em uso e pressupõe que `npm ci` já foi executado.

O pacote sai em `artifacts/publish`. Em produção, configure `ConnectionStrings__Default`, `Setup__Token`, `DataProtection__Path`, persistência das chaves de proteção e HTTPS no proxy/servidor. Aplique as migrations antes de iniciar a API:

```powershell
dotnet StorageManager.Api.dll --migrate
dotnet StorageManager.Api.dll
```

Não exponha diretamente o PostgreSQL. Backup, restauração testada, retenção de arquivos e certificados pertencem à operação da instalação e ainda serão automatizados em um incremento próprio.

## Estrutura

- `src/backend/StorageManager.Api`: endpoints, autenticação e hospedagem do frontend;
- `src/backend/StorageManager.Core`: entidades e regras centrais;
- `src/backend/StorageManager.Infrastructure`: EF Core, mapeamentos e migrations;
- `src/frontend`: aplicação React e biblioteca visual incorporada;
- `scripts`: inicialização, parada, testes integrados e publicação;
- `docs`: produto, arquitetura, domínio, operação e roteiro de evolução.

O contrato exato deste incremento está em [docs/06-contrato-primeiro-incremento.md](docs/06-contrato-primeiro-incremento.md). O desenho completo está dividido entre [produto](docs/01-produto.md), [arquitetura e segurança](docs/02-arquitetura.md), [domínio e dados](docs/03-dominio-e-dados.md), [operação](docs/04-operacao.md) e [execução](docs/05-execucao.md).

## Próximos incrementos

1. empréstimo, devolução, atraso, responsável e transferências entre locais;
2. importação validada, exportação CSV/XLSX e etiquetas/QR code;
3. imagens e anexos com armazenamento local ou compatível com S3;
4. alertas de mínimo, vencimento e atraso, com caixa interna e canais externos;
5. backup/restauração assistidos, observabilidade e endurecimento da implantação;
6. inventário cíclico, reservas, lotes, validade e relatórios avançados.

Este é um primeiro incremento operacional, não a declaração de que todo o roadmap já está pronto.
