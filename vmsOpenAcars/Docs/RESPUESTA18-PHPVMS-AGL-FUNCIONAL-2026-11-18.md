# Respuesta (18) al equipo de phpVMS — la clasificación corregida, verificada; el contrato, implementado

> **De:** equipo de vmsOpenACars · **Para:** equipo de phpVMS
> **Fecha:** 18/11/2026
> **Respecto a:** vuestra respuesta (17)
> **Estado:** la corrección de la clasificación es **correcta y la hemos verificado con los logs**;
> el contrato de la Capa 2 está **implementado en el cliente v0.9.75**, con build Debug/Release y la
> suite completa. Queda la verificación en el **primer vuelo real** con los campos.

---

## 1. La clasificación corregida: verificada

**Los cuatro go-arounds están confirmados con vuestro log**, al segundo:

| PIREP | Log |
|---|---|
| `5ojPGn0rYkNN0aZA` | `✈️ Go-around at 286 ft AGL, VS +1653 fpm` → `── CLIMB ──` |
| `lA4ZVAzgxYzlG4vD` | `✈️ Go-around at 453 ft AGL, VS +3016 fpm` → `── CLIMB ──` |
| `vN0ZOPk3wvrwYqwD` | `✈️ Go-around at 462 ft AGL, VS +2798 fpm` → `── CLIMB ──` |
| `0aNnVLMo8pxpbNPq` | `✈️ Go-around a 402 ft AGL, VS +3471 fpm` → `── CLIMB ──` |

Y los otros dos, como decíais: `9o45JZ5eAVZ11qAe` es **datum 0** por el vuelo retomado (el escalón
está desde la primera muestra, no en el aire) y `wk0896ajpxKalPrp` es **desvío sin pista resuelta**
(`runway_name = null`, `dist_ft = 0` en las 359 muestras). La tabla **134 + 10 + 4 + 1 = 149** cierra.
El fallo del detector era el que decíais: comparar muestras consecutivas sin filtrar `on_ground`, y
el **0 forzado del toque** se leía como un escalón de la elevación del campo. Es exactamente la misma
familia de error que el resto del hilo: un cero de cierre tomado por una medida.

`Og9zwbrrmjob0MXv` queda en **datum del plan con 111,4 ft**, no en «cambio de fase». Con el nominal
de 114 ft (MPTO 135 − umbral 03R 21), la puerta de 10 ft de AGL medía **~112 ft de ruedas**: es el
peor caso del hilo y no era ruido.

## 2. El contrato, implementado (v0.9.75)

Lo que ya manda el cliente, con los nombres acordados:

**Por muestra (`trace[]`)**

| Campo | Qué lleva |
|---|---|
| `radar_alt_ft` | La **indicación cruda** del radar, como hasta ahora |
| `radio_alt_ft` | **Altura de ruedas**: `max(0, radar − cero)`; `null` sin radar o sin cero |
| `alt_msl_ft` | Altitud MSL, sin restar nada |
| `agl_ft` | Se queda como **legado** (`altitud − elevación del plan`); **`null`** cuando el plan no la trae y **sin el 0 forzado en tierra** |

**Por aterrizaje (`metrics`)**: `radio_alt_zero_ft`, `threshold_elevation_ft`, `end_elevation_ft`,
`tdz_elevation_ft` (`null`, NavData no la publica), `tch_ft` + `tch_source` + `tch_unavailable`,
`glide_slope_deg`.

Las reglas que las sostienen:

- **El cero del radar se mide una vez por vuelo**: mediana de las muestras en tierra del principio.
  Los dos casos reales que lo justifican: **0,41 ft** en el ToLiss A320 de `9o45…` y **12,15 ft** en
  el Fenix A320 de `Og9…`. El mismo tipo, dos ceros: no se puede derivar del avión.
- **`agl_ft` es anulable de verdad.** `SimbriefPlan.HasDestinationElevation`/`HasOriginElevation`
  distinguen «mide 0» (KIAH) de «no lo tengo»; `ResumeFlight` y `LoadFlightFromBid` ya no ponen 0, y
  `FlightManager.TraceAglFt` devuelve `null` sin datum. La traza **no fuerza 0 en tierra**: ese 0 era
  un cierre, no una medida.
- **La referencia de llegada es pegajosa**: un `Approach → Climb` (go-around) sigue siendo el destino.
  El salto de `0aNn…` (395 → −1.076 en una muestra) ya no puede ocurrir.
- **El TCH es derivado y no se inventa.** `SKRG 01` viaja con **53,7 / `derived`**; `SKBG 35` con
  **`tch_ft = null` / `tch_unavailable = "displaced_threshold"`**; `SKBO 14L` con
  `incoherent_geometry`; el resto `no_ils`. El `end_elevation_ft` sale de la pista recíproca, como
  acordamos, y se documenta como aproximación.
- **`flights` guarda los mismos campos** (migración `EnsureColumn`, sin recrear nada) para que el
  registro local diga lo mismo que el payload y se pueda releer el análisis sin volver a NavData.

## 3. Verificación

- **Build Debug y Release** con el `MSBuild.exe` de VS2017: los dos compilan.
- **Suite completa: 1.223 tests, 1.222 pasan, 1 se omite sola, 0 fallos.** Ocho tests nuevos:
  `RadioAltPolicyTests` (ceros del Fenix/ToLiss, resta, `max(0, …)`, `null` sin radar/cero),
  tres de `LandingUploadPayloadTests` (Capa 2 en el JSON, TCH nulo con motivo, TCH 53,7 `derived`,
  `radio_alt_ft = 0` como medida, `agl_ft` nulo escrito) y uno de `ApproachThresholdTests`
  (umbral, extremo recíproco, TCH y senda desde el `NavRunway` elegido).
- **Versión**: `AppInfo.Version` y `ProductVersion` del `.exe` dan **0.9.75**.

## 4. Lo que falta: el vuelo real

El primer vuelo con la 0.9.75 es la verificación definitiva. Lo que hay que mirar:

1. **Un aterrizaje en SKBG 35**: `tch_ft = null` con `displaced_threshold`, `threshold_elevation_ft
   = 3.854` y el `radio_alt_ft` de ruedas. La puerta de 10 ft debe caer donde caía la de 57 ft de
   AGL.
2. **Un addon con cero alto** (Fenix o iFly): `radio_alt_zero_ft` ≈ 12–13 y `radio_alt_ft` en el
   toque ≈ 0, no 12.
3. **Un vuelo retomado**: `agl_ft` `null` en la traza, `alt_msl_ft` y `radio_alt_ft` poblados.
4. **Un go-around**: el AGL ya no salta al cambiar a `Climb`; la captura se cierra en el ascenso y
   el análisis no la usa para juzgar el aterrizaje.

Con eso, la Capa 2 queda cerrada de punta a punta. Del resto —la Capa 1 en las puertas y la Capa 3
contra la senda— ya tenemos el contrato y los datos; lo que falta es que los consumáis.

## 5. El método

De acuerdo con vuestro §5. El detector que contaba el 0 del toque es la misma lección que el
`agl_ft` que restaba una referencia equivocada: **un campo derivado no entra en un cálculo sin una
comprobación que lo delate**. En el cliente la comprobación es `on_ground` (el 0 de tierra no es una
altura) y el `null` explícito (sin datum no hay AGL); en el vuestro, filtrar `on_ground` y no aplicar
el estimador sin pista resuelta. Las dos están ya en el código, no solo en el documento.
