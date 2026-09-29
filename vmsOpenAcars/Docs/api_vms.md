# API phpVMS v7 — Documentación de Endpoints

Sistema de gestión de aerolíneas virtuales basado en Laravel 10. La API está dividida en:

- **Core API** (`/api`) — rutas públicas y autenticadas del núcleo
- **SmartCARS3** (`/api/smartcars`) — endpoints para el cliente ACARS smartCARS 3
- **DisposableBasic** — endpoints de estadísticas y roster
- **DisposableSpecial** — endpoints de tours y asignaciones
- **VmsOpenOps** (`/api/vmsopenops`) — operaciones de jumpseat y ferry
- **Módulos varios** — stubs de ejemplo

---

## Autenticación

| Tipo | Método |
|---|---|
| **api.auth** | API Key via header `X-API-KEY: <api_key>` (o `Authorization: <api_key>`, **sin** `Bearer`) |
| **SCAuth** | smartCARS 3 usa su propio middleware y sistema de sesión |
| **x-service-key** | DisposableBasic/Special usan header `x-service-key` para endpoints públicos |
| **web + auth** | VmsOpenOps usa sesión web + cookie, no API token |

---

# 1. Núcleo (Core API)

## 1.1 Estado y Versión

### `GET /api`
### `GET /api/status`
### `GET /api/version`

Sin autenticación. Devuelve info de la aplicación.

```json
{
  "name": "phpVMS Virtual Airline",
  "version": "7.0.0",
  "php": "8.1.27"
}
```

---

## 1.2 Noticias

### `GET /api/news`

Sin autenticación. Noticias paginadas ordenadas por `created_at DESC`.

**Response:**
```json
{
  "data": [
    {
      "id": "uuid",
      "user_id": 1,
      "subject": "Título de la noticia",
      "body": "<p>Contenido HTML</p>",
      "created_at": "2026-05-25T12:00:00.000000Z",
      "updated_at": "2026-05-25T12:00:00.000000Z",
      "user": { "id": 1, "name": "Admin" }
    }
  ],
  "meta": { "pagination": { ... } }
}
```

---

## 1.3 Vuelos en Vivo (ACARS)

### `GET /api/acars`

Sin autenticación. PIREPs con posición reciente.

**Response:** Colección de `PirepResource` (solo PIREPs en `IN_PROGRESS` con posición dentro de `acars.live_time`).

### `GET /api/acars/geojson`

Sin autenticación. FeatureCollection GeoJSON de vuelos en vivo.

```json
{
  "data": {
    "type": "FeatureCollection",
    "features": [ ... ]
  }
}
```

### `GET /api/pireps/{pirep_id}/acars/geojson`

Sin autenticación. GeoJSON de la ruta ACARS de un PIREP específico.

---

## 1.4 Aeropuertos

### `GET /api/airports/hubs`

Sin autenticación. Todos los aeropuertos marcados como `hub=true`.

### `GET /api/airports/search`

Sin autenticación. Búsqueda paginada de aeropuertos.

| Query Param | Tipo | Descripción |
|---|---|---|
| `hubs` | bool | Opcional, filtrar solo hubs |

### `GET /api/airports` _(api.auth)_

Lista paginada de aeropuertos.

| Query Param | Tipo | Descripción |
|---|---|---|
| `hub` | bool | Opcional, filtrar hubs |

### `GET /api/airports/{id}` _(api.auth)_

Detalle de un aeropuerto. `id` se convierte a mayúsculas.

### `GET /api/airports/{id}/lookup` _(api.auth)_

Busca aeropuerto en vaCentral y lo cachea localmente.

### `GET /api/airports/{id}/distance/{to}` _(api.auth)_

Distancia entre dos aeropuertos.

```json
{
  "fromIcao": "KLAX",
  "toIcao": "KJFK",
  "distance": "2476.32"
}
```

---

## 1.5 Aerolíneas

### `GET /api/airlines` _(api.auth)_

Lista de aerolíneas activas ordenadas por nombre.

```json
{
  "data": [
    {
      "id": 1,
      "icao": "VHA",
      "iata": "VH",
      "name": "Vholar Virtual Airlines",
      "country": "US",
      "logo": "https://..."
    }
  ]
}
```

### `GET /api/airlines/{id}` _(api.auth)_

Detalle de una aerolínea.

---

## 1.6 Flota / Subflota

### `GET /api/fleet` _(api.auth)_
### `GET /api/subfleet` _(api.auth)_

Sin parámetros. Lista paginada de subflotas con aircraft, airline, fares y ranks.

### `GET /api/fleet/aircraft/{id}` _(api.auth)_
### `GET /api/subfleet/aircraft/{id}` _(api.auth)_

Detalle de una aeronave.

| Query Param | Tipo | Descripción |
|---|---|---|
| `type` | string | Opcional. Columna de búsqueda alternativa (ej: `registration`) |

```json
{
  "data": {
    "id": "uuid",
    "subfleet_id": "uuid",
    "airline_id": 1,
    "name": "G-AAAA",
    "registration": "G-AAAA",
    "icao": "B738",
    "iata": "738",
    "hex_code": null,
    "status": "PARKED",
    "ident": "G-AAAA",
    "dow": 41689.00,
    "zfw": 62779.00,
    "mtow": 79015.00,
    "mlw": 66360.00,
    "fuel_onboard": 5000.00,
    "subfleet": {
      "fares": [ ... ]
    }
  }
}
```

---

## 1.7 Vuelos

### `GET /api/flights` _(api.auth)_

| Query Param | Tipo | Descripción |
|---|---|---|
| `with` | string | Relaciones separadas por coma (ej: `subfleets`) |
| `ignore_restrictions` | 0/1 | Ignorar restricciones de subflota |
| Params RequestCriteria | varios | Filtros, sorting y paginación estándar |

