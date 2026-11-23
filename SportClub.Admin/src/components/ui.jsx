import { useEffect, useState } from 'react'

export function Pager({ page, totalPages, totalCount, onPage }) {
  if (!totalCount) return null
  return (
    <div className="pager">
      <span>Kokku {totalCount}</span>
      <button className="btn ghost small" disabled={page <= 1} onClick={() => onPage(page - 1)}>‹ Eelmine</button>
      <span>{page} / {Math.max(1, totalPages)}</span>
      <button className="btn ghost small" disabled={page >= totalPages} onClick={() => onPage(page + 1)}>Järgmine ›</button>
    </div>
  )
}

export function Modal({ title, children, onClose }) {
  useEffect(() => {
    const onKey = (e) => e.key === 'Escape' && onClose()
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [onClose])
  return (
    <div className="modal-backdrop" onMouseDown={onClose}>
      <div className="modal" role="dialog" aria-label={title} onMouseDown={(e) => e.stopPropagation()}>
        <div className="modal-head">
          <h2>{title}</h2>
          <button className="btn ghost small" onClick={onClose} aria-label="Sulge">✕</button>
        </div>
        {children}
      </div>
    </div>
  )
}

export function Field({ label, error, children }) {
  return (
    <label className="field">
      <span>{label}</span>
      {children}
      {error && <small className="field-error">{error}</small>}
    </label>
  )
}

export function ErrorBox({ error }) {
  if (!error) return null
  return <div className="error-box">{error}</div>
}

export function SortHeader({ field, label, sort, onSort }) {
  const active = sort === field || sort === `-${field}`
  const desc = sort === `-${field}`
  return (
    <th>
      <button className="sort" onClick={() => onSort(active && !desc ? `-${field}` : field)}>
        {label} {active ? (desc ? '↓' : '↑') : ''}
      </button>
    </th>
  )
}

/** Debounced value for search inputs. */
export function useDebounced(value, delay = 300) {
  const [v, setV] = useState(value)
  useEffect(() => {
    const t = setTimeout(() => setV(value), delay)
    return () => clearTimeout(t)
  }, [value, delay])
  return v
}

/** Loads a paged list from the API whenever the query changes. */
export function usePagedList(loader, deps) {
  const [data, setData] = useState({ items: [], page: 1, totalPages: 0, totalCount: 0 })
  const [error, setError] = useState('')
  const [loading, setLoading] = useState(false)
  const [reloadKey, setReloadKey] = useState(0)

  useEffect(() => {
    let cancelled = false
    setLoading(true)
    loader()
      .then((d) => { if (!cancelled) { setData(d); setError('') } })
      .catch((e) => { if (!cancelled) setError(e.message) })
      .finally(() => { if (!cancelled) setLoading(false) })
    return () => { cancelled = true }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [...deps, reloadKey])

  return { data, error, loading, reload: () => setReloadKey((k) => k + 1) }
}
