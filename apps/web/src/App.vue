<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'

type Summary = {
  available: boolean
  snapshotKind: string
  periodStart: string
  periodEndExclusive: string
  totalGroups: number
  criticalGroups: number
  highGroups: number
  averageScore: number
  totalCases: number
  criticalCases: number
  criticalCaseShare: number
}

type GroupRow = {
  id: number
  economicGroup: string
  activeStores: number
  totalCases: number
  densityVsAverage: number
  slaViolatedRate: number
  fcrRate: number
  issueRate: number
  recurrenceRate: number
  score: number
  riskBand: string
  mainReason: string
}

type GroupDetail = GroupRow & {
  periodStart: string
  periodEndExclusive: string
  metrics: Record<string, number>
  factors: Array<{ name: string; points: number; maximum: number }>
  scoreRuleVersionId: number
  calculatedAt: string
  suggestedAction: string
}

type EvolutionItem = { periodStart: string; totalCases: number; density: number; score: number; riskBand: string }
type AccountItem = { accountId: string; name: string; cnpj?: string; brand?: string; totalCases: number; slaRate: number; fcrRate: number; issueRate: number }
type TaxonomyItem = { taxonomy: string; totalCases: number; recurrenceCases: number; recurrenceRate: number; slaRate: number; issueRate: number }
type ScoreConfiguration = {
  periodDays: number
  recurrenceWindowDays: number
  criticalPriorities: string[]
  weights: { density: number; growth: number; sla: number; fcr: number; criticality: number; issue: number; recurrence: number }
  bands: { lowMax: number; attentionMax: number; highMax: number }
}
type Simulation = { groups: number; currentAverage: number; simulatedAverage: number; changedBands: number; distribution: Record<string, number> }
type ActionPlanHistory = { id: number; eventType: string; changedBy: string; createdAt: string }
type PeriodOption = { snapshotKind: string; periodStart: string; periodEndExclusive: string; groups: number }
type OperationsOverview = {
  generatedAt: string
  ingestion: { accounts: number; cases: number; lastRuns: Array<{ id: number; entityName: string; status: string; startedAt: string; finishedAt?: string; recordsRead: number; recordsWritten: number; error?: string }> }
  quality: { accountsWithoutGroup: number; accountsWithoutCnpj: number; casesWithoutGroup: number; accountsWithoutGroupRate: number; accountsWithoutCnpjRate: number; casesWithoutGroupRate: number }
  analytics: { lastSnapshot?: string; snapshotGroups: number; activeRule: { version: number; name: string; publishedAt: string; createdBy: string } }
  actionPlans: Array<{ status: string; total: number }>
}

const summary = ref<Summary | null>(null)
const groups = ref<GroupRow[]>([])
const total = ref(0)
const page = ref(1)
const pageSize = 25
const riskBand = ref('')
const search = ref('')
const appliedSearch = ref('')
const loading = ref(true)
const error = ref('')
const selected = ref<GroupDetail | null>(null)
const detailLoading = ref(false)
const evolution = ref<EvolutionItem[]>([])
const accounts = ref<AccountItem[]>([])
const taxonomies = ref<TaxonomyItem[]>([])
const calibrationOpen = ref(false)
const calibrationLoading = ref(false)
const publishing = ref(false)
const draftConfig = ref<ScoreConfiguration | null>(null)
const simulation = ref<Simulation | null>(null)
const justification = ref('')
const calibrationError = ref('')
const actionStatus = ref('not_started')
const actionResponsible = ref('')
const actionNotes = ref('')
const actionHistory = ref<ActionPlanHistory[]>([])
const actionSaving = ref(false)
const actionMessage = ref('')
const periods = ref<PeriodOption[]>([])
const selectedPeriod = ref('rolling30:')
const exporting = ref(false)
const operationsOpen = ref(false)
const operationsLoading = ref(false)
const operations = ref<OperationsOverview | null>(null)

