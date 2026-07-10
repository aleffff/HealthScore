<script setup lang="ts">
import { onMounted, ref } from "vue";

type Rule = {
  version: number;
  name: string;
  publishedAt?: string;
  periodDays: number;
  recurrenceWindowDays: number;
  criticalPriorities: string[];
  weights: Record<string, number>;
  bands: { lowMax: number; attentionMax: number; highMax: number };
};
const rule = ref<Rule>({
  version: 1,
  name: "Regra inicial v1",
  periodDays: 30,
  recurrenceWindowDays: 30,
  criticalPriorities: ["Altíssima", "Alta", "High", "Disaster", "P0", "P1"],
  weights: {
    density: 25,
    growth: 15,
    sla: 15,
    fcr: 10,
    criticality: 15,
    issue: 10,
    recurrence: 10,
  },
  bands: { lowMax: 29, attentionMax: 49, highMax: 69 },
});
const live = ref(false);
onMounted(async () => {
  try {
    const r = await window.fetch("/api/v1/public/score-methodology");
    if (r.ok) {
      rule.value = await r.json();
      live.value = true;
    }
  } catch {
    /* fallback local */
  }
});

const items = [
  {
    key: "density",
    name: "Densidade",
    base: 25,
    direction: "Quanto maior, maior o risco",
    source: "O total de chamados do grupo para o produto padronizado pelo de-para interno, as lojas ativas do grupo, o total de chamados da carteira desse produto, as lojas ativas mensais informadas para o produto padronizado e os dias úteis.",
    formula:
      "1) Densidade do grupo = chamados do grupo para o produto padronizado ÷ (lojas ativas do grupo × dias úteis). 2) Benchmark da carteira = todos os chamados do produto padronizado no recorte ÷ (lojas ativas cadastradas para o produto padronizado e mês × dias úteis). 3) Indicador pontuado = densidade do grupo ÷ benchmark da carteira.",
    note: "Produto é obrigatório. O sistema preserva o produto bruto vindo do Salesforce e aplica o de-para interno apenas para filtros, cálculo e carteira. O cadastro de lojas usado é o do mês que contém o último dia da janela. Sem esse cadastro, nenhum score é calculado. No denominador do grupo, lojas ativas continuam sendo todos os CNPJs ativos do grupo, pois não existe quantidade por produto no nível da loja. Chamados da carteira sem grupo resolvido entram no benchmark, mas não aparecem no ranking.",
    example: "Exemplo: o grupo tem 20 chamados, 2 lojas e 5 dias úteis, portanto densidade 2,00. A carteira tem 1.000 chamados e 200 lojas no mesmo período: benchmark = 1.000 ÷ (200 × 5) = 1,00. O indicador do grupo é 2,00×, resultando em 10 pontos-base.",
    ranges: [
      ["até 1,00×", "0"],
      ["> 1,00× até 1,50×", "5"],
      ["> 1,50× até 2,00×", "10"],
      ["> 2,00× até 3,00×", "18"],
      ["> 3,00×", "25"],
    ],
  },
  {
    key: "growth",
    name: "Crescimento",
    base: 15,
    direction: "Quanto maior, maior o risco",
    source: "O total atual e todos os chamados entre 60 dias antes do início da janela e o final da janela.",
    formula:
      "1) Base = chamados desde (início da janela − 60 dias) até o final da janela. 2) Referência = total da base ÷ 3. 3) Crescimento = (chamados da janela atual ÷ referência) − 1.",
    note: "Comportamento real atual: a base inclui os chamados da própria janela atual e é sempre dividida por 3, mesmo quando a janela escolhida não possui 30 dias. Se o total da base for zero, o crescimento é definido como zero.",
    example: "Exemplo: 30 chamados atuais e 60 chamados na base produzem referência 60 ÷ 3 = 20. O crescimento é 30 ÷ 20 − 1 = 50%, resultando em 10 pontos-base.",
    ranges: [
      ["até 10%", "0"],
      ["> 10% até 30%", "5"],
      ["> 30% até 60%", "10"],
      ["> 60%", "15"],
    ],
  },
  {
    key: "sla",
    name: "SLA violado",
    base: 15,
    direction: "Quanto maior, maior o risco",
    source: "Campo Case.SLA_violado__c do Salesforce.",
    formula:
      "Taxa = chamados com SLA violado igual a verdadeiro ÷ total de chamados do grupo no período.",
    note: "Valores nulos não entram no numerador, mas continuam no denominador.",
    example: "Exemplo: 4 violações em 20 chamados = 20%. Como 20% é maior que 15% e menor ou igual a 30%, o resultado é 10 pontos-base.",
    ranges: [
      ["até 5%", "0"],
      ["> 5% até 15%", "5"],
      ["> 15% até 30%", "10"],
      ["> 30%", "15"],
    ],
  },
  {
    key: "fcr",
    name: "FCR",
    base: 10,
    direction: "Quanto menor, maior o risco",
    source: "Campo calculado Case.FCR_Formula__c do Salesforce.",
    formula:
      "Taxa = chamados com FCR verdadeiro ÷ total de chamados do grupo no período.",
    note: "Falso e nulo não entram no numerador e reduzem a taxa. Uma taxa alta de FCR é positiva.",
    example: "Exemplo: 12 chamados com FCR verdadeiro em um total de 20 = 60%. O limite de 60% está incluído na faixa de 3 pontos-base.",
    ranges: [
      ["75% ou mais", "0"],
      ["60% até < 75%", "3"],
      ["45% até < 60%", "7"],
      ["abaixo de 45%", "10"],
    ],
  },
  {
    key: "criticality",
    name: "Criticidade",
    base: 15,
    direction: "Quanto maior, maior o risco",
    source: "Campo padrão Case.Priority do Salesforce.",
    formula:
      "Taxa = chamados cuja prioridade pertence à lista crítica ÷ total de chamados do grupo.",
    note: "A lista exibida abaixo vem da regra publicada e pode ser alterada por calibragem.",
    example: "Exemplo: 3 chamados com prioridade crítica em 20 = 15%. Como 15% é maior que 10% e menor ou igual a 20%, o resultado é 10 pontos-base.",
    ranges: [
      ["até 5%", "0"],
      ["> 5% até 10%", "5"],
      ["> 10% até 20%", "10"],
      ["> 20%", "15"],
    ],
  },
  {
    key: "issue",
    name: "Issue/JIRA",
    base: 10,
    direction: "Quanto maior, maior o risco",
    source: "Campo Case.Issue_Code_Jira__c do Salesforce.",
    formula:
      "Taxa = chamados com código de Issue/JIRA preenchido ÷ total de chamados do grupo.",
    note: "Qualquer texto não vazio no campo conta no numerador.",
    example: "Exemplo: 2 chamados com código JIRA em 20 = 10%. Como 10% é maior que 8% e menor ou igual a 15%, o resultado é 7 pontos-base.",
    ranges: [
      ["até 3%", "0"],
      ["> 3% até 8%", "3"],
      ["> 8% até 15%", "7"],
      ["> 15%", "10"],
    ],
  },
  {
    key: "recurrence",
    name: "Recorrência",
    base: 10,
    direction: "Quanto maior, maior o risco",
    source: "O tema do chamado: Nível 4; se vazio, Nível 3; depois Nível 2; por fim, Descrição da Taxonomia.",
    formula:
      "1) Busca chamados desde 30 dias antes do início da janela até o final. 2) Ordena por grupo, tema e criação. 3) Um chamado atual é recorrente quando o registro imediatamente anterior tem o mesmo grupo e tema e ocorreu há no máximo 30 dias. 4) Taxa = recorrentes ÷ total atual.",
    note: "Comportamento real atual: a comparação usa 30 dias fixos. Chamados sem taxonomia não podem ser recorrentes, mas continuam no denominador total.",
    example: "Exemplo: 2 chamados classificados como recorrentes em 20 chamados atuais = 10%. O limite de 10% está incluído na faixa de 5 pontos-base.",
    ranges: [
      ["até 5%", "0"],
      ["> 5% até 10%", "5"],
      ["> 10%", "10"],
    ],
  },
];
const weight = (key: string) => rule.value.weights[key] ?? 0;
</script>

