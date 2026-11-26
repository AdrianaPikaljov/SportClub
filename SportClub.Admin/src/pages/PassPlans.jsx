import { useState } from 'react'
import { api } from '../api.js'
import { ErrorBox, Field, Modal, useList } from '../components/ui.jsx'

const empty = { name: '', visits: 8, days: 30, price: 39 }
const eur = (n) => Number(n).toLocaleString('et-EE', { minimumFractionDigits: 2, maximumFractionDigits: 2 }) + ' €'

export default function PassPlans() {
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
    if (!window.confirm(`Eemaldada tariif ${p.name} müügist? Juba ostetud kuukaardid jäävad alles.`)) return
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
        <h1>Kuukaardi tariifid</h1>
        <button className="btn primary" onClick={() => setEditing(empty)}>+ Lisa tariif</button>
      </header>
      <p className="muted">Tariifid, mida kliendid saavad mobiilirakenduses osta.</p>
      <ErrorBox error={error} />
      <div className="table-wrap">
        <table className={loading ? 'loading' : ''}>
          <thead>
            <tr>
              <th>ID</th>
              <th>Nimi</th>
              <th>Külastusi</th>
              <th>Kehtib</th>
              <th>Hind</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {items.map((p) => (
              <tr key={p.id}>
                <td>{p.id}</td>
                <td><strong>{p.name}</strong></td>
                <td>{p.visits}</td>
                <td>{p.days} päeva</td>
                <td>{eur(p.price)}</td>
                <td className="actions">
                  <button className="btn ghost small" onClick={() => edit(p)}>Muuda</button>
                  <button className="btn danger small" onClick={() => remove(p)}>Eemalda müügist</button>
                </td>
              </tr>
            ))}
            {!items.length && !loading && <tr><td colSpan="6" className="muted center">Tariife pole.</td></tr>}
          </tbody>
        </table>
      </div>
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
    if (!(form.days >= 1 && form.days <= 365)) v.days = '1–365 päeva.'
    if (!(form.price >= 0 && form.price <= 1000)) v.price = 'Hind 0–1000 €.'
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
    <Modal title={form.id ? 'Muuda tariifi' : 'Uus tariif'} onClose={onClose}>
      <form onSubmit={submit} noValidate className="form">
        <ErrorBox error={error} />
        <Field label="Nimi" error={errors.name}>
          <input value={form.name} onChange={(e) => set('name', e.target.value)} autoFocus />
        </Field>
        <div className="grid2">
          <Field label="Külastusi" error={errors.visits}>
            <input type="number" value={form.visits} onChange={(e) => set('visits', e.target.valueAsNumber)} />
          </Field>
          <Field label="Kehtib (päeva)" error={errors.days}>
            <input type="number" value={form.days} onChange={(e) => set('days', e.target.valueAsNumber)} />
          </Field>
          <Field label="Hind (€)" error={errors.price}>
            <input type="number" step="0.01" value={form.price} onChange={(e) => set('price', e.target.valueAsNumber)} />
          </Field>
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
