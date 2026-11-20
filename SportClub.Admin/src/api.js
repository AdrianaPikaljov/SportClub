// All communication with the backend goes through the Web API.
const BASE = import.meta.env.VITE_API_URL || 'http://localhost:5080'

let token = localStorage.getItem('token')

export function setToken(t) {
  token = t
  if (t) localStorage.setItem('token', t)
  else localStorage.removeItem('token')
}

export function hasToken() {
  return !!token
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
  const res = await fetch(url, {
    method,
    headers: {
      'Content-Type': 'application/json',
      ...(token ? { Authorization: `Bearer ${token}` } : {})
    },
    body: body ? JSON.stringify(body) : undefined
  })
  if (res.status === 401 && path !== '/api/auth/login') {
    setToken(null)
    window.dispatchEvent(new Event('logout'))
  }
  if (res.status === 204) return null
  const text = await res.text()
  const data = text ? JSON.parse(text) : null
  if (!res.ok) throw new ApiError(data, res.status)
  return data
}

// Local (Tallinn) <-> UTC helpers for datetime-local inputs.
export function toLocalInput(utcIso) {
  const d = new Date(utcIso)
  const pad = (n) => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`
}

export function fromLocalInput(local) {
  return new Date(local).toISOString()
}

export function formatDateTime(utcIso) {
  return new Date(utcIso).toLocaleString('et-EE', {
    weekday: 'short', day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit'
  })
}

export function formatDate(utcIso) {
  return new Date(utcIso).toLocaleDateString('et-EE')
}
