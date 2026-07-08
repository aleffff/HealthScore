# HealthScore FARMA

Aplicação para identificar e explicar atrito operacional por grupo econômico da vertical `FARMA`, usando dados sincronizados diretamente do Salesforce.

O produto funciona como radar operacional: prioriza grupos que exigem atenção, mostra os fatores que compõem o score e registra planos de ação. O score não deve ser interpretado como previsão determinística de churn.

## Estado atual

O MVP está funcional em Docker e possui quatro serviços:

- `web`: dashboard Vue 3, Vite e TypeScript;
- `api`: API ASP.NET Core .NET 8;
- `worker`: sincronização Salesforce e processamento analítico;
- `db`: MariaDB 11.4 com volume persistente.

URLs locais:

- Dashboard: `http://localhost:5173`
- API: `http://localhost:8080`
- MariaDB: `127.0.0.1:3307`

## O que já foi implementado

### Integração e persistência

- autenticação Salesforce OAuth 2.0 por `client_credentials`;
- REST API Salesforce `v63.0` e paginação por `nextRecordsUrl`;
- carga completa inicial de contas FARMA;
- sincronização incremental de `Account` e `Case` por `SystemModstamp`;
- filtro de chamados por `Account.Vertical__c = 'FARMA'`;
- renovação do token após resposta `401`;
- upsert idempotente em lotes;
- watermarks separados por entidade;
- histórico de execuções, erros e interrupções;
- migrations do Entity Framework Core aplicadas pelo Worker.

### Motor analítico

- grupos econômicos e quantidade materializada de lojas ativas;
- calendário de dias úteis de segunda a sexta;
- densidade por grupo e benchmark médio da carteira;
- crescimento recente 30/90 dias;
- SLA violado, FCR, Issue/JIRA e criticidade;
- recorrência por grupo, taxonomia e janela de 30 dias;
- score de 0 a 100 e faixas Baixo, Atenção, Alto e Crítico;
- desempate auditável do principal motivo;
- ação sugerida conforme os indicadores;
- snapshots móveis de 30 dias;
- seis competências mensais para comparação histórica;
- versão da regra associada a cada snapshot.

### Dashboard

- cards executivos;
- ranking paginado;
- busca por grupo econômico;
- filtro por faixa de risco;
- filtro entre últimos 30 dias e competências mensais;
- exportação CSV compatível com Excel;
- decomposição dos sete fatores do score;
- evolução mensal;
- principais taxonomias;
- contas que mais acionaram;
- recomendação automática;
- plano de ação com responsável, status e observações;
- histórico append-only das alterações da tratativa;
- visão de operação e qualidade dos dados.

### Calibragem e governança

- edição dos sete pesos;
- edição das faixas finais de risco;
- validação da soma de pesos em 100 pontos;
- simulação de impacto antes da publicação;
- distribuição de grupos por faixa na simulação;
- versionamento de regras;
- autor e justificativa obrigatória;
- publicação e recálculo na mesma transação;
- preservação dos snapshots por versão.

### Segurança

- autenticação local automática para desenvolvimento;
- suporte preparado para OIDC/JWT em produção;
- papéis `Viewer`, `Operator`, `ScoreAdmin` e `SystemAdmin`;
- políticas específicas para leitura, tratativas, calibragem e dados brutos;
- segredos em `.env` ignorado pelo Git;
- portas locais restritas a `127.0.0.1`.

### Operação

- containers executados como usuário não root;
- health checks de aplicação e banco;
- logs estruturados;
- contagem de registros processados;
- visão das últimas sincronizações;
- indicadores de contas sem grupo, contas sem CNPJ e chamados sem grupo;
- imagens Docker multi-stage;
- volume persistente para o MariaDB.

## O que ainda falta

### Regras e dados

- tornar os thresholds internos de cada fator configuráveis;
- aplicar dinamicamente `periodDays` e a janela de recorrência publicada;
- cadastrar feriados oficiais no calendário;
- confirmar a semântica final do crescimento 30/90 dias;
- resolver a faixa ambígua de recorrência presente na especificação original;
- fechar regras oficiais para grupos nulos, CNPJs duplicados e lojas inativas;
- validar paridade completa com o Power BI usando um conjunto congelado de referência.

