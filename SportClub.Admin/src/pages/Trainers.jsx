import { useState } from 'react'
import { api } from '../api.js'
import { ErrorBox, Field, Modal, useList } from '../components/ui.jsx'
import { usePrefs } from '../i18n.jsx'

const empty = { name: '', specialty: '', description: '', phone: '', email: '', password: '' }

export default function Trainers() {
  const { t } = usePrefs()
  const [editing, setEditing] = useState(null)
  // Admin saab nimekirjas ka treeneri konto e-posti.
  const { items, error, loading, reload } = useList(() => api('/api/trainers'), [])

  async function edit(tr) {
    try {
      // GET /api/trainers/{id} annab andmed PUT-i jaoks (koos konto e-postiga).
      const data = await api(`/api/trainers/${tr.id}`)
      setEditing({ ...data, description: data.description || '', phone: data.phone || '', password: '', id: tr.id })
    } catch (e) {
      window.alert(e.message)
    }
  }

  async function remove(tr) {
    if (!window.confirm(t('trainers.confirmDelete', { name: tr.name }))) return
    try {
      await api(`/api/trainers/${tr.id}`, { method: 'DELETE' })
      reload()
    } catch (e) {
      window.alert(e.message)
    }
  }

  return (
    <section>
      <header className="page-head">
        <h1>{t('trainers.title')}</h1>
        <button className="btn primary" onClick={() => setEditing(empty)}>{t('trainers.add')}</button>
      </header>
      <p className="muted">{t('trainers.hint')}</p>
      <ErrorBox error={error} />
      <div className="table-wrap">
        <table className={loading ? 'loading' : ''}>
          <thead>
            <tr>
              <th>{t('common.id')}</th>
              <th>{t('common.name')}</th>
              <th>{t('trainers.specialty')}</th>
              <th>{t('trainers.email')}</th>
              <th>{t('trainers.phone')}</th>
              <th>{t('common.description')}</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {items.map((tr) => (
              <tr key={tr.id}>
                <td>{tr.id}</td>
                <td><strong>{tr.name}</strong></td>
                <td>{tr.specialty}</td>
                <td>{tr.email || <span className="muted">{t('trainers.noAccount')}</span>}</td>
                <td>{tr.phone || '–'}</td>
                <td className="muted">{tr.description || '–'}</td>
                <td className="actions">
                  <button className="btn ghost small" onClick={() => edit(tr)}>{t('common.edit')}</button>
                  <button className="btn danger small" onClick={() => remove(tr)}>{t('common.delete')}</button>
                </td>
              </tr>
            ))}
            {!items.length && !loading && <tr><td colSpan="7" className="muted center">{t('trainers.empty')}</td></tr>}
          </tbody>
        </table>
      </div>
      {editing && <TrainerForm initial={editing} onClose={() => setEditing(null)} onSaved={() => { setEditing(null); reload() }} />}
    </section>
  )
}

function TrainerForm({ initial, onClose, onSaved }) {
  const { t } = usePrefs()
  const [form, setForm] = useState(initial)
  const [errors, setErrors] = useState({})
  const [error, setError] = useState('')
  const set = (k, v) => setForm((f) => ({ ...f, [k]: v }))
  const isNew = !form.id

  async function submit(e) {
    e.preventDefault()
    const v = {}
    if (form.name.trim().length < 2) v.name = t('trainers.v.name')
    if (!form.specialty.trim()) v.specialty = t('trainers.v.specialty')
    if (!/^\S+@\S+\.\S+$/.test(form.email)) v.email = t('trainers.v.email')
    if (form.phone && !/^\+?[0-9 -]{5,30}$/.test(form.phone)) v.phone = t('trainers.v.phone')
    if (isNew && form.password.length < 8) v.password = t('trainers.v.password')
    if (!isNew && form.password && form.password.length < 8) v.password = t('trainers.v.password')
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
    <Modal title={isNew ? t('trainers.newTitle') : t('trainers.editTitle')} onClose={onClose}>
      <form onSubmit={submit} noValidate className="form">
        <ErrorBox error={error} />
        <div className="grid2">
          <Field label={t('common.name')} error={errors.name}>
            <input value={form.name} onChange={(e) => set('name', e.target.value)} autoFocus />
          </Field>
          <Field label={t('trainers.specialty')} error={errors.specialty}>
            <input value={form.specialty} onChange={(e) => set('specialty', e.target.value)} />
          </Field>
          <Field label={t('trainers.loginEmail')} error={errors.email}>
            <input type="email" value={form.email} onChange={(e) => set('email', e.target.value)} />
          </Field>
          <Field label={t('trainers.phone')} error={errors.phone}>
            <input value={form.phone} placeholder="+372 5555 1234" onChange={(e) => set('phone', e.target.value)} />
          </Field>
        </div>
        <Field label={t('common.description')}>
          <textarea rows="3" value={form.description} onChange={(e) => set('description', e.target.value)} />
        </Field>
        <Field label={isNew ? t('trainers.password') : t('trainers.newPassword')} error={errors.password}>
          <input type="password" value={form.password} autoComplete="new-password" onChange={(e) => set('password', e.target.value)} />
        </Field>
        <div className="form-actions">
          <button type="button" className="btn ghost" onClick={onClose}>{t('common.cancel')}</button>
          <button className="btn primary">{t('common.save')}</button>
        </div>
      </form>
    </Modal>
  )
}
