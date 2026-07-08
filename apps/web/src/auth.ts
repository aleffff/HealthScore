import { UserManager, WebStorageStateStore, type User } from 'oidc-client-ts'

export type AuthConfig = {
  mode: 'local' | 'oidc'
  authority?: string
  clientId?: string
  scope?: string
}

let manager: UserManager | null = null
let currentUser: User | null = null

export async function initializeAuth(): Promise<AuthConfig> {
  const response = await window.fetch('/api/v1/auth/config')
  if (!response.ok) throw new Error('Configuração de autenticação indisponível.')
  const config = await response.json() as AuthConfig
  if (config.mode !== 'oidc') return config
  if (!config.authority || !config.clientId) throw new Error('Configuração OIDC incompleta.')

  manager = new UserManager({
    authority: config.authority,
    client_id: config.clientId,
    redirect_uri: `${window.location.origin}/auth/callback`,
    post_logout_redirect_uri: window.location.origin,
    response_type: 'code',
    scope: config.scope ?? 'openid profile email',
    userStore: new WebStorageStateStore({ store: window.sessionStorage }),
    automaticSilentRenew: true,
    monitorSession: true,
  })

  if (window.location.pathname === '/auth/callback') {
    currentUser = await manager.signinRedirectCallback()
    window.history.replaceState({}, document.title, '/')
  } else {
    currentUser = await manager.getUser()
  }
  manager.events.addUserLoaded(user => { currentUser = user })
  manager.events.addUserUnloaded(() => { currentUser = null })
  return config
}

export function isAuthenticated() {
  return manager === null || Boolean(currentUser && !currentUser.expired)
}

export function authenticatedUser() {
  if (!currentUser) return null
  const profile = currentUser.profile
  return { name: String(profile.name ?? profile.preferred_username ?? profile.email ?? profile.sub) }
}

export async function login() {
  if (manager) await manager.signinRedirect({ state: window.location.pathname + window.location.search })
}

export async function logout() {
  if (manager) await manager.signoutRedirect()
}

export async function apiFetch(input: RequestInfo | URL, init: RequestInit = {}) {
  const headers = new Headers(init.headers)
  if (manager) {
    currentUser = await manager.getUser()
    if (!currentUser || currentUser.expired) {
      await login()
      throw new Error('Autenticação necessária.')
    }
    headers.set('Authorization', `Bearer ${currentUser.access_token}`)
  }
  const response = await window.fetch(input, { ...init, headers })
  if (response.status === 401 && manager) {
    await login()
    throw new Error('Sessão expirada.')
  }
  return response
}
