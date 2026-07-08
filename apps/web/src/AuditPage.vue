<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { apiFetch as fetch } from './auth'

type GroupResult = { id: number; economicGroup: string; score: number; riskBand: string; totalCases: number; activeStores: number; mainReason: string }
type AuditData = any

const query = ref('')
const results = ref<GroupResult[]>([])
const audit = ref<AuditData | null>(null)
const cases = ref<any[]>([])
const casesPage = ref(1)
const casesTotal = ref(0)
const anomaliesOnly = ref(false)
const caseSearch = ref('')
const loading = ref(false)
const error = ref('')

function displayGroup(value: string) { return value.replace(/ \[(?:P|C|A):[^\]]+\]$/, '') }
function integer(value: number) { return Number(value ?? 0).toLocaleString('pt-BR') }
function decimal(value: number, digits = 2) { return Number(value ?? 0).toLocaleString('pt-BR', { minimumFractionDigits: digits, maximumFractionDigits: digits }) }
function pct(value: number) { return `${decimal(Number(value ?? 0) * 100, 1)}%` }
function date(value: string) { return new Date(value).toLocaleString('pt-BR') }

async function searchGroups() {
  loading.value = true; error.value = ''
  try {
    const params = new URLSearchParams({ page: '1', pageSize: '30' })
    if (query.value.trim()) params.set('search', query.value.trim())
    const response = await fetch(`/api/v1/risk-score/groups?${params}`)
    if (!response.ok) throw new Error('Não foi possível localizar grupos.')
    results.value = (await response.json()).items ?? []
  } catch (reason) { error.value = reason instanceof Error ? reason.message : 'Erro inesperado.' }
  finally { loading.value = false }
}

async function selectGroup(id: number) {
  loading.value = true; error.value = ''; audit.value = null
  window.history.replaceState({}, '', `/audit?group=${id}`)
  try {
    const response = await fetch(`/api/v1/audit/groups/${id}`)
    if (!response.ok) throw new Error('Conferência indisponível para este grupo.')
    audit.value = await response.json(); casesPage.value = 1
    await loadCases()
  } catch (reason) { error.value = reason instanceof Error ? reason.message : 'Erro inesperado.' }
  finally { loading.value = false }
}

async function loadCases() {
  if (!audit.value) return
  const params = new URLSearchParams({ page: String(casesPage.value), pageSize: '50', anomaliesOnly: String(anomaliesOnly.value) })
  if (caseSearch.value.trim()) params.set('search', caseSearch.value.trim())
  const response = await fetch(`/api/v1/audit/groups/${audit.value.group.id}/cases?${params}`)
  if (!response.ok) throw new Error('Chamados indisponíveis.')
  const data = await response.json(); cases.value = data.items ?? []; casesTotal.value = data.total ?? 0
}

async function changeCasesPage(delta: number) { casesPage.value = Math.max(1, casesPage.value + delta); await loadCases() }
async function toggleAnomalies() { casesPage.value = 1; await loadCases() }

function evidence(value: any) { return value ? String(value.points) : '—' }

onMounted(async () => {
  const id = Number(new URLSearchParams(window.location.search).get('group'))
  if (id) await selectGroup(id); else await searchGroups()
})
</script>

