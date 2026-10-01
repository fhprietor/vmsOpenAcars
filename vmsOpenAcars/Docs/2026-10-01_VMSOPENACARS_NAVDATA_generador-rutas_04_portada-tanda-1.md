# Tanda 1 — datos para la validación del generador de rutas de rodaje

> **De:** equipo de vmsOpenACars · **Para:** equipo de NavData · **Fecha:** 2026-10-01
> **Responde a:** `…_03_carta-cinco-condiciones-aceptadas.md` (§3.1) y al formato de su **anexo 2**.
> **Adjunto:** `2026-10-01_VMSOPENACARS_NAVDATA_generador-rutas_04_tanda-1.json` (**61 KB**).

## 1. Qué trae y en qué esquema

Un único JSON con **38 casos** en el esquema del anexo 2: `icao`, `runway`, `route_written[]`,
`trace[{lat, lon, t}]`, `baseline_polyline` y `stand`/`lat`/`lon` como forma de origen. `t` en
**segundos epoch** (entero). Cabecera `"ejemplo_ilustrativo": false`, para no confundirlo con
vuestro ejemplo.

- **37 casos** = los PIREPs del corpus (`Fixtures/taxi-corpus-2026-09-29.csv`, `sha256 b66fc6a3…`).
  El ident del PIREP es la columna **`pirep`**, no `ident`.
- **1 caso** = LMML, PIREP `V15MObj3MxOdAZab` (55CH, LMML→DAAG, 30/09/2026, cliente 0.9.18).

**No hay que partirlo**: 696 puntos y 61 KB entran en un `.json`. No hay segundo fichero.
**Traza conseguida: 38/38 vuelos, 0 fallos de API, 696 puntos.** Las posiciones salen de
`GET /api/pireps/{pirep}/acars/position`; el `/acars/logs` de la receta sirve eventos (`type = 2`),
**no posiciones** —verificado contra las dos rutas—. Rodaje de salida = filas `status = PBT` o
`TXI` hasta la primera pausa de más de 30 min (la llegada queda al otro lado del vuelo),
**contrastado con la columna `n_rodaje`: coincide en 37/37**.

## 2. Los 37 `Taxi Route` van vacíos — por eso `route_written` es `null`

**Los 37 PIREPs del corpus tienen `Taxi Route` VACÍO en el servidor** (comprobado vuelo a vuelo).
La causa es una **fecha de corte de versión: el cliente solo guarda `Taxi Route` desde 0.9.18**, y
esos 37 vuelos son anteriores (0.8.9 a 0.9.16). No es que no rodasen con ruta: el vuelo de KMIA
del 29/09 confirmó el popup y su `Taxi Route` sigue vacío con 0.9.16.

**No se ha rellenado con ninguna ruta inferida de la traza.** Va `route_written: null` y el
`"no_se_infiere"` escrito en el propio JSON. Un dato inventado envenenaría la métrica 3 y la
métrica 4 —umbral pre-registrado: **0 falsos `FUERA DE RUTA`**—, que es la que decide.

Lo único escrito de verdad que tenemos es el **`T J K L`** del mantenedor en LMML (30/09, 0.9.18),
que viaja como `["T","J","K","L"]`.

## 3. El caso LMML

`T J K L` **escrito por el piloto** contra el **`F I J K L` generado** (3.914 m, con los empalmes
`J` —23,2 m, conf 0,96— e `I|F` —188,3 m, conf 0,56—). Pista **05**. Traza: **29 puntos reales**.

Ojo a una cifra de nuestra propia carta: el «LMML de **300 posiciones**» de §3.1 **no cuadra con
el dato**. La traza de posición de ese PIREP tiene 536 filas y su rodaje de salida son **29
puntos**. Los «300» salen de un volcado local de *logs* (`type = 2`, que mezcla eventos de LMML y
de DAAG), no de la traza. Entregamos los 29 reales y lo decimos.

## 4. Campos a `null`, y por qué

| Campo | Valor | Motivo |
|---|---|---|
| `route_written` | `null` en **37/38** — *corregido: decía 36/38, ver mensaje #7* | Los 37 `Taxi Route` del corpus están vacíos (corte 0.9.18). §2. El único no nulo es el `T J K L` de LMML. |
| `runway` | `null` en 37/38 | `Departure Runway` vacío en los 37 (mismo corte) y el CSV no trae pista. En 3 el log sí la nombra, pero se deja `null` en vez de una columna con 3 de 37. |
| `stand` | `null` en 38/38 | El corpus no trae puesto, solo la posición inicial; va la **primera posición real de la traza**, que el anexo 2 admite. |
| `baseline_polyline` | `null` en 38/38 | No guardamos la polilínea que propone el cliente; solo 3 de 37 la dejaron en el log, y como texto. Pasar la propia traza sería circular. |

**Aviso de huecos (lo pide el anexo 2):** 37 de las 38 trazas tienen algún intervalo > 60 s; 10
superan los 300 s y el mayor es de **1.764 s**. El paso mediano es de **30 s**, así que no son
puntos perdidos: son **pausas reales** (freno, espera, cola). Se conservan sin interpolar.

## 5. Parámetros RAAS — confirmados

Son los nuestros, en `Helpers/RaasAdvisor.cs`. **Confirmamos vuestra lista tal cual:**

- Hold-short: **`APROXIMANDO` a 150 m**, **`ESPERA ANTES DE PISTA` a 40 m**, solo **yendo hacia**
  el punto (GS ≥ 2 kt).
- Giro: **`CALLE X ... EN N M` a 250 m**, **`GIRA AHORA` a 60 m**.
- `FUERA DE RUTA`: tras **15 s** fuera de ruta **sin acercarse** a la pista (**≥ 50 m** respecto al
  punto más cercano del episodio), y habiendo estado en ruta alguna vez.
- Enfriamiento de **20 s** por aviso, con re-armado al terminar la situación.
- Evaluación a **1 Hz sobre la telemetría cruda**, **no** sobre el envío a phpVMS (ese va cada
  30 s en rodaje: a 15 kt son ~230 m entre muestras, demasiado para avisar a 150 m).

Va también en `raas_parameters` dentro del JSON, para que el arnés lo lea del mismo fichero.
