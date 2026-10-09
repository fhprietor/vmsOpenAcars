# Pedido al equipo de phpVMS — almacenar y servir el análisis del aterrizaje (flare)

> **De:** equipo de vmsOpenAcars · **Para:** equipo de phpVMS
> **Fecha:** 09/10/2026 · **Estado del cliente:** v0.9.36 (los dos fixes del flare —`dist_ft` tras el
> toque y el cero desde el umbral legal— van en la siguiente versión)
> **Contexto:** el cliente captura desde v0.9.32 una **traza fina del flare** (~8–10 Hz, 60–100
> muestras) y las **métricas del aterrizaje**, pero hoy **solo viven en la base local** del piloto
> (`landing_log.sqlite`). Queremos subirlas al servidor para que el análisis de un PIREP pueda mirar
> también el flare, comparar aterrizajes en la **misma pista** o con el **mismo avión**, y más
> adelante recomendar acciones. **Este pedido es la ingesta y la lectura**; la agregación y la capa
> de IA van en un segundo paso, una vez el dato exista en vuestro lado.

---

## 1. Lo que ya existe, medido

La captura está **verificada en vuelo real** (dos vuelos, 09/10/2026), no contra un remuestreo:

| | Vuelo 43 (`8062`) | Vuelo 44 (`9060`) |
|---|---|---|
| Ruta | SKCL → SKCC | SKCC → SKBG |
| Pista | 16 | 17 |
| Landing rate | −530 fpm | **−789 fpm** |
| G | 2,4 | **2,5** |
| Score | 43 | 42 |
| Touchdown (umbral legal) | 1 156 ft | 202 ft |
| Muestras en la traza | 97 | 81 |
| LTOW | 150 468 lb | 150 821 lb |
| Aeronave | B38M `iFly B38M VHR N665VH (178Seat)` | B38M (misma) |

Cada muestra de la traza lleva **hasta 17 campos** (instante, distancia al umbral, AGL,
radioaltímetro, IAS, VS, pitch, bank, GS, N1 de los dos motores, N2, flaps %, detent de flaps,
spoilers, on-ground) — y las métricas, otros ~20 (todo lo de arriba más meteo del aterrizaje,
identidad de aeronave, resumen de flaps y corte de potencia). Todo esto **ya está en memoria y en
`landing_log.sqlite`** en el momento de filear; subirlo es serializar lo que ya existe.

---

## 2. Lo que pedimos

### 2.1 Endpoint de ingesta (lo principal)

**`POST /api/pireps/{pirep_id}/landing`** _(api.auth, el mismo `X-API-KEY` de siempre)_

- Se llama **una sola vez al filear** el PIREP (no en streaming a 10 Hz durante el vuelo).
- **Idempotente**: re-POST con el mismo `pirep_id` reemplaza, no duplica (UPSERT).
- Lleva `schema_version` para poder evolucionar el payload sin romper lectores viejos.

Payload (ejemplo real, vuelo 44):

```json
{
  "schema_version": 1,
  "source_name": "vmsOpenAcars/0.9.37",
  "metrics": {
    "landing_rate_fpm": -789,
    "g_force": 2.5,
    "touchdown_dist_ft": 202.3,
    "centerline_dev_ft": 24.4,
    "score": 42,
    "runway_name": "17",
    "runway_length_ft": 7362.0,
    "runway_true_deg": 173.2,
    "landing_weight_lbs": 150821.6,
    "aircraft_icao": "B38M",
    "aircraft_title": "iFly B38M VHR N665VH (178Seat)",
    "aircraft_model": "737 MAX 8",
    "flaps_label": "FLAPS 15",
    "flaps_pct": 62.5,
    "flaps_index": 5,
    "power_cut_sec": 4.2,
    "flare_capture_armed": true,
    "landing_metar": "SKBG ...",
    "wind_dir_deg": 10,
    "wind_speed_kt": 3,
    "wind_gust_kt": null,
    "headwind_kt": 1.4,
    "crosswind_kt": -2.7
  },
  "trace": [
    {
      "ts_utc": "2026-10-09T11:26:39.733Z",
      "dist_ft": 1482.8,
      "agl_ft": 95.0,
      "radar_alt_ft": 362.8,
      "ias_kt": 149.0,
      "vs_fpm": -850,
      "pitch_deg": 2.5,
      "bank_deg": 0.3,
      "gs_kt": 145,
      "n1_1_pct": 55.0,
      "n1_2_pct": 55.0,
      "n2_1_pct": null,
      "n2_2_pct": null,
      "flaps_pct": 62.5,
      "flaps_index": 5,
      "spoilers": false,
      "on_ground": false
    }
  ]
}
```

**Convenciones que pedimos respetar tal cual** (son las que hacen el dato comparable):

- **`dist_ft` es negativo pasado el umbral** (positivo = acercándose, 0 = umbral, negativo = sobre
  la pista). El cero es el **umbral legal** de aterrizaje (ya descontado el desplazamiento), el
  mismo que usa `touchdown_dist_ft` (que va en **positivo**). No convertir signos.
- **`null` = sin dato, nunca 0.** Aplica a `agl_ft`, `radar_alt_ft`, N1/N2, `flaps_index`,
  `wind_gust_kt`, etc. Un 0 que parezca una medida (p. ej. radioaltímetro) es falso.