<template>
  <div class="audit-shell">
    <header class="audit-topbar">
      <a href="/">← Dashboard</a><div><strong>Conferência do HealthScore</strong><span>Rastreabilidade de grupos, dados e cálculo</span></div>
    </header>
    <main>
      <section class="audit-intro">
        <div><p class="audit-eyebrow">AUDITORIA OPERACIONAL</p><h1>Por que este grupo recebeu esse score?</h1><p>Confira a composição matemática, as lojas agrupadas, os chamados usados e possíveis distorções.</p></div>
        <form class="audit-search" @submit.prevent="searchGroups"><input v-model="query" placeholder="Buscar grupo econômico" /><button>Buscar</button></form>
      </section>

      <div v-if="error" class="audit-error">{{ error }}</div>
      <div v-if="loading" class="audit-loading">Carregando evidências…</div>

      <section v-if="!audit && results.length" class="audit-results">
        <button v-for="group in results" :key="group.id" @click="selectGroup(group.id)">
          <span><strong>{{ displayGroup(group.economicGroup) }}</strong><small>{{ integer(group.activeStores) }} lojas · {{ integer(group.totalCases) }} chamados · {{ group.mainReason }}</small></span>
          <b>{{ group.score }}</b><em>{{ group.riskBand }}</em>
        </button>
      </section>

      <template v-if="audit">
        <section class="audit-hero">
          <div><button class="change-group" @click="audit = null; searchGroups()">Trocar grupo</button><p class="audit-eyebrow">{{ audit.group.groupKey }}</p><h2>{{ audit.group.displayName }}</h2><span>{{ audit.group.periodStart }} a {{ audit.group.periodEndExclusive }}</span></div>
          <div class="audit-score"><strong>{{ audit.group.score }}</strong><span>{{ audit.group.riskBand }}</span><small>{{ audit.group.mainReason }}</small></div>
        </section>

        <section class="answer-grid">
          <article><span>Por que recebeu este score?</span><strong>{{ audit.group.mainReason }}</strong><small>{{ audit.calculation.factors.reduce((sum: number, item: any) => sum + item.points, 0) }} pontos somados em 7 fatores.</small></article>
          <article><span>As lojas agrupadas estão corretas?</span><strong>{{ integer(audit.grouping.totalAccounts) }} contas</strong><small>{{ integer(audit.grouping.activeStores) }} entram como lojas ativas; confira as evidências abaixo.</small></article>
          <article><span>Quais chamados foram usados?</span><strong>{{ integer(audit.calculation.totalCases) }} chamados</strong><small>Exclusivamente dentro do período exibido e associados às contas resolvidas.</small></article>
          <article :class="{ alert: audit.quality.anomalies.length }"><span>Há dados estranhos?</span><strong>{{ audit.quality.anomalies.length }} alertas</strong><small>Alertas indicam pontos para revisão, não erros confirmados.</small></article>
        </section>

        <section class="audit-card">
          <div class="audit-heading"><div><p class="audit-eyebrow">CÁLCULO EXPLICADO</p><h3>Composição do score</h3></div><span>Regra v{{ audit.calculation.rule.version }} · {{ audit.calculation.rule.name }}</span></div>
          <div class="factor-audit-grid">
            <article v-for="factor in audit.calculation.factors" :key="factor.name">
              <div><strong>{{ factor.name }}</strong><b>{{ factor.points }} / {{ factor.maximum }}</b></div>
              <div class="audit-track"><span :style="{ width: `${factor.maximum ? factor.points / factor.maximum * 100 : 0}%` }"></span></div>
              <small>{{ factor.formula }}</small><em>Valor apurado: {{ ['SLA','FCR','Criticidade','Issue/JIRA','Recorrência','Crescimento'].includes(factor.name) ? pct(factor.value) : decimal(factor.value, 4) }}</em>
            </article>
          </div>
          <div class="calculation-base"><span><b>{{ integer(audit.calculation.activeStores) }}</b> lojas ativas</span><span><b>{{ audit.calculation.businessDays }}</b> dias úteis</span><span><b>{{ decimal(audit.calculation.density, 6) }}</b> densidade</span><span><b>{{ decimal(audit.calculation.averageDensity, 6) }}</b> benchmark</span><span><b>{{ integer(audit.calculation.historicalTotal) }}</b> chamados na base 90d</span></div>
          <p class="rule-note">Publicada por {{ audit.calculation.rule.createdBy }} em {{ date(audit.calculation.rule.publishedAt) }}. {{ audit.calculation.rule.justification }}</p>
        </section>

        <section class="audit-card">
          <div class="audit-heading"><div><p class="audit-eyebrow">FORMAÇÃO DO GRUPO</p><h3>Lojas e evidências de vínculo</h3></div><span>{{ integer(audit.grouping.totalAccounts) }} contas · {{ audit.grouping.parentIds.length }} contas pai · {{ audit.grouping.cnpjRoots.length }} raízes</span></div>
          <div class="audit-table-wrap"><table><thead><tr><th>Conta</th><th>CNPJ / raiz</th><th>Conta pai</th><th>Grupo informado</th><th>Marca</th><th>Status</th><th>Ligação</th></tr></thead>
            <tbody><tr v-for="account in audit.grouping.accounts" :key="account.salesforceId"><td><strong>{{ account.name }}</strong><small>{{ account.salesforceId }}</small></td><td>{{ account.cnpj || '—' }}<small>{{ account.cnpjRoot || 'raiz inválida/ausente' }}</small></td><td>{{ account.parentName || '—' }}<small>{{ account.parentSalesforceId || '' }}</small></td><td>{{ account.reportedEconomicGroup || '—' }}</td><td>{{ account.brand || '—' }}</td><td><span :class="['store-status', { active: account.activeStore }]">{{ account.status || '—' }}</span></td><td><span v-if="account.evidence.length" v-for="item in account.evidence" :key="item" class="evidence">{{ item === 'conta_pai' ? 'Conta pai' : 'Raiz CNPJ' }}</span><span v-else class="evidence single">Individual</span></td></tr></tbody>
          </table></div>
        </section>

        <section class="audit-card">
          <div class="audit-heading"><div><p class="audit-eyebrow">QUALIDADE DOS DADOS</p><h3>Pontos que podem distorcer o resultado</h3></div></div>
          <div v-if="audit.quality.anomalies.length" class="anomaly-grid"><article v-for="item in audit.quality.anomalies" :key="item.title" :class="item.severity"><b>{{ item.count }}</b><div><strong>{{ item.title }}</strong><small>{{ item.explanation }}</small></div></article></div>
          <p v-else class="no-anomalies">Nenhuma anomalia automática encontrada neste recorte.</p>
        </section>

        <section class="audit-card">
          <div class="audit-heading"><div><p class="audit-eyebrow">BASE DO CÁLCULO</p><h3>Chamados e participação no score</h3><small>Inclui o período atual e a base histórica do crescimento. Os pontos exibidos pertencem ao fator agregado do grupo, não são somados por linha.</small></div><div class="case-tools"><form @submit.prevent="casesPage = 1; loadCases()"><input v-model="caseSearch" placeholder="Número do chamado" /><button>Buscar</button></form><label class="anomaly-toggle"><input v-model="anomaliesOnly" type="checkbox" @change="toggleAnomalies" /> Mostrar apenas anomalias</label></div></div>
          <div class="audit-table-wrap score-cases"><table><thead><tr><th>Chamado</th><th>Criação</th><th>Densidade</th><th>Crescimento</th><th>SLA</th><th>FCR</th><th>Criticidade</th><th>Issue/JIRA</th><th>Recorrência</th><th>Conta / produto</th><th>Taxonomia</th></tr></thead>
            <tbody><tr v-for="item in cases" :key="item.salesforceId"><td><strong>{{ item.caseNumber }}</strong><small>{{ item.salesforceId }}</small></td><td>{{ date(item.salesforceCreatedAt) }}</td><td>{{ evidence(item.evidence.density) }}</td><td>{{ evidence(item.evidence.growth) }}</td><td>{{ evidence(item.evidence.sla) }}</td><td>{{ evidence(item.evidence.fcr) }}</td><td>{{ evidence(item.evidence.criticality) }}</td><td>{{ evidence(item.evidence.issue) }}</td><td>{{ evidence(item.evidence.recurrence) }}</td><td>{{ item.accountSalesforceId || '—' }}<small>{{ item.product || '—' }} · {{ item.openingVertical || '—' }}</small></td><td>{{ item.taxonomy || '—' }}</td></tr></tbody>
          </table></div>
          <footer class="audit-pagination"><span>{{ integer(casesTotal) }} chamados · página {{ casesPage }}</span><div><button :disabled="casesPage === 1" @click="changeCasesPage(-1)">Anterior</button><button :disabled="casesPage * 50 >= casesTotal" @click="changeCasesPage(1)">Próxima</button></div></footer>
        </section>
      </template>
    </main>
  </div>
