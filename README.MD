# Barber Bookment API (Demo)

Small .NET Web API to **experiment with AWS DynamoDB via the official .NET SDK** and to **load runtime configuration from a YAML file**. It simulates a barber shop where **unauthenticated clients** can check availability and create an appointment.

> Scope: this is a learning/demo repo, intentionally minimal (no auth, no retries, no complex domain).

## Why this exists
- Try out **DynamoDB .NET SDK** end-to-end (table creation + queries + writes).
- Try out **YAML-based config** at runtime using YamlDotNet.
- Provide a tiny backend that a simple front‑end could call: pick a **day → time → barber** flow.

## Tech & Libraries
- **ASP.NET Core** minimal API + controllers
- **AWS DynamoDB .NET SDK**
- **YamlDotNet** for `AppointConfig.yaml`
- **DotNetEnv** to load AWS credentials/region from a local `.env`
- **Swagger / OpenAPI** (dev only)

## Runtime configuration

### 1) `.env` (not committed)
Set the standard AWS env vars (the SDK will use them automatically), and `AWS_REGION` if you want to override.

```
AWS_ACCESS_KEY_ID=your_key_id
AWS_SECRET_ACCESS_KEY=your_secret
AWS_SESSION_TOKEN=optional_if_using_temporary_creds
AWS_REGION=eu-north-1
```

> The app calls `DotNetEnv.Env.Load()` on startup so these env vars can live in a local `.env` file during development.

### 2) `AppointConfig.yaml`
Holds the shop schedule and the available barbers. Example:

```yaml
# AppointConfig.yaml
daysUpToCreateAppointments: 7

schedule:
  startMorning: 9       # 09:00
  endMorning: 12        # 12:00 (exclusive)
  startAfternoon: 14    # 14:00
  endAfternoon: 18      # 18:00 (exclusive)
  minutesInterval: 30   # slot length in minutes

barbers:
  - barberId: "alex"
    name: "Alex"
  - barberId: "maria"
    name: "Maria"
```

> Only `barberId` is mandatory for the code paths here. Add more fields as you wish for display.

## DynamoDB

On application start, the API **creates the `Appointments` table** if it doesn’t exist (on-demand capacity).

- **PK**: `id` (string GUID)
- **GSI `DateIndex`**:  
  - Partition key: `day` (e.g., `yyyyMMdd`)  
  - Sort key: `hour` (e.g., `HHmm`)

Each appointment item contains:
- `id`, `day`, `hour`, `barberId`, `client`

> This layout allows quick day/hour lookups and simple availability checks. For a production system you’d likely switch to a *slot-based primary key* (`{yyyyMMdd}#{HHmm}#{barberId}`) with conditional writes to prevent double-booking.

## Endpoints

Base path: `/api/Appointment`

### `GET /availableDays?daysAhead={int}`
Returns a list of **days that still have capacity** within the next `daysAhead` days.  
**Response:** list of strings (currently formatted as `MMdd`, e.g., `0817`).

### `GET /availableHours?day={yyyyMMdd}`
Given a day, returns **available time slots** (considering schedule, interval, and current time).  
**Response:** list of strings formatted as `HH:mm`, e.g., `["09:00","09:30","10:00"]`.

### `GET /availableBarbers?day={yyyyMMdd}&hour={HHmm}`
Returns the **barbers not yet booked** for the given day & hour.  
**Response:** array of barbers from the YAML config filtered by availability.

### `POST /createAppointment`
Creates an appointment if:
- `barberId` exists in the YAML config
- `date` is inside schedule windows and aligned with the configured interval
- the specific `(day, hour, barber)` is not already booked

**Request body (JSON):**
```json
{
  "date": "2025-08-17T09:00:00",
  "barberId": "alex",
  "client": "John Doe"
}
```

**Responses:**
- `201 Created` on success
- `400 Bad Request` when validation fails
- `500` for unexpected errors (demo/learning behavior)

## Quickstart

Prereqs: .NET 8+, an AWS account (or credentials that can talk to DynamoDB).

```bash
# 1) clone
git clone https://github.com/yourname/barber-bookment-api.git
cd barber-bookment-api

# 2) create .env with AWS creds/region (see above)
# 3) create AppointConfig.yaml (example above)
# 4) run
dotnet run
```

Open Swagger UI (development): `https://localhost:5001/swagger` or `http://localhost:5000/swagger`.

## Example requests

```bash
# list available days for the next 7 days
curl "https://localhost:5001/api/Appointment/availableDays?daysAhead=7"

# list available hours for a day
curl "https://localhost:5001/api/Appointment/availableHours?day=20250817"

# list available barbers for a given day+hour
curl "https://localhost:5001/api/Appointment/availableBarbers?day=20250817&hour=0900"

# create an appointment
curl -X POST "https://localhost:5001/api/Appointment/createAppointment"   -H "Content-Type: application/json"   -d '{"date":"2025-08-17T09:00:00","barberId":"alex","client":"John Doe"}'
```

## Notes & limitations
- No authentication/authorization (by design, it’s a simple public booking demo).
- No idempotency or concurrency guards (kept intentionally simple for learning).
- Uses system time for “past slot” filtering.
- The project name includes the word “Bookment” to match the original code; feel free to rename.

## License
MIT (or your choice).
