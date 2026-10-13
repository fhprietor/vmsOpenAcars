# Respuesta (11) al equipo de phpVMS — la clave exacta de `trace_stop_reason`

> **De:** equipo de vmsOpenACars · **Para:** equipo de phpVMS
> **Fecha:** 12/10/2026
> **Respecto a:** vuestra pregunta —«¿con qué clave exacta envían `trace_stop_reason`?»
> **Estado:** respuesta corta y verificada **en el código**, no de memoria.

---

## 1. La clave, sin ambigüedad

**`trace_stop_reason`** — todo en minúsculas, con guion bajo y **en singular** (no
`trace_stop_reasons`, no `traceStopReason`).

**Va dentro de `metrics`**, no en la raíz y **no** en cada muestra de `trace[]`. Es hermana de
`offset_threshold_ft`, `vref_kt` y `landing_weight_lbs`:

```json
{
  "schema_version": 1,
  "source_name": "vmsOpenACars/0.9.38",
  "metrics": {
    "landing_rate_fpm": -156,
    "offset_threshold_ft": 0,
    "vref_kt": 141,
    "trace_stop_reason": "stopped:post-touchdown 2.0s"
  },
  "trace": [ { "ts_utc": "2026-10-12T14:35:43.100Z", "dist_ft": -1736.4, "on_ground": false } ]
}
```

## 2. Todo lo que la acompaña, que es lo que se necesita para mapearla

| | |
|---|---|
| **Tipo** | `string` anulable. **El campo viaja siempre**: sin valor va `null` explícito, nunca omitido |
| **Tope** | **32 caracteres** — vuestra validación — y lo recortamos **nosotros, por la cola** (`Substring(0, 32)`), así que **el prefijo sobrevive** |
| **Cultura** | `InvariantCulture`: el número es **`2.0s`**, no `2,0s` |
| **Valores de hoy (4)** | `stopped:post-touchdown 2.0s`, `stopped:on-ground`, `stopped:cap`, `stopped:timeout` |
| **Más larga que producimos** | **27** caracteres, así que entra sin recorte; los 32 solo actuarían si crece el vocabulario |
| **`null` significa** | «**la captura no llegó a cerrarse**» —no se armó, o el vuelo terminó con ella abierta—, y **no** «no se sabe»: es el caso que vuestra lectura debe tomar como «aquí no hubo cierre» |
| **Traza truncada** | Los dos motivos que la marcan son **`stopped:cap`** y **`stopped:timeout`** |
| **Desde qué versión** | **0.9.38**. En la **0.9.37 no viaja** —esa fue nuestra errata, ya reconocida en las respuestas (8) y (9)— |

Que no lo validéis contra lista cerrada es lo que hace que un motivo nuevo se guarde en vez de
costar el análisis; por eso el recorte lo hacemos nosotros y con **vuestro** número, no con un
margen nuestro: vuestra validación es **todo o nada**, y un motivo de 33+ caracteres no se recorta
en vuestro lado, **tumba el envío entero** —traza y métricas incluidas—.

## 3. Dónde mirarlo por vuestro lado

En nuestro código, si queréis cotejarlo:

- `Models/LandingUpload.cs:99` — la propiedad, dentro de `LandingUploadMetrics` (la clase que
  serializa como `metrics`).
- `Helpers/LandingUploadPayload.cs:75` — el tope de **32**.
- `Helpers/LandingUploadPayload.cs:197` — dónde se rellena.

Y el cuerpo se serializa con `JsonConvert.SerializeObject` y su configuración **por defecto**, que
es la que emite los `null`: **no** hay `NullValueHandling.Ignore` en este camino, a propósito.

---

*Si os sirve otro formato —una tabla de motivos con su significado, o la lista de topes completa del
payload—, decidlo y va en la siguiente. La lista de los ocho topes ya os la pasamos en la respuesta
(10).*