</template>

<style scoped>
.audit-shell{min-height:100vh;background:#f3f6f4;color:#22342d}.audit-topbar{height:68px;padding:0 4vw;display:flex;align-items:center;gap:22px;background:#123a2d;color:white}.audit-topbar a{color:#bcd6cc;text-decoration:none}.audit-topbar div{display:flex;flex-direction:column}.audit-topbar span{font-size:12px;color:#9fc1b5}.audit-shell main{width:min(1500px,94vw);margin:auto;padding:36px 0 70px}.audit-intro,.audit-hero,.audit-heading{display:flex;align-items:flex-end;justify-content:space-between;gap:24px}.audit-intro h1{margin:4px 0 8px;font-size:32px}.audit-intro p{margin:0;color:#687a72}.audit-eyebrow{margin:0!important;color:#40836d!important;font-size:11px!important;font-weight:700;letter-spacing:1.4px}.audit-search{display:flex;min-width:380px}.audit-search input{flex:1;padding:12px;border:1px solid #ccd9d3;border-radius:9px 0 0 9px}.audit-search button,.audit-pagination button,.change-group,.case-tools button{border:0;background:#176b57;color:white;padding:11px 17px;border-radius:0 9px 9px 0}.audit-results{margin-top:28px;display:grid;gap:8px}.audit-results button{display:flex;align-items:center;text-align:left;padding:16px 18px;border:1px solid #dce5e1;border-radius:10px;background:white}.audit-results span{flex:1;display:flex;flex-direction:column}.audit-results small,td small{display:block;color:#77877f;margin-top:3px}.audit-results b{font-size:22px;margin-right:18px}.audit-results em{font-style:normal;color:#557067}.audit-loading,.audit-error{margin:28px 0;padding:18px;border-radius:10px;background:white}.audit-error{color:#a33}.audit-hero{margin-top:28px;padding:24px 28px;border-radius:14px;background:#173e32;color:white}.audit-hero h2{margin:6px 0}.change-group{padding:6px 9px;border-radius:6px;background:#315f50}.audit-score{text-align:right;display:grid}.audit-score strong{font-size:48px}.audit-score span{font-weight:700;color:#bfe1d5}.audit-score small{color:#a9c6bc}.answer-grid{display:grid;grid-template-columns:repeat(4,1fr);gap:14px;margin:18px 0}.answer-grid article,.audit-card{background:white;border:1px solid #dfe7e3;border-radius:12px}.answer-grid article{padding:18px;display:flex;flex-direction:column;gap:7px}.answer-grid span{font-size:11px;text-transform:uppercase;color:#73837b}.answer-grid strong{font-size:19px}.answer-grid small{color:#6c7d75}.answer-grid .alert{border-color:#e6bd91;background:#fffaf4}.audit-card{margin-top:18px;padding:24px}.audit-heading{align-items:center;margin-bottom:20px}.audit-heading h3{margin:4px 0 0}.audit-heading>span{font-size:12px;color:#6d7e76}.case-tools{display:flex;align-items:center;gap:14px}.case-tools form{display:flex}.case-tools input{padding:9px;border:1px solid #ccd9d3;border-radius:8px 0 0 8px}.case-tools button{padding:9px 12px}.factor-audit-grid{display:grid;grid-template-columns:repeat(4,1fr);gap:12px}.factor-audit-grid article{padding:15px;border:1px solid #e0e7e3;border-radius:9px}.factor-audit-grid article>div:first-child{display:flex;justify-content:space-between}.factor-audit-grid small,.factor-audit-grid em{display:block;margin-top:9px;font-size:11px;color:#687a72;font-style:normal}.audit-track{height:6px;margin-top:11px;border-radius:5px;background:#e6ece9;overflow:hidden}.audit-track span{display:block;height:100%;background:#2a8b6d}.calculation-base{display:flex;gap:12px;flex-wrap:wrap;margin-top:18px}.calculation-base span{padding:10px 13px;border-radius:8px;background:#f2f6f4}.rule-note{font-size:12px;color:#687a72}.audit-table-wrap{overflow:auto;max-height:600px;border:1px solid #e1e8e4;border-radius:9px}.audit-table-wrap table{width:100%;border-collapse:collapse;font-size:12px}.audit-table-wrap th{position:sticky;top:0;background:#eef4f1;text-align:left;padding:11px;white-space:nowrap}.audit-table-wrap td{padding:10px 11px;border-top:1px solid #e7ece9;vertical-align:top}.score-cases table{min-width:1500px}.score-cases th:nth-child(n+3):nth-child(-n+9),.score-cases td:nth-child(n+3):nth-child(-n+9){text-align:center}.evidence{display:inline-block;margin:1px;padding:4px 6px;border-radius:5px;background:#daf0e7;color:#23634f}.evidence.single{background:#edf0ef;color:#65736d}.store-status{color:#8b6f52}.store-status.active{color:#257158}.anomaly-grid{display:grid;grid-template-columns:repeat(2,1fr);gap:10px}.anomaly-grid article{display:flex;gap:13px;padding:14px;border-left:4px solid #d7a35f;background:#fff9f1}.anomaly-grid article.danger{border-color:#bd5a5a;background:#fff4f4}.anomaly-grid article.info{border-color:#6794b1;background:#f4f9fc}.anomaly-grid b{font-size:24px}.anomaly-grid div{display:flex;flex-direction:column}.anomaly-grid small{color:#6e7c76}.no-anomalies{color:#39715e}.anomaly-toggle{font-size:12px}.audit-pagination{display:flex;justify-content:space-between;align-items:center;margin-top:14px}.audit-pagination button{border-radius:7px;margin-left:7px;padding:8px 12px}.audit-pagination button:disabled{opacity:.4}@media(max-width:1000px){.answer-grid,.factor-audit-grid{grid-template-columns:repeat(2,1fr)}.audit-intro{align-items:stretch;flex-direction:column}.audit-search{min-width:0}.anomaly-grid{grid-template-columns:1fr}.case-tools{align-items:flex-start;flex-direction:column}}@media(max-width:600px){.answer-grid,.factor-audit-grid{grid-template-columns:1fr}.audit-card{padding:15px}.audit-heading{align-items:flex-start;flex-direction:column}}
</style>