const totalPages = computed(() => Math.max(1, Math.ceil(total.value / pageSize)))
const weightTotal = computed(() => draftConfig.value ? Object.values(draftConfig.value.weights).reduce((sum, value) => sum + Number(value), 0) : 0)
const periodLabel = computed(() => {
  if (!summary.value?.periodEndExclusive || !summary.value.periodStart) return 'Período indisponível'
  const end = new Date(`${summary.value.periodEndExclusive}T00:00:00`)
  end.setDate(end.getDate() - 1)
  const start = new Date(`${summary.value.periodStart}T00:00:00`)
  return `${formatDate(start)} — ${formatDate(end)}`
})

function periodParams() {
  const [snapshotKind, periodStart] = selectedPeriod.value.split(':')
  const params = new URLSearchParams({ snapshotKind })
  if (periodStart) params.set('periodStart', periodStart)
  return params
}

async function loadPeriods() {
  const response = await fetch('/api/v1/risk-score/periods')
  if (!response.ok) throw new Error('Períodos indisponíveis.')
  periods.value = await response.json()
  const rolling = periods.value.find(item => item.snapshotKind === 'rolling30')
  if (rolling) selectedPeriod.value = `rolling30:${rolling.periodStart}`
}

async function loadSummary() {
  const response = await fetch(`/api/v1/risk-score/summary?${periodParams()}`)
  if (!response.ok) throw new Error('Não foi possível carregar o resumo.')
  summary.value = await response.json()
}

async function loadGroups() {
  loading.value = true
  error.value = ''
  try {
    const params = periodParams()
    params.set('page', String(page.value)); params.set('pageSize', String(pageSize))
    if (riskBand.value) params.set('riskBand', riskBand.value)
    if (appliedSearch.value) params.set('search', appliedSearch.value)
    const response = await fetch(`/api/v1/risk-score/groups?${params}`)
    if (!response.ok) throw new Error('Não foi possível carregar o ranking.')
    const data = await response.json()
    groups.value = data.items ?? []
    total.value = data.total ?? 0
  } catch (reason) {
    error.value = reason instanceof Error ? reason.message : 'Erro inesperado.'
  } finally {
    loading.value = false
  }
}

async function changePeriod() {
  page.value = 1
  selected.value = null
  await Promise.all([loadSummary(), loadGroups()])
}

async function exportRanking() {
  exporting.value = true
  try {
    const params = periodParams()
    if (riskBand.value) params.set('riskBand', riskBand.value)
    if (appliedSearch.value) params.set('search', appliedSearch.value)
    const response = await fetch(`/api/v1/risk-score/groups/export?${params}`)
    if (!response.ok) throw new Error('Exportação indisponível.')
    const blob = await response.blob()
    const url = URL.createObjectURL(blob)
    const link = document.createElement('a')
    link.href = url; link.download = `healthscore-farma-${summary.value?.periodStart ?? 'ranking'}.csv`; link.click()
    URL.revokeObjectURL(url)
  } catch (reason) {
    error.value = reason instanceof Error ? reason.message : 'Erro inesperado.'
  } finally {
    exporting.value = false
  }
}

async function openOperations() {
  operationsOpen.value = true
  operationsLoading.value = true
  try {
    const response = await fetch('/api/v1/operations/overview')
    if (!response.ok) throw new Error('Visão operacional indisponível.')
    operations.value = await response.json()
  } catch (reason) {
    error.value = reason instanceof Error ? reason.message : 'Erro inesperado.'
  } finally {
    operationsLoading.value = false
  }
}

function runStatusLabel(status: string) {
  return ({ succeeded: 'Concluída', running: 'Em execução', failed: 'Falhou', interrupted: 'Interrompida' } as Record<string, string>)[status] ?? status
}

function applySearch() {
  page.value = 1
  appliedSearch.value = search.value.trim()
  loadGroups()
}

