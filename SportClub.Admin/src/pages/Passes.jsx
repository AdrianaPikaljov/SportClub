import { useEffect, useState } from 'react'
import { api, formatDate, fromLocalInput } from '../api.js'
import { ErrorBox, Field, Modal, Pager, SortHeader, useDebounced, usePagedList } from '../components/ui.jsx'

export default function Passes() {
  const [searchText, setSearchText] = useState('')
  const [sort, setSort] = useState('-validUntil')
  const [page, setPage] = useState(1)
  const [creating, setCreating] = useState(false)
  const search = useDebounced(searchText)

  const { data, error, loading, reload } = usePagedList(
    () => api('/api/passes', { query: { search, sort, page, pageSize: 10 } }),
    [search, sort, page]
  )

  async function remove(p) {
    if (!window.confirm(`Kustutada ${p.userName} kuukaart?`)) return
    try {
      await api(`/api/passes/${p.id}`, { method: 'DELETE' })
      reload()
    } catch (e) {
      window.alert(e.message)
    }
  }

  return (
    <section>
      <header className="page-head">
        <h1>Kuukaardid</h1>
        <button className="btn primary" onClick={() => setCreating(true)}>+ Anna kuukaart</button>
      </header>
      <div className="filters">
        <input placeholder="Otsi kliendi nime või e-posti järgi" value={searchText} onChange={(e) => { setSearchText(e.target.value); setPage(1) }} />
      </div>
      <ErrorBox error={error} />
      <div className="table-wrap">
        <table className={loading ? 'loading' : ''}>
          <thead>
            <tr>
              <SortHeader field="user" label="Klient" sort={sort} onSort={setSort} />
              <th>Kehtib</th>
              <SortHeader field="validUntil" label="Kuni" sort={sort} onSort={setSort} />
              <th>Külastusi</th>
              <th>Pakett / hind</th>
              <th>Staatus</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {data.items.map((p) => (
              <tr key={p.id}>
                <td><strong>{p.userName}</strong></td>
                <td>{formatDate(p.validFrom)}</td>
                <td>{formatDate(p.validUntil)}</td>
                <td>{p.visitsLeft} / {p.visitsTotal} alles</td>
                <td>{p.planName ? `${p.planName} · ${Number(p.pricePaid).toFixed(2)} €` : <span className="muted">admin andis</span>}</td>
                <td>
                  <span className={p.isActive ? 'badge ok' : p.isUpcoming ? 'badge few' : 'badge full'}>
                    {p.isActive ? 'Aktiivne' : p.isUpcoming ? 'Ootel' : 'Ei kehti'}
                  </span>
                </td>
                <td className="actions">
                  <button className="btn danger small" disabled={p.visitsUsed > 0} onClick={() => remove(p)}>Kustuta</button>
                </td>
              </tr>
            ))}
            {!data.items.length && !loading && <tr><td colSpan="7" className="muted center">Kuukaarte ei leitud.</td></tr>}
          </tbody>
        </table>
      </div>
      <Pager page={data.page} totalPages={data.totalPages} totalCount={data.totalCount} onPage={setPage} />
      {creating && <PassForm onClose={() => setCreating(false)} onSaved={() => { setCreating(false); reload() }} />}
    </section>
  )
}

function monthRange() {
  const now = new Date()
  const pad = (n) => String(n).padStart(2, '0')
  const first = new Date(now.getFullYear(), now.getMonth(), 1)
  const last = new Date(now.getFullYear(), now.getMonth() + 1, 0)
  const f = (d) => `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`
  return { from: f(first), until: f(last) }
}

function PassForm({ onClose, onSaved }) {
  const range = monthRange()
  const [clients, setClients] = useState([])
  const [form, setForm] = useState({ userId: '', validFrom: range.from, validUntil: range.until, visitsTotal: 12 })
  const [errors, setErrors] = useState({})
  const [error, setError] = useState('')

  useEffect(() => {
    api('/api/users', { query: { role: 'Client', pageSize: 100 } }).then((d) => setClients(d.items))
  }, [])

  async function submit(e) {
    e.preventDefault()
    const v = {}
    if (!form.userId) v.userId = 'Vali klient.'
    if (!form.validFrom) v.validFrom = 'Kohustuslik.'
    if (!form.validUntil) v.validUntil = 'Kohustuslik.'
    else if (form.validUntil < form.validFrom) v.validUntil = 'Lõpp peab olema pärast algust.'
    if (!(form.visitsTotal >= 1 && form.visitsTotal <= 100)) v.visitsTotal = '1–100 külastust.'
    setErrors(v)
    if (Object.keys(v).length) return

    try {
      await api('/api/passes', {
        method: 'POST',
        body: {
          userId: Number(form.userId),
          validFrom: fromLocalInput(form.validFrom + 'T00:00'),
          validUntil: fromLocalInput(form.validUntil + 'T23:59'),
          visitsTotal: Number(form.visitsTotal)
        }
      })
      onSaved()
    } catch (err) {
      setError(err.message)
    }
  }

  return (
    <Modal title="Uus kuukaart" onClose={onClose}>
      <form onSubmit={submit} noValidate className="form">
        <ErrorBox error={error} />
        <Field label="Klient" error={errors.userId}>
          <select value={form.userId} onChange={(e) => setForm({ ...form, userId: e.target.value })}>
            <option value="">Vali…</option>
            {clients.map((c) => <option key={c.id} value={c.id}>{c.fullName} ({c.email})</option>)}
          </select>
        </Field>
        <div className="grid2">
          <Field label="Alates" error={errors.validFrom}>
            <input type="date" value={form.validFrom} onChange={(e) => setForm({ ...form, validFrom: e.target.value })} />
          </Field>
          <Field label="Kuni" error={errors.validUntil}>
            <input type="date" value={form.validUntil} onChange={(e) => setForm({ ...form, validUntil: e.target.value })} />
          </Field>
        </div>
        <Field label="Külastuste arv" error={errors.visitsTotal}>
          <input type="number" value={form.visitsTotal} onChange={(e) => setForm({ ...form, visitsTotal: e.target.valueAsNumber })} />
        </Field>
        <div className="form-actions">
          <button type="button" className="btn ghost" onClick={onClose}>Loobu</button>
          <button className="btn primary">Salvesta</button>
        </div>
      </form>
    </Modal>
  )
}
