# vmsOpenAcars → phpVMS: respuesta sobre los datos de pruebas

> **De:** equipo de vmsOpenAcars · **Para:** equipo de phpVMS
> **Fecha:** 2026-09-29 · **Responde a:** `RESPUESTA-PHPVMS-PRUEBAS.md`
> **Estado del cliente:** v0.9.16

Gracias por mirarlo contra el código y la base. **Dos de nuestras premisas eran falsas y las dos nos
habrían costado el trabajo**: el corpus no es de un vuelo, y el `score` que faltaba no era un fallo
nuestro. Verificado desde nuestro lado antes de contestar.

---

## 1. Aceptadas las dos correcciones, y reproducidas

**La fase va en `status`, no en `phase`.** Nuestra medición decía «cero posiciones de rodaje» porque
leíamos `phase`, que está vacía en todo el servidor. Reproducido con `status` en cuatro de los siete
vuelos, y **coincide con vuestra tabla al número**:

| PIREP | Versión | `n` | `PBT` | `TXI` |
|---|---|---|---|---|
| `Aj3N4gqn9omdoJE2` | 0.8.10 | 157 | **1** | **17** |
| `XAW2A5Gybw6BJ1NO` | 0.9.1 | 138 | **3** | **27** |
| `E7DK47e88XdabzoL` | 0.9.1 | 204 | **4** | **20** |
| `Z12naE7Ox6pxVyde` | 0.9.10 | 199 | **2** | **16** |

Es decir: **el banco no tenía un vuelo, tenía 39 desde el principio**, y nosotros no los veíamos. El
error es nuestro y queda anotado en nuestro `CHANGELOG` para que nadie lo repita.

**`state=2` es `ACCEPTED`.** Leímos `state` como estado de vuelo por el `status_text="Arrived"` de al
lado. Con vuestra tabla (0 `IN_PROGRESS`, 1 `PENDING`, 2 `ACCEPTED`…) nuestro punto 2.6 **ya estaba
respondido** y no hace falta nada más: con `state` y `comments` en el detalle nos basta. **No
necesitamos `activity_log`** ni el historial de quién cambió qué.

**Y `MNjR664PBAr25RbD` sí está en la API**: es del piloto 76 y sale a la primera con `?id=76`.
Nuestra lista era la de nuestra propia cuenta, y ahí no podía estar.

## 2. Nuestras respuestas a lo que dejasteis abierto

### 2.4 Pista usada → opción 1, con vuestros nombres

**Campos personalizados, y usad la convención que ya existe: `departure-runway` y
`arrival-runway`.** Cero código en el servidor y coherente con `DisposableBasic`. Empezaremos a
enviarlos en `prefile`/`update` en cuanto estén creados. **No** queremos columnas nuevas ni poblar
`simbrief`: la opción 3 implicaría mandaros el OFP entero y no hace falta para esto.

### 2.5 Ruta de rodaje → nombre del campo: **`Taxi Route`**

De ahí sale el `slug` `taxi-route`. Contenido: **el texto tal cual lo escribió el piloto** en el
diálogo de rodaje, con las calles separadas por espacios (p. ej. `F E M A A3`). Es la autorización de
ATC tal como la entendió, que es el dato bueno; no lo normalizamos nosotros para no perder
información. Si más adelante hace falta distinguir «tecleada» de «deducida de la traza», añadimos un
segundo campo, pero de momento con uno basta.

### 2.2 `source_name` en los históricos → nuestra propuesta

Para el *backfill*, y sabiendo que son importaciones y PIREPs manuales:

- importaciones (CrewSystem, `vholar:migrate-pireps`) → **`CrewSystem/import`**
- PIREPs creados a mano en el panel → **`manual`**

Mientras tanto **filtramos por `acars.source`** (`vmsOp`), como decís, que además es el marcador que
distingue «hay telemetría» de «no la hay». No es urgente: los 39 del corpus ya los localizamos con
`?source_name=vmsOpenACars` por piloto.

### 2.1 Endpoint global → no lo necesitamos ahora

Iterar los 8 pilotos con `?id=` y `limit=1000` es perfectamente asumible para 39 PIREPs. Si algún día
el corpus crece a cientos, lo pedimos: un global con filtro por productor y rango de fechas sería más
limpio, pero no merece una línea de trabajo vuestra hoy.

### 2.6 (ya respondido arriba) y `TXI` mezclado

Sobre `TXI` que agrupa salida y llegada: **no hace falta que lo cambiéis**. Para el banco de rodaje
distinguimos por la posición en el vuelo —`PBT`/`TXI` antes del `TOF` es salida, después del `LDG` es
llegada— y eso lo resolvemos en el cliente. Un cambio de esquema por comodidad nuestra no compensa.

### 6 `fuel` por posición → arregladlo, pero no por nosotros

**No enviamos `fuel` en las posiciones** (verificado en el cliente: no está en el `payload`), así que
el bug no nos afecta y no lo necesitamos: el análisis de aterrizaje guarda su propia traza en local.
Dicho eso: es una línea en `$fillable` y el dato es útil para la aerolínea, así que **si lo activáis
nosotros empezamos a mandarlo** —es un cambio pequeño en el cliente—. Decididlo por el valor del
dato, no por nosotros.

## 3. Lo que hacemos nosotros (vuestro §7)

1. **Avisos del RAAS al `log` de `acars/logs`**: confirmado que el endpoint acepta texto. Los
   enviaremos agrupados y con un prefijo propio para que se distingan de las `CHK`; os avisamos
   cuando empiece a llegar y los verificáis.
2. **El banco de las 39 trazas**: reconstruible con vuestra receta (8 pilotos × `?source_name=vmsOpenACars&limit=1000`).
   Es lo que faltaba para medir el grafo de rodaje por aeropuerto en vez de por anécdota.
3. **Y el dato que nos llevamos apuntado**: el desglose del score que enviamos en las `CHK`
   (`SC:ov=…`) **también está en el servidor**, no solo en el log del piloto. Eso nos permite auditar
   la puntuación de un vuelo desde el PIREP, que hasta hoy no sabíamos que era posible.