### Filtros analíticos

- marca;
- produto;
- escopo;
- com ou sem Issue/JIRA;
- benchmark recalculado corretamente para cada recorte;
- comparação visual entre versões de regra.

### Salesforce

- retry para `429` e `5xx` com backoff exponencial e jitter;
- dead-letter para registros inválidos;
- reconciliação de contas que deixaram a vertical FARMA;
- tratamento de registros excluídos no Salesforce;
- avaliação da Bulk API para cargas iniciais maiores;
- alertas externos para sincronização atrasada ou com falha.

### Segurança corporativa

- definir o provedor OIDC oficial;
- configurar Authority e Audience de produção;
- implementar login e logout no frontend;
- validar o mapeamento dos grupos corporativos para os papéis da aplicação;
- desabilitar `AUTH_MODE=local` fora do ambiente de desenvolvimento.

### Testes

- testes de integração com MariaDB via Testcontainers;
- contratos simulados do Salesforce;
- testes de endpoints e autorização;
- testes do Worker, watermarks e idempotência;
- testes E2E do dashboard;
- regressão automática contra os resultados do Power BI;
- testes de carga e concorrência.

### Deploy e continuidade

- pipeline CI/CD;
- ambiente de homologação;
- registry e tags imutáveis das imagens;
- secret manager;
- TLS e domínio corporativo;
- backup e restauração automatizados do MariaDB;
- política de retenção;
- limites de CPU e memória;
- métricas e alertas externos;
- procedimento testado de rollback.

## Ordem recomendada para as próximas entregas

1. Criar baseline de regressão Power BI versus HealthScore.
2. Implementar testes de integração e contratos Salesforce.
3. Tornar thresholds e janelas totalmente configuráveis.
4. Implementar filtros de marca, produto, escopo e Issue/JIRA.
5. Configurar OIDC corporativo.
6. Criar CI/CD, backup, restore e rollback.
7. Executar homologação com usuários de Suporte, CS/ECS, Produto e P&D.

## Executar localmente

1. Copie `.env.example` para `.env`.
2. Preencha as credenciais da Connected App do Salesforce.
3. Execute:

```powershell
docker compose up -d --build
```

Verifique os serviços:

```powershell
docker compose ps
Invoke-RestMethod http://127.0.0.1:8080/health/ready
```

## Desenvolvimento

```powershell
dotnet restore
dotnet build HealthScore.slnx
dotnet test HealthScore.slnx

cd apps/web
npm install
npm run build
```

## Autenticação

O ambiente local usa:

```text
AUTH_MODE=local
```

Nesse modo a API autentica como `local-admin`. Para produção:

```text
AUTH_MODE=oidc
AUTH_AUTHORITY=https://provedor-corporativo/
AUTH_AUDIENCE=healthscore-api
```

O modo local não deve ser utilizado em produção.

## Principais endpoints

- `GET /health/live`
- `GET /health/ready`
- `GET /api/v1/session`
- `GET /api/v1/operations/overview`
- `GET /api/v1/risk-score/periods`
- `GET /api/v1/risk-score/summary`
- `GET /api/v1/risk-score/groups`
- `GET /api/v1/risk-score/groups/export`
- `GET /api/v1/risk-score/groups/{id}`
- `GET /api/v1/risk-score/groups/{id}/evolution`
- `GET /api/v1/risk-score/groups/{id}/accounts`
- `GET /api/v1/risk-score/groups/{id}/taxonomy`
- `GET/PUT /api/v1/risk-score/groups/{id}/action-plan`
- `GET /api/v1/score-config`
- `POST /api/v1/score-config/simulate`
- `POST /api/v1/score-config/publish`

O plano técnico completo está em [docs/PLANO_DESENVOLVIMENTO.md](docs/PLANO_DESENVOLVIMENTO.md).
