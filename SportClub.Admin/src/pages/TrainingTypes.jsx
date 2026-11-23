import { useState } from 'react'
import { api } from '../api.js'
import { ErrorBox, Field, Modal, Pager, SortHeader, useDebounced, usePagedList } from '../components/ui.jsx'

const empty = { name: '', description: '', intensity: 'Keskmine' }
const INTENSITIES = ['Madal', 'Keskmine', 'Kõrge']

export default function TrainingTypes() {
  const [searchText, setSearchText] = useState('')
  const [sort, setSort] = useState('name')
  const [page, setPage] = useState(1)
  const [editing, setEditing] = useState(null)
  const search = useDebounced(searchText)

  const { data, error, loading, reload } = usePagedList(
    () => api('/api/training-types', { query: { search, sort, page, pageSize: 10 } }),
    [search, sort, page]
  )

  async function remove(t) {
    if (!window.confirm(`Kustutada treeningutüüp ${t.name}?`)) return
    try {
      await api(`/api/training-types/${t.id}`, { method: 'DELETE' })
      reload()
    } catch (e) {
      window.alert(e.message)
    }
  }

  return (
    <section>
      <header className="page-head">
        <h1>Treeningutüübid</h1>
        <button className="btn primary" onClick={() => setEditing(empty)}>+ Lisa tüüp</button>
      </header>
      <div className="filters">
        <input placeholder="Otsi nime või kirjelduse järgi" value={searchText} onChange={(e) => { setSearchText(e.target.value); setPage(1) }} />
      </div>
      <ErrorBox error={error} />
      <div className="table-wrap">
        <table className={loading ? 'loading' : ''}>
          <thead>
            <tr>
              <SortHeader field="name" label="Nimi" sort={sort} onSort={setSort} />
              <SortHeader field="intensity" label="Intensiivsus" sort={sort} onSort={setSort} />
              <th>Kirjeldus</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {data.items.map((t) => (
              <tr key={t.id}>
                <td><strong>{t.name}</strong></td>
                <td>{t.intensity}</td>
                <td className="muted">{t.description || '–'}</td>
                <td className="actions">
                  <button className="btn ghost small" onClick={() => setEditing({ ...t, description: t.description || '' })}>Muuda</button>
                  <button className="btn danger small" onClick={() => remove(t)}>Kustuta</button>
                </td>
              </tr>
            ))}
            {!data.items.length && !loading && <tr><td colSpan="4" className="muted center">Tüüpe ei leitud.</td></tr>}
          </tbody>
        </table>
      </div>
      <Pager page={data.page} totalPages={data.totalPages} totalCount={data.totalCount} onPage={setPage} />
      {editing && <TypeForm initial={editing} onClose={() => setEditing(null)} onSaved={() => { setEditing(null); reload() }} />}
    </section>
  )
}

function TypeForm({ initial, onClose, onSaved }) {
  const [form, setForm] = useState(initial)
  const [errors, setErrors] = useState({})
  const [error, setError] = useState('')

  async function submit(e) {
    e.preventDefault()
    const v = {}
    if (form.name.trim().length < 2) v.name = 'Nimi peab olema vähemalt 2 tähemärki.'
    if (form.name.length > 60) v.name = 'Nimi võib olla kuni 60 tähemärki.'
    setErrors(v)
    if (Object.keys(v).length) return

    const body = { name: form.name, description: form.description || null, intensity: form.intensity }
    try {
      if (form.id) await api(`/api/training-types/${form.id}`, { method: 'PUT', body })
      else await api('/api/training-types', { method: 'POST', body })
      onSaved()
    } catch (err) {
      setError(err.message)
    }
  }

  return (
    <Modal title={form.id ? 'Muuda tüüpi' : 'Uus treeningutüüp'} onClose={onClose}>
      <form onSubmit={submit} noValidate className="form">
        <ErrorBox error={error} />
        <Field label="Nimi" error={errors.name}>
          <input value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} autoFocus />
        </Field>
        <Field label="Intensiivsus">
          <select value={form.intensity} onChange={(e) => setForm({ ...form, intensity: e.target.value })}>
            {INTENSITIES.map((i) => <option key={i}>{i}</option>)}
          </select>
        </Field>
        <Field label="Kirjeldus">
          <textarea rows="3" value={form.description} onChange={(e) => setForm({ ...form, description: e.target.value })} />
        </Field>
        <div className="form-actions">
          <button type="button" className="btn ghost" onClick={onClose}>Loobu</button>
          <button className="btn primary">Salvesta</button>
        </div>
      </form>
    </Modal>
  )
}
