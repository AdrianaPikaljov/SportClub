import { useEffect, useState } from 'react'
import { getSavedUser, setSession } from './api.js'
import Login from './pages/Login.jsx'
import Sessions from './pages/Sessions.jsx'
import Trainers from './pages/Trainers.jsx'
import TrainingTypes from './pages/TrainingTypes.jsx'
import PassPlans from './pages/PassPlans.jsx'
import Bookings from './pages/Bookings.jsx'
import Passes from './pages/Passes.jsx'
import Users from './pages/Users.jsx'
import Stats from './pages/Stats.jsx'

const PAGES = [
  { id: 'sessions', label: 'Tunniplaan', component: Sessions },
  { id: 'trainers', label: 'Treenerid', component: Trainers },
  { id: 'types', label: 'Treeningutüübid', component: TrainingTypes },
  { id: 'plans', label: 'Tariifid', component: PassPlans },
  { id: 'bookings', label: 'Broneeringud', component: Bookings },
  { id: 'passes', label: 'Kuukaardid', component: Passes },
  { id: 'users', label: 'Kasutajad', component: Users },
  { id: 'stats', label: 'Statistika', component: Stats }
]

export default function App() {
  // Kasutaja (name, role) on salvestatud sisselogimisel; token aegumisel saadab API 401 → logout.
  const [user, setUser] = useState(() => {
    const saved = getSavedUser()
    return saved?.role === 'Admin' ? saved : null
  })
  const [page, setPage] = useState('sessions')

  useEffect(() => {
    const onLogout = () => setUser(null)
    window.addEventListener('logout', onLogout)
    return () => window.removeEventListener('logout', onLogout)
  }, [])

  function logout() {
    setSession(null)
    setUser(null)
  }

  if (!user) return <Login onLogin={setUser} />

  const Current = PAGES.find((p) => p.id === page).component

  return (
    <div className="layout">
      <aside className="sidebar">
        <div className="brand">
          <span className="brand-mark" aria-hidden="true">
            <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="#14161A" strokeWidth="2.2" strokeLinecap="round"><path d="M6 8v8M18 8v8M3 10v4M21 10v4M6 12h12" /></svg>
          </span>
          SportClub <small>admin</small>
        </div>
        <nav>
          {PAGES.map((p) => (
            <button key={p.id} className={p.id === page ? 'nav-item active' : 'nav-item'} onClick={() => setPage(p.id)}>
              {p.label}
            </button>
          ))}
        </nav>
        <div className="sidebar-user">
          <span>{user.name}</span>
          <button className="btn ghost small" onClick={logout}>Logi välja</button>
        </div>
      </aside>
      <main className="content">
        <Current />
      </main>
    </div>
  )
}
