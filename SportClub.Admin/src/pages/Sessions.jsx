import { useEffect, useState } from 'react'
import { api, formatDateTime, today } from '../api.js'
import { ErrorBox, Field, Modal, useList } from '../components/ui.jsx'

const empty = { typeId: '', trainerId: '', date: '', time: '18:00', durationMinutes: 60, capacity: 12 }

export default function Sessions() {
  const [types, setTypes] = useState([])
  const [trainers, setTrainers] = useState([])
  const [filters, setFilters] = useState({ from: today(), to: '', type: '', trainerId: '' })
  const [editing, setEditing] = useState(null)

  useEffect(() => {
    api('/api/types').then(setTypes)
    api('/api/trainers').then(setTrainers)
  }, [])

  // GET /api/sessions?from=&to=&type= (type = tüübi nimi)
  const { items, error, loading, reload } = useList(
    () => api('/api/sessions', { query: { from: filters.from, to: filters.to, type: filters.type } }),
    [filters.from, filters.to, filters.type]
  )
  const shown = filters.trainerId ? items.filter((s) => String(s.trainerId) === filters.trainerId) : items

  const setFilter = (k, v) => setFilters((f) => ({ ...f, [k]: v }))

  async function edit(s) {
    try {
      // GET /api/sessions/{id} annab andmed täpselt sellisel kujul, nagu PUT vajab.
      const data = await api(`/api/sessions/${s.id}`)
      setEditing({ ...data, id: s.id })
    } catch (e) {
      window.alert(e.message)
    }
  }

  async function remove(s) {
    const text = `Kustutada ${s.type} ${formatDateTime(s.startsAt)}?\nKõik broneeringud tühistatakse ja külastused tagastatakse klientidele.`
    if (!window.confirm(text)) return
    try {
      await api(`/api/sessions/${s.id}`, { method: 'DELETE' })
      reload()
    } catch (e) {
      window.alert(e.message)
    }
  }

  return (
    <section>
      <header className="page-head">
        <h1>Tunniplaan</h1>
        <button className="btn primary" onClick={() => setEditing({ ...empty, date: today() })}>+ Lisa treening</button>
      </header>

      <div className="filters">
        <label>Alates <input type="date" value={filters.from} onChange={(e) => setFilter('from', e.target.value)} /></label>
        <label>Kuni <input type="date" value={filters.to} onChange={(e) => setFilter('to', e.target.value)} /></label>
        <select value={filters.type} onChange={(e) => setFilter('type', e.target.value)}>
          <option value="">Kõik tüübid</option>
          {types.map((t) => <option key={t.id} value={t.name}>{t.name}</option>)}
        </select>
        <select value={filters.trainerId} onChange={(e) => setFilter('trainerId', e.target.value)}>
          <option value="">Kõik treenerid</option>
          {trainers.map((t) => <option key={t.id} value={t.id}>{t.name}</option>)}
        </select>
      </div>

      <ErrorBox error={error} />
      <div className="table-wrap">
        <table className={loading ? 'loading' : ''}>
          <thead>
            <tr>
              <th>ID</th>
              <th>Aeg</th>
              <th>Treening</th>
              <th>Treener</th>
              <th>Kohad</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {shown.map((s) => (
              <tr key={s.id}>
                <td className="muted">{s.id}</td>
                <td>{formatDateTime(s.startsAt)} <span className="muted">· {s.durationMinutes} min</span></td>
                <td>{s.type}</td>
                <td>{s.trainer}</td>
                <td>
                  <span className={s.free === 0 ? 'badge full' : s.free <= 3 ? 'badge few' : 'badge ok'}>
                    {s.capacity - s.free}/{s.capacity}
                  </span>
                </td>
                <td className="actions">
                  <button className="btn ghost small" onClick={() => edit(s)}>Muuda</button>
                  <button className="btn danger small" onClick={() => remove(s)}>Kustuta</button>
                </td>
              </tr>
            ))}
            {!shown.length && !loading && <tr><td colSpan="6" className="muted center">Treeninguid ei leitud.</td></tr>}
          </tbody>
        </table>
      </div>
      <p className="muted">Kokku {shown.length}</p>

      {editing && (
        <SessionForm initial={editing} types={types} trainers={trainers}
          onClose={() => setEditing(null)} onSaved={() => { setEditing(null); reload() }} />
      )}
    </section>
  )
}

