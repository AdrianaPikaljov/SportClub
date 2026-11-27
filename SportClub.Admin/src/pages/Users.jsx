import { useState } from 'react'
import { api, getSavedUser } from '../api.js'
import { ErrorBox, useDebounced, useList } from '../components/ui.jsx'
import { usePrefs } from '../i18n.jsx'


/** Kõik kasutajad (GET /api/users?search=). */
export default function Users() {
  const { t } = usePrefs()
  const [searchText, setSearchText] = useState('')
  const search = useDebounced(searchText)
  const { items, error, loading, reload } = useList(() => api('/api/users', { query: { search } }), [search])
  const me = getSavedUser() // iseennast kustutada ei saa (API kontrollib ka)

  async function remove(u) {
    if (!window.confirm(t('users.confirmDelete', { name: u.name }))) return
    try {
      await api(`/api/users/${u.id}`, { method: 'DELETE' })
      reload()
    } catch (e) {
      window.alert(e.message)
    }
  }

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
              <th></th>
            </tr>
          </thead>
          <tbody>
            {items.map((u) => (
              <tr key={u.id}>
                <td>{u.id}</td>
                <td><strong>{u.name}</strong></td>
                <td>{u.email}</td>
                <td>{t(`role.${u.role}`)}</td>
                <td className="actions">
                  {u.role === 'Trainer'
                    ? <span className="muted">{t('users.trainerHint')}</span>
                    : u.role !== 'Service' && u.name !== me?.name &&
                      <button className="btn danger small" onClick={() => remove(u)}>{t('common.delete')}</button>}
                </td>
              </tr>
            ))}
            {!items.length && !loading && <tr><td colSpan="5" className="muted center">{t('users.empty')}</td></tr>}
          </tbody>
        </table>
      </div>
      <p className="muted">{t('common.total', { n: items.length })}</p>
    </section>
  )
}
