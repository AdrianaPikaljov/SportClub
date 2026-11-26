import { useState } from 'react'
import { api } from '../api.js'
import { ErrorBox, Field, Modal, useList } from '../components/ui.jsx'

const empty = { name: '', specialty: '', description: '', phone: '', email: '', password: '' }

export default function Trainers() {
  const [editing, setEditing] = useState(null)
  // Admin saab nimekirjas ka treeneri konto e-posti.
  const { items, error, loading, reload } = useList(() => api('/api/trainers'), [])

  async function edit(t) {
    try {
      // GET /api/trainers/{id} annab andmed PUT-i jaoks (koos konto e-postiga).
      const data = await api(`/api/trainers/${t.id}`)
      setEditing({ ...data, description: data.description || '', phone: data.phone || '', password: '', id: t.id })
    } catch (e) {
      window.alert(e.message)
    }
  }

  async function remove(t) {
    if (!window.confirm(`Kustutada treener ${t.name}? Kustutatakse ka tema konto ja toimunud treeningud.`)) return
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
      <p className="muted">Igal treeneril on oma konto: ta logib mobiilirakendusse sisse siin näidatud e-posti ja parooliga.</p>
      <ErrorBox error={error} />
      <div className="table-wrap">
        <table className={loading ? 'loading' : ''}>
          <thead>
            <tr>
              <th>ID</th>
              <th>Nimi</th>
              <th>Eriala</th>
              <th>E-post (konto)</th>
              <th>Telefon</th>
              <th>Kirjeldus</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {items.map((t) => (
              <tr key={t.id}>
                <td>{t.id}</td>
                <td><strong>{t.name}</strong></td>
                <td>{t.specialty}</td>
                <td>{t.email || <span className="muted">konto puudub</span>}</td>
                <td>{t.phone || '–'}</td>
                <td className="muted">{t.description || '–'}</td>
                <td className="actions">
                  <button className="btn ghost small" onClick={() => edit(t)}>Muuda</button>
                  <button className="btn danger small" onClick={() => remove(t)}>Kustuta</button>
                </td>
              </tr>
            ))}
            {!items.length && !loading && <tr><td colSpan="7" className="muted center">Treenereid pole.</td></tr>}
          </tbody>
        </table>
      </div>
      {editing && <TrainerForm initial={editing} onClose={() => setEditing(null)} onSaved={() => { setEditing(null); reload() }} />}
    </section>
  )
}

function TrainerForm({ initial, onClose, onSaved }) {
  const [form, setForm] = useState(initial)
  const [errors, setErrors] = useState({})
  const [error, setError] = useState('')
  const set = (k, v) => setForm((f) => ({ ...f, [k]: v }))
  const isNew = !form.id

  async function submit(e) {
    e.preventDefault()
    const v = {}
    if (form.name.trim().length < 2) v.name = 'Nimi peab olema vähemalt 2 tähemärki.'
    if (!form.specialty.trim()) v.specialty = 'Eriala on kohustuslik.'
    if (!/^\S+@\S+\.\S+$/.test(form.email)) v.email = 'Sisesta korrektne e-post.'
    if (form.phone && !/^\+?[0-9 -]{5,30}$/.test(form.phone)) v.phone = 'Vigane telefoninumber, nt +372 5555 1234.'
    if (isNew && form.password.length < 8) v.password = 'Parool peab olema vähemalt 8 tähemärki.'
    if (!isNew && form.password && form.password.length < 8) v.password = 'Uus parool peab olema vähemalt 8 tähemärki.'
    setErrors(v)
    if (Object.keys(v).length) return

    const body = {
      name: form.name,
      specialty: form.specialty,
      description: form.description || null,
      phone: form.phone || null,
      email: form.email
    }
    try {
      if (isNew) await api('/api/trainers', { method: 'POST', body: { ...body, password: form.password } })
      else await api(`/api/trainers/${form.id}`, { method: 'PUT', body: { ...body, newPassword: form.password || null } })
      onSaved()
    } catch (err) {
      setError(err.message)
    }
  }

  return (
    <Modal title={isNew ? 'Uus treener' : 'Muuda treenerit'} onClose={onClose}>
      <form onSubmit={submit} noValidate className="form">
        <ErrorBox error={error} />
        <div className="grid2">
          <Field label="Nimi" error={errors.name}>
            <input value={form.name} onChange={(e) => set('name', e.target.value)} autoFocus />
          </Field>
          <Field label="Eriala" error={errors.specialty}>
            <input value={form.specialty} onChange={(e) => set('specialty', e.target.value)} />
          </Field>
          <Field label="E-post (sisselogimiseks)" error={errors.email}>
            <input type="email" value={form.email} onChange={(e) => set('email', e.target.value)} />
          </Field>
          <Field label="Telefon" error={errors.phone}>
            <input value={form.phone} placeholder="+372 5555 1234" onChange={(e) => set('phone', e.target.value)} />
          </Field>
        </div>
        <Field label="Kirjeldus">
          <textarea rows="3" value={form.description} onChange={(e) => set('description', e.target.value)} />
        </Field>
        <Field label={isNew ? 'Parool' : 'Uus parool (jäta tühjaks, kui ei muuda)'} error={errors.password}>
          <input type="password" value={form.password} autoComplete="new-password" onChange={(e) => set('password', e.target.value)} />
        </Field>
        <div className="form-actions">
          <button type="button" className="btn ghost" onClick={onClose}>Loobu</button>
          <button className="btn primary">Salvesta</button>
        </div>
      </form>
    </Modal>
  )
}
