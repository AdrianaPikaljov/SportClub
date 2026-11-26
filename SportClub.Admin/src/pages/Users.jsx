import { useState } from 'react'
import { api } from '../api.js'
import { ErrorBox, useDebounced, useList } from '../components/ui.jsx'

const ROLES = { Admin: 'Admin', Trainer: 'Treener', Client: 'Klient', Service: 'Taustateenus' }

/** Kõik kasutajad (GET /api/users?search=). */
export default function Users() {
  const [searchText, setSearchText] = useState('')
  const search = useDebounced(searchText)
  const { items, error, loading } = useList(() => api('/api/users', { query: { search } }), [search])

  return (
    <section>
      <header className="page-head"><h1>Kasutajad</h1></header>
      <div className="filters">
        <input placeholder="Otsi nime järgi" value={searchText} onChange={(e) => setSearchText(e.target.value)} />
      </div>
      <ErrorBox error={error} />
      <div className="table-wrap">
        <table className={loading ? 'loading' : ''}>
          <thead>
            <tr>
              <th>ID</th>
              <th>Nimi</th>
              <th>E-post</th>
              <th>Roll</th>
            </tr>
          </thead>
          <tbody>
            {items.map((u) => (
              <tr key={u.id}>
                <td>{u.id}</td>
                <td><strong>{u.name}</strong></td>
                <td>{u.email}</td>
                <td>{ROLES[u.role] || u.role}</td>
              </tr>
            ))}
            {!items.length && !loading && <tr><td colSpan="4" className="muted center">Kasutajaid ei leitud.</td></tr>}
          </tbody>
        </table>
      </div>
      <p className="muted">Kokku {items.length}</p>
    </section>
  )
}
