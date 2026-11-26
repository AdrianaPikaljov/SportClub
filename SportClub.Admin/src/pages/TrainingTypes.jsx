import { useState } from 'react'
import { api } from '../api.js'
import { ErrorBox, Field, Modal, useList } from '../components/ui.jsx'

const empty = { name: '', description: '' }

export default function TrainingTypes() {
  const [editing, setEditing] = useState(null)
  const { items, error, loading, reload } = useList(() => api('/api/types'), [])

  async function edit(t) {
    try {
      const data = await api(`/api/types/${t.id}`)
      setEditing({ ...data, description: data.description || '', id: t.id })
    } catch (e) {
      window.alert(e.message)
    }
  }

  async function remove(t) {
    if (!window.confirm(`Kustutada treeningutüüp ${t.name}?`)) return
    try {
      await api(`/api/types/${t.id}`, { method: 'DELETE' })
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
      <ErrorBox error={error} />
      <div className="table-wrap">
        <table className={loading ? 'loading' : ''}>
          <thead>
            <tr>
              <th>ID</th>
              <th>Nimi</th>
              <th>Kirjeldus</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {items.map((t) => (
              <tr key={t.id}>
                <td>{t.id}</td>
                <td><strong>{t.name}</strong></td>
                <td className="muted">{t.description || '–'}</td>
                <td className="actions">
                  <button className="btn ghost small" onClick={() => edit(t)}>Muuda</button>
                  <button className="btn danger small" onClick={() => remove(t)}>Kustuta</button>
                </td>
              </tr>
            ))}
            {!items.length && !loading && <tr><td colSpan="4" className="muted center">Tüüpe pole.</td></tr>}
          </tbody>
        </table>
      </div>
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

    const body = { name: form.name, description: form.description || null }
    try {
      if (form.id) await api(`/api/types/${form.id}`, { method: 'PUT', body })
      else await api('/api/types', { method: 'POST', body })
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
