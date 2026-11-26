import { useState } from 'react'
import { api } from '../api.js'
import { ErrorBox, useDebounced, useList } from '../components/ui.jsx'
import { usePrefs } from '../i18n.jsx'


/** Kõik kasutajad (GET /api/users?search=). */
export default function Users() {
  const { t } = usePrefs()
  const [searchText, setSearchText] = useState('')
  const search = useDebounced(searchText)
  const { items, error, loading } = useList(() => api('/api/users', { query: { search } }), [search])

  return (
    <section>
      <header className="page-head"><h1>{t('users.title')}</h1></header>
      <div className="filters">
        <input placeholder={t('common.searchName')} value={searchText} onChange={(e) => setSearchText(e.target.value)} />
      </div>
      <ErrorBox error={error} />
      <div className="table-wrap">
        <table className={loading ? 'loading' : ''}>
          <thead>
            <tr>
              <th>{t('common.id')}</th>
              <th>{t('common.name')}</th>
              <th>{t('users.email')}</th>
              <th>{t('users.role')}</th>
            </tr>
          </thead>
          <tbody>
            {items.map((u) => (
              <tr key={u.id}>
                <td>{u.id}</td>
                <td><strong>{u.name}</strong></td>
                <td>{u.email}</td>
                <td>{t(`role.${u.role}`)}</td>
              </tr>
            ))}
            {!items.length && !loading && <tr><td colSpan="4" className="muted center">{t('users.empty')}</td></tr>}
          </tbody>
        </table>
      </div>
      <p className="muted">{t('common.total', { n: items.length })}</p>
    </section>
  )
}
