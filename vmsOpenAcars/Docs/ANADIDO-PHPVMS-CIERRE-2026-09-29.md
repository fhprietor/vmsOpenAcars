# vmsOpenAcars → phpVMS: añadido al cierre — dos cosas que la prueba en vivo no confirma

> **De:** equipo de vmsOpenAcars · **Para:** equipo de phpVMS
> **Fecha:** 2026-09-29 · **Ref:** `CIERRE-PHPVMS-PRUEBAS.md`
> **Contexto:** el mantenedor nos autorizó a escribir en los PIREPs de prueba, así que **probamos el
> contrato contra vuestra API** en vez de darlo por bueno. Dos resultados, uno nuestro y otro vuestro.

---

## 1. Vuestro: `fuel` no se persiste todavía

Pedimos una posición nueva con combustible y la volvemos a leer:

```
POST /api/pireps/Z12naE7Ox6pxVyde/acars/position
{"positions":[{"type":0,"status":"SCH","log":"RAAS: prueba type=0","lat":4.71253,
               "lon":-74.152347,"heading":352,"fuel":222.2,"source":"vmsOpenACars"}]}
→ 200 {"message":"1 positions added","attrs":[],"count":1}
```

Y esa misma fila, al leerla:

```json
{"id":"ZQ8ZQadPY0m2VEWD","log":"RAAS: prueba type=0","status":"SCH",
 "fuel":{"localUnit":"kg","internalUnit":"lbs","responseUnits":["kg","lbs"]}}
```

**El objeto `fuel` vuelve con las unidades y sin valor** — exactamente igual que `distance` cuando
está vacío. La fila se guarda (se sirve, tiene `id`), así que el `$fillable` no es lo único que falta.

Nosotros ya enviábamos `fuel` desde antes de vuestro cambio (`TelemetryCoordinator.PrepareTelemetry`,
`fuel = Math.Round(e.FuelLbs, 1)`, en **libras**). Si el cast espera la unidad *local* (kg) y no la
interna (lbs), decidnos cuál queréis y lo cambiamos; pero primero hay que ver un valor guardado.

## 2. Nuestro: `type = 2` se acepta y no se sirve

Lo encontramos preparando lo anterior. Dos peticiones idénticas salvo el `type`:

| Petición | Respuesta | ¿Aparece en `/acars/position`? |
|---|---|---|
| `type: 2` | `200 {"message":"1 positions added"}` | **No** |
| `type: 0` | `200 {"message":"1 positions added"}` | **Sí** (199 → 200 filas) |

Es un fallo nuestro y está corregido: los avisos del RAAS y el batch informativo del arranque
—versión, CPU, GPU, OS, AIRAC— iban con `type = 2`, así que **llevaban años guardándose invisibles**
para cualquier consumidor. Justo lo contrario de lo que os pedimos: que fueran auditables.

```json
{"id":"ZQ8ZQadPY0m2VEWD","type":0,"status":"SCH","log":"RAAS: prueba type=0","source":"vmsOp"}
```

**Desde la próxima versión los avisos llegan con `type = 0` y el prefijo `RAAS:`**, como acordamos. Os
avisamos cuando empiecen a llegar de un vuelo real para que los verifiquéis. Y si el `type` tiene un
significado que debamos respetar (2 no se sirve, ¿es «mensaje»?), decídnoslo y lo usamos bien.

## 3. Lo que sí verificamos

- **`PUT /api/pireps/{id}`** sobre un PIREP cerrado responde `400 PIREP is read-only`: correcto, y
  nuestro cliente solo actualiza durante el vuelo. Los campos `Departure Runway` / `Arrival Runway` /
  `Taxi Route` se mandan en el prefile y al confirmar la ruta, así que **el contrato de `fields` se
  verificará en el primer vuelo real**, no sobre un PIREP viejo.
- `POST /acars/position` con `type = 0` guarda y sirve la fila, con `source: "vmsOp"` ✓.

> Las filas sueltas que quedan en `Z12naE7Ox6pxVyde` son de esta prueba (una visible «RAAS: prueba
> type=0» y dos invisibles de `type=2`), no de un vuelo. Son datos de prueba y se pueden borrar.
