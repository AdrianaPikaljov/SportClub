import { useState } from 'react'
import { api } from '../api.js'
import { ErrorBox, Field, Modal, Pager, SortHeader, useDebounced, usePagedList } from '../components/ui.jsx'

const empty = { fullName: '', bio: '', phone: '' }

export default function Trainers() {
  const [searchText, setSearchText] = useState('')
  const [sort, setSort] = useState('name')
  const [page, setPage] = useState(1)
  const [editing, setEditing] = useState(null)
  const search = useDebounced(searchText)

  const { data, error, loading, reload } = usePagedList(
    () => api('/api/trainers', { query: { search, sort, page, pageSize: 10 } }),
    [search, sort, page]
  )

  async function remove(t) {
    if (!window.confirm(`Kustutada treener ${t.fullName}?`)) return
    try {
      await api(`/api/trainers/${t.id}`, { method: 'DELETE' })
      reload()
    } catch (e) {
      window.alert(e.message)
    }
  }

  return (
    <section>
      <header className="page-head">
        <h1>Treenerid</h1>
        <button className="btn primary" onClick={() => setEditing(empty)}>+ Lisa treener</button>
      </header>
      <div className="filters">
        <input placeholder="Otsi nime või kirjelduse järgi" value={searchText} onChange={(e) => { setSearchText(e.target.value); setPage(1) }} />
      </div>
      <ErrorBox error={error} />
      <div className="table-wrap">
        <table className={loading ? 'loading' : ''}>
          <thead>
            <tr>
              <SortHeader field="id" label="ID" sort={sort} onSort={setSort} />
              <SortHeader field="name" label="Nimi" sort={sort} onSort={setSort} />
              <th>Telefon</th>
              <th>Kirjeldus</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {data.items.map((t) => (
              <tr key={t.id}>
                <td>{t.id}</td>
                <td><strong>{t.fullName}</strong>{t.userId && <span className="badge ok inline">konto</span>}</td>
                <td>{t.phone || '–'}</td>
                <td className="muted">{t.bio || '–'}</td>
                <td className="actions">
                  <button className="btn ghost small" onClick={() => setEditing({ ...t, bio: t.bio || '', phone: t.phone || '' })}>Muuda</button>
                  <button className="btn danger small" onClick={() => remove(t)}>Kustuta</button>
                </td>
              </tr>
            ))}
            {!data.items.length && !loading && <tr><td colSpan="5" className="muted center">Treenereid ei leitud.</td></tr>}
          </tbody>
        </table>
      </div>
      <Pager page={data.page} totalPages={data.totalPages} totalCount={data.totalCount} onPage={setPage} />
      {editing && <TrainerForm initial={editing} onClose={() => setEditing(null)} onSaved={() => { setEditing(null); reload() }} />}
    </section>
  )
}

function TrainerForm({ initial, onClose, onSaved }) {
  const [form, setForm] = useState(initial)
  const [errors, setErrors] = useState({})
  const [error, setError] = useState('')

  async function submit(e) {
    e.preventDefault()
    const v = {}
    if (form.fullName.trim().length < 2) v.fullName = 'Nimi peab olema vähemalt 2 tähemärki.'
    if (form.phone && !/^[+\d][\d\s-]{5,}$/.test(form.phone)) v.phone = 'Vigane telefoninumber.'
    setErrors(v)
    if (Object.keys(v).length) return

    const body = { fullName: form.fullName, bio: form.bio || null, phone: form.phone || null, userId: form.userId ?? null }
    try {
      if (form.id) await api(`/api/trainers/${form.id}`, { method: 'PUT', body })
      else await api('/api/trainers', { method: 'POST', body })
      onSaved()
    } catch (err) {
      setError(err.message)
    }
  }

  return (
    <Modal title={form.id ? 'Muuda treenerit' : 'Uus treener'} onClose={onClose}>
      <form onSubmit={submit} noValidate className="form">
        <ErrorBox error={error} />
        <Field label="Nimi" error={errors.fullName}>
          <input value={form.fullName} onChange={(e) => setForm({ ...form, fullName: e.target.value })} autoFocus />
        </Field>
        <Field label="Telefon" error={errors.phone}>
          <input value={form.phone} onChange={(e) => setForm({ ...form, phone: e.target.value })} />
        </Field>
        <Field label="Kirjeldus">
          <textarea rows="3" value={form.bio} onChange={(e) => setForm({ ...form, bio: e.target.value })} />
        </Field>
        <div className="form-actions">
          <button type="button" className="btn ghost" onClick={onClose}>Loobu</button>
          <button className="btn primary">Salvesta</button>
        </div>
      </form>
    </Modal>
  )
}
