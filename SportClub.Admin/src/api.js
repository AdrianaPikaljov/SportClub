// All communication with the backend goes through the Web API.
const BASE = import.meta.env.VITE_API_URL || 'http://localhost:5080'

let token = localStorage.getItem('token')

/** Salvestab tokeni ja kasutaja (POST /api/auth/login vastus: token, role, name). */
export function setSession(login) {
  token = login?.token || null
  if (login) localStorage.setItem('token', login.token)
  else localStorage.removeItem('token')
  if (login) localStorage.setItem('user', JSON.stringify({ name: login.name, role: login.role }))
  else localStorage.removeItem('user')
}

export function getSavedUser() {
  try {
    return token ? JSON.parse(localStorage.getItem('user')) : null
  } catch {
    return null
  }
}

/** Error with the ProblemDetails body from the API. */
export class ApiError extends Error {
  constructor(problem, status) {
    const fieldErrors = problem?.errors ? Object.values(problem.errors).flat().join(' ') : ''
    super(problem?.detail || fieldErrors || problem?.title || `Viga ${status}`)
    this.status = status
    this.problem = problem
  }
}

export async function api(path, { method = 'GET', body, query } = {}) {
  const url = new URL(path, BASE)
  if (query) {
    Object.entries(query).forEach(([k, v]) => {
      if (v !== undefined && v !== null && v !== '') url.searchParams.set(k, v)
    })
  }
  let res
  try {
    res = await fetch(url, {
      method,
      headers: {
        'Content-Type': 'application/json',
        ...(token ? { Authorization: `Bearer ${token}` } : {})
      },
      body: body ? JSON.stringify(body) : undefined
    })
  } catch {
    // Võrguviga: API ei tööta või CORS blokeeris päringu.
    throw new ApiError({ detail: `API-ga ei saa ühendust (${BASE}). Kas SportClub.Api töötab?` }, 0)
  }
  if (res.status === 401 && path !== '/api/auth/login') {
    setSession(null)
    window.dispatchEvent(new Event('logout'))
  }
  if (res.status === 204) return null
  const text = await res.text()
  const data = text ? JSON.parse(text) : null
  if (!res.ok) throw new ApiError(data, res.status)
  return data
}

// API annab ajad Eesti aja järgi tekstina: "2026-11-27 18:00", kuupäevad "2026-11-27".
export function formatDateTime(text) {
  if (!text) return ''
  const [date, time] = text.split(' ')
  const [y, m, d] = date.split('-')
  const weekday = new Date(Number(y), Number(m) - 1, Number(d)).toLocaleDateString('et-EE', { weekday: 'short' })
  return `${weekday} ${d}.${m}.${y}${time ? ' ' + time : ''}`
}

export function formatDate(text) {
  if (!text) return ''
  const [y, m, d] = text.split('-')
  return `${d}.${m}.${y}`
}

/** Tänane kuupäev kujul AAAA-KK-PP. */
export function today() {
  const d = new Date()
  const pad = (n) => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`
}