async function openDetail(id: number) {
  detailLoading.value = true
  selected.value = null
  evolution.value = []
  accounts.value = []
  taxonomies.value = []
  actionStatus.value = 'not_started'
  actionResponsible.value = ''
  actionNotes.value = ''
  actionHistory.value = []
  actionMessage.value = ''
  try {
    const [detailResponse, evolutionResponse, accountsResponse, taxonomyResponse, actionResponse] = await Promise.all([
      fetch(`/api/v1/risk-score/groups/${id}`), fetch(`/api/v1/risk-score/groups/${id}/evolution`),
      fetch(`/api/v1/risk-score/groups/${id}/accounts`), fetch(`/api/v1/risk-score/groups/${id}/taxonomy`),
      fetch(`/api/v1/risk-score/groups/${id}/action-plan`)
    ])
    if (!detailResponse.ok) throw new Error('Detalhe indisponível.')
    selected.value = await detailResponse.json()
    if (evolutionResponse.ok) evolution.value = (await evolutionResponse.json()).items ?? []
    if (accountsResponse.ok) accounts.value = (await accountsResponse.json()).items ?? []
    if (taxonomyResponse.ok) taxonomies.value = (await taxonomyResponse.json()).items ?? []
    if (actionResponse.ok) {
      const actionData = await actionResponse.json()
      actionStatus.value = actionData.plan?.status ?? 'not_started'
      actionResponsible.value = actionData.plan?.responsible ?? ''
      actionNotes.value = actionData.plan?.notes ?? ''
      actionHistory.value = actionData.history ?? []
    }
  } finally {
    detailLoading.value = false
  }
}

async function saveActionPlan() {
  if (!selected.value) return
  actionSaving.value = true
  actionMessage.value = ''
  try {
    const response = await fetch(`/api/v1/risk-score/groups/${selected.value.id}/action-plan`, {
      method: 'PUT', headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ status: actionStatus.value, responsible: actionResponsible.value, notes: actionNotes.value, changedBy: 'local-user' })
    })
    if (!response.ok) throw new Error('Não foi possível salvar a tratativa.')
    actionMessage.value = 'Tratativa salva e registrada no histórico.'
    const refreshed = await fetch(`/api/v1/risk-score/groups/${selected.value.id}/action-plan`)
    if (refreshed.ok) actionHistory.value = (await refreshed.json()).history ?? []
  } catch (reason) {
    actionMessage.value = reason instanceof Error ? reason.message : 'Erro inesperado.'
  } finally {
    actionSaving.value = false
  }
}

function changePage(value: number) {
  page.value = Math.min(Math.max(value, 1), totalPages.value)
  loadGroups()
  window.scrollTo({ top: 300, behavior: 'smooth' })
}

function scoreClass(score: number) {
  if (score >= 70) return 'critical'
  if (score >= 50) return 'high'
  if (score >= 30) return 'attention'
  return 'low'
}

function pct(value: number) {
  return `${(value * 100).toLocaleString('pt-BR', { maximumFractionDigits: 1 })}%`
}

function decimal(value: number) {
  return value.toLocaleString('pt-BR', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
}

function integer(value: number) {
  return value.toLocaleString('pt-BR')
}

function formatDate(value: Date) {
  return value.toLocaleDateString('pt-BR', { day: '2-digit', month: 'short', year: 'numeric' })
}

async function openCalibration() {
  calibrationOpen.value = true
  calibrationLoading.value = true
  calibrationError.value = ''
  simulation.value = null
  try {
    const response = await fetch('/api/v1/score-config')
    if (!response.ok) throw new Error('Configuração indisponível.')
    const versions = await response.json()
    const active = versions.find((item: { status: string }) => item.status === 'published')
    draftConfig.value = JSON.parse(JSON.stringify(active.configuration))
  } catch (reason) {
    calibrationError.value = reason instanceof Error ? reason.message : 'Erro inesperado.'
  } finally {
    calibrationLoading.value = false
  }
}

async function simulateCalibration() {
  if (!draftConfig.value) return
  calibrationError.value = ''
  const response = await fetch('/api/v1/score-config/simulate', {
    method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(draftConfig.value)
  })
  const data = await response.json()
  if (!response.ok) { calibrationError.value = data.error ?? 'Simulação inválida.'; return }
  simulation.value = data
}

async function publishCalibration() {
  if (!draftConfig.value || !justification.value.trim()) return
  publishing.value = true
  calibrationError.value = ''
  try {
    const response = await fetch('/api/v1/score-config/publish', {
      method: 'POST', headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ name: `Calibragem ${new Date().toLocaleDateString('pt-BR')}`, createdBy: 'local-admin', justification: justification.value, configuration: draftConfig.value })
    })
    const data = await response.json()
    if (!response.ok) throw new Error(data.error ?? 'Não foi possível publicar.')
    calibrationOpen.value = false
    justification.value = ''
    await Promise.all([loadSummary(), loadGroups()])
  } catch (reason) {
    calibrationError.value = reason instanceof Error ? reason.message : 'Erro inesperado.'
  } finally {
    publishing.value = false
  }
}

