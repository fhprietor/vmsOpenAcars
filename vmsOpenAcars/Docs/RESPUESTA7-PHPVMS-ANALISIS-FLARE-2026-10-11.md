# Respuesta (7) al equipo de phpVMS — los dos campos: sí, y de dónde salen

> **De:** equipo de vmsOpenACars · **Para:** equipo de phpVMS
> **Fecha:** 11/10/2026
> **Respecto a:** vuestra petición de dos campos (`offset_threshold_ft`, `vref_kt`) y los cinco opcionales
> **Estado:** **los dos nombres y las dos unidades nos valen y los vamos a mandar**. Los dos datos
> existen y sabemos de dónde salen. Hay **una regla** que conviene fijar por escrito antes de
> empezar (`0` contra `null`), y de vuestros cinco opcionales **dos ya los tenéis**.

---

## 1. Confirmado: nombres, unidades y versión de esquema

| Campo | Unidad | Veredicto |
|---|---|---|
| `metrics.offset_threshold_ft` | pies | **aceptado tal cual** |
| `metrics.vref_kt` | nudos | **aceptado tal cual** |

**No subimos `schema_version`**: los dos son campos **opcionales y aditivos**, vosotros ya tenéis las
columnas y el contrato no cambia. Quien marca la versión de verdad es `source_name`
(`vmsOpenACars/<versión>`), que ya viaja en cada envío.

**Y una regla que sí hay que escribir, porque es la trampa al revés de la que llevamos todo el
hilo:** en `offset_threshold_ft`, **`0` es un valor legítimo** (umbral legal y extremo físico
coinciden) y **`null` es «no lo sé»** —que es la señal para que sigáis deduciéndolo—. Es lo
contrario de `dist_ft = 0` o de una tasa `0`: aquí un cero **mide**. Lo mismo para `vref_kt`: `null`
si el plan no traía TLR.

## 2. `offset_threshold_ft`: lo tenemos, y en el sitio correcto

Sale del dataset de NavData —`offset_threshold_ft` del objeto de pista, el mismo dato que ya viaja
en `/runways/`— y lo consumimos desde la 0.9.37 para **alinear la traza del flare** (los 496 ft de la
17 de SKBG que calculamos a mano en la respuesta (4) salen de ahí, no de una estimación). Así que el
valor existe en memoria **en el momento del toque**, que es donde hay que capturarlo.

**Lo que cuesta:** una columna nueva en nuestro log local (`flights.offset_threshold_ft`), el campo
en el bloque inmutable de la pista del toque —donde vive ya `runway_length_ft`, que va con él por el
mismo motivo: los dos describen la **misma** pista— y la línea del payload. Con su test.

**Lo que no va a pasar:** el **histórico no lo tendrá.** Ni los 32 ya subidos ni los rellenos: la
columna no existía, y no vamos a deducir el offset a posteriori para rellenarla —eso sería
exactamente la deducción que este campo viene a eliminar—. Así que **vuestro camino de deducción
sigue haciendo falta para el histórico**, y esa es una razón más para no borrarlo todavía. Para el
vuelo nuevo: **resta**, como decís.

## 3. `vref_kt`: lo publica SimBrief, y no lo vamos a inventar

**Sí existe y es el bueno.** El OFP trae el análisis de aterrizaje (TLR) con la referencia **al peso
de planeo**:

```text
tlr.landing.distance_dry.speeds_vref = 148        (kt)
tlr.landing.distance_wet.speeds_vref = 148
tlr.landing.conditions.planned_weight = 247261    (lb)
```

Y la tabla del propio informe confirma que es dependiente del peso: `LDW 2400 → VREF 146`,
`2440 → 147`, `2480 → 148`, `2520 → 150`, `2560 → 151`. Es decir: **148 nudos es el Vref de este
plan, a este peso**, no un número de catálogo.

**Dos matices que os toca saber:**

1. **Es el Vref del peso de planeo**, no del peso real en la toma. El peso real ya lo tenéis
   (`landing_weight_lbs`, que es una lectura medida del simulador), así que la comparación
   planeado-contra-real la podéis hacer vosotros; nosotros mandamos el que el plan declara y **no lo
   corregimos**.
2. **No hay Vapp en el OFP.** Buscado: lo único que aparece con esa pinta es una etiqueta de
   configuración (`VREF30` = flaps 30). Así que mandamos **solo `vref_kt`**; si algún día queréis
   Vapp, tendrá que salir de otro sitio y os lo diremos antes de mandar nada.

**Y lo que no vamos a hacer:** derivar un Vref del tipo de avión y el peso. Tenemos una **tabla de
ventanas Vapp por tipo** para el gate de aproximación estabilizada, y esa ventana **no es un Vref**
—es un rango de nuestra política de puntuación—. Mandarla con el nombre `vref_kt` sería meter una
etiqueta donde hay un valor, que es justo lo que vuestro detector de cifras acaba de cazar en el
otro sentido.

## 4. Los cinco opcionales, uno a uno

**1. La geometría de la TDZ — cuidado, no es geometría del dataset.** Las bandas que tenemos son
**nuestra política de puntuación**, calculadas a partir de `runway_length_ft` (que ya os mandamos),
y son exactamente éstas:

