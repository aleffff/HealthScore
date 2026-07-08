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

- grupos reais de lojas resolvidos pela união transitiva de `ParentId` compartilhado e raiz dos CNPJs válidos;
- validação dos dígitos verificadores do CNPJ antes de utilizar sua raiz de oito dígitos;
- grupo informado pelo Salesforce preservado separadamente para auditoria e nomeação;
- chamados reassociados ao grupo resolvido por meio do `AccountId`;
- grupos econômicos e quantidade materializada de lojas ativas;
- calendário de dias úteis de segunda a sexta;
- densidade por grupo e benchmark médio da carteira;
- crescimento recente 30/90 dias;
- SLA violado, FCR, Issue/JIRA e criticidade;
- FCR obtido do campo calculado oficial `FCR_Formula__c`;
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
- janela de análise com presets únicos para Hoje, Ontem, últimos 7, 15 e 30 dias e competências mensais;
- períodos diários calculados pelos limites de `America/Sao_Paulo`, evitando deslocamento de chamados entre dias;
- filtros de marca, produto, escopo/vertical e presença de Issue/JIRA;
- recálculo do benchmark, crescimento, recorrência e score para cada recorte analítico;
- cache de 10 minutos por combinação de filtros e índices dedicados no MariaDB;
- exportação CSV compatível com Excel;
- decomposição dos sete fatores do score;
- evolução mensal;
- principais taxonomias;
- contas que mais acionaram;
- recomendação automática;
- plano de ação com responsável, status e observações;
- histórico append-only das alterações da tratativa;
- visão de operação e qualidade dos dados.
- página de conferência em `/audit`, acessível pelo menu e pelo drill-down do grupo;
- explicação do score com fórmula, numeradores, denominadores, benchmark e versão da regra;
- conferência de todas as contas agrupadas, CNPJ/raiz, conta pai, grupo reportado, status e evidência de vínculo;
- listagem paginada dos chamados efetivamente utilizados no período;
- busca de chamados por número ou Salesforce Id na página de conferência;
- alertas automáticos para CNPJ ausente/inválido, divergência de conta pai/grupo reportado, SLA/FCR/taxonomia ausentes e inconsistência temporal.

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
- login e logout OIDC no frontend usando Authorization Code com PKCE;
- validação JWT de issuer, audience, assinatura e expiração na API;
- envio automático do bearer token nas chamadas do dashboard;
- mapeamento configurável de grupos corporativos para papéis da aplicação;
- bloqueio de autenticação local quando o ambiente é `Production`;
- identidade autenticada utilizada nos registros de auditoria;
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
- indicadores de cobertura por conta pai, raiz de CNPJ válida e total de grupos resolvidos;
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

### Salesforce

- retry para `429` e `5xx` com backoff exponencial e jitter;
- dead-letter para registros inválidos;
- reconciliação de contas que deixaram a vertical FARMA;
- tratamento de registros excluídos no Salesforce;
- avaliação da Bulk API para cargas iniciais maiores;
- alertas externos para sincronização atrasada ou com falha.

### Segurança corporativa

- definir no deploy o provedor OIDC oficial e seus valores de Authority, Audience e Client ID;
- cadastrar no provedor as URLs de callback e logout do ambiente;
- preencher os mapeamentos dos grupos corporativos reais para os papéis da aplicação;
- validar o fluxo com usuários reais em homologação.

### Dashboard e calibragem

- implementar comparação visual entre versões de regra;
- substituir os seletores extensos por busca/autocomplete caso a cardinalidade de marcas permaneça alta;
- validar o tempo do primeiro cálculo de cada recorte sob carga de produção e, se necessário, materializar os recortes mais utilizados.

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
4. Implementar comparação visual entre versões de regra.
5. Homologar o OIDC com o provedor e os grupos corporativos oficiais.
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
AUTH_CLIENT_ID=healthscore-web
AUTH_SCOPE=openid profile email healthscore-api
```

Cadastre no provedor OIDC:

- callback: `https://seu-dominio/auth/callback`;
- logout: `https://seu-dominio/`;
- fluxo: Authorization Code com PKCE, sem client secret no frontend.

O frontend obtém a configuração pública em `GET /api/v1/auth/config`, executa login/logout e envia o access token em todas as chamadas da API. A API valida issuer, audience, assinatura e expiração. O modo local é recusado automaticamente quando `ASPNETCORE_ENVIRONMENT=Production`.

Papéis suportados: `Viewer`, `Operator`, `ScoreAdmin` e `SystemAdmin`. Claims de papéis podem ser consumidas diretamente, ou grupos corporativos podem ser mapeados por configuração:

```text
AUTH_GROUP_VIEWER=grupo-healthscore-leitura
AUTH_GROUP_OPERATOR=grupo-healthscore-operacao
AUTH_GROUP_SCORE_ADMIN=grupo-healthscore-score-admin
AUTH_GROUP_SYSTEM_ADMIN=grupo-healthscore-system-admin
```

As alterações de tratativas e publicações de regras registram a identidade obtida do token; valores de usuário enviados pelo navegador não são usados para auditoria.

## Datas e horários

Datas do Salesforce e do MariaDB são armazenadas em UTC. A API sempre serializa `DateTime` com o sufixo `Z`; o navegador converte para o fuso local do usuário. Exemplo: `2026-07-07T21:31:45Z` é exibido como `07/07/2026 18:31:45` em `America/Sao_Paulo`.

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
- `GET /api/v1/audit/groups/{id}`
- `GET /api/v1/audit/groups/{id}/cases`
- `GET /api/v1/risk-score/groups/{id}/evolution`
- `GET /api/v1/risk-score/groups/{id}/accounts`
- `GET /api/v1/risk-score/groups/{id}/taxonomy`
- `GET/PUT /api/v1/risk-score/groups/{id}/action-plan`
- `GET /api/v1/score-config`
- `POST /api/v1/score-config/simulate`
- `POST /api/v1/score-config/publish`

O plano técnico completo está em [docs/PLANO_DESENVOLVIMENTO.md](docs/PLANO_DESENVOLVIMENTO.md).