watch(riskBand, () => {
  page.value = 1
  loadGroups()
})

onMounted(async () => {
  try {
    await loadPeriods()
    await Promise.all([loadSummary(), loadGroups()])
  } catch (reason) {
    error.value = reason instanceof Error ? reason.message : 'Erro inesperado.'
  }
})
</script>

<template>
  <div class="app-shell">
    <header class="topbar">
      <div class="brand-mark">HS</div>
      <div class="brand-copy">
        <strong>HealthScore</strong>
        <span>Inteligência operacional</span>
      </div>
      <button class="operations-link" @click="openOperations">Operação</button>
      <button class="calibration-link" @click="openCalibration">Calibragem</button>
      <div class="scope-pill"><span></span> Vertical FARMA</div>
    </header>

    <main>
      <section class="page-heading">
        <div>
          <p class="eyebrow">RADAR DE RISCO OPERACIONAL</p>
          <h1>Score de Atrito</h1>
          <p class="subtitle">Priorize grupos econômicos com maior desgaste em suporte e produto.</p>
        </div>
        <div class="period-card">
          <span>Janela analisada</span>
          <strong>{{ periodLabel }}</strong>
          <select v-model="selectedPeriod" aria-label="Período analisado" @change="changePeriod">
            <option v-for="period in periods" :key="`${period.snapshotKind}:${period.periodStart}`" :value="`${period.snapshotKind}:${period.periodStart}`">
              {{ period.snapshotKind === 'rolling30' ? 'Últimos 30 dias' : new Date(`${period.periodStart}T00:00:00`).toLocaleDateString('pt-BR', { month: 'long', year: 'numeric' }) }}
            </option>
          </select>
          <small>Atualização automática a cada hora</small>
        </div>
      </section>

      <section v-if="summary" class="metric-grid" aria-label="Resumo executivo">
        <article class="metric-card danger">
          <span>Grupos críticos</span>
          <strong>{{ integer(summary.criticalGroups) }}</strong>
          <small>{{ pct(summary.criticalGroups / Math.max(summary.totalGroups, 1)) }} dos grupos analisados</small>
        </article>
        <article class="metric-card warning">
          <span>Alto risco</span>
          <strong>{{ integer(summary.highGroups) }}</strong>
          <small>Requer acompanhamento preventivo</small>
        </article>
        <article class="metric-card neutral">
          <span>Score médio</span>
          <strong>{{ decimal(summary.averageScore) }}</strong>
          <small>Base: {{ integer(summary.totalGroups) }} grupos com chamados</small>
        </article>
        <article class="metric-card accent">
          <span>Chamados em críticos</span>
          <strong>{{ integer(summary.criticalCases) }}</strong>
          <small>{{ pct(summary.criticalCaseShare) }} da demanda do período</small>
        </article>
      </section>

      <section class="ranking-card">
        <div class="section-heading">
          <div>
            <p class="eyebrow">PRIORIZAÇÃO</p>
            <h2>Ranking de grupos econômicos</h2>
          </div>
          <div class="section-actions"><span class="result-count">{{ integer(total) }} resultados</span><button @click="exportRanking">{{ exporting ? 'Exportando…' : 'Exportar CSV' }}</button></div>
        </div>

        <div class="filters">
          <form class="search" @submit.prevent="applySearch">
            <span aria-hidden="true">⌕</span>
            <input v-model="search" type="search" placeholder="Buscar grupo econômico" aria-label="Buscar grupo econômico" />
            <button type="submit">Buscar</button>
          </form>
          <label>
            <span>Faixa de risco</span>
            <select v-model="riskBand">
              <option value="">Todas as faixas</option>
              <option>Crítico</option>
              <option>Alto</option>
              <option>Atenção</option>
              <option>Baixo</option>
            </select>
          </label>
        </div>

        <div v-if="error" class="error-state">{{ error }}</div>
        <div class="table-wrap">
          <table>
            <thead>
              <tr>
                <th>Grupo econômico</th><th>Score</th><th>Faixa</th><th>Principal motivo</th>
                <th>Chamados</th><th>Densidade</th><th>SLA violado</th><th>FCR</th><th></th>
              </tr>
            </thead>
            <tbody>
              <tr v-if="loading" v-for="index in 6" :key="index" class="skeleton-row"><td colspan="9"><span></span></td></tr>
              <tr v-else v-for="group in groups" :key="group.id">
                <td><strong>{{ group.economicGroup }}</strong><small>{{ group.activeStores }} lojas ativas</small></td>
                <td><span class="score" :class="scoreClass(group.score)">{{ group.score }}</span></td>
                <td><span class="band" :class="scoreClass(group.score)">{{ group.riskBand }}</span></td>
                <td>{{ group.mainReason }}</td><td>{{ integer(group.totalCases) }}</td>
                <td>{{ decimal(group.densityVsAverage) }}×</td><td>{{ pct(group.slaViolatedRate) }}</td><td>{{ pct(group.fcrRate) }}</td>
                <td><button class="detail-button" @click="openDetail(group.id)" aria-label="Abrir detalhe">→</button></td>
              </tr>
              <tr v-if="!loading && groups.length === 0"><td colspan="9" class="empty">Nenhum grupo encontrado.</td></tr>
            </tbody>
          </table>
        </div>

        <footer class="pagination">
          <span>Página {{ page }} de {{ totalPages }}</span>
          <div><button :disabled="page === 1" @click="changePage(page - 1)">Anterior</button><button :disabled="page === totalPages" @click="changePage(page + 1)">Próxima</button></div>
        </footer>
      </section>
    </main>

    <div v-if="detailLoading || selected" class="drawer-backdrop" @click.self="selected = null">
      <aside class="drawer" aria-live="polite">
        <div v-if="detailLoading" class="drawer-loading">Carregando diagnóstico…</div>
        <template v-else-if="selected">
          <button class="close" @click="selected = null" aria-label="Fechar">×</button>
          <p class="eyebrow">DIAGNÓSTICO DO GRUPO</p>
          <h2>{{ selected.economicGroup }}</h2>
          <div class="drawer-score"><span class="score large" :class="scoreClass(selected.score)">{{ selected.score }}</span><div><strong>{{ selected.riskBand }}</strong><small>Principal motivo: {{ selected.mainReason }}</small></div></div>
          <div class="action-callout"><span>Ação sugerida</span><p>{{ selected.suggestedAction }}</p></div>
          <div class="action-plan-form">
            <div class="action-plan-heading"><h3>Tratativa operacional</h3><span v-if="actionHistory.length">{{ actionHistory.length }} alterações</span></div>
            <div class="action-plan-fields">
              <label>Status<select v-model="actionStatus"><option value="not_started">Não iniciada</option><option value="in_progress">Em andamento</option><option value="blocked">Bloqueada</option><option value="completed">Concluída</option></select></label>
              <label>Responsável<input v-model="actionResponsible" placeholder="Nome ou equipe" /></label>
            </div>
            <label>Observações<textarea v-model="actionNotes" maxlength="4000" placeholder="Contexto, próximos passos e acordos"></textarea></label>
            <div class="action-plan-footer"><small>{{ actionMessage }}</small><button :disabled="actionSaving" @click="saveActionPlan">{{ actionSaving ? 'Salvando…' : 'Salvar tratativa' }}</button></div>
          </div>
          <h3>Composição do score</h3>
          <div class="factors">
            <div v-for="factor in selected.factors" :key="factor.name" class="factor">
              <div><span>{{ factor.name }}</span><strong>{{ factor.points }} / {{ factor.maximum }}</strong></div>
              <div class="track"><span :style="{ width: `${(factor.points / factor.maximum) * 100}%` }"></span></div>
            </div>
          </div>
          <h3>Indicadores</h3>
          <div class="indicator-grid">
            <div><span>Chamados</span><strong>{{ integer(selected.metrics.totalCases) }}</strong></div>
            <div><span>Lojas ativas</span><strong>{{ integer(selected.metrics.activeStores) }}</strong></div>
            <div><span>Densidade vs. média</span><strong>{{ decimal(selected.metrics.densityVsAverage) }}×</strong></div>
            <div><span>Recorrência</span><strong>{{ pct(selected.metrics.recurrenceRate) }}</strong></div>
            <div><span>Issue/JIRA</span><strong>{{ pct(selected.metrics.issueRate) }}</strong></div>
            <div><span>Criticidade</span><strong>{{ pct(selected.metrics.criticalRate) }}</strong></div>
          </div>
          <template v-if="evolution.length">
            <h3>Evolução mensal</h3>
            <div class="evolution">
              <div v-for="item in evolution" :key="item.periodStart" class="month-bar">
                <strong>{{ new Date(`${item.periodStart}T00:00:00`).toLocaleDateString('pt-BR', { month: 'short' }) }}</strong>
                <div><span :style="{ height: `${Math.max(item.score, 4)}%` }" :class="scoreClass(item.score)"></span></div>
                <small>{{ item.score }}</small>
              </div>
            </div>
          </template>
          <template v-if="taxonomies.length">
            <h3>Principais ofensores</h3>
            <div class="detail-list">
              <div v-for="item in taxonomies.slice(0, 5)" :key="item.taxonomy">
                <span><strong>{{ item.taxonomy }}</strong><small>{{ pct(item.recurrenceRate) }} recorrentes</small></span>
                <b>{{ item.totalCases }}</b>
              </div>
            </div>
          </template>
          <template v-if="accounts.length">
            <h3>Contas que mais acionaram</h3>
            <div class="detail-list">
              <div v-for="item in accounts.slice(0, 5)" :key="item.accountId">
                <span><strong>{{ item.name }}</strong><small>{{ item.cnpj || 'CNPJ não informado' }}</small></span>
                <b>{{ item.totalCases }}</b>
              </div>
            </div>
          </template>
          <p class="audit-note">Regra v{{ selected.scoreRuleVersionId }} · calculado em {{ new Date(selected.calculatedAt).toLocaleString('pt-BR') }}</p>
        </template>
      </aside>
    </div>

    <div v-if="calibrationOpen" class="modal-backdrop" @click.self="calibrationOpen = false">
      <section class="calibration-modal">
        <button class="close" @click="calibrationOpen = false" aria-label="Fechar calibragem">×</button>
        <p class="eyebrow">GOVERNANÇA DO SCORE</p>
        <h2>Calibragem de pesos e faixas</h2>
        <p class="modal-intro">Simule o impacto na carteira antes de publicar uma nova versão da regra.</p>
        <div v-if="calibrationLoading" class="drawer-loading">Carregando configuração…</div>
        <template v-else-if="draftConfig">
          <div class="calibration-layout">
            <div>
              <div class="calibration-title"><h3>Pesos dos fatores</h3><span :class="{ invalid: weightTotal !== 100 }">Total {{ weightTotal }}</span></div>
              <div class="weight-grid">
                <label>Densidade<input v-model.number="draftConfig.weights.density" type="number" min="0" max="100" /></label>
                <label>Crescimento<input v-model.number="draftConfig.weights.growth" type="number" min="0" max="100" /></label>
                <label>SLA<input v-model.number="draftConfig.weights.sla" type="number" min="0" max="100" /></label>
                <label>FCR<input v-model.number="draftConfig.weights.fcr" type="number" min="0" max="100" /></label>
                <label>Criticidade<input v-model.number="draftConfig.weights.criticality" type="number" min="0" max="100" /></label>
                <label>Issue/JIRA<input v-model.number="draftConfig.weights.issue" type="number" min="0" max="100" /></label>
                <label>Recorrência<input v-model.number="draftConfig.weights.recurrence" type="number" min="0" max="100" /></label>
              </div>
              <h3>Limites das faixas</h3>
              <div class="band-grid">
                <label>Baixo até<input v-model.number="draftConfig.bands.lowMax" type="number" /></label>
                <label>Atenção até<input v-model.number="draftConfig.bands.attentionMax" type="number" /></label>
                <label>Alto até<input v-model.number="draftConfig.bands.highMax" type="number" /></label>
              </div>
            </div>
            <div class="simulation-panel">
              <h3>Impacto simulado</h3>
              <div v-if="simulation" class="simulation-results">
                <div><span>Score médio atual</span><strong>{{ decimal(simulation.currentAverage) }}</strong></div>
                <div><span>Score médio simulado</span><strong>{{ decimal(simulation.simulatedAverage) }}</strong></div>
                <div><span>Grupos que mudam de faixa</span><strong>{{ integer(simulation.changedBands) }}</strong></div>
                <div class="distribution"><span v-for="(value, key) in simulation.distribution" :key="key"><b>{{ key }}</b>{{ value }}</span></div>
              </div>
              <p v-else>Altere os parâmetros e execute uma simulação para comparar o impacto.</p>
              <button class="secondary-action" :disabled="weightTotal !== 100" @click="simulateCalibration">Simular impacto</button>
            </div>
          </div>
          <div v-if="calibrationError" class="calibration-error">{{ calibrationError }}</div>
          <div class="publish-row">
            <label><span>Justificativa da alteração</span><textarea v-model="justification" placeholder="Descreva o motivo e o resultado esperado"></textarea></label>
            <button :disabled="!simulation || !justification.trim() || publishing" @click="publishCalibration">{{ publishing ? 'Publicando…' : 'Publicar nova versão' }}</button>
          </div>
        </template>
      </section>
    </div>

    <div v-if="operationsOpen" class="modal-backdrop" @click.self="operationsOpen = false">
      <section class="operations-modal">
        <button class="close" @click="operationsOpen = false" aria-label="Fechar operação">×</button>
        <p class="eyebrow">OPERAÇÃO E QUALIDADE</p>
        <h2>Saúde do pipeline</h2>
        <p class="modal-intro">Sincronização, materialização e completude dos dados FARMA.</p>
        <div v-if="operationsLoading" class="operations-loading">Carregando indicadores…</div>
        <template v-else-if="operations">
          <div class="operations-grid">
            <div><span>Contas persistidas</span><strong>{{ integer(operations.ingestion.accounts) }}</strong></div>
            <div><span>Chamados persistidos</span><strong>{{ integer(operations.ingestion.cases) }}</strong></div>
            <div><span>Grupos no snapshot</span><strong>{{ integer(operations.analytics.snapshotGroups) }}</strong></div>
            <div><span>Regra ativa</span><strong>v{{ operations.analytics.activeRule.version }}</strong></div>
          </div>
          <h3>Qualidade dos dados</h3>
          <div class="quality-list">
            <div><span>Contas sem grupo econômico</span><strong>{{ integer(operations.quality.accountsWithoutGroup) }}</strong><small>{{ pct(operations.quality.accountsWithoutGroupRate) }}</small></div>
            <div><span>Contas sem CNPJ</span><strong>{{ integer(operations.quality.accountsWithoutCnpj) }}</strong><small>{{ pct(operations.quality.accountsWithoutCnpjRate) }}</small></div>
            <div><span>Chamados sem grupo</span><strong>{{ integer(operations.quality.casesWithoutGroup) }}</strong><small>{{ pct(operations.quality.casesWithoutGroupRate) }}</small></div>
          </div>
          <h3>Últimas sincronizações</h3>
          <div class="run-list">
            <div v-for="run in operations.ingestion.lastRuns" :key="run.id">
              <span><strong>{{ run.entityName }}</strong><small>{{ new Date(run.startedAt).toLocaleString('pt-BR') }}</small></span>
              <b :class="run.status">{{ runStatusLabel(run.status) }}</b>
              <em>{{ integer(run.recordsWritten) }} registros</em>
            </div>
          </div>
          <p class="operations-footnote">Snapshot atualizado em {{ operations.analytics.lastSnapshot ? new Date(operations.analytics.lastSnapshot).toLocaleString('pt-BR') : '—' }} · regra publicada por {{ operations.analytics.activeRule.createdBy }}</p>
        </template>
      </section>
    </div>
  </div>
</template>
