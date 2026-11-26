import { useState } from 'react'
import { api, setSession } from '../api.js'
import { ErrorBox, Field } from '../components/ui.jsx'

export default function Login({ onLogin }) {
  const [email, setEmail] = useState('admin@sportclub.ee')
  const [password, setPassword] = useState('')
  const [errors, setErrors] = useState({})
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)

  async function submit(e) {
    e.preventDefault()
    const v = {}
    if (!/^\S+@\S+\.\S+$/.test(email)) v.email = 'Sisesta korrektne e-post.'
    if (!password) v.password = 'Parool on kohustuslik.'
    setErrors(v)
    if (Object.keys(v).length) return

    setBusy(true)
    setError('')
    try {
      const res = await api('/api/auth/login', { method: 'POST', body: { email, password } })
      if (res.role !== 'Admin') {
        setError('Admin-liidesesse saab siseneda ainult administraator.')
        return
      }
      setSession(res)
      onLogin({ name: res.name, role: res.role })
    } catch (err) {
      setError(err.message)
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="login">
      <form className="login-card" onSubmit={submit} noValidate>
        <h1>SportClub admin</h1>
        <p className="muted">Halda treenereid, treeningutüüpe, tunniplaani, tariife ja broneeringuid.</p>
        <ErrorBox error={error} />
        <Field label="E-post" error={errors.email}>
          <input type="email" value={email} onChange={(e) => setEmail(e.target.value)} autoFocus />
        </Field>
        <Field label="Parool" error={errors.password}>
          <input type="password" value={password} onChange={(e) => setPassword(e.target.value)} />
        </Field>
        <button className="btn primary" disabled={busy}>{busy ? 'Sisenen…' : 'Logi sisse'}</button>
      </form>
    </div>
  )
}