### `GET /api/flights/search` _(api.auth)_

Similar a index pero aplica `active=true`, `visible=true`, restricciones de compañía y aeropuerto actual.

### `GET /api/flights/{id}` _(api.auth)_

Detalle de un vuelo.

```json
{
  "data": {
    "id": "uuid",
    "airline_id": 1,
    "flight_number": "100",
    "route_code": null,
    "route_leg": null,
    "dpt_airport_id": "KLAX",
    "arr_airport_id": "KJFK",
    "alt_airport_id": null,
    "dpt_time": "1200",
    "arr_time": "2000",
    "days": 127,
    "level": 36000,
    "flight_type": "J",
    "load_factor": null,
    "load_factor_variance": null,
    "distance": 2476,
    "route": "ROUTE STRING",
    "ident": "VHA100",
    "airline": { ... },
    "subfleets": [ ... ],
    "fields": { "key": "value" },
    "simbrief": { ... }
  }
}
```

### `GET /api/flights/{id}/briefing` _(api.auth)_

Devuelve XML sin procesar del OFP de SimBrief (`application/xml`).

### `GET /api/flights/{id}/route` _(api.auth)_

Colección de `NavdataResource` con detalles de navaids.

### `GET /api/flights/{id}/aircraft` _(api.auth)_

Aeronaves disponibles para el vuelo (filtradas por subflota, estado, aeropuerto y bids).

---

## 1.8 PIREPs (Reportes de Vuelo)

### `GET /api/pireps/{pirep_id}`

Sin autenticación. Detalle completo de un PIREP.

### `POST /api/pireps/prefile` _(api.auth)_

Prefila un nuevo PIREP (estado inicial).

| Parámetro | Obligatorio | Tipo | Descripción |
|---|---|---|---|
| `airline_id` | Sí | int | |
| `aircraft_id` | Sí | uuid | |
| `flight_id` | No | uuid | |
| `flight_number` | Sí | string | |
| `dpt_airport_id` | Sí | string | |
| `arr_airport_id` | Sí | string | |
| `source_name` | Sí | string | Ej: `manual`, `acars` |
| `alt_airport_id` | No | string | |
| `status` | No | enum | |
| `level` | No | numeric | Nivel de vuelo |
| `flight_type` | No | enum | |
| `route_code` | No | string | |
| `route_leg` | No | string | |
| `distance` | No | numeric | |
| `block_time` | No | integer | Minutos |
| `flight_time` | No | integer | Minutos |
| `planned_distance` | No | numeric | |
| `planned_flight_time` | No | integer | Minutos |
| `zfw` | No | numeric | Zero Fuel Weight |
| `block_fuel` | No | numeric | |
| `route` | No | string | |
| `notes` | No | string | |
| `score` | No | integer | |
| `block_off_time` | No | date | |
| `block_on_time` | No | date | |
| `created_at` | No | date | |
| `fares` | No | array | `[{id, count}]` |

### `POST /api/pireps/{pirep_id}` _(api.auth)_
### `PUT /api/pireps/{pirep_id}` _(api.auth)_
### `PATCH /api/pireps/{pirep_id}` _(api.auth)_
### `PUT /api/pireps/{pirep_id}/update` _(api.auth)_
### `POST /api/pireps/{pirep_id}/update` _(api.auth)_

Actualiza un PIREP existente. Mismos campos que prefile pero todos opcionales. Añade:

| Parámetro | Tipo | Descripción |
|---|---|---|
| `fuel_used` | numeric | |
| `landing_rate` | numeric | fpm |

### `POST /api/pireps/{pirep_id}/file` _(api.auth)_

Finaliza y envía un PIREP. Autoría verificada.

| Parámetro | Obligatorio | Tipo |
|---|---|---|
| `distance` | Sí | numeric |
| `flight_time` | Sí | integer |
| `fuel_used` | No | numeric |
| `block_time` | No | integer |
| `landing_rate` | No | numeric |
| `score` | No | integer |
| demás campos | No | varios (mismos que prefile) |

### `PUT /api/pireps/{pirep_id}/cancel` _(api.auth)_
### `DELETE /api/pireps/{pirep_id}/cancel` _(api.auth)_

Cancela un PIREP.

```json
{ "message": "PIREP {id} cancelled" }
```

### `GET /api/pireps/{pirep_id}/comments`

Comentarios de un PIREP (público).

### `POST /api/pireps/{pirep_id}/comments` _(api.auth)_

| Parámetro | Obligatorio | Tipo |
|---|---|---|
| `comment` | Sí | string |
| `created_at` | No | date |

### `GET /api/pireps/{pirep_id}/fields` _(api.auth)_

Campos personalizados del PIREP (key-value object).

### `POST /api/pireps/{pirep_id}/fields` _(api.auth)_

| Parámetro | Obligatorio | Tipo |
|---|---|---|
| `fields` | Sí | array |

### `GET /api/pireps/{pirep_id}/finances` _(api.auth)_

Transacciones financieras del PIREP.

### `POST /api/pireps/{pirep_id}/finances/recalculate` _(api.auth)_

Recalcula finanzas del PIREP.

### `GET /api/pireps/{pirep_id}/route` _(api.auth)_

Ruta ACARS del PIREP (type=ROUTE).

### `POST /api/pireps/{pirep_id}/route` _(api.auth)_

| Parámetro | Obligatorio | Tipo |
|---|---|---|
| `route` | Sí | array | `[{name, order, nav_type, lat, lon}]` |

```json
{ "message": "5 points added", "count": 5 }
```

