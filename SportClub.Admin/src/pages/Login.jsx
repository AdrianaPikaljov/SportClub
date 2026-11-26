import { useState } from 'react'
import { api, setSession } from '../api.js'
import { ErrorBox, Field } from '../components/ui.jsx'
import { PrefsSwitch, usePrefs } from '../i18n.jsx'

export default function Login({ onLogin }) {
  const { t } = usePrefs()
  const [email, setEmail] = useState('admin@sportclub.ee')
  const [password, setPassword] = useState('')
  const [errors, setErrors] = useState({})
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)

  async function submit(e) {
    e.preventDefault()
    const v = {}
    if (!/^\S+@\S+\.\S+$/.test(email)) v.email = t('login.badEmail')
    if (!password) v.password = t('login.noPassword')
    setErrors(v)
    if (Object.keys(v).length) return

    setBusy(true)
    setError('')
    try {
      const res = await api('/api/auth/login', { method: 'POST', body: { email, password } })
      if (res.role !== 'Admin') {
        setError(t('login.notAdmin'))
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
        <PrefsSwitch />
        <h1>{t('login.title')}</h1>
        <p className="muted">{t('login.text')}</p>
        <ErrorBox error={error} />
        <Field label={t('login.email')} error={errors.email}>
          <input type="email" value={email} onChange={(e) => setEmail(e.target.value)} autoFocus />
        </Field>
        <Field label={t('login.password')} error={errors.password}>
          <input type="password" value={password} onChange={(e) => setPassword(e.target.value)} />
        </Field>
        <button className="btn primary" disabled={busy}>{busy ? t('login.busy') : t('login.submit')}</button>
      </form>
    </div>
  )
}
