import { useState } from 'react'
import { api } from '../api.js'
import { ErrorBox, Field, Modal, useList } from '../components/ui.jsx'
import { usePrefs } from '../i18n.jsx'

const empty = { name: '', description: '' }

export default function TrainingTypes() {
  const { t } = usePrefs()
  const [editing, setEditing] = useState(null)
  const { items, error, loading, reload } = useList(() => api('/api/types'), [])

  async function edit(tr) {
    try {
      const data = await api(`/api/types/${tr.id}`)
      setEditing({ ...data, description: data.description || '', id: tr.id })
    } catch (e) {
      window.alert(e.message)
    }
  }

  async function remove(tr) {
    if (!window.confirm(t('types.confirmDelete', { name: tr.name }))) return
    try {
      await api(`/api/types/${tr.id}`, { method: 'DELETE' })
      reload()
    } catch (e) {
      window.alert(e.message)
    }
  }

  return (
    <section>
      <header className="page-head">
        <h1>{t('types.title')}</h1>
        <button className="btn primary" onClick={() => setEditing(empty)}>{t('types.add')}</button>
      </header>
      <ErrorBox error={error} />
      <div className="table-wrap">
        <table className={loading ? 'loading' : ''}>
          <thead>
            <tr>
              <th>{t('common.id')}</th>
              <th>{t('common.name')}</th>
              <th>{t('common.description')}</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {items.map((tr) => (
              <tr key={tr.id}>
                <td>{tr.id}</td>
                <td><strong>{tr.name}</strong></td>
                <td className="muted">{tr.description || '–'}</td>
                <td className="actions">
                  <button className="btn ghost small" onClick={() => edit(tr)}>{t('common.edit')}</button>
                  <button className="btn danger small" onClick={() => remove(tr)}>{t('common.delete')}</button>
                </td>
              </tr>
            ))}
            {!items.length && !loading && <tr><td colSpan="4" className="muted center">{t('types.empty')}</td></tr>}
          </tbody>
        </table>
      </div>
      {editing && <TypeForm initial={editing} onClose={() => setEditing(null)} onSaved={() => { setEditing(null); reload() }} />}
    </section>
  )
}

function TypeForm({ initial, onClose, onSaved }) {
  const { t } = usePrefs()
  const [form, setForm] = useState(initial)
  const [errors, setErrors] = useState({})
  const [error, setError] = useState('')

  async function submit(e) {
    e.preventDefault()
    const v = {}
    if (form.name.trim().length < 2 || form.name.length > 60) v.name = t('types.v.name')
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
    <Modal title={form.id ? t('types.editTitle') : t('types.newTitle')} onClose={onClose}>
      <form onSubmit={submit} noValidate className="form">
        <ErrorBox error={error} />
        <Field label={t('common.name')} error={errors.name}>
          <input value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} autoFocus />
        </Field>
        <Field label={t('common.description')}>
          <textarea rows="3" value={form.description} onChange={(e) => setForm({ ...form, description: e.target.value })} />
        </Field>
        <div className="form-actions">
          <button type="button" className="btn ghost" onClick={onClose}>{t('common.cancel')}</button>
          <button className="btn primary">{t('common.save')}</button>
        </div>
      </form>
    </Modal>
  )
}
