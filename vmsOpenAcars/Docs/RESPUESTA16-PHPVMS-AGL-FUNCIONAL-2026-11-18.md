# Respuesta (16) al equipo de phpVMS — el estimador, la clasificación y `radio_alt_ft`

> **De:** equipo de vmsOpenACars · **Para:** equipo de phpVMS
> **Fecha:** 18/11/2026
> **Respecto a:** vuestra respuesta (15)
> **Estado:** el estimador del §2 es correcto y lo damos por bueno, con **dos condiciones**; la
> clasificación tiene **un caso mal etiquetado** (`wk0896ajpxKalPrp`) y **`Og9zwbrrmjob0MXv` es real**;
> la decisión de `radio_alt_ft` se acepta y queda cerrada con los campos exactos.

---

## 1. El estimador del desfase: sí, y con dos condiciones

La fórmula

```
desfase_estimado = radar_alt_ft − agl_ft      en la muestra más cercana al umbral
```

es la misma cuenta que hicimos para inferir la referencia del plan, y **funciona sin el plan**. La
damos por buena. Comprobada en nuestras trazas:

| Aeródromo · pista | Desfase real | Estimador | Diferencia |
|---|---:|---:|---:|
| SKBG 35 | 47 | **46,9** | 0,1 |
| SKMD 02 | 28 | **26,2** | 1,8 |
| SKRG 01 | 59 | **57,8** | 1,2 |
| MPTO 03R | 114 | **111,4** | 2,6 |

Las diferencias son las que esperabais: la muestra no cae exactamente en `dist = 0` y el terreno
varía unos pies. El ±5 ft es correcto.

**Condición 1: solo si hay pista resuelta.** El estimador supone que `dist_ft = 0` **es** el umbral.
Si `runway_name` es `null` o `offset_threshold_ft` es `null`, ese cero puede ser un relleno y la
cuenta no mide ningún datum. El contraejemplo es vuestro `wk0896ajpxKalPrp` (§2).

**Condición 2: en trazas anteriores a la 0.9.37, el cero es el extremo físico.** La alineación con
el umbral legal se hizo en la 0.9.37; antes, `dist_ft = 0` era el inicio del pavimento. En una pista
con umbral desplazado, el estimador mide contra el **extremo**, no contra el umbral, y se lleva la
pendiente de los primeros cientos de pies (en SKMD 02, 1.312 ft de offset: puede ser una decena de
pies). Para esas trazas: o se corrige con `offset_threshold_ft`, o se marcan como **no estimables**.

## 2. La clasificación: 147 de 149, y `wk0896` no es «datum 0»

Vuestra tabla suma **141 + 5 + 1 = 147**, no 149. Si las dos que faltan son las que no tienen
muestra en el umbral, decidlo y las clasificamos aparte; si no, hay dos trazas sin clase.

**`wk0896ajpxKalPrp` no es un datum 0.** Es el caso de **desvío** que ya conocíamos: SKBQ→SKCL, el
avión terminó en SKMR y el cliente no resolvió pista. La traza lo dice sin ambigüedad:
`runway_name = null`, `offset_threshold_ft = null` y **`dist_ft = 0` en las 359 muestras**. La
muestra que da el +2.630,7 es `agl = 300`, `radar = 2.930,7`: no está en ningún umbral, está sobre el
terreno de los Andes. El mínimo −915 tampoco es la altura sobre el campo: es el AGL contra **SKCL**
(3.162 ft) volando sobre terreno a ~609 ft, con el destino real a 41 ft.

Es una **cuarta clase**, no un datum 0:

| Clase | Qué la define | Qué hacer |
|---|---|---|
| **Sin pista resuelta / desvío** | `runway_name = null` o `offset_threshold_ft = null`, `dist_ft = 0` en toda la traza | No estimar desfase; el AGL es `null` o se calcula contra el campo real si el desvío está confirmado |

Es justo el caso que motivó `DiversionQueryGate` en la 0.9.42: el AGL se medía contra el destino
planeado (SKCL), la puerta de altitud no se abría y el desvío no se confirmaba. La 0.9.42 lo
arregla; la traza de la 0.9.40 es anterior y se queda como está.

**`Og9zwbrrmjob0MXv`: 111,4 ft es real, no ruido.** Lo hemos cruzado con NavData y con el propio
addon:

| Dato | Valor |
|---|---:|
| Aeropuerto MPTO (`Info.elevation_ft`) | **135 ft** |
| Umbral de la 03R (NavData) | **21 ft** |
| Referencia del plan (la que restaba el cliente) | **135 ft** |
| Muestra más cercana al umbral (`dist = +8,1 ft`) | `agl = −74`, `radar = 37,37` → **111,37** |
| Terreno bajo esa muestra | 23,6 ft |
| Cero del radar de ese addon (Fenix A320) | **12,15 ft** |

El desfase nominal es **114 ft** (135 − 21); el estimador da 111,4 porque la muestra cae 8 ft antes
del umbral y el terreno está a 23,6. La altura de ruedas en esa muestra es **25,2 ft**
(`37,37 − 12,15`), así que **la puerta de 10 ft de AGL está midiendo a ~112 ft de ruedas**
(`10 + 114 − 12,15`): es el peor caso del hilo, y no es ruido del radar. Para esa pista, además,
`tch_unavailable = incoherent_geometry`; no hay TCH derivado.

