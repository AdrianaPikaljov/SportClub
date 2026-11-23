import { useEffect, useState } from 'react'
import { api, formatDateTime, fromLocalInput, toLocalInput } from '../api.js'
import { ErrorBox, Field, Modal, Pager, SortHeader, useDebounced, usePagedList } from '../components/ui.jsx'

const empty = { trainingTypeId: '', trainerId: '', startsAt: '', durationMinutes: 60, room: '', maxParticipants: 10 }

export default function Sessions() {
  const [types, setTypes] = useState([])
  const [trainers, setTrainers] = useState([])
  const [filters, setFilters] = useState({ from: '', to: '', type: '', trainerId: '', search: '' })
  const [sort, setSort] = useState('startsAt')
  const [page, setPage] = useState(1)
  const [editing, setEditing] = useState(null)
  const search = useDebounced(filters.search)

  useEffect(() => {
    api('/api/training-types', { query: { pageSize: 100 } }).then((d) => setTypes(d.items))
    api('/api/trainers', { query: { pageSize: 100 } }).then((d) => setTrainers(d.items))
  }, [])

  const { data, error, loading, reload } = usePagedList(
    () => api('/api/sessions', {
      query: {
        from: filters.from ? fromLocalInput(filters.from + 'T00:00') : undefined,
        to: filters.to ? fromLocalInput(filters.to + 'T23:59') : undefined,
        type: filters.type, trainerId: filters.trainerId, search, sort, page, pageSize: 15
      }
    }),
    [filters.from, filters.to, filters.type, filters.trainerId, search, sort, page]
  )

  const setFilter = (k, v) => { setFilters((f) => ({ ...f, [k]: v })); setPage(1) }

  async function remove(s) {
    if (!window.confirm(`Kustutada ${s.trainingTypeName} ${formatDateTime(s.startsAt)}?`)) return
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
        <button className="btn primary" onClick={() => setEditing(empty)}>+ Lisa treening</button>
      </header>

      <div className="filters">
        <input placeholder="Otsi tüübi, treeneri või ruumi järgi" value={filters.search} onChange={(e) => setFilter('search', e.target.value)} />
        <label>Alates <input type="date" value={filters.from} onChange={(e) => setFilter('from', e.target.value)} /></label>
        <label>Kuni <input type="date" value={filters.to} onChange={(e) => setFilter('to', e.target.value)} /></label>
        <select value={filters.type} onChange={(e) => setFilter('type', e.target.value)}>
          <option value="">Kõik tüübid</option>
          {types.map((t) => <option key={t.id} value={t.id}>{t.name}</option>)}
        </select>
        <select value={filters.trainerId} onChange={(e) => setFilter('trainerId', e.target.value)}>
          <option value="">Kõik treenerid</option>
          {trainers.map((t) => <option key={t.id} value={t.id}>{t.fullName}</option>)}
        </select>
      </div>

      <ErrorBox error={error} />
      <div className="table-wrap">
        <table className={loading ? 'loading' : ''}>
          <thead>
            <tr>
              <SortHeader field="startsAt" label="Aeg" sort={sort} onSort={setSort} />
              <SortHeader field="type" label="Treening" sort={sort} onSort={setSort} />
              <SortHeader field="trainer" label="Treener" sort={sort} onSort={setSort} />
              <SortHeader field="room" label="Ruum" sort={sort} onSort={setSort} />
              <th>Kohad</th>
              <th>Ootel</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {data.items.map((s) => (
              <tr key={s.id}>
                <td>{formatDateTime(s.startsAt)} <span className="muted">· {s.durationMinutes} min</span></td>
                <td>{s.trainingTypeName}</td>
                <td>{s.trainerName}</td>
                <td>{s.room}</td>
                <td><span className={s.freeSpots === 0 ? 'badge full' : s.freeSpots <= 3 ? 'badge few' : 'badge ok'}>{s.bookedCount}/{s.maxParticipants}</span></td>
                <td>{s.waitlistCount || '–'}</td>
                <td className="actions">
                  <button className="btn ghost small" onClick={() => setEditing({ ...s, startsAt: toLocalInput(s.startsAt) })}>Muuda</button>
                  <button className="btn danger small" onClick={() => remove(s)}>Kustuta</button>
                </td>
              </tr>
            ))}
            {!data.items.length && !loading && <tr><td colSpan="7" className="muted center">Treeninguid ei leitud.</td></tr>}
          </tbody>
        </table>
      </div>
      <Pager page={data.page} totalPages={data.totalPages} totalCount={data.totalCount} onPage={setPage} />

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
    if (!form.trainingTypeId) v.trainingTypeId = 'Vali treeningutüüp.'
    if (!form.trainerId) v.trainerId = 'Vali treener.'
    if (!form.startsAt) v.startsAt = 'Vali algusaeg.'
    else if (new Date(form.startsAt) < new Date()) v.startsAt = 'Treening ei saa alata minevikus.'
    if (!form.room.trim()) v.room = 'Ruum on kohustuslik.'
    if (!(form.durationMinutes >= 15 && form.durationMinutes <= 240)) v.durationMinutes = '15–240 minutit.'
    if (!(form.maxParticipants >= 1 && form.maxParticipants <= 200)) v.maxParticipants = '1–200 kohta.'
    setErrors(v)
    return !Object.keys(v).length
  }

  async function submit(e) {
    e.preventDefault()
    if (!validate()) return
    const body = {
      trainingTypeId: Number(form.trainingTypeId),
      trainerId: Number(form.trainerId),
      startsAt: fromLocalInput(form.startsAt),
      durationMinutes: Number(form.durationMinutes),
      room: form.room,
      maxParticipants: Number(form.maxParticipants)
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
          <Field label="Treeningutüüp" error={errors.trainingTypeId}>
            <select value={form.trainingTypeId} onChange={(e) => set('trainingTypeId', e.target.value)}>
              <option value="">Vali…</option>
              {types.map((t) => <option key={t.id} value={t.id}>{t.name}</option>)}
            </select>
          </Field>
          <Field label="Treener" error={errors.trainerId}>
            <select value={form.trainerId} onChange={(e) => set('trainerId', e.target.value)}>
              <option value="">Vali…</option>
              {trainers.map((t) => <option key={t.id} value={t.id}>{t.fullName}</option>)}
            </select>
          </Field>
          <Field label="Algus" error={errors.startsAt}>
            <input type="datetime-local" value={form.startsAt} onChange={(e) => set('startsAt', e.target.value)} />
          </Field>
          <Field label="Kestus (min)" error={errors.durationMinutes}>
            <input type="number" value={form.durationMinutes} onChange={(e) => set('durationMinutes', e.target.valueAsNumber)} />
          </Field>
          <Field label="Ruum" error={errors.room}>
            <input value={form.room} onChange={(e) => set('room', e.target.value)} />
          </Field>
          <Field label="Max osalejaid" error={errors.maxParticipants}>
            <input type="number" value={form.maxParticipants} onChange={(e) => set('maxParticipants', e.target.valueAsNumber)} />
          </Field>
        </div>
        <div className="form-actions">
          <button type="button" className="btn ghost" onClick={onClose}>Loobu</button>
          <button className="btn primary">Salvesta</button>
        </div>
      </form>
    </Modal>
  )
}
