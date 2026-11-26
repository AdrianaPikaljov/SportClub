import { useState } from 'react'
import { api, formatDateTime } from '../api.js'
import { ErrorBox, Pager, useDebounced, usePagedList } from '../components/ui.jsx'

/** Kõik broneeringud: otsing, filter, sorteerimine ja leheküljed (GET /api/bookings). */
export default function Bookings() {
  const [searchText, setSearchText] = useState('')
  const [status, setStatus] = useState('')
  const [sort, setSort] = useState('Uusimad')
  const [page, setPage] = useState(1)
  const [info, setInfo] = useState('')
  const search = useDebounced(searchText)

  const { data, error, loading, reload } = usePagedList(
    () => api('/api/bookings', { query: { search, status, sort, page } }),
    [search, status, sort, page]
  )

  async function cancel(b) {
    const what = b.status === 'Waitlisted' ? 'eemaldada ootejärjekorrast' : 'tühistada broneering'
    if (!window.confirm(`${b.client}: ${what} (${b.type} ${formatDateTime(b.startsAt)})?`)) return
    try {
      await api(`/api/bookings/${b.id}`, { method: 'DELETE' })
      reload()
    } catch (e) {
      window.alert(e.message)
    }
  }

  async function promote() {
    try {
      const res = await api('/api/waitlist/promote', { method: 'POST' })
      const names = res.promoted.map((p) => p.name).join(', ')
      setInfo(res.promoted.length
        ? `Järjekorrast viidi treeningule: ${names}.`
        : 'Kedagi polnud järjekorrast viia.' + (res.removed ? ` Järjekorrast eemaldati ${res.removed}.` : ''))
      reload()
    } catch (e) {
      setInfo(e.message)
    }
  }

  const setFilter = (setter) => (e) => { setter(e.target.value); setPage(1) }

  return (
    <section>
      <header className="page-head">
        <h1>Broneeringud</h1>
        <button className="btn ghost" onClick={promote}>Vii järjekorrast kohtadele</button>
      </header>
      {info && <p className="muted">{info}</p>}
      <div className="filters">
        <input placeholder="Otsi kliendi nime järgi" value={searchText} onChange={setFilter(setSearchText)} />
        <select value={status} onChange={setFilter(setStatus)}>
          <option value="">Kõik</option>
          <option value="Booked">Registreeritud</option>
          <option value="Waitlisted">Ootejärjekorras</option>
        </select>
        <select value={sort} onChange={setFilter(setSort)}>
          <option value="Uusimad">Uusimad enne</option>
          <option value="Vanimad">Vanimad enne</option>
        </select>
      </div>
      <ErrorBox error={error} />
      <div className="table-wrap">
        <table className={loading ? 'loading' : ''}>
          <thead>
            <tr>
              <th>Klient</th>
              <th>Treening</th>
              <th>Aeg</th>
              <th>Staatus</th>
              <th>Kohalolu</th>
              <th>Loodud</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {data.items.map((b) => (
              <tr key={b.id}>
                <td><strong>{b.client}</strong></td>
                <td>{b.type} <span className="muted">· {b.trainer}</span></td>
                <td>{formatDateTime(b.startsAt)}</td>
                <td>
                  {b.status === 'Booked'
                    ? <span className="badge ok">Registreeritud</span>
                    : <span className="badge few">Järjekorras #{b.position}</span>}
                </td>
                <td>{b.attended === true ? 'Kohal' : b.attended === false ? 'Puudus' : <span className="muted">–</span>}</td>
                <td className="muted">{formatDateTime(b.createdAt)}</td>
                <td className="actions">
                  <button className="btn danger small" onClick={() => cancel(b)}>Tühista</button>
                </td>
              </tr>
            ))}
            {!data.items.length && !loading && <tr><td colSpan="7" className="muted center">Broneeringuid ei leitud.</td></tr>}
          </tbody>
        </table>
      </div>
      <Pager page={data.page} totalPages={data.totalPages} total={data.total} onPage={setPage} />
    </section>
  )
}