Los otros dos que citáis (`A6MKANJ25OKAz4gY` 132 ft y `Og9…` 101 ft) no son del mismo orden: el de
`Og9…` **no es ruido** —la muestra más cercana al umbral da 111,4 y el nominal 114; el 101 sale de
otra muestra o de no descontar el cero—. El de 132 ft, si cae en una pista con umbral desplazado y el
terreno acompaña, también puede ser real. La prueba es la misma: mirar `runway_name`,
`offset_threshold_ft` y el terreno en el umbral antes de llamarlo ruido.

## 3. `radio_alt_ft`: aceptado, y así queda el contrato

La decisión —**altura de ruedas, corregida en origen**— es la correcta, y estos datos la justifican
mejor que el argumento de flota: **el mismo tipo tiene ceros distintos según el addon**. En el A320,
el ToLiss da **0,1–0,7 ft** y el Fenix da **12,15 ft**. No es un sesgo por tipo: es el datum del
modelo. Por eso el cero tiene que viajar con el vuelo.

Campos, sin duplicar el contrato que ya existe:

| Campo | Dónde | Qué es |
|---|---|---|
| `radar_alt_ft` | muestra | La **indicación cruda** del radar. **Se queda** con su nombre actual; no hace falta un `radio_alt_raw_ft` duplicado. `null` si el addon no lo publica (el 0 de «no lo publica» sigue siendo `null`) |
| `radio_alt_ft` | muestra | **Altura de ruedas**: `max(0, radar_alt_ft − radio_alt_zero_ft)`. `null` si falta cualquiera de los dos |
| `radio_alt_zero_ft` | `metrics` | Mediana del radar en tierra **al principio del vuelo** (antes del despegue), en pies. `null` si el vuelo empieza en el aire (retomado) o el addon no lo publica |

Reglas:

- Los dos (`radio_alt_ft` y `radio_alt_zero_ft`) van **`null` juntos** cuando no hay cero; nunca la
  indicación cruda disfrazada de altura de ruedas.
- El cero se mide **al principio**, no en el destino: la compresión del tren y la superficie cambian,
  y el cero del destino no sirve para las puertas de la aproximación.
- Las puertas de 50/30/10 ft usan `radio_alt_ft` cuando venga; si no, `alt_msl_ft` + la elevación de
  pista interpolada; `agl_ft` queda como histórico y para pintar la curva con su aviso.
- `radar_alt_ft` se sigue mandando aunque haya `radio_alt_ft`: es el dato que permite auditar el cero
  y comparar con la indicación que vio el piloto.

## 4. Lo que implementamos

En la próxima versión, sin cambiar `schema_version`:

1. **`alt_msl_ft`** por muestra del flare (y en la traza de 2 s si hace falta).
2. **`radio_alt_ft` + `radio_alt_zero_ft`** con la regla del §3; el cero se captura en las muestras
   en tierra del inicio del vuelo y se guarda en `flights` (no en cada muestra).
3. **`agl_ft` anulable de verdad**: `ResumeFlight` y `LoadFlightFromBid` dejan de poner 0 y el AGL de
   la traza será `null` cuando no haya datum; `CurrentAGL` interno sigue para las fases.
4. **Referencia de llegada pegajosa**: un `Approach → Climb` (go-around) ya no devuelve la referencia
   al origen.
5. **Los cinco campos de pista**: `threshold_elevation_ft` (`elevation_ft` de `/runways/`),
   `end_elevation_ft` (la recíproca, documentada como aproximación), `tch_ft` + `tch_source` +
   `tch_unavailable`, `glide_slope_deg` (`ils_glideslope_angle`), `tdz_elevation_ft = null`.
   **SKBG 35** irá con `tch_ft = null` y `tch_unavailable = "displaced_threshold"`; no inventamos el
   TCH de carta.
6. **Tests con datos reales**: el cero del Fenix (12,15 → altura de ruedas), el del ToLiss (0,2), el
   umbral de MPTO 03R (21 vs 135), SKBG 35 (3.854 y `displaced_threshold`) y SKRG 01 (6.965 y 53,7).
   Y el caso `runway_name = null` para que el estimador no se aplique.

## 5. El método

De acuerdo con el §7. Las tres veces el error tuvo la misma forma, y la conclusión es la del contrato:
**cada altura viaja con su referencia declarada, y ninguna se usa para una puerta sin decir de qué
es.** El `radio_alt_zero_ft` es exactamente eso aplicado al radar: sin él, la «altura de 10 ft» no
significa lo mismo en un ToLiss que en un Fenix.

---

## Anexo: cómo se ha comprobado

- **`Og9zwbrrmjob0MXv`**: `GET /api/pireps/Og9.../landing` (traza de 76 muestras, 67 en el aire,
  9 en tierra; `runway_name 03R`), `GET /api/airports/MPTO` (135 ft) y NavData
  `/airport/MPTO/runways/` (03R = **21 ft**, 21L = 34, 21R = 134).
- **`wk0896ajpxKalPrp`**: `GET /api/pireps/wk0896.../landing`; `runway_name = null`,
  `offset_threshold_ft = null`, `dist_ft = 0` en las 359 muestras; `agl = 300`, `radar = 2.930,7` en
  la muestra que da el +2.630,7.
- **Cero del Fenix**: mediana de las 9 muestras en tierra de `Og9...` = **12,15 ft**; el ToLiss de
  `9o45...` = **0,1–0,7 ft**.
- **Sin geometría inventada**: las cifras son de las trazas y de la API; las que se infieren van
  marcadas.
