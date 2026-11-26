import { createContext, useContext, useEffect, useState } from 'react'

// Liidese tekstid kahes keeles. API veateated tulevad eesti keeles.
const texts = {
  et: {
    // menüü
    'nav.sessions': 'Tunniplaan', 'nav.trainers': 'Treenerid', 'nav.types': 'Treeningutüübid', 'nav.plans': 'Tariifid',
    'nav.bookings': 'Broneeringud', 'nav.passes': 'Kuukaardid', 'nav.users': 'Kasutajad', 'nav.stats': 'Statistika',
    'logout': 'Logi välja', 'theme.light': 'Hele', 'theme.dark': 'Tume',
    // ühised
    'common.edit': 'Muuda', 'common.delete': 'Kustuta', 'common.cancel': 'Loobu', 'common.save': 'Salvesta',
    'common.choose': 'Vali…', 'common.total': 'Kokku {n}', 'common.from': 'Alates', 'common.to': 'Kuni',
    'common.searchName': 'Otsi nime järgi', 'common.searchClient': 'Otsi kliendi nime järgi',
    'common.id': 'ID', 'common.name': 'Nimi', 'common.description': 'Kirjeldus', 'common.minutes': 'min',
    'pager.prev': '‹ Eelmine', 'pager.next': 'Järgmine ›',
    'err.connection': 'API-ga ei saa ühendust ({url}). Kas SportClub.Api töötab?',
    // login
    'login.title': 'SportClub admin', 'login.text': 'Halda treenereid, treeningutüüpe, tunniplaani, tariife ja broneeringuid.',
    'login.email': 'E-post', 'login.password': 'Parool', 'login.submit': 'Logi sisse', 'login.busy': 'Sisenen…',
    'login.badEmail': 'Sisesta korrektne e-post.', 'login.noPassword': 'Parool on kohustuslik.',
    'login.notAdmin': 'Admin-liidesesse saab siseneda ainult administraator.',
    // tunniplaan
    'sessions.title': 'Tunniplaan', 'sessions.add': '+ Lisa treening', 'sessions.allTypes': 'Kõik tüübid',
    'sessions.allTrainers': 'Kõik treenerid', 'sessions.time': 'Aeg', 'sessions.training': 'Treening',
    'sessions.trainer': 'Treener', 'sessions.spots': 'Kohad', 'sessions.empty': 'Treeninguid ei leitud.',
    'sessions.confirmDelete': 'Kustutada {name} {time}?\nKõik broneeringud tühistatakse ja külastused tagastatakse klientidele.',
    'sessions.editTitle': 'Muuda treeningut', 'sessions.newTitle': 'Uus treening', 'sessions.type': 'Treeningutüüp',
    'sessions.date': 'Kuupäev', 'sessions.clock': 'Kellaaeg', 'sessions.duration': 'Kestus (min)', 'sessions.capacity': 'Kohtade arv',
    'sessions.capacityHint': 'Kui kohti lisandub, viib Worker ootejärjekorrast inimesed uutele kohtadele.',
    'sessions.v.type': 'Vali treeningutüüp.', 'sessions.v.trainer': 'Vali treener.', 'sessions.v.date': 'Vali kuupäev.',
    'sessions.v.time': 'Kellaaeg kujul TT:MM.', 'sessions.v.past': 'Treening ei saa alata minevikus.',
    'sessions.v.duration': '15–240 minutit.', 'sessions.v.capacity': '1–200 kohta.',
    // treenerid
    'trainers.title': 'Treenerid', 'trainers.add': '+ Lisa treener',
    'trainers.hint': 'Igal treeneril on oma konto: ta logib mobiilirakendusse sisse siin näidatud e-posti ja parooliga.',
    'trainers.specialty': 'Eriala', 'trainers.email': 'E-post (konto)', 'trainers.phone': 'Telefon', 'trainers.noAccount': 'konto puudub',
    'trainers.empty': 'Treenereid pole.', 'trainers.confirmDelete': 'Kustutada treener {name}? Kustutatakse ka tema konto ja toimunud treeningud.',
    'trainers.newTitle': 'Uus treener', 'trainers.editTitle': 'Muuda treenerit', 'trainers.loginEmail': 'E-post (sisselogimiseks)',
    'trainers.password': 'Parool', 'trainers.newPassword': 'Uus parool (jäta tühjaks, kui ei muuda)',
    'trainers.v.name': 'Nimi peab olema vähemalt 2 tähemärki.', 'trainers.v.specialty': 'Eriala on kohustuslik.',
    'trainers.v.email': 'Sisesta korrektne e-post.', 'trainers.v.phone': 'Vigane telefoninumber, nt +372 5555 1234.',
    'trainers.v.password': 'Parool peab olema vähemalt 8 tähemärki.',
    // tüübid
    'types.title': 'Treeningutüübid', 'types.add': '+ Lisa tüüp', 'types.empty': 'Tüüpe pole.',
    'types.confirmDelete': 'Kustutada treeningutüüp {name}?', 'types.editTitle': 'Muuda tüüpi', 'types.newTitle': 'Uus treeningutüüp',
    'types.v.name': 'Nimi peab olema 2–60 tähemärki.',
    // tariifid
    'plans.title': 'Kuukaardi tariifid', 'plans.add': '+ Lisa tariif', 'plans.hint': 'Tariifid, mida kliendid saavad mobiilirakenduses osta.',
    'plans.visits': 'Külastusi', 'plans.valid': 'Kehtib', 'plans.days': '{n} päeva', 'plans.price': 'Hind',
    'plans.remove': 'Eemalda müügist', 'plans.empty': 'Tariife pole.',
    'plans.confirmDelete': 'Eemaldada tariif {name} müügist? Juba ostetud kuukaardid jäävad alles.',
    'plans.editTitle': 'Muuda tariifi', 'plans.newTitle': 'Uus tariif', 'plans.daysField': 'Kehtib (päeva)', 'plans.priceField': 'Hind (€)',
    'plans.editHint': 'Muudatus ei mõjuta juba ostetud kuukaarte.',
    'plans.v.name': 'Nimi peab olema vähemalt 2 tähemärki.', 'plans.v.visits': '1–100 külastust.', 'plans.v.days': '1–365 päeva.',
    'plans.v.price': 'Hind 0–1000 €.',
    // broneeringud
    'bookings.title': 'Broneeringud', 'bookings.promote': 'Vii järjekorrast kohtadele', 'bookings.all': 'Kõik',
    'bookings.booked': 'Registreeritud', 'bookings.waitlisted': 'Ootejärjekorras', 'bookings.newest': 'Uusimad enne',
    'bookings.oldest': 'Vanimad enne', 'bookings.client': 'Klient', 'bookings.status': 'Staatus', 'bookings.attendance': 'Kohalolu',
    'bookings.created': 'Loodud', 'bookings.queue': 'Järjekorras #{n}', 'bookings.present': 'Kohal', 'bookings.absent': 'Puudus',
    'bookings.cancel': 'Tühista', 'bookings.empty': 'Broneeringuid ei leitud.',
    'bookings.confirmCancel': '{client}: tühistada broneering ({training})?',
    'bookings.confirmLeave': '{client}: eemaldada ootejärjekorrast ({training})?',
    'bookings.promoted': 'Järjekorrast viidi treeningule: {names}.', 'bookings.nobody': 'Kedagi polnud järjekorrast viia.',
    'bookings.removed': ' Järjekorrast eemaldati {n}.',
    // kuukaardid
    'passes.title': 'Kuukaardid', 'passes.hint': 'Kliendid ostavad kuukaardi ise mobiilirakenduses. Korraga saab olla üks aktiivne kaart.',
    'passes.plan': 'Tariif', 'passes.left': '{left} / {total} alles', 'passes.active': 'Aktiivne', 'passes.inactive': 'Ei kehti',
    'passes.empty': 'Kuukaarte ei leitud.', 'passes.card': 'Kaart',
    // kasutajad
    'users.title': 'Kasutajad', 'users.email': 'E-post', 'users.role': 'Roll', 'users.empty': 'Kasutajaid ei leitud.',
    'role.Admin': 'Admin', 'role.Trainer': 'Treener', 'role.Client': 'Klient', 'role.Service': 'Taustateenus',
    // statistika
    'stats.title': 'Populaarseimad treeningud', 'stats.bookings': 'broneeringut', 'stats.attended': 'käis kohal',
    'stats.fill': 'keskmine täituvus', 'stats.row': '{bookings} broneeringut · {sessions} treeningut · kohal {attended} · täituvus {fill}%',
    'stats.empty': 'Andmed puuduvad.'
  },
  en: {
    'nav.sessions': 'Schedule', 'nav.trainers': 'Trainers', 'nav.types': 'Training types', 'nav.plans': 'Pass plans',
    'nav.bookings': 'Bookings', 'nav.passes': 'Monthly passes', 'nav.users': 'Users', 'nav.stats': 'Statistics',
    'logout': 'Log out', 'theme.light': 'Light', 'theme.dark': 'Dark',
    'common.edit': 'Edit', 'common.delete': 'Delete', 'common.cancel': 'Cancel', 'common.save': 'Save',
    'common.choose': 'Choose…', 'common.total': 'Total {n}', 'common.from': 'From', 'common.to': 'To',
    'common.searchName': 'Search by name', 'common.searchClient': 'Search by client name',
    'common.id': 'ID', 'common.name': 'Name', 'common.description': 'Description', 'common.minutes': 'min',
    'pager.prev': '‹ Previous', 'pager.next': 'Next ›',
    'err.connection': 'Cannot reach the API ({url}). Is SportClub.Api running?',
    'login.title': 'SportClub admin', 'login.text': 'Manage trainers, training types, the schedule, pass plans and bookings.',
    'login.email': 'E-mail', 'login.password': 'Password', 'login.submit': 'Log in', 'login.busy': 'Logging in…',
    'login.badEmail': 'Enter a valid e-mail.', 'login.noPassword': 'Password is required.',
    'login.notAdmin': 'Only an administrator can log in to the admin UI.',
    'sessions.title': 'Schedule', 'sessions.add': '+ Add training', 'sessions.allTypes': 'All types',
    'sessions.allTrainers': 'All trainers', 'sessions.time': 'Time', 'sessions.training': 'Training',
    'sessions.trainer': 'Trainer', 'sessions.spots': 'Spots', 'sessions.empty': 'No trainings found.',
    'sessions.confirmDelete': 'Delete {name} {time}?\nAll bookings will be cancelled and visits returned to the clients.',
    'sessions.editTitle': 'Edit training', 'sessions.newTitle': 'New training', 'sessions.type': 'Training type',
    'sessions.date': 'Date', 'sessions.clock': 'Time', 'sessions.duration': 'Duration (min)', 'sessions.capacity': 'Number of spots',
    'sessions.capacityHint': 'If spots are added, the Worker moves people from the waitlist to the new spots.',
    'sessions.v.type': 'Choose a training type.', 'sessions.v.trainer': 'Choose a trainer.', 'sessions.v.date': 'Choose a date.',
    'sessions.v.time': 'Time as HH:MM.', 'sessions.v.past': 'A training cannot start in the past.',
    'sessions.v.duration': '15–240 minutes.', 'sessions.v.capacity': '1–200 spots.',
    'trainers.title': 'Trainers', 'trainers.add': '+ Add trainer',
    'trainers.hint': 'Every trainer has an account: they log in to the mobile app with the e-mail and password shown here.',
    'trainers.specialty': 'Specialty', 'trainers.email': 'E-mail (account)', 'trainers.phone': 'Phone', 'trainers.noAccount': 'no account',
    'trainers.empty': 'No trainers.', 'trainers.confirmDelete': 'Delete trainer {name}? Their account and past trainings are deleted too.',
    'trainers.newTitle': 'New trainer', 'trainers.editTitle': 'Edit trainer', 'trainers.loginEmail': 'E-mail (for login)',
    'trainers.password': 'Password', 'trainers.newPassword': 'New password (leave empty to keep)',
    'trainers.v.name': 'Name must be at least 2 characters.', 'trainers.v.specialty': 'Specialty is required.',
    'trainers.v.email': 'Enter a valid e-mail.', 'trainers.v.phone': 'Invalid phone number, e.g. +372 5555 1234.',
    'trainers.v.password': 'Password must be at least 8 characters.',
    'types.title': 'Training types', 'types.add': '+ Add type', 'types.empty': 'No types.',
    'types.confirmDelete': 'Delete training type {name}?', 'types.editTitle': 'Edit type', 'types.newTitle': 'New training type',
    'types.v.name': 'Name must be 2–60 characters.',
    'plans.title': 'Monthly pass plans', 'plans.add': '+ Add plan', 'plans.hint': 'Plans that clients can buy in the mobile app.',
    'plans.visits': 'Visits', 'plans.valid': 'Valid', 'plans.days': '{n} days', 'plans.price': 'Price',
    'plans.remove': 'Remove from sale', 'plans.empty': 'No plans.',
    'plans.confirmDelete': 'Remove plan {name} from sale? Passes already bought are kept.',
    'plans.editTitle': 'Edit plan', 'plans.newTitle': 'New plan', 'plans.daysField': 'Valid (days)', 'plans.priceField': 'Price (€)',
    'plans.editHint': 'The change does not affect passes already bought.',
    'plans.v.name': 'Name must be at least 2 characters.', 'plans.v.visits': '1–100 visits.', 'plans.v.days': '1–365 days.',
    'plans.v.price': 'Price 0–1000 €.',
    'bookings.title': 'Bookings', 'bookings.promote': 'Move waitlist to free spots', 'bookings.all': 'All',
    'bookings.booked': 'Booked', 'bookings.waitlisted': 'Waitlisted', 'bookings.newest': 'Newest first',
    'bookings.oldest': 'Oldest first', 'bookings.client': 'Client', 'bookings.status': 'Status', 'bookings.attendance': 'Attendance',
    'bookings.created': 'Created', 'bookings.queue': 'In queue #{n}', 'bookings.present': 'Present', 'bookings.absent': 'Absent',
    'bookings.cancel': 'Cancel', 'bookings.empty': 'No bookings found.',
    'bookings.confirmCancel': '{client}: cancel the booking ({training})?',
    'bookings.confirmLeave': '{client}: remove from the waitlist ({training})?',
    'bookings.promoted': 'Moved from the waitlist to the training: {names}.', 'bookings.nobody': 'Nobody to move from the waitlist.',
    'bookings.removed': ' Removed from the waitlist: {n}.',
    'passes.title': 'Monthly passes', 'passes.hint': 'Clients buy a pass themselves in the mobile app. Only one pass can be active at a time.',
    'passes.plan': 'Plan', 'passes.left': '{left} / {total} left', 'passes.active': 'Active', 'passes.inactive': 'Not valid',
    'passes.empty': 'No passes found.', 'passes.card': 'Card',
    'users.title': 'Users', 'users.email': 'E-mail', 'users.role': 'Role', 'users.empty': 'No users found.',
    'role.Admin': 'Admin', 'role.Trainer': 'Trainer', 'role.Client': 'Client', 'role.Service': 'Background service',
    'stats.title': 'Most popular trainings', 'stats.bookings': 'bookings', 'stats.attended': 'attended',
    'stats.fill': 'average fill rate', 'stats.row': '{bookings} bookings · {sessions} trainings · attended {attended} · fill {fill}%',
    'stats.empty': 'No data.'
  }
}

