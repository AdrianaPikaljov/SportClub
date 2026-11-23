import { useState } from 'react'
import { api } from '../api.js'
import { ErrorBox, Field, Modal, Pager, SortHeader, useDebounced, usePagedList } from '../components/ui.jsx'

const empty = { name: '', description: '', visits: 12, durationDays: 30, price: 49, isActive: true }
const eur = (n) => Number(n).toLocaleString('et-EE', { minimumFractionDigits: 2, maximumFractionDigits: 2 }) + ' €'

export default function PassPlans() {
  const [searchText, setSearchText] = useState('')
  const [sort, setSort] = useState('price')
  const [page, setPage] = useState(1)
  const [editing, setEditing] = useState(null)
  const search = useDebounced(searchText)

  const { data, error, loading, reload } = usePagedList(
    () => api('/api/pass-plans', { query: { search, sort, page, pageSize: 10, includeInactive: true } }),
    [search, sort, page]
  )

  async function remove(p) {
    if (!window.confirm(`Kustutada pakett ${p.name}?`)) return
    try {
      await api(`/api/pass-plans/${p.id}`, { method: 'DELETE' })
      reload()
    } catch (e) {
      window.alert(e.message)
    }
  }

  return (
    <section>
      <header className="page-head">
        <h1>Kuukaardi paketid</h1>
        <button className="btn primary" onClick={() => setEditing(empty)}>+ Lisa pakett</button>
      </header>
      <p className="muted">Paketid, mida kliendid saavad mobiilirakenduses osta.</p>
      <div className="filters">
        <input placeholder="Otsi nime järgi" value={searchText} onChange={(e) => { setSearchText(e.target.value); setPage(1) }} />
      </div>
      <ErrorBox error={error} />
      <div className="table-wrap">
        <table className={loading ? 'loading' : ''}>
          <thead>
            <tr>
              <SortHeader field="name" label="Nimi" sort={sort} onSort={setSort} />
              <SortHeader field="visits" label="Külastusi" sort={sort} onSort={setSort} />
              <th>Kestus</th>
              <SortHeader field="price" label="Hind" sort={sort} onSort={setSort} />
              <th>Müügis</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {data.items.map((p) => (
              <tr key={p.id}>
                <td><strong>{p.name}</strong><div className="muted">{p.description}</div></td>
                <td>{p.visits}</td>
                <td>{p.durationDays} päeva</td>
                <td>{eur(p.price)}</td>
                <td><span className={p.isActive ? 'badge ok' : 'badge full'}>{p.isActive ? 'Jah' : 'Ei'}</span></td>
                <td className="actions">
                  <button className="btn ghost small" onClick={() => setEditing({ ...p, description: p.description || '' })}>Muuda</button>
                  <button className="btn danger small" onClick={() => remove(p)}>Kustuta</button>
                </td>
              </tr>
            ))}
            {!data.items.length && !loading && <tr><td colSpan="6" className="muted center">Pakette ei leitud.</td></tr>}
          </tbody>
        </table>
      </div>
      <Pager page={data.page} totalPages={data.totalPages} totalCount={data.totalCount} onPage={setPage} />
      {editing && <PlanForm initial={editing} onClose={() => setEditing(null)} onSaved={() => { setEditing(null); reload() }} />}
    </section>
  )
}

function PlanForm({ initial, onClose, onSaved }) {
  const [form, setForm] = useState(initial)
  const [errors, setErrors] = useState({})
  const [error, setError] = useState('')
  const set = (k, v) => setForm((f) => ({ ...f, [k]: v }))

  async function submit(e) {
    e.preventDefault()
    const v = {}
    if (form.name.trim().length < 2) v.name = 'Nimi peab olema vähemalt 2 tähemärki.'
    if (!(form.visits >= 1 && form.visits <= 100)) v.visits = '1–100 külastust.'
    if (!(form.durationDays >= 1 && form.durationDays <= 365)) v.durationDays = '1–365 päeva.'
    if (!(form.price >= 0 && form.price <= 1000)) v.price = 'Hind 0–1000 €.'
    setErrors(v)
    if (Object.keys(v).length) return

    const body = {
      name: form.name, description: form.description || null, visits: Number(form.visits),
      durationDays: Number(form.durationDays), price: Number(form.price), isActive: form.isActive
    }
    try {
      if (form.id) await api(`/api/pass-plans/${form.id}`, { method: 'PUT', body })
      else await api('/api/pass-plans', { method: 'POST', body })
      onSaved()
    } catch (err) {
      setError(err.message)
    }
  }

  return (
    <Modal title={form.id ? 'Muuda paketti' : 'Uus pakett'} onClose={onClose}>
      <form onSubmit={submit} noValidate className="form">
        <ErrorBox error={error} />
        <Field label="Nimi" error={errors.name}>
          <input value={form.name} onChange={(e) => set('name', e.target.value)} autoFocus />
        </Field>
        <Field label="Kirjeldus">
          <input value={form.description} onChange={(e) => set('description', e.target.value)} />
        </Field>
        <div className="grid2">
          <Field label="Külastusi" error={errors.visits}>
            <input type="number" value={form.visits} onChange={(e) => set('visits', e.target.valueAsNumber)} />
          </Field>
          <Field label="Kestus (päeva)" error={errors.durationDays}>
            <input type="number" value={form.durationDays} onChange={(e) => set('durationDays', e.target.valueAsNumber)} />
          </Field>
          <Field label="Hind (€)" error={errors.price}>
            <input type="number" step="0.01" value={form.price} onChange={(e) => set('price', e.target.valueAsNumber)} />
          </Field>
          <label className="field checkbox">
            <span>Müügis</span>
            <input type="checkbox" checked={form.isActive} onChange={(e) => set('isActive', e.target.checked)} />
          </label>
        </div>
        {form.id && <p className="muted">Muudatus ei mõjuta juba ostetud kuukaarte.</p>}
        <div className="form-actions">
          <button type="button" className="btn ghost" onClick={onClose}>Loobu</button>
          <button className="btn primary">Salvesta</button>
        </div>
      </form>
    </Modal>
  )
}