### `DELETE /api/pireps/{pirep_id}/route` _(api.auth)_

```json
{ "message": "Route deleted" }
```

---

## 1.9 ACARS (Posiciones, Logs y Eventos)

> `acars.type`: `0` = FLIGHT_PATH (posiciones), `1` = ROUTE (ruta planificada), `2` = LOG (mensajes y
> eventos). Cada endpoint sirve un tipo: `acars/position` solo devuelve `type=0` y `acars/logs`, `type=2`.

### `GET /api/pireps/{pirep_id}/acars/position` _(api.auth)_

Puntos de posición (`type = 0`, FLIGHT_PATH) ordenados por `sim_time`. Los campos con unidades
(`fuel`, `distance`) se devuelven expandidos como `{"kg":…,"lbs":…}` / `{"m":…,"km":…,"mi":…,"nmi":…}`.

### `POST /api/pireps/{pirep_id}/acars/position` _(api.auth)_
### `POST /api/pireps/{pirep_id}/acars/positions` _(api.auth)_

| Parámetro | Obligatorio | Tipo | Descripción |
|---|---|---|---|
| `positions` | Sí | array | Array de objetos posición |

Cada objeto posición:

| Campo | Obligatorio | Tipo |
|---|---|---|
| `lat` | Sí | float |
| `lon` | Sí | float |
| `status` | No | enum |
| `altitude` | No | float |
| `altitude_agl` | No | float |
| `altitude_msl` | No | float |
| `heading` | No | float (0-360) |
| `vs` | No | float |
| `gs` | No | float |
| `transponder` | No | string |
| `autopilot` | No | bool |
| `fuel` | No | float |
| `fuel_flow` | No | float |
| `log` | No | string |
| `sim_time` | No | string |
| `created_at` | No | string |

> **Notas sobre los campos de posición:**
> * `phase` existe en la tabla pero **no se usa** (siempre `null`). La fase de cada posición llega en **`status`**, con los códigos de `PirepStatus` (`PBT` pushback, `TXI` taxi —agrupa salida y llegada—, `TOF` despegue, `ICL` ascenso, `ENR` crucero, `APR` aproximación, `LDG`, `ARR`, `CHK`, `FIN`, `BST`, `INI`).
> * `fuel` se persiste desde el 2026-09-29 (se añadió a `$fillable` de `App\Models\Acars`). Antes se descartaba en silencio aunque se enviara. Al leer las posiciones se devuelve con su valor (`{"kg":100.79,"lbs":222.2}`); hasta esa fecha el cast se serializaba solo con los nombres de unidad, sin valor.

### `GET /api/pireps/{pirep_id}/acars/logs` _(api.auth)_

Mensajes y eventos del PIREP (`type = 2`, LOG) en orden cronológico. Es donde quedan los textos
enviados por `acars/logs` y `acars/events`; **no** aparecen en `acars/position`.

### `POST /api/pireps/{pirep_id}/acars/logs` _(api.auth)_

| Parámetro | Obligatorio | Tipo |
|---|---|---|
| `logs` | Sí | array | `[{log, lat?, lon?, created_at?}]` |

### `POST /api/pireps/{pirep_id}/acars/events` _(api.auth)_

| Parámetro | Obligatorio | Tipo |
|---|---|---|
| `events` | Sí | array | `[{event, lat?, lon?, created_at?}]` |

---

## 1.10 Usuarios

### `GET /api/user` _(api.auth)_
### `GET /api/users/me` _(api.auth)_

Perfil del usuario autenticado.

| Query Param | Tipo | Descripción |
|---|---|---|
| `with` | string | Relaciones separadas por coma (ej: `subfleets`) |

```json
{
  "data": {
    "id": "uuid",
    "pilot_id": 1001,
    "ident": "VHA1001",
    "name": "John Doe",
    "name_private": "John D.",
    "avatar": { "url": "https://..." },
    "discord_id": "123456",
    "vatsim_id": "1234567",
    "ivao_id": "123456",
    "rank_id": 3,
    "home_airport": { "id": "KLAX", ... },
    "curr_airport": { "id": "KJFK", ... },
    "flights": 42,
    "flight_time": 5000,
    "transfer_time": 120,
    "timezone": "America/New_York",
    "state": 2,
    "airline": { ... },
    "rank": { ... },
    "subfleets": [ ... ]
  }
}
```

### `GET /api/users/{id}` _(api.auth)_

Perfil público de un usuario por ID.

### `GET /api/user/fleet` _(api.auth)_
### `GET /api/users/{id}/fleet` _(api.auth)_

Subflotas a las que tiene acceso el usuario (con fares y aircraft).

### `GET /api/user/pireps` _(api.auth)_
### `GET /api/users/{id}/pireps` _(api.auth)_
### `GET /api/pireps` _(api.auth)_

PIREPs del usuario. Paginados por `created_at DESC`. Excluye cancelados por defecto.

| Query Param | Tipo | Descripción |
|---|---|---|
| `state` | enum | Filtrar por estado |
| `source_name` | string | Filtro por cliente productor, por **prefijo**. `source_name=vmsOpenACars` devuelve todas sus versiones (`vmsOpenACars/0.9.16`, etc.) |
| `limit` | int | Tamaño de página. Por defecto `20` (`repository.pagination.limit`), sin tope máximo |
| `page` | int | Número de página |
| `id` | uuid | Piloto a consultar (`?id=3`); por defecto el dueño del token. Permite leer los PIREPs de otro piloto |

**Response:** colección de `PirepResource` con `meta.pagination` (`current_page`, `per_page`, `total`, `last_page`).

