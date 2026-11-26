import { useState } from 'react'
import { api } from '../api.js'
import { ErrorBox, Field, Modal, useList } from '../components/ui.jsx'
import { usePrefs } from '../i18n.jsx'

const empty = { name: '', visits: 8, days: 30, price: 39 }
const eur = (n) => Number(n).toLocaleString('et-EE', { minimumFractionDigits: 2, maximumFractionDigits: 2 }) + ' €'

export default function PassPlans() {
  const { t } = usePrefs()
  const [editing, setEditing] = useState(null)
  const { items, error, loading, reload } = useList(() => api('/api/pass-plans'), [])

  async function edit(p) {
    try {
      const data = await api(`/api/pass-plans/${p.id}`)
      setEditing({ ...data, id: p.id })
    } catch (e) {
      window.alert(e.message)
    }
  }

  async function remove(p) {
    if (!window.confirm(t('plans.confirmDelete', { name: p.name }))) return
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
        <h1>{t('plans.title')}</h1>
        <button className="btn primary" onClick={() => setEditing(empty)}>{t('plans.add')}</button>
      </header>
      <p className="muted">{t('plans.hint')}</p>
      <ErrorBox error={error} />
      <div className="table-wrap">
        <table className={loading ? 'loading' : ''}>
          <thead>
            <tr>
              <th>{t('common.id')}</th>
              <th>{t('common.name')}</th>
              <th>{t('plans.visits')}</th>
              <th>{t('plans.valid')}</th>
              <th>{t('plans.price')}</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {items.map((p) => (
              <tr key={p.id}>
                <td>{p.id}</td>
                <td><strong>{p.name}</strong></td>
                <td>{p.visits}</td>
                <td>{t('plans.days', { n: p.days })}</td>
                <td>{eur(p.price)}</td>
                <td className="actions">
                  <button className="btn ghost small" onClick={() => edit(p)}>{t('common.edit')}</button>
                  <button className="btn danger small" onClick={() => remove(p)}>{t('plans.remove')}</button>
                </td>
              </tr>
            ))}
            {!items.length && !loading && <tr><td colSpan="6" className="muted center">{t('plans.empty')}</td></tr>}
          </tbody>
        </table>
      </div>
      {editing && <PlanForm initial={editing} onClose={() => setEditing(null)} onSaved={() => { setEditing(null); reload() }} />}
    </section>
  )
}

function PlanForm({ initial, onClose, onSaved }) {
  const { t } = usePrefs()
  const [form, setForm] = useState(initial)
  const [errors, setErrors] = useState({})
  const [error, setError] = useState('')
  const set = (k, v) => setForm((f) => ({ ...f, [k]: v }))

  async function submit(e) {
    e.preventDefault()
    const v = {}
    if (form.name.trim().length < 2) v.name = t('plans.v.name')
    if (!(form.visits >= 1 && form.visits <= 100)) v.visits = t('plans.v.visits')
    if (!(form.days >= 1 && form.days <= 365)) v.days = t('plans.v.days')
    if (!(form.price >= 0 && form.price <= 1000)) v.price = t('plans.v.price')
    setErrors(v)
    if (Object.keys(v).length) return

    const body = { name: form.name, visits: Number(form.visits), days: Number(form.days), price: Number(form.price) }
    try {
      if (form.id) await api(`/api/pass-plans/${form.id}`, { method: 'PUT', body })
      else await api('/api/pass-plans', { method: 'POST', body })
      onSaved()
    } catch (err) {
      setError(err.message)
    }
  }

  return (
    <Modal title={form.id ? t('plans.editTitle') : t('plans.newTitle')} onClose={onClose}>
      <form onSubmit={submit} noValidate className="form">
        <ErrorBox error={error} />
        <Field label={t('common.name')} error={errors.name}>
          <input value={form.name} onChange={(e) => set('name', e.target.value)} autoFocus />
        </Field>
        <div className="grid2">
          <Field label={t('plans.visits')} error={errors.visits}>
            <input type="number" value={form.visits} onChange={(e) => set('visits', e.target.valueAsNumber)} />
          </Field>
          <Field label={t('plans.daysField')} error={errors.days}>
            <input type="number" value={form.days} onChange={(e) => set('days', e.target.valueAsNumber)} />
          </Field>
          <Field label={t('plans.priceField')} error={errors.price}>
            <input type="number" step="0.01" value={form.price} onChange={(e) => set('price', e.target.valueAsNumber)} />
          </Field>
        </div>
        {form.id && <p className="muted">{t('plans.editHint')}</p>}
        <div className="form-actions">
          <button type="button" className="btn ghost" onClick={onClose}>{t('common.cancel')}</button>
          <button className="btn primary">{t('common.save')}</button>
        </div>
      </form>
    </Modal>
  )
}