<template>
  <div class="method-page">
    <header>
      <a class="brand" href="/"
        ><b>HS</b><span>HealthScore<small>Metodologia pública</small></span></a
      ><a href="/">Ir para o dashboard →</a>
    </header>
    <main>
      <section class="hero">
        <p class="eyebrow">COMO O SCORE É CALCULADO</p>
        <h1>Metodologia do HealthScore</h1>
        <p>
          Entenda quais dados são observados, como cada indicador é calculado e
          como os pontos formam o risco operacional de cada grupo econômico.
        </p>
        <div class="rule">
          <i :class="{ live }"></i
          ><span
            ><strong>{{ rule.name }} · versão {{ rule.version }}</strong
            ><small
              >{{ live ? "Regra publicada atualmente" : "Regra padrão"
              }}<template v-if="rule.publishedAt">
                ·
                {{
                  new Date(rule.publishedAt).toLocaleString("pt-BR")
                }}</template
              ></small
            ></span
          >
        </div>
      </section>
      <nav>
        <a v-for="item in items" :key="item.key" :href="`#${item.key}`"
          ><span>{{ item.name }}</span
          ><b>{{ weight(item.key) }} pts</b></a
        >
      </nav>
      <section class="card overview">
        <p class="eyebrow">VISÃO GERAL</p>
        <h2>Do chamado ao score</h2>
        <div class="steps">
          <article>
            <b>1</b
            ><span
              ><strong>Janela</strong
              ><small>Hoje, ontem, 7, 15, 30 dias ou mês.</small></span
            >
          </article>
          <article>
            <b>2</b
            ><span
              ><strong>Dados</strong
              ><small>Aplica o período e filtros selecionados.</small></span
            >
          </article>
          <article>
            <b>3</b
            ><span
              ><strong>Agrupamento</strong
              ><small>Conta pai e raiz do CNPJ unem as lojas.</small></span
            >
          </article>
          <article>
            <b>4</b
            ><span
              ><strong>Sete sinais</strong
              ><small>Taxas viram pontos pelas faixas.</small></span
            >
          </article>
          <article>
            <b>5</b
            ><span
              ><strong>Resultado</strong
              ><small>Soma limitada a 100 e faixa de risco.</small></span
            >
          </article>
        </div>
        <div class="sum">
          Score = Densidade + Crescimento + SLA + FCR + Criticidade + Issue/JIRA
          + Recorrência <small>máximo 100</small>
        </div>
      </section>
      <section class="card definitions">
        <p class="eyebrow">DEFINIÇÕES EXATAS</p>
        <h2>Termos usados nas fórmulas</h2>
        <dl>
          <div><dt>Janela atual</dt><dd>Intervalo selecionado na tela. A data/hora inicial entra no cálculo; a data/hora final não entra. Por exemplo, Hoje vai de 00:00 de hoje até, sem incluir, 00:00 de amanhã.</dd></div>
          <div><dt>Total de chamados</dt><dd>Quantidade de registros Case associados ao grupo, criados dentro da janela e que atendem aos filtros ativos. Campos SLA, FCR, prioridade, JIRA ou taxonomia vazios não retiram o chamado desse total.</dd></div>
          <div><dt>Loja ativa</dt><dd>Conta com grupo resolvido preenchido, CNPJ preenchido e Status exatamente igual a ATIVO ou Ativa. Dentro do grupo, CNPJs idênticos são contados uma única vez. O código exige CNPJ preenchido, mas não exige que ele seja válido para esta contagem.</dd></div>
          <div><dt>Grupo elegível</dt><dd>Grupo que tem pelo menos um chamado no total atual e cuja contagem de lojas ativas é maior que zero. Grupos sem chamados na janela ou sem loja ativa ficam fora do ranking e também fora do benchmark de densidade.</dd></div>
          <div><dt>Dias úteis</dt><dd>Datas da janela marcadas no calendário interno como dia útil. Hoje esse calendário marca segunda a sexta como úteis e sábado e domingo como não úteis; não existe desconto de feriados. Se a contagem for zero, o código usa 1.</dd></div>
          <div><dt>Benchmark de densidade</dt><dd>Total de chamados da carteira para o produto padronizado e demais filtros, dividido pela quantidade mensal de lojas ativas informada manualmente para esse produto e pelos dias úteis. Não é mais a média das densidades dos grupos.</dd></div>
          <div><dt>Mês da carteira</dt><dd>É o mês do último dia incluído na janela. Uma janela encerrada em 8 de julho usa o cadastro de julho. O valor é histórico por produto e mês.</dd></div>
        </dl>
      </section>
      <section class="card grouping">
        <p class="eyebrow">COMO AS LOJAS SÃO AGRUPADAS</p>
        <h2>Definição real do grupo econômico</h2>
        <ol>
          <li><b>Conta pai compartilhada:</b> contas com o mesmo ParentId não vazio são unidas.</li>
          <li><b>Raiz de CNPJ compartilhada:</b> o valor é reduzido a dígitos e precisa ter 14 dígitos, não pode repetir o mesmo dígito e precisa passar nos dois dígitos verificadores. Contas válidas com os mesmos oito primeiros dígitos são unidas.</li>
          <li><b>Relação com a conta pai:</b> se o ParentId corresponde a uma Account carregada, a conta filha também é unida diretamente à conta pai.</li>
          <li><b>União transitiva:</b> os vínculos são combinados. Se A está ligada a B pela conta pai e B está ligada a C pela raiz do CNPJ, A, B e C ficam no mesmo grupo.</li>
          <li><b>Conta isolada:</b> sem nenhum vínculo válido, a conta forma um grupo individual.</li>
        </ol>
        <p class="note">O nome exibido usa, nesta ordem: nome de conta pai mais frequente; GrupoEconomico__c mais frequente; nome técnico baseado na raiz do CNPJ quando há mais de uma conta; ou nome da primeira conta. Uma chave técnica estável é anexada internamente para evitar colisões, mas é ocultada na tela.</p>
      </section>
      <section class="card filter-behavior">
        <p class="eyebrow">EFEITO DOS FILTROS</p>
        <h2>O que muda quando um filtro é aplicado</h2>
        <p>A sincronização traz somente Case cuja conta relacionada possui Account.Vertical__c igual a FARMA. A janela usa Case.CreatedDate, armazenada internamente em UTC. Chamados sem grupo econômico resolvido ficam fora dos cálculos.</p>
        <p>Produto é obrigatório. O produto selecionado é padronizado pelo de-para interno, mas o valor bruto salvo do Salesforce não é alterado. Produto, escopo/vertical, unidade de negócio e Issue/JIRA restringem os chamados usados no período atual, na base de crescimento, na busca de recorrência e no total de chamados do benchmark da carteira.</p>
        <p>A quantidade de lojas ativas não é reduzida por produto, escopo, unidade ou Issue/JIRA: ela continua sendo a quantidade total de CNPJs ativos do grupo. Ao filtrar somente chamados com Issue, todos os chamados restantes possuem Issue e a taxa de Issue será 100%; ao filtrar sem Issue, ela será 0%.</p>
      </section>
      <section
        v-for="(item, index) in items"
        :id="item.key"
        :key="item.key"
        class="card composition"
      >
        <div class="title">
          <em>{{ String(index + 1).padStart(2, "0") }}</em
          ><span
            ><p class="eyebrow">COMPOSIÇÃO</p>
            <h2>{{ item.name }}</h2>
            <small>{{ item.direction }}</small></span
          ><b>{{ weight(item.key) }}<small>pontos máximos</small></b>
        </div>
        <div class="explain">
          <article>
            <h3>O que observa</h3>
            <p>{{ item.source }}</p>
          </article>
          <article>
            <h3>Como calcula</h3>
            <p>{{ item.formula }}</p>
          </article>
        </div>
        <p class="note">{{ item.note }}</p>
        <p class="example"><b>Exemplo numérico</b>{{ item.example }}</p>
        <div v-if="item.key === 'criticality'" class="priorities">
          <span v-for="p in rule.criticalPriorities" :key="p">{{ p }}</span>
        </div>
        <table>
          <thead>
            <tr>
              <th>Resultado do indicador</th>
              <th>Pontos-base</th>
              <th>Peso vigente</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="range in item.ranges" :key="range[0]">
              <td>{{ range[0] }}</td>
              <td>{{ range[1] }}</td>
              <td>
                {{
                  Math.round((Number(range[1]) / item.base) * weight(item.key))
                }}
              </td>
            </tr>
          </tbody>
        </table>
      </section>
      <section class="card">
        <p class="eyebrow">RESULTADO FINAL</p>
        <h2>Faixas de risco</h2>
        <div class="bands">
          <article class="low">
            Baixo <small>0 a {{ rule.bands.lowMax }}</small>
          </article>
          <article class="attention">
            Atenção
            <small
              >{{ rule.bands.lowMax + 1 }} a
              {{ rule.bands.attentionMax }}</small
            >
          </article>
          <article class="high">
            Alto
            <small
              >{{ rule.bands.attentionMax + 1 }} a
              {{ rule.bands.highMax }}</small
            >
          </article>
          <article class="critical">
            Crítico <small>{{ rule.bands.highMax + 1 }} a 100</small>
          </article>
        </div>
      </section>
      <section class="card">
        <p class="eyebrow">REGRAS IMPORTANTES</p>
        <h2>Leitura correta</h2>
        <ul>
          <li>
            <b>Denominadores:</b> SLA, FCR, Criticidade, Issue e Recorrência dividem seus numeradores pelo mesmo total de chamados atuais do grupo depois dos filtros.
          </li>
          <li>
            <b>Fuso:</b> Hoje, Ontem, 7 e 15 dias convertem meia-noite de America/São_Paulo para UTC. Os snapshots de 30 dias e mensais usam limites de data em UTC.
          </li>
          <li>
            <b>Principal motivo:</b> maior pontuação; empate prioriza Issue,
            SLA, Criticidade, Recorrência, Densidade, Crescimento e FCR.
          </li>
          <li>
            <b>Calibragem:</b> pontos-base são escalados proporcionalmente ao
            peso vigente e arredondados.
          </li>
          <li>
            <b>Filtros:</b> produto usa Produto_Taxonomia__c, escopo usa
            Segmento__c e unidade usa Unidade_de_Negocio_de_Abertura__c.
          </li>
          <li>
            <b>Período:</b> o início é incluído e o instante final é exclusivo.
          </li>
        </ul>
      </section>
    </main>
    <footer>HealthScore · Vertical FARMA · Metodologia operacional</footer>
  </div>
