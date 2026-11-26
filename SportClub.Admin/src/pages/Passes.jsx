import { useState } from 'react'
import { api, formatDate } from '../api.js'
import { ErrorBox, useDebounced, useList } from '../components/ui.jsx'
import { usePrefs } from '../i18n.jsx'

/** Kõik ostetud kuukaardid (GET /api/passes?search=). */
export default function Passes() {
  const { t } = usePrefs()
  const [searchText, setSearchText] = useState('')
  const search = useDebounced(searchText)
  const { items, error, loading } = useList(() => api('/api/passes', { query: { search } }), [search])

  return (
    <section>
      <header className="page-head"><h1>{t('passes.title')}</h1></header>
      <p className="muted">{t('passes.hint')}</p>
      <div className="filters">
        <input placeholder={t('common.searchClient')} value={searchText} onChange={(e) => setSearchText(e.target.value)} />
      </div>
      <ErrorBox error={error} />
      <div className="table-wrap">
        <table className={loading ? 'loading' : ''}>
          <thead>
            <tr>
              <th>{t('bookings.client')}</th>
              <th>{t('passes.plan')}</th>
              <th>{t('plans.valid')}</th>
              <th>{t('plans.visits')}</th>
              <th>{t('plans.price')}</th>
              <th>{t('passes.card')}</th>
              <th>{t('bookings.status')}</th>
            </tr>
          </thead>
          <tbody>
            {items.map((p) => (
              <tr key={p.id}>
                <td><strong>{p.client}</strong><div className="muted">{p.email}</div></td>
                <td>{p.plan}</td>
                <td>{formatDate(p.validFrom)} – {formatDate(p.validUntil)}</td>
                <td>{t('passes.left', { left: p.visitsLeft, total: p.visitsTotal })}</td>
                <td>{Number(p.pricePaid).toFixed(2)} €</td>
                <td className="muted">{p.card || '—'}</td>
                <td><span className={p.active ? 'badge ok' : 'badge full'}>{p.active ? t('passes.active') : t('passes.inactive')}</span></td>
              </tr>
            ))}
            {!items.length && !loading && <tr><td colSpan="7" className="muted center">{t('passes.empty')}</td></tr>}
          </tbody>
        </table>
      </div>
    </section>
  )
}
