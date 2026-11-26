import { useState } from 'react'
import { api, formatDate } from '../api.js'
import { ErrorBox, useDebounced, useList } from '../components/ui.jsx'

/** Kõik ostetud kuukaardid (GET /api/passes?search=). */
export default function Passes() {
  const [searchText, setSearchText] = useState('')
  const search = useDebounced(searchText)
  const { items, error, loading } = useList(() => api('/api/passes', { query: { search } }), [search])

  return (
    <section>
      <header className="page-head"><h1>Kuukaardid</h1></header>
      <p className="muted">Kliendid ostavad kuukaardi ise mobiilirakenduses. Korraga saab olla üks aktiivne kaart.</p>
      <div className="filters">
        <input placeholder="Otsi kliendi nime järgi" value={searchText} onChange={(e) => setSearchText(e.target.value)} />
      </div>
      <ErrorBox error={error} />
      <div className="table-wrap">
        <table className={loading ? 'loading' : ''}>
          <thead>
            <tr>
              <th>Klient</th>
              <th>Tariif</th>
              <th>Kehtib</th>
              <th>Külastusi</th>
              <th>Hind</th>
              <th>Staatus</th>
            </tr>
          </thead>
          <tbody>
            {items.map((p) => (
              <tr key={p.id}>
                <td><strong>{p.client}</strong><div className="muted">{p.email}</div></td>
                <td>{p.plan}</td>
                <td>{formatDate(p.validFrom)} – {formatDate(p.validUntil)}</td>
                <td>{p.visitsLeft} / {p.visitsTotal} alles</td>
                <td>{Number(p.pricePaid).toFixed(2)} €</td>
                <td><span className={p.active ? 'badge ok' : 'badge full'}>{p.active ? 'Aktiivne' : 'Ei kehti'}</span></td>
              </tr>
            ))}
            {!items.length && !loading && <tr><td colSpan="6" className="muted center">Kuukaarte ei leitud.</td></tr>}
          </tbody>
        </table>
      </div>
    </section>
  )
}