</template>

<style scoped>
.method-page {
  min-height: 100vh;
  background: #f3f7f5;
  color: #17362d;
}
.method-page header {
  height: 70px;
  padding: 0 max(4vw, 24px);
  display: flex;
  align-items: center;
  justify-content: space-between;
  background: #123a2d;
  color: white;
}
.method-page a {
  color: inherit;
  text-decoration: none;
}
.brand {
  display: flex;
  align-items: center;
  gap: 11px;
}
.brand > b {
  display: grid;
  place-items: center;
  width: 36px;
  height: 36px;
  border-radius: 9px;
  background: white;
  color: #174b3b;
}
.brand > span,
.rule span {
  display: flex;
  flex-direction: column;
}
.brand small,
.rule small {
  font-size: 10px;
  font-weight: 400;
  color: #aac8bd;
}
.method-page main {
  width: min(1100px, 92vw);
  margin: auto;
  padding: 60px 0 80px;
}
.hero {
  max-width: 820px;
}
.eyebrow {
  margin: 0 0 8px;
  color: #2f8068;
  font-size: 10px;
  font-weight: 800;
  letter-spacing: 1.5px;
}
.hero h1 {
  margin: 0;
  font-size: clamp(38px, 6vw, 62px);
  line-height: 1.05;
}
.hero > p:not(.eyebrow) {
  color: #5b7068;
  font-size: 18px;
  line-height: 1.7;
}
.rule {
  display: flex;
  align-items: center;
  gap: 10px;
  width: max-content;
  padding: 12px 15px;
  border: 1px solid #d9e5e0;
  border-radius: 10px;
  background: white;
}
.rule i {
  width: 9px;
  height: 9px;
  border-radius: 50%;
  background: #b98750;
}
.rule i.live {
  background: #29996e;
}
.method-page nav {
  display: grid;
  grid-template-columns: repeat(7, 1fr);
  gap: 8px;
  margin: 40px 0;
}
.method-page nav a {
  padding: 13px;
  border: 1px solid #dbe6e1;
  border-radius: 9px;
  background: white;
}
.method-page nav span,
.method-page nav b {
  display: block;
}
.method-page nav span {
  font-size: 11px;
  color: #687b74;
}
.method-page nav b {
  margin-top: 5px;
}
.card {
  margin-top: 20px;
  padding: 32px;
  border: 1px solid #dbe6e1;
  border-radius: 15px;
  background: white;
  scroll-margin-top: 18px;
}
.card h2 {
  margin: 0;
  font-size: 27px;
}
.steps {
  display: grid;
  grid-template-columns: repeat(5, 1fr);
  gap: 15px;
  margin: 25px 0;
}
.steps article {
  display: flex;
  gap: 9px;
}
.steps article > b {
  display: grid;
  place-items: center;
  flex: 0 0 28px;
  height: 28px;
  border-radius: 50%;
  background: #e7f2ed;
  color: #26735a;
}
.steps span {
  display: flex;
  flex-direction: column;
}
.steps small {
  margin-top: 5px;
  color: #74847d;
}
.sum {
  padding: 17px;
  border-radius: 9px;
  background: #153d30;
  color: white;
}
.sum small {
  float: right;
  color: #abcabd;
}
.title {
  display: grid;
  grid-template-columns: auto 1fr auto;
  align-items: center;
  gap: 18px;
}
.title em {
  font-size: 28px;
  color: #b8c9c2;
  font-style: normal;
}
.title > span > small {
  color: #70817a;
}
.title > b {
  font-size: 30px;
  color: #1c6c53;
  text-align: right;
}
.title > b small {
  display: block;
  font-size: 10px;
  color: #71847c;
}
.explain {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 14px;
  margin: 24px 0 12px;
}
.explain article {
  padding: 18px;
  border-radius: 9px;
  background: #f4f8f6;
}
.explain h3 {
  margin: 0 0 7px;
  font-size: 11px;
  text-transform: uppercase;
}
.explain p,
.note {
  margin: 0;
  color: #536961;
  line-height: 1.65;
}
.note {
  padding: 14px 17px;
  border-left: 3px solid #65a58f;
  background: #f7faf9;
}
.example {
  margin: 10px 0 0;
  padding: 14px 17px;
  border-radius: 8px;
  background: #eef5f2;
  color: #435f55;
  line-height: 1.6;
}
.example b {
  display: block;
  margin-bottom: 3px;
  color: #245d4b;
  font-size: 11px;
  text-transform: uppercase;
  letter-spacing: .05em;
}
.definitions dl {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 12px;
  margin: 24px 0 0;
}
.definitions dl > div {
  padding: 18px;
  border: 1px solid #e1e9e5;
  border-radius: 9px;
  background: #f8faf9;
}
.definitions dt {
  margin-bottom: 7px;
  font-weight: 750;
  color: #245d4b;
}
.definitions dd,
.grouping li,
.filter-behavior p {
  margin: 0;
  color: #536961;
  line-height: 1.65;
}
.grouping ol {
  display: grid;
  gap: 10px;
  margin: 22px 0;
  padding-left: 22px;
}
.filter-behavior p {
  margin-top: 14px;
}
.priorities {
  display: flex;
  flex-wrap: wrap;
  gap: 7px;
  margin: 15px 0;
}
.priorities span {
  padding: 6px 9px;
  border-radius: 6px;
  background: #edf3f0;
  font-size: 12px;
}
table {
  width: 100%;
  margin-top: 18px;
  border-collapse: collapse;
}
th,
td {
  padding: 12px;
  border-bottom: 1px solid #e5ede9;
  text-align: left;
}
th {
  font-size: 10px;
  text-transform: uppercase;
  color: #71847c;
}
th:nth-child(n + 2),
td:nth-child(n + 2) {
  text-align: center;
}
.bands {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  gap: 10px;
  margin-top: 22px;
}
.bands article {
  padding: 18px;
  border-radius: 9px;
  font-weight: 700;
}
.bands small {
  display: block;
  margin-top: 6px;
}
.low {
  background: #e7f4ed;
  color: #276c51;
}
.attention {
  background: #fff5d9;
  color: #84691f;
}
.high {
  background: #ffead8;
  color: #985329;
}
.critical {
  background: #f9dfdf;
  color: #963f3f;
}
ul {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 12px 28px;
  color: #586d65;
  line-height: 1.6;
}
.method-page footer {
  padding: 24px;
  text-align: center;
  background: #e8f0ec;
  color: #6c7f77;
  font-size: 12px;
}
@media (max-width: 900px) {
  .method-page nav {
    grid-template-columns: repeat(2, 1fr);
  }
  .steps {
    grid-template-columns: 1fr 1fr;
  }
}
@media (max-width: 620px) {
  .card {
    padding: 22px;
  }
  .steps,
  .explain,
  .bands,
  .definitions dl,
  ul {
    grid-template-columns: 1fr;
  }
  .title {
    grid-template-columns: auto 1fr;
  }
  .title > b {
    grid-column: 2;
    text-align: left;
  }
  .sum small {
    float: none;
    display: block;
    margin-top: 8px;
  }
  .method-page header > a:last-child {
    display: none;
  }
}
</style>