function SessionForm({ initial, types, trainers, onClose, onSaved }) {
  const [form, setForm] = useState(initial)
  const [errors, setErrors] = useState({})
  const [error, setError] = useState('')
  const set = (k, v) => setForm((f) => ({ ...f, [k]: v }))

  function validate() {
    const v = {}
    if (!form.typeId) v.typeId = 'Vali treeningutüüp.'
    if (!form.trainerId) v.trainerId = 'Vali treener.'
    if (!/^\d{4}-\d{2}-\d{2}$/.test(form.date)) v.date = 'Vali kuupäev.'
    if (!/^\d{2}:\d{2}$/.test(form.time)) v.time = 'Kellaaeg kujul TT:MM.'
    else if (new Date(`${form.date}T${form.time}`) < new Date()) v.date = 'Treening ei saa alata minevikus.'
    if (!(form.durationMinutes >= 15 && form.durationMinutes <= 240)) v.durationMinutes = '15–240 minutit.'
    if (!(form.capacity >= 1 && form.capacity <= 200)) v.capacity = '1–200 kohta.'
    setErrors(v)
    return !Object.keys(v).length
  }

  async function submit(e) {
    e.preventDefault()
    if (!validate()) return
    const body = {
      typeId: Number(form.typeId),
      trainerId: Number(form.trainerId),
      date: form.date,
      time: form.time,
      durationMinutes: Number(form.durationMinutes),
      capacity: Number(form.capacity)
    }
    try {
      if (form.id) await api(`/api/sessions/${form.id}`, { method: 'PUT', body })
      else await api('/api/sessions', { method: 'POST', body })
      onSaved()
    } catch (err) {
      setError(err.message)
    }
  }

  return (
    <Modal title={form.id ? 'Muuda treeningut' : 'Uus treening'} onClose={onClose}>
      <form onSubmit={submit} noValidate className="form">
        <ErrorBox error={error} />
        <div className="grid2">
          <Field label="Treeningutüüp" error={errors.typeId}>
            <select value={form.typeId} onChange={(e) => set('typeId', e.target.value)}>
              <option value="">Vali…</option>
              {types.map((t) => <option key={t.id} value={t.id}>{t.name}</option>)}
            </select>
          </Field>
          <Field label="Treener" error={errors.trainerId}>
            <select value={form.trainerId} onChange={(e) => set('trainerId', e.target.value)}>
              <option value="">Vali…</option>
              {trainers.map((t) => <option key={t.id} value={t.id}>{t.name} ({t.specialty})</option>)}
            </select>
          </Field>
          <Field label="Kuupäev" error={errors.date}>
            <input type="date" value={form.date} onChange={(e) => set('date', e.target.value)} />
          </Field>
          <Field label="Kellaaeg" error={errors.time}>
            <input type="time" value={form.time} onChange={(e) => set('time', e.target.value)} />
          </Field>
          <Field label="Kestus (min)" error={errors.durationMinutes}>
            <input type="number" value={form.durationMinutes} onChange={(e) => set('durationMinutes', e.target.valueAsNumber)} />
          </Field>
          <Field label="Kohtade arv" error={errors.capacity}>
            <input type="number" value={form.capacity} onChange={(e) => set('capacity', e.target.valueAsNumber)} />
          </Field>
        </div>
        {form.id && <p className="muted">Kui kohti lisandub, viib Worker ootejärjekorrast inimesed uutele kohtadele.</p>}
        <div className="form-actions">
          <button type="button" className="btn ghost" onClick={onClose}>Loobu</button>
          <button className="btn primary">Salvesta</button>
        </div>
      </form>
    </Modal>
  )
}