- **`touchdown_dist_ft` y `centerline_dev_ft` son los que ya puntúan** el PIREP (criterio Touchdown
  Zone y Centreline); no hay que recalcularlos, solo guardarlos.
- **`flaps_label` puede llevar `≈`** cuando sale de traducir el porcentaje del mando; sin `≈` cuando
  sale del detent real (`flaps_index`). Es información, no ruido.

### 2.2 Modelo de datos (migración)

**Una tabla** `pirep_landings` (1:1 con `pireps`), con las métricas **en columnas** (para poder
agregar) y la traza como **columna JSON** (para el replay de Chart.js, sin una tabla por muestra):

```
pirep_landings
  id                  uuid PK
  pirep_id            uuid UNIQUE FK → pireps.id
  schema_version      integer
  source_name         string nullable
  landing_rate_fpm    integer nullable
  g_force             float   nullable
  touchdown_dist_ft   float   nullable
  centerline_dev_ft   float   nullable
  score               integer nullable
  runway_name         string  nullable
  runway_length_ft    float   nullable
  runway_true_deg     float   nullable
  landing_weight_lbs  float   nullable
  aircraft_icao       string  nullable
  aircraft_title      string  nullable
  aircraft_model      string  nullable
  flaps_label         string  nullable
  flaps_pct           float   nullable
  flaps_index         integer nullable
  power_cut_sec       float   nullable
  flare_capture_armed boolean nullable
  landing_metar       text    nullable
  wind_dir_deg        float   nullable
  wind_speed_kt       float   nullable
  wind_gust_kt        float   nullable
  headwind_kt         float   nullable
  crosswind_kt        float   nullable
  trace               json    nullable
  created_at / updated_at
```

**Por qué métricas en columnas y traza en JSON**: la agregación entre pilotos se hace sobre las
métricas (medianas, distribuciones), no sobre 80 muestras; y la traza solo se lee entera para el
replay. Columnas = SQL barato y agregable; JSON = cero migraciones cada vez que cambie un campo de
la traza. Si preferís una tabla hija `pirep_landing_trace` (1:N), el contrato no cambia: la
decisión de almacenamiento es vuestra.

### 2.3 Lectura

- **`GET /api/pireps/{pirep_id}/landing`** _(api.auth)_ — devuelve `metrics` + `trace` para el
  replay en la web. Con `404` si el PIREP no tiene landing (vuelos anteriores al dato o sin traza).
- **`GET /api/landings/aggregate?airport=&runway=&aircraft=`** _(api.auth, segundo paso)_ —
  agregación determinista por pista o por tipo de aeronave: conteo, mediana y cuartiles de
  `landing_rate_fpm`, `touchdown_dist_ft`, `g_force`, `power_cut_sec`. Es el insumo del feedback
  entre pilotos **sin** IA. Lo dejamos para la fase 2; lo listamos aquí para que el esquema de
  columnas (que sí es de la fase 1) lo haga posible.

---

## 3. Lo que hacemos nosotros, sin pediros nada

- **Serializar y enviar el payload** al filear, reutilizando el patrón que ya nos costó un bug
  (copiar los buffers **antes** del `await` del file). Un solo POST, en segundo plano.
- **Detrás de un flag configurable** (`pirep_landing_analysis_enabled`), apagado por defecto hasta
  que vuestro endpoint exista: si responde `404`/`401`/`400`, **el vuelo se filea igual** y el dato
  queda solo en el log local. No bloqueamos el file por esto.
- **El cómputo de `flaps_label`, `power_cut_sec` y la identidad de aeronave** (`aircraft_*`) ya lo
  hacemos en el cliente; vosotros solo guardáis. No hay que reimplementar reglas.
- Avisaros con un PIREP de prueba real (con `source_name` nuestro) en cuanto empecemos a enviar,
  para que validéis la ingesta contra un vuelo, no contra un payload sintético.

---

## 4. Volumen y retención (para cerrar la discusión antes de que exista)

- **~60–100 muestras × ~17 campos ≈ 1–2 KB** por aterrizaje (la traza domina; las métricas son
  ~300 B). Con **500 aterrizajes/mes = ~1 MB/mes**. No es un problema de almacenamiento.
- Retención: proponemos **indefinida** (el valor está en comparar a lo largo del tiempo), pero si
  preferís un TTL, la traza se puede purgar y conservar solo las métricas.

---

## 5. Por qué ahora y qué viene después

El dato ya **se captura y se guarda en local**, verificado en dos vuelos. Sin este endpoint, se
pierde al cerrar el simulador: la aerolínea no puede ver el flare de sus pilotos, y el `landing_rate`
del PIREP es solo el titular de una historia que la traza cuenta entera.

Hoja de ruta, para que se vea que esto no pide un subsistema de golpe:

1. **Ingesta + almacenamiento** (este pedido): `POST /landing` + tabla + `GET /landing` para replay.
2. **Agregación determinista** (`/landings/aggregate`): feedback entre pilotos por pista / por
   avión, sin IA.
3. **Recomendaciones** (capa de IA servidora, gated): explicar un aterrizaje y sugerir acciones;
   **no toca el `score` ni la acreditación**.
4. **Web**: replay del flare con Chart.js + comparación + recomendaciones en la página del PIREP.
