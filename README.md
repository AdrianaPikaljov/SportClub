# SportClub

SportClub – a sports club training management and booking system built with ASP.NET Core Web API, .NET MAUI, EF Core, JWT authentication, and a background Worker Service.

Clients book group trainings with a monthly pass. Every training has a limited number of spots; when it is full the client is put on a waitlist automatically, and the Worker moves the first person in the queue to a freed spot.

## Projects

| Project | What it is |
|---|---|
| `SportClub.Api` | ASP.NET Core Web API (controllers, JWT, Swagger, WebSocket `/ws/sessions`) |
| `SportClub.Core` | Entities, DTOs, business logic (services), repository interfaces |
| `SportClub.Infrastructure` | EF Core `AppDbContext` (SQLite), repositories, seed data |
| `SportClub.Worker` | Separate Worker Service – logs in with the `Service` account and calls the API |
| `SportClub.Maui` | Mobile app for clients and trainers (MVVM) |
| `SportClub.Admin` | React (Vite) admin web UI |
| `SportClub.Tests` | xUnit + Moq unit tests |

## Running

```bash
# 1. Database migration (first time, or after changing entities)
dotnet tool install --global dotnet-ef        # once
dotnet ef migrations add InitialCreate -p SportClub.Infrastructure -s SportClub.Api -o Data/Migrations

# 2. API – http://localhost:5080/swagger (the database and demo data are created on start)
dotnet run --project SportClub.Api

# 3. Worker (in another terminal)
dotnet run --project SportClub.Worker

# 4. Admin UI – http://localhost:5173
cd SportClub.Admin && npm install && npm run dev

# Tests
dotnet test
```

All times are Estonian local time. Dates are sent as `2026-11-27`, times as `18:00`, and returned as `2026-11-27 18:00`.

## Demo accounts

| Role | E-mail | Password |
|---|---|---|
| Client | klient@sportclub.ee (also karl@, anna@, peeter@) | Klient123! |
| Trainer | treener@sportclub.ee (also kertu@, liis@, rasmus@) | Treener123! |
| Admin | admin@sportclub.ee | Admin123! |
| Service (Worker) | service@sportclub.ee | Service123! |

Tomorrow's 19:00 Crossfit is already full, so the waitlist can be tried right away.

## Business rules

- A booking needs an active monthly pass with visits left; one visit is used when booking and returned when cancelling.
- A client can have only one active pass at a time; a new pass is valid from today.
- Cancelling is allowed until 2 hours before the start (Admin: always).
- Full training → `409`, and the client is added to the waitlist (the response contains `position`).
- The Worker promotes the first person in the queue every minute. People without a valid pass are removed from the queue. One hour before the start the whole queue is removed.
- Attendance can be marked from the start of the training. A trainer sees only their own trainings and statistics; Admin sees everything.

## Email notifications

The API sends emails on registration, booking, waitlist, promotion from the waitlist, the 1-hour reminder,
cancelled sessions and pass purchases (receipt). Sending happens in the background; if it fails, it is only logged.

Emails are sent through **Gmail**. By default `Email:User` is empty – emails are only written to the log.
To send real emails:

1. Turn on 2-Step Verification on the Google account.
2. Create an *App password* at https://myaccount.google.com/apppasswords (16 letters).
3. Copy `SportClub.Api/appsettings.Local.example.json` to `SportClub.Api/appsettings.Local.json`
   (this file is in `.gitignore`) and fill in your Gmail address as `User` and the app password as `Password`.

Restart the API after changing the file.

## Payment

Buying a monthly pass uses a **simulated** card payment – no money moves. The card number and CVC are checked
(Luhn, expiry, CVC) and then discarded; only the card brand, last 4 digits and a payment reference are stored.

| Test card | Result |
|---|---|
| 4242 4242 4242 4242 | succeeds |
| 4000 0000 0000 0002 | declined (402) |

Any future expiry (e.g. `12/28`) and any 3-digit CVC work.

## API

| Method | Endpoint | Who |
|---|---|---|
| POST | `/api/auth/register`, `/api/auth/login` | everyone |
| GET | `/api/sessions?from=&to=&type=` | everyone |
| GET | `/api/trainers`, `/api/trainers/{id}/sessions`, `/api/types`, `/api/pass-plans` | everyone |
| GET / POST | `/api/passes/my`, `/api/passes` | Client |
| POST | `/api/sessions/{id}/bookings` (full → 409 + waitlist), `/api/sessions/{id}/waitlist` | Client |
| GET | `/api/bookings/my` | Client |
| DELETE | `/api/bookings/{id}` (booking or waitlist place) | Client, Admin |
| GET | `/api/trainer/sessions` | Trainer |
| GET / PATCH | `/api/sessions/{id}/attendance` (whole list at once) | Trainer, Admin |
| PATCH | `/api/bookings/{id}/attendance` (one person) | Trainer, Admin |
| GET | `/api/stats/popular?from=&to=` | Trainer (own), Admin (all) |
| GET {id} / POST / PUT / DELETE | `/api/trainers`, `/api/types`, `/api/sessions`, `/api/pass-plans` | Admin |
| GET | `/api/bookings?search=&status=&sort=&page=` | Admin |
| GET | `/api/passes?search=`, `/api/users?search=` | Admin |
| POST | `/api/waitlist/promote` | Service, Admin |
| GET | `/api/sessions/starting?within=60` | Service |
| POST | `/api/notifications` | Service |

Realtime: `ws://localhost:5080/ws/sessions?access_token=<JWT>` sends `{"type":"FreeSpotsChanged","sessionId":12,"free":3}` whenever the number of free spots changes.

Errors are returned as ProblemDetails (`status`, `title`, `detail`). Test requests: `SportClub.Api/SportClub.http`.