const read = (key, fallback) => {
  try { return localStorage.getItem(key) || fallback } catch { return fallback }
}

/** Praegune keel väljaspool Reacti (nt kuupäevade vormindamiseks). */
export function currentLang() {
  return read('lang', 'et')
}

const PrefsContext = createContext(null)

/** Hoiab keelt ja teemat; valik jääb brauserisse meelde. */
export function PrefsProvider({ children }) {
  const [lang, setLang] = useState(() => read('lang', 'et'))
  const [theme, setTheme] = useState(() =>
    read('sc-theme', 'dark')) // vaikimisi alati tume teema

  useEffect(() => {
    try { localStorage.setItem('lang', lang) } catch { /* ignore */ }
    document.documentElement.lang = lang
  }, [lang])

  useEffect(() => {
    try { localStorage.setItem('sc-theme', theme) } catch { /* ignore */ }
    document.documentElement.dataset.theme = theme
  }, [theme])

  /** t('common.total', { n: 5 }) → "Kokku 5" */
  const t = (key, vars) => {
    let text = texts[lang]?.[key] ?? texts.et[key] ?? key
    if (vars) Object.entries(vars).forEach(([k, v]) => { text = text.replaceAll(`{${k}}`, v) })
    return text
  }

  return <PrefsContext.Provider value={{ lang, setLang, theme, setTheme, t }}>{children}</PrefsContext.Provider>
}

export function usePrefs() {
  return useContext(PrefsContext)
}

/** Keele (ET / EN) ja teema (hele / tume) lülitid. */
export function PrefsSwitch() {
  const { lang, setLang, theme, setTheme, t } = usePrefs()
  return (
    <div className="prefs">
      <div className="seg" role="group" aria-label="Language">
        <button type="button" className={lang === 'et' ? 'on' : ''} onClick={() => setLang('et')}>ET</button>
        <button type="button" className={lang === 'en' ? 'on' : ''} onClick={() => setLang('en')}>EN</button>
      </div>
      <div className="seg" role="group" aria-label="Theme">
        <button type="button" className={theme === 'light' ? 'on' : ''} onClick={() => setTheme('light')}>☀ {t('theme.light')}</button>
        <button type="button" className={theme === 'dark' ? 'on' : ''} onClick={() => setTheme('dark')}>☾ {t('theme.dark')}</button>
      </div>
    </div>
  )
}