> Los tres endpoints comparten controlador, así que `source_name`, `limit`, `page` e `id` funcionan igual en `GET /api/pireps` y `GET /api/users/{id}/pireps`.
>
> Valores habituales de `source_name`: `vmsOpenACars/<versión>` (cliente ACARS), `manual` (PIREPs archivados a mano en el panel) y `CrewSystem/import` (importación histórica de CrewSystem).

---

## 1.11 Bids (Reservas de Vuelo)

### `GET /api/bids` _(api.auth)_
### `GET /api/user/bids` _(api.auth)_
### `GET /api/users/{id}/bids` _(api.auth)_

Lista de bids del usuario.

| Query Param | Tipo | Descripción |
|---|---|---|
| `with` | string | Relaciones (ej: `subfleets`, `simbrief_aircraft`) |

### `PUT /api/user/bids` _(api.auth)_
### `POST /api/user/bids` _(api.auth)_
### `PUT /api/users/{id}/bids` _(api.auth)_

Crear un bid.

| Parámetro | Obligatorio | Tipo |
|---|---|---|
| `flight_id` | Sí | uuid |
| `aircraft_id` | No | uuid | Solo si `block_aircraft` está activo |

### `DELETE /api/user/bids` _(api.auth)_
### `DELETE /api/users/{id}/bids` _(api.auth)_

Eliminar un bid.

| Parámetro | Obligatorio | Tipo |
|---|---|---|
| `bid_id` | No | uuid |
| `flight_id` | No | uuid |

### `GET /api/bids/{id}` _(api.auth)_
### `GET /api/user/bids/{id}` _(api.auth)_

Detalle de un bid específico. Verifica pertenencia al usuario autenticado.

---

## 1.12 Mantenimiento / Cron

### `GET /api/cron/{id}`

Sin autenticación, pero requiere que `id` coincida con `setting('cron.random_id')`.

```json
{
  "count": 3,
  "tasks": [
    "SetActiveFlights",
    "RemoveExpiredBids",
    "RemoveExpiredLiveFlights"
  ]
}
```

---

## 1.13 Settings (ruta comentada, no activa)

### `GET /api/settings`

Ruta comentada en el RouteServiceProvider. No disponible actualmente.

---

# 2. SmartCARS 3 phpVMS7 API

Prefijo: `/api/smartcars`
Middleware: `SCHeaders` (global), `SCAuth` (para rutas protegidas)

## 2.1 Información General

### `GET /api/smartcars`

```json
{
  "apiVersion": "1.0.2",
  "handler": "phpvms7"
}
```

## 2.2 Piloto

### `POST /api/smartcars/pilot/login`

| Parámetro | Obligatorio | Tipo | Descripción |
|---|---|---|---|
| `username` | Sí | string | Email (si contiene @) o pilot_id |
| `password` | Sí | string | Contraseña en texto plano |

```json
{
  "dbID": "uuid",
  "pilotID": "VHA1001",
  "firstName": "John",
  "lastName": "Doe",
  "email": "john@example.com",
  "rank": "Captain",
  "rankImage": null,
  "rankLevel": 3,
  "avatar": "https://...",
  "session": "api_key_string"
}
```

Error 401: `{ "message": "The username or password is incorrect" }`

### `POST /api/smartcars/pilot/resume`

| Parámetro | Obligatorio | Tipo |
|---|---|---|
| `session` | Sí | string | api_key del usuario |

Misma respuesta que login.

### `POST /api/smartcars/pilot/verify`

| Parámetro | Obligatorio | Tipo |
|---|---|---|
| `session` | Sí | string | api_key |

Misma respuesta que login.

### `GET /api/smartcars/pilot/statistics` _(SCAuth)_

```json
{
  "hoursFlown": 123.45,
  "flightsFlown": 42,
  "averageLandingRate": -185.5,
  "pirepsFiled": 42
}
```

## 2.3 Datos

### `GET/POST /api/smartcars/data/aircraft` _(SCAuth)_

```json
[
  {
    "id": "uuid",
    "code": "B738",
    "name": "Boeing 737-800 (G-AAAA) | Active",
    "status": "Available",
    "serviceCeiling": "40000",
    "maximumPassengers": 300,
    "maximumCargo": 1000,
    "minimumRank": 0
  }
]
```

Estados: `PARKED → Available`, `IN_USE → In Use`, `IN_AIR → In Air`

### `GET/POST /api/smartcars/data/airports` _(SCAuth)_

```json
[
  {
    "id": "uuid",
    "code": "KJFK",
    "name": "John F Kennedy International",
    "latitude": 40.64,
    "longitude": -73.78
  }
]
```

### `GET /api/smartcars/data/subfleets` _(SCAuth)_

Todas las subflotas con relación `airline` cargada.

### `GET /api/smartcars/data/news` _(SCAuth)_

```json
{
  "title": "subject",
  "body": "body",
  "postedAt": "timestamp",
  "postedBy": "Admin"
}
```

Solo la noticia más reciente.

### `GET /api/smartcars/data/flight_types` _(SCAuth)_

Pares key-value del enum `FlightType`.

## 2.4 PIREPs

### `GET/POST /api/smartcars/pireps/details` _(SCAuth)_

| Parámetro | Tipo | Descripción |
|---|---|---|
| `id` | string | PIREP ID |
| `pilotID` | string | User ID (no usado en lógica) |

```json
{
  "locationData": [
    { "latitude": "...", "longitude": "...", "heading": "..." }
  ],
  "flightData": [
    {
      "eventId": "uuid",
      "eventTimestamp": "timestamp",
      "eventElapsedTime": 0,
      "eventCondition": null,
      "message": "string"
    }
  ]
}
```

### `GET/POST /api/smartcars/pireps/search` _(SCAuth)_

| Parámetro | Tipo |
|---|---|
| `pilotID` | string |