| Tramo | Con dato de longitud | Sin dato |
|---|---|---|
| primer tramo | `min(3000, max(1500, 15 % L))` | 1 500 ft |
| segundo tramo | hasta `min(3000, L/2)` | 2 500 ft |
| el resto | más allá | más allá de 2 500 ft |

O sea: **se pueden derivar de un dato que ya tenéis**. Si preferís no replicar la regla, os mandamos
los **dos cortes en pies** (que es lo que sale de esas cuentas para esa pista) o un
`tdz_policy_version`, y decidlo vosotros — nosotros preferimos los cortes antes que duplicar una
política en dos sitios: una regla copiada se queda atrás en el primer cambio y nadie se entera.

**2. Autopiloto y autothrottle en la toma.** El **A/P sí** lo leemos (maestro `0x07BC` más bits de
modo, a 20 Hz, con debounce) y **ya os va en las posiciones** (`autopilot`); podemos añadirlo a las
métricas del aterrizaje: estado en el toque y **a qué AGL se desconectó**, que lo registramos en el
log. El **A/T no lo leemos en absoluto**: no hay *offset* de FSUIPC para él y habría que ir por
LVar, que exige WASM de FSUIPC7 y **solo MSFS** — es una investigación, no un campo.

**3. Por qué se cerró la captura — sí, y el vocabulario ya existe.** No es un texto libre que
tengamos que inventar ahora: lo produce la política de captura, y éstos son los valores exactos:

```text
armed:threshold          armed:agl
stopped:post-touchdown   stopped:on-ground
stopped:cap              stopped:timeout
```

Con eso sabéis si la traza está completa o si la cortó un tope. Lo mandamos como
`metrics.trace_stop_reason` (nombre propuesto, decid si os vale).

**4. Viento instantáneo a baja altura — ya lo tenéis, y conviene que lo sepáis.** `wind_dir_deg` y
`wind_speed_kt` **son del simulador en el instante del toque** (FSUIPC `0x0E92` y `0x0E90`), no del
METAR; el METAR va aparte en `landing_metar`; y `headwind_kt`/`crosswind_kt` son **derivados de ese
viento del simulador** contra el eje verdadero de la pista. Lo único que **no** es del simulador es
**`wind_gust_kt`, que sale del METAR**: FSUIPC no publica offset de racha y preferimos dejarlo nulo
antes que leer basura de un offset que no existe.
Si lo que queréis es un **perfil** de viento a lo largo de la aproximación, eso es otra cosa: la
traza no lleva viento. Serían **dos campos más por muestra** (~81 por vuelo) leyendo el viento del
simulador a 8 Hz. Se puede, y somos nosotros los que pagamos el ruido; decidlo.

**5. Peso y CG.** El **peso ya os va** (`landing_weight_lbs`, lectura medida del simulador, con
`null` si no se pudo leer). El **CG no lo tenemos**: el TLR de SimBrief imprime ese campo **vacío**
(`BEW/CG 167541/.....`) y en el simulador no está en los offsets estándar — otra vez LVar, con las
mismas condiciones que el A/T.

## 5. Y sobre vuestro §5: se ha cruzado con nuestra respuesta (6)

Vuestro mensaje es «respecto a la respuesta (5)» y el nuestro (6) salió a la vez, así que ahí están
las tres respuestas:

- **El #1 de los 10** (2026-05-04 12:03): **ninguno de los dos.** Vuestros dos de ese día están a
  **−8,5 h** y **−10,1 h** de nuestra marca. Se queda como **vuelo sin PIREP**, y el porqué de fondo
  es que vuestro piloto tiene **544 PIREPs** y nuestro log **44 registros de una instalación**: ese
  mismo día vosotros tenéis dos vuelos que aquí no existen y aquí hay uno que allí no está.
- **Los 6 números inexistentes**: nuestra respuesta (6) sostiene que **sí se filearon**. El registro
  local se escribe **solo** dentro de `if (filed)`, y `filed` significa 2xx **o** un PIREP que
  **existe** con estado ya procesado —un PIREP que no existe **no** cuenta, hay una guarda
  deliberada para eso—. Así que en esos 7 vuelos **el cliente vio un PIREP**: la consulta que toca no
  es «¿existe un PIREP con este número?» sino **«¿qué PIREP de este piloto hay en esta ventana de
  ±10 min, aunque acabara rechazado o borrado?»**, con `deleted_at` a la vista. En (6) van las siete
  horas exactas.
- **El primer vuelo con la 0.9.37**: el punto de control sigue igual, y ahora tiene una casilla más
  —que `offset_threshold_ft` y `vref_kt` lleguen y se guarden—.

---

## 6. Lo que queda

1. **Confirmad la regla del `0`** en `offset_threshold_ft` (valor) contra `null` (sin dato), y si
   `trace_stop_reason` os vale como nombre.
2. **Decidid sobre la TDZ**: cortes en pies, versión de política, o replicáis la regla.
3. **Nuestro lado**: los dos campos, con su columna, su test y sin tocar el histórico.
