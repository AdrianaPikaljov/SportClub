import { useState } from 'react'
import { api, formatDateTime } from '../api.js'
import { ErrorBox, Pager, useDebounced, usePagedList } from '../components/ui.jsx'
import { usePrefs } from '../i18n.jsx'

/** Kõik broneeringud: otsing, filter, sorteerimine ja leheküljed (GET /api/bookings). */
export default function Bookings() {
  const { t } = usePrefs()
  const [searchText, setSearchText] = useState('')
  const [status, setStatus] = useState('')
  const [sort, setSort] = useState('Uusimad')
  const [page, setPage] = useState(1)
  const [info, setInfo] = useState('')
  const search = useDebounced(searchText)

  const { data, error, loading, reload } = usePagedList(
    () => api('/api/bookings', { query: { search, status, sort, page } }),
    [search, status, sort, page]
  )

  async function cancel(b) {
    const vars = { client: b.client, training: `${b.type} ${formatDateTime(b.startsAt)}` }
    if (!window.confirm(t(b.status === 'Waitlisted' ? 'bookings.confirmLeave' : 'bookings.confirmCancel', vars))) return
    try {
      await api(`/api/bookings/${b.id}`, { method: 'DELETE' })
      reload()
    } catch (e) {
      window.alert(e.message)
    }
  }

  async function promote() {
    try {
      const res = await api('/api/waitlist/promote', { method: 'POST' })
      const names = res.promoted.map((p) => p.name).join(', ')
      setInfo(res.promoted.length
        ? t('bookings.promoted', { names })
        : t('bookings.nobody') + (res.removed ? t('bookings.removed', { n: res.removed }) : ''))
      reload()
    } catch (e) {
      setInfo(e.message)
    }
  }

  const setFilter = (setter) => (e) => { setter(e.target.value); setPage(1) }

  return (
    <section>
      <header className="page-head">
        <h1>{t('bookings.title')}</h1>
        <button className="btn ghost" onClick={promote}>{t('bookings.promote')}</button>
      </header>
      {info && <p className="muted">{info}</p>}
      <div className="filters">
        <input placeholder={t('common.searchClient')} value={searchText} onChange={setFilter(setSearchText)} />
        <select value={status} onChange={setFilter(setStatus)}>
          <option value="">{t('bookings.all')}</option>
          <option value="Booked">{t('bookings.booked')}</option>
          <option value="Waitlisted">{t('bookings.waitlisted')}</option>
        </select>
        <select value={sort} onChange={setFilter(setSort)}>
          <option value="Uusimad">{t('bookings.newest')}</option>
          <option value="Vanimad">{t('bookings.oldest')}</option>
        </select>
      </div>
      <ErrorBox error={error} />
      <div className="table-wrap">
        <table className={loading ? 'loading' : ''}>
          <thead>
            <tr>
              <th>{t('bookings.client')}</th>
              <th>{t('sessions.training')}</th>
              <th>{t('sessions.time')}</th>
              <th>{t('bookings.status')}</th>
              <th>{t('bookings.attendance')}</th>
              <th>{t('bookings.created')}</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {data.items.map((b) => (
              <tr key={b.id}>
                <td><strong>{b.client}</strong></td>
                <td>{b.type} <span className="muted">· {b.trainer}</span></td>
                <td>{formatDateTime(b.startsAt)}</td>
                <td>
                  {b.status === 'Booked'
                    ? <span className="badge ok">{t('bookings.booked')}</span>
                    : <span className="badge few">{t('bookings.queue', { n: b.position })}</span>}
                </td>
                <td>{b.attended === true ? t('bookings.present') : b.attended === false ? t('bookings.absent') : <span className="muted">–</span>}</td>
                <td className="muted">{formatDateTime(b.createdAt)}</td>
                <td className="actions">
                  <button className="btn danger small" onClick={() => cancel(b)}>{t('bookings.cancel')}</button>
                </td>
              </tr>
            ))}
            {!data.items.length && !loading && <tr><td colSpan="7" className="muted center">{t('bookings.empty')}</td></tr>}
          </tbody>
        </table>
      </div>
      <Pager page={data.page} totalPages={data.totalPages} total={data.total} onPage={setPage} />
    </section>
  )
}