```json
[
  {
    "id": "uuid",
    "submitDate": "Y-m-d H:i:s",
    "airlineCode": "VHA",
    "route": "SID STAR",
    "number": "100",
    "distance": "2476",
    "flightType": "P",
    "departureAirport": "KLAX",
    "arrivalAirport": "KJFK",
    "aircraft": "uuid",
    "status": "Accepted",
    "flightTime": "5.5",
    "landingRate": -150,
    "fuelUsed": "15000"
  }
]
```

Estados: `1 → Pending`, `2 → Accepted`, `6 → Rejected`

### `GET/POST /api/smartcars/pireps/latest` _(SCAuth)_

Último PIREP del usuario autenticado o `[]`.

## 2.5 Vuelos / Bids

### `POST /api/smartcars/flights/book` _(SCAuth)_

| Parámetro | Tipo |
|---|---|
| `flightID` | uuid |
| `pilotID` | uuid |
| `aircraftID` | uuid (opcional) |

```json
{ "bidID": "uuid" }
```

### `POST /api/smartcars/flights/rebook` _(SCAuth)_

| Parámetro | Tipo |
|---|---|
| `bidID` | uuid |
| `aircraftID` | uuid |

### `GET /api/smartcars/flights/bookings` _(SCAuth)_

| Parámetro | Tipo |
|---|---|
| `pilotID` | uuid |

```json
[
  {
    "bidID": "uuid",
    "number": "100",
    "code": "VHA",
    "departureAirport": "KLAX",
    "arrivalAirport": "KJFK",
    "route": ["WPT1", "WPT2"],
    "flightLevel": "36000",
    "distance": 2476,
    "departureTime": "1200",
    "arrivalTime": "2000",
    "flightTime": 5.50,
    "daysOfWeek": "127",
    "flightID": "uuid",
    "type": "P",
    "aircraft": "uuid",
    "notes": null
  }
]
```

`type`: `J/E/C/G/O → P` (Passenger), resto → `C` (Cargo)

### `POST /api/smartcars/flights/charter` _(SCAuth)_

| Parámetro | Tipo |
|---|---|
| `number` | string | Puede incluir prefijo aerolínea (DAL123) |
| `departure` | string | ID aeropuerto |
| `arrival` | string | ID aeropuerto |
| `aircraft` | uuid | |

```json
{ "bidID": "uuid" }
```

Crea un vuelo charter invisible. El código de aerolínea se extrae del número.

### `POST /api/smartcars/flights/complete` _(SCAuth)_

| Parámetro | Tipo | Descripción |
|---|---|---|
| `uuid` | string | PIREP ID |
| `bidID` | string | Alternativa si no hay uuid |
| `flightLog` | string o array | Base64 o array de strings |
| `flightData` | string o array | Base64 (JSON) o array de objetos con `message`, `eventTimestamp` |
| `landingRate` | number | |
| `fuelUsed` | number | |
| `flightTime` | number | Horas (float) |
| `route` | array | Waypoints |
| `comments` | string/null | |

```json
{ "pirepID": "uuid" }
```

### `POST /api/smartcars/flights/cancel` _(SCAuth)_

| Parámetro | Tipo |
|---|---|
| `uuid` | string |
| `bidID` | string (alternativa) |

```json
{ "status": 200 }
```

### `POST /api/smartcars/flights/start` _(SCAuth)_

| Parámetro | Tipo |
|---|---|
| `bidID` | uuid |

```json
{ "trackingID": "pirep_uuid" }
```

### `GET /api/smartcars/flights/search` _(SCAuth)_

| Query Param | Tipo | Descripción |
|---|---|---|
| `limit` | int | Máx 100, default 100 |
| `departureAirport` | string | ICAO |
| `arrivalAirport` | string | ICAO |
| `aircraft` | string | Subfleet ID |

### `POST /api/smartcars/flights/unbook` _(SCAuth)_

| Parámetro | Tipo |
|---|---|
| `bidID` | uuid |

```json
{ "status": 200 }
```

### `POST /api/smartcars/flights/update` _(SCAuth)_

| Parámetro | Tipo | Descripción |
|---|---|---|
| `uuid` | string | PIREP ID (opcional si no existe aún) |
| `bidID` | uuid | |
| `phase` | string | Ver tabla abajo |
| `latitude` | float | |
| `longitude` | float | |
| `distanceRemaining` | float | |
| `heading` | float | |
| `altitude` | float | |
| `groundSpeed` | float | |
| `aircraft` | uuid | Solo para nuevo PIREP |

**Mapeo phase → PirepStatus:**

| Phase | Status |
|---|---|
| `boarding` | BOARDING |
| `push_back` | PUSHBACK_TOW |
| `taxi` | TAXI |
| `take_off` | TAKEOFF |
| `rejected_take_off` | TAXI |
| `climb_out` | INIT_CLIM |
| `climb` | ENROUTE |
| `cruise` | ENROUTE |
| `descent` | APPROACH |
| `approach` | APPROACH_ICAO |
| `final` | LANDING |
| `landed` | LANDED |
| `taxi_to_gate` | LANDED |
| `deboarding` | ARRIVED |
| `go_around` | APPROACH |
| `diverted` | DIVERTED |

---

# 3. DisposableBasic API

Prefijo: raíz (`/dbapi/...`, `/dstable/...`)
Middleware: `api`
Auth: Header `x-service-key` (validado contra `dbasic.srvkey`)

## 3.1 Reporte de Stable Approach

### `POST /dstable/new`

Sin auth por service key.

Endpoint pensado para plugin X-Plane. Body es JSON con:

| Campo | Descripción |
|---|---|
| `userID` | ID del piloto (campo personalizado) |
| `plugin_version` | Versión del plugin |
| `messages` | Array de objetos con campo `type` (type=2 = warning → inestable) |
| `analysis` | Objeto con `id` para detección de duplicados |

```json
{ "received": "OK" }
```

Posibles errores: `"Plugin support disabled"`, `"Report is not valid"`, `"Already received"`.

## 3.2 Eventos

### `GET /dbapi/events`

Header: `x-service-key`

Vuelos con `route_code = EVENT` y `start_date >= today`.

```json
{
  "current": [ /* flights starting today */ ],
  "upcoming": [ /* future flights */ ]
}
```

Cada flight:
```json
{
  "flight_number": "VHA 100",
  "flight_rcode": "EVENT",
  "flight_rleg": "1",
  "departure": "KLAX",
  "dep_iata": "LAX",
  "dep_icao": "KLAX",
  "dep_name": "Los Angeles Intl",
  "arrival": "KJFK",
  "arr_iata": "JFK",
  "arr_icao": "KJFK",
  "arr_name": "John F Kennedy Intl",
  "date": "25.May.2026",
  "time": "1200",
  "time_diff": "3 hours from now"
}
```

## 3.3 Módulos

### `GET /dbapi/modules`

```json
{
  "App Name": "phpVMS",
  "App URL": "https://...",
  "Disposable Basic": "Installed: 1 | Enabled: 1",
  "Disposable Special": "Installed: 1 | Enabled: 0"
}
```

## 3.4 Noticias

### `GET /dbapi/news`

Header opcional: `x-news-count` (default 3, noticias más recientes).

## 3.5 PIREPs

### `GET /dbapi/pireps`

Headers:

| Header | Descripción |
|---|---|
| `x-service-key` | Obligatorio |
| `x-pirep-type` | `live` para vuelos en vivo, otro valor para aceptados |
| `x-pirep-count` | Número de PIREPs (default 25, ignorado si type=live) |

## 3.6 Roster

### `GET /dbapi/roster`

Headers:

| Header | Descripción |
|---|---|
| `x-service-key` | Obligatorio |
| `x-roster-type` | `full` incluye todos excepto REJECTED/DELETED |

## 3.7 Estadísticas

### `GET /dbapi/stats`

```json
{
  "basic": { /* stats básicas de DB_StatServices */ },
  "pireps": { /* stats de PIREPs */ },
  "network": {
    "network_ivao_ttl": 0,
    "network_ivao_l90": 0,
    "network_ivao_l180": 0,
    "network_vatsim_ttl": 0,
    "network_vatsim_l90": 0,
    "network_vatsim_l180": 0
  }
}
```

---

# 4. DisposableSpecial API

Prefijo: raíz (`/dsapi/...`)
Middleware: `api`
Auth: Header `x-service-key`

## 4.1 Asignaciones

### `GET /dsapi/assignments`

Headers:

| Header | Descripción |
|---|---|
| `x-service-key` | Obligatorio |
| `x-a-year` | Año (default: año actual) |
| `x-a-month` | Mes (default: mes actual) |

```json
[
  {
    "id": 1,
    "pilot_id": "uuid",
    "pilot_ident": "VHA1001",
    "pilot_name": "John D.",
    "year": 2026,
    "month": 5,
    "order": 1,
    "flt_id": "uuid",
    "flt_number": "100",
    "flt_al_icao": "VHA",
    "flt_dep_icao": "KLAX",
    "flt_arr_icao": "KJFK",
    "prp_id": "uuid",
    "prp_date": "25.May.2026 14:00",
    "completed": true,
    "created_at": "25.May.2026 00:00",
    "updated_at": "25.May.2026 14:00"
  }
]
```

## 4.2 Módulos

### `GET /dsapi/modules`

Misma estructura que `/dbapi/modules`.

## 4.3 Tours

### `GET /dsapi/tours`

```json
{
  "active": [ /* tours activos */ ],
  "planned": [ /* tours futuros */ ]
}
```

Cada tour:
```json
{
  "code": "TOUR01",
  "name": "European Tour",
  "desc": "Description",
  "rules": "Rules",
  "type": "Airline Tour",
  "start": "01.May.2026 00:00",
  "end": "31.May.2026 23:59",
  "airline_id": 1,
  "airline_icao": "VHA",
  "airline_name": "Vholar Virtual",
  "state": "Active",
  "leg_count": 5,
  "plt_count": 10,
  "legs": [ /* array de flight legs */ ]
}
```

---

# 5. VmsOpenOps

Prefijo: `/api/vmsopenops`
Middleware: `web`, `auth` (usa sesión, NO api.auth)

## 5.1 Operaciones

### `GET /api/vmsopenops/operations`

| Query Param | Descripción |
|---|---|
| `type` | Filtrar: `jumpseat`, `ferry`, `all` (default) |

### `GET /api/vmsopenops/operations/pending`

| Query Param | Descripción |
|---|---|
| `type` | Filtrar por tipo de operación |

### `GET /api/vmsopenops/user/balance`

```json
{
  "success": true,
  "balance": 10000,
  "balance_formatted": "$100.00"
}
```

### `POST /api/vmsopenops/jumpseat/preview`

| Parámetro | Obligatorio | Tipo |
|---|---|---|
| `to_airport_id` | Sí | string (exists:airports) |

```json
{
  "success": true,
  "data": {
    "from_airport": { "id": "KLAX", "name": "Los Angeles Intl" },
    "to_airport": { "id": "KJFK", "name": "John F Kennedy Intl" },
    "distance": { "value": 2476.0, "formatted": "2,476 NM" },
    "cost": { "value": 5000, "formatted": "$50.00" },
    "user_balance": { "value": 10000, "formatted": "$100.00" },
    "can_pay_immediately": true,
    "is_same_airport": false
  }
}
```

