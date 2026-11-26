import { useEffect, useState } from 'react'
import { api } from '../api.js'
import { ErrorBox } from '../components/ui.jsx'

export default function Stats() {
  const [from, setFrom] = useState('')
  const [to, setTo] = useState('')
  const [rows, setRows] = useState([])
  const [error, setError] = useState('')

  useEffect(() => {
    // Admin näeb kogu klubi statistikat (treener ainult oma treeninguid).
    api('/api/stats/popular', { query: { from, to } })
      .then((d) => { setRows(d); setError('') })
      .catch((e) => setError(e.message))
  }, [from, to])

  const max = Math.max(1, ...rows.map((r) => r.bookings))
  const totalBookings = rows.reduce((s, r) => s + r.bookings, 0)
  const totalAttended = rows.reduce((s, r) => s + r.attended, 0)
  const avgFill = rows.length ? Math.round(rows.reduce((s, r) => s + r.fillRate, 0) / rows.length) : 0

  return (
    <section>
      <header className="page-head"><h1>Populaarseimad treeningud</h1></header>
      <div className="filters">
        <label>Alates <input type="date" value={from} onChange={(e) => setFrom(e.target.value)} /></label>
        <label>Kuni <input type="date" value={to} onChange={(e) => setTo(e.target.value)} /></label>
      </div>
      <ErrorBox error={error} />
      <div className="kpis">
        <div className="kpi dark"><strong>{totalBookings}</strong><span>broneeringut</span></div>
        <div className="kpi"><strong>{totalAttended}</strong><span>käis kohal</span></div>
        <div className="kpi"><strong>{avgFill}%</strong><span>keskmine täituvus</span></div>
      </div>
      <div className="card">
        {rows.map((r, i) => (
          <div className="bar-row" key={r.type}>
            <div className="bar-label">
              <span><strong>{i + 1}. {r.type}</strong></span>
              <span className="muted">{r.bookings} broneeringut · {r.sessions} treeningut · kohal {r.attended} · täituvus {r.fillRate}%</span>
            </div>
            <div className="bar"><div className={i === 0 ? 'bar-fill top' : 'bar-fill'} style={{ width: `${(100 * r.bookings) / max}%` }} /></div>
          </div>
        ))}
        {!rows.length && <p className="muted">Andmed puuduvad.</p>}
      </div>
    </section>
  )
}