### `POST /api/vmsopenops/jumpseat`

| Parámetro | Obligatorio | Tipo |
|---|---|---|
| `to_airport_id` | Sí | string (exists:airports) |
| `type` | Sí | in:0,1 (1=inmediato con cobro) |
| `reason` | Condicional | string, según setting `vms_open_ops_require_reason` |

### `POST /api/vmsopenops/ferry/preview`

| Parámetro | Obligatorio | Tipo |
|---|---|---|
| `aircraft_id` | Sí | string (exists:aircraft) |

### `POST /api/vmsopenops/ferry/available`

| Parámetro | Obligatorio | Tipo |
|---|---|---|
| `subfleet_id` | Sí | string (exists:subfleets) |

```json
{
  "success": true,
  "aircraft": [
    {
      "id": "uuid",
      "registration": "G-AAAA",
      "name": "Boeing 737-800",
      "current_airport": "KLAX",
      "distance": 123.45,
      "distance_formatted": "123.5 NM",
      "cost": 2500,
      "cost_formatted": "$25.00"
    }
  ]
}
```

### `POST /api/vmsopenops/ferry`

| Parámetro | Obligatorio | Tipo |
|---|---|---|
| `aircraft_id` | Sí | string (exists:aircraft) |
| `type` | Sí | in:0,1 |
| `reason` | Condicional | string |

### `DELETE /api/vmsopenops/{id}`

Cancela una solicitud de operación.

## 5.2 Estadísticas

### `GET /api/vmsopenops/stats`

| Query Param | Tipo | Descripción |
|---|---|---|
| `period` | string | `week`, `month` (default), `quarter`, `semester`, `year` |
| `days` | int | Usado si period no coincide con named periods (default 30) |
| `year` | int | Año específico |
| `month` | int | Mes específico (requiere year) |

```json
{
  "success": true,
  "data": {
    "top_landing_score": [ { "pilot": "...", "pilot_id": 1, "avg_landing_rate": -100 } ],
    "top_miles": [ { "pilot": "...", "pilot_id": 1, "total_distance": 5000 } ],
    "top_flights_count": [ { "pilot": "...", "pilot_id": 1, "flight_count": 20 } ],
    "top_score": [ { "pilot": "...", "pilot_id": 1, "avg_score": 95.5, "flight_count": 10 } ],
    "top_routes": [ { "route": "KLAX → KJFK", "count": 15 } ],
    "top_subfleets": [ { "subfleet": "Boeing 738", "subfleet_type": "B738", "flight_count": 30 } ],
    "top_aircraft_flights": [ { "aircraft_registration": "G-AAAA", "aircraft": "B738", "aircraft_type": "B738", "flight_count": 10 } ],
    "top_aircraft_miles": [ { ... } ],
    "top_arr_airports": [ { "airport_id": "KJFK", "airport": "KJFK - John F Kennedy Intl", "arrival_count": 25 } ],
    "top_dpt_airports": [ { ... } ],
    "stats": {
      "total_pireps": 100,
      "total_block_time": "150h 30m",
      "avg_block_time": "1h 30m",
      "total_fuel_burn": 500000,
      "avg_fuel_burn": 5000,
      "total_distance": 100000,
      "avg_distance": 1000.5,
      "avg_fuel_burn_per_hour": 3333,
      "avg_distance_per_hour": 666.7,
      "avg_landing_rate": -150,
      "total_passengers": 500,
      "avg_passengers": 5.0,
      "total_freight": 10000,
      "avg_freight": 100.0,
      "avg_score": 0
    }
  }
}
```

Los datos se cachean por 5 minutos.

---

# 6. Módulos de Ejemplo / Stub

## CHJumpSeat (prefijo: `/api/chjumpseat`)

### `GET /api/chjumpseat`
```json
{ "message": "Hello, world!" }
```

### `GET /api/chjumpseat/hello` _(api.auth)_
```json
{ "name": "John Doe" }
```

## VmsOpenFileManager (prefijo: `/api/vmsopenfilemanager`)

### `GET /api/vmsopenfilemanager`
```json
{ "message": "Hello, world!" }
```

### `GET /api/vmsopenfilemanager/hello` _(api.auth)_
```json
{ "name": "John Doe" }
```

### `GET /api/vmsopenfilemanager/test` (ruta standalone, si está registrada)
```json
{ "message": "API working" }
```

## TestABC (prefijo: `/api/testabc`)

### `GET /api/testabc`
```json
{ "message": "Hello, world!" }
```

### `GET /api/testabc/hello` _(api.auth)_
```json
{ "name": "John Doe" }
```

## Sample (prefijo: `/api/sample`)

### `GET /api/sample`
```json
{ "message": "Hello, world!" }
```

### `GET /api/sample/hello` _(api.auth)_
```json
{ "name": "John Doe" }
```

---

# Apéndice: Mapa de Enums

## PirepState

| ID | Clave | Etiqueta |
|---|---|---|
| 0 | `IN_PROGRESS` | In Progress |
| 1 | `PENDING` | Pending |
| 2 | `ACCEPTED` | Accepted |
| 3 | `CANCELLED` | Cancelled |
| 4 | `DELETED` | Deleted |
| 5 | `DRAFT` | Draft |
| 6 | `REJECTED` | Rejected |
| 7 | `PAUSED` | Paused |

> `state=2` es **ACCEPTED**, no "Arrived". "Arrived" es el `status` del PIREP (`status=ARR`, `status_text=Arrived`).

## PirepStatus

| Clave | Etiqueta |
|---|---|
| `BOARDING` | Boarding |
| `PUSHBACK_TOW` | Pushback & Tow |
| `TAXI` | Taxi |
| `TAKEOFF` | Takeoff |
| `INIT_CLIM` | Initial Climb |
| `ENROUTE` | En Route |
| `APPROACH` | Approach |
| `APPROACH_ICAO` | Approach (ICAO) |
| `LANDING` | Landing |
| `LANDED` | Landed |
| `ARRIVED` | Arrived |
| `CANCELLED` | Cancelled |
| `DIVERTED` | Diverted |
| `PAUSED` | Paused |
| `SCHEDULED` | Scheduled |

## FlightType

| Clave | Etiqueta |
|---|---|
| `J` | Passenger |
| `E` | Passenger |
| `C` | Passenger |
| `G` | Passenger |
| `O` | Passenger |
| `A` | Cargo |
| `H` | Cargo |
| `I` | Cargo |
| `K` | Cargo |
| `M` | Cargo |
| `P` | Cargo |
| `T` | Cargo |
| `W` | Cargo |
| `X` | Cargo |

## AircraftState

| Clave | Descripción |
|---|---|
| `PARKED` | Estacionado |
| `IN_USE` | En uso |
| `IN_AIR` | En aire |

## UserState

| ID | Clave |
|---|---|
| 0 | `PENDING` |
| 1 | `ACTIVE` |
| 2 | `REJECTED` |
| 3 | `DELETED` |
| 4 | `ON_LEAVE` |

---

# Apéndice: Recursos (Resource Structures)

## PirepResource

```json
{
  "id": "uuid",
  "ident": "VHA100",
  "phase": null,
  "status_text": "ARRIVED",
  "airline_id": 1,
  "aircraft_id": "uuid",
  "flight_id": "uuid",
  "flight_number": "100",
  "dpt_airport_id": "KLAX",
  "arr_airport_id": "KJFK",
  "alt_airport_id": null,
  "status": 11,
  "state": 2,
  "source": 0,
  "source_name": "manual",
  "distance": 2476.0,
  "planned_distance": 2500.0,
  "block_time": 320,
  "flight_time": 300,
  "planned_flight_time": 310,
  "fuel_used": 15000.0,
  "block_fuel": 16000.0,
  "block_off_time": "2026-05-25T12:00:00Z",
  "block_on_time": "2026-05-25T17:00:00Z",
  "route_code": null,
  "route_leg": null,
  "level": 36000,
  "zfw": 60000.0,
  "landing_rate": -150,
  "score": 95,
  "notes": "Flight notes",
  "route": "SID STAR",
  "created_at": "2026-05-25T11:00:00Z",
  "updated_at": "2026-05-25T17:00:00Z",
  "submitted_at": "2026-05-25T17:00:00Z",
  "read_only": false,
  "cancelled": false,
  "aircraft": { ... },
  "airline": { ... },
  "dpt_airport": { ... },
  "arr_airport": { ... },
  "position": { ... },
  "comments": [ ... ],
  "user": { ... },
  "flight": { ... },
  "fields": { "field_name": "value" },
  "simbrief": { ... }
}
```

## UserResource

```json
{
  "id": "uuid",
  "pilot_id": 1001,
  "ident": "VHA1001",
  "name": "John Doe",
  "name_private": "John D.",
  "avatar": { "url": "https://..." },
  "discord_id": "123456789",
  "vatsim_id": "1234567",
  "ivao_id": "123456",
  "rank_id": 3,
  "home_airport": { ... },
  "curr_airport": { ... },
  "last_pirep_id": "uuid",
  "flights": 42,
  "flight_time": 5000,
  "transfer_time": 120,
  "total_time": 5120,
  "timezone": "America/New_York",
  "state": 1,
  "airline": { ... },
  "bids": [ ... ],
  "rank": { ... },
  "subfleets": [ ... ]
}
```

## FlightResource

```json
{
  "id": "uuid",
  "airline_id": 1,
  "flight_number": "100",
  "route_code": null,
  "route_leg": null,
  "dpt_airport_id": "KLAX",
  "arr_airport_id": "KJFK",
  "alt_airport_id": null,
  "dpt_time": "1200",
  "arr_time": "2000",
  "days": 127,
  "level": 36000,
  "flight_type": "J",
  "load_factor": null,
  "load_factor_variance": null,
  "distance": 2476,
  "route": "ROUTE STRING",
  "ident": "VHA100",
  "airline": { ... },
  "subfleets": [ ... ],
  "fields": { ... },
  "simbrief": { ... }
}
```

## AirportResource

```json
{
  "id": "KLAX",
  "iata": "LAX",
  "icao": "KLAX",
  "name": "Los Angeles International",
  "full_name": "Los Angeles International Airport",
  "location": "Los Angeles, California, United States",
  "country": "US",
  "lat": 33.9425,
  "lon": -118.4081,
  "altitude": 125,
  "timezone": "America/Los_Angeles",
  "hub": true
}
```

## AircraftResource

```json
{
  "id": "uuid",
  "subfleet_id": "uuid",
  "airline_id": 1,
  "name": "G-AAAA",
  "registration": "G-AAAA",
  "icao": "B738",
  "iata": "738",
  "hex_code": null,
  "status": "PARKED",
  "ident": "G-AAAA",
  "dow": 41689.0,
  "zfw": 62779.0,
  "mtow": 79015.0,
  "mlw": 66360.0,
  "fuel_onboard": 5000.0,
  "subfleet": {
    "fares": [
      { "id": "uuid", "code": "Y", "name": "Economy", "price": 200, "capacity": 150, "count": 0 }
    ]
  }
}
```

---

> Documentación generada a partir del código fuente. Última actualización: Mayo 2026.
