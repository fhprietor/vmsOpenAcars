# Verificación del primer vuelo 0.9.75 — `nELjXrV8mvZg2PZK`

> **De:** equipo de vmsOpenACars · **Para:** equipo de phpVMS y mantenedor
> **Fecha:** 19/11/2026
> **Vuelo:** `nELjXrV8mvZg2PZK` · SKBO→SKBG, pista **35**, B738, `vmsOpenACars/0.9.75`,
> 95 muestras de flare, toque **−49 fpm**, score 90.
> **Estado:** los campos nuevos de la muestra **funcionan**; los cinco campos de pista **no
> llegaron** por un fallo de resolución de la pista en el cliente, ya corregido en el árbol.

---

## 1. Lo que sí funciona: la altura de ruedas y la MSL

El primer vuelo 0.9.75 confirma la parte que más importaba:

| Dato | Valor |
|---|---:|
| `radio_alt_zero_ft` | **8,05 ft** (B738) |
| Primera muestra: `radar_alt_ft` / `radio_alt_ft` / `alt_msl_ft` | 408,14 / **400,08** / 4.013,1 |
| Comprobación | 408,14 − 8,05 = **400,09** → el cero se aplicó bien |
| Última muestra en el aire: `radar` / `radio` / `msl` | 9,28 / **1,22** / 3.885,1 |
| Muestra más baja en el aire: `agl_ft` / `radio_alt_ft` | **−16** / 1,22 |
| Muestras en el aire con `agl_ft < 0` | **39 de 95** |
| Muestras en tierra en la traza | **0** (`trace_stop_reason = stopped:on-ground`) |

El `radio_alt_ft` es la altura de ruedas: en la primera muestra el avión está a 400 ft sobre el
valle, no a 408 (el radar crudo) ni a 112 (el AGL del plan). En la última muestra en el aire las
ruedas están a **1,22 ft** y el AGL legado dice **−16**: la diferencia es el desfase del plan de
SKBG, no un error del radar.

La **puerta de 10 ft** de `radio` cae en `dist = −1.139,5 ft` (pasado el umbral), con `radio = 9,99`
y `agl_ft = −16`. La puerta de 10 ft de `agl` habría caído unas decenas de pies antes: la diferencia
es el datum. Con `radio` la puerta mide lo que dice.

El cero del B738 es **8,05**, no 9,5 como en la traza de `ez9OqzRnYxbnGLR8`: **el mismo título de
B738, otro vuelo**. La diferencia es la compresión del tren/carga, no el tipo; es exactamente el
motivo de que el cero viaje con el vuelo y no se derive del avión.

## 2. Lo que falló: los cinco campos de pista llegaron `null`

`metrics` de este vuelo:

| Campo | Valor |
|---|---|
| `threshold_elevation_ft` | **null** |
| `end_elevation_ft` | **null** |
| `tch_ft` / `tch_source` / `tch_unavailable` | **null / null / null** |
| `glide_slope_deg` | **null** |
| `runway_length_ft` / `offset_threshold_ft` | **null / null** |
| `touchdown_dist_ft` / `centerline_dev_ft` | **0 / null** |
| `g_force` | **null** (el log dice «— G») |

No es que SKBG 35 no tenga datos: NavData publica umbral **3.854**, extremo **3.901**, TCH
`displaced_threshold` y senda **3,0**. Es que el cliente **no resolvió la pista de la toma**.

La secuencia, con las horas del log y de las posiciones ACARS:

| Hora | Qué pasa |
|---|---|
| 02:40:51 | Posición ACARS **7,12213 / −73,18308**, rumbo **339**, 158 kt: **en la pista**, a ~2.200 ft del umbral |
| 02:40:53 | `Landing recorded: −49 fpm` — el toque, y `RegisterTouchdown` captura la posición |
| 02:41:26 | `ACCURATE TOUCHDOWN DATA` — el evento de FSUIPC, **33 s después** |
| 02:41:27 | `⚠️ NavMap: pista no encontrada para SKBG (rumbo 330°)` |
| 02:41:29 | Posición ACARS **7,13766 / −73,18909**, rumbo **335**: **pasado el extremo** de la 35 (7,1364 / −73,1887) |

El `LookupRunwayData` usaba la posición del **evento de FSUIPC** (02:41:26), que ya está fuera del
footprint de la pista; `FindTouchdownRunway` devolvía `null` y los cinco campos se quedaban sin
copiar. El `TouchdownState` sí tenía la posición buena (02:40:53); simplemente no se usaba para la
búsqueda.

El `g_force` nulo es el mismo evento tardío: la ventana de 1 s del contacto se evaluó a las 02:41:26,
no a las 02:40:53, y quedó a 0. El log pintó «— G» y el scoring omitió el criterio.

Y el `stopped:on-ground` **sí era cierto**, pero la traza no lo enseñaba: `FlareCapturePolicy`
decía que **la muestra del cierre no entra en la traza**, así que la muestra que dispara el cierre
por tierra (la primera con `on_ground = true`, ya pasada la pista) se descartaba y la última muestra
guardada era la anterior, todavía en el aire. De ahí las 95 muestras sin una sola `on_ground = true`
y la última a 1,2 ft. El consumidor no podía situar el toque y su respaldo lo inventaba 484 ft antes;
es el fallo que phpVMS encontró y corrigió en su lado.

## 3. La corrección

En el árbol, pendiente de la 0.9.76:

1. **`LookupRunwayData` usa la posición y el rumbo del primer contacto** (`TouchdownLat`,
   `TouchdownLon`, `TouchdownHeadingDeg`, capturados por `RegisterTouchdown`), no los del evento de
   FSUIPC. Con la posición de las 02:40:51/53, la 35 está en el footprint y sus campos se copian:
   umbral 3.854, extremo 3.901, `tch_unavailable = displaced_threshold`, senda 3,0, longitud 7.811,
   offset 421, distancia y eje de la toma.
2. **El G del toque** cae a `LastRawData.GForce` cuando `TouchdownGForce` viene a 0 por el evento
   tardío; el log y el registro usan el G del frame del toque, no el hueco.
3. **La muestra del toque en tierra se guarda**: `FlareCapturePolicy` la mantiene para el cierre
   `stopped:on-ground` (la única excepción a «la muestra del cierre no entra»), así que la traza
   termina con `on_ground = true`, `radio_alt_ft` ≈ 0 y el `dist_ft` del toque. La de
   `stopped:post-touchdown` sigue sin guardarse: es la frenada, no el toque.

Build Debug/Release y **1.222 tests verdes** (1 se omite sola). No está commiteado ni publicado:
la 0.9.75 sigue siendo la release; la 0.9.76 es la que lleva esto.

## 4. La lista de verificación, punto por punto

| # | Caso | Resultado |
|---|---|---|
| 1 | **SKBG 35** | **Falla antes del fix**: `tch_ft`/`tch_unavailable`/umbral `null`; `touchdown_dist = 0`. La causa es la resolución de pista, no el dato. Con el fix debe dar 3.854, 3.901, `displaced_threshold`, 3,0 y la distancia de la toma |
| 2 | **Addon con cero alto** | **Pasa**: cero 8,05; `radio` 400,08 = 408,14 − 8,05; última en el aire 1,22; `radar − radio` = el cero. Es un B738, no el Fenix; el cero se mide, no se deriva |
| 3 | **Vuelo retomado** | **No ejercitado**: no hubo «Vuelo retomado» en el log |
| 4 | **Go-around** | **No ejercitado**: el vuelo aterrizó; no hubo `Go-around`/`CLIMB` de aproximación |

Y el cierre `stopped:on-ground` con `on_ground = false` en todas las muestras: **la muestra del
cierre no se guardaba**. Con el fix la traza lleva el toque real (`on_ground = true`), así que la
prueba de phpVMS (`derivadas.toque_en_traza`) pasa a `true` y su respaldo de `agl <= 0` ya no se
ejecuta.

## 5. Para phpVMS

- La **Capa 1** de este vuelo tiene `radio` en todas las muestras; la fuente `radio` es la correcta y
  la puerta de 10 ft cae donde debe. `pista` no se puede usar en este vuelo porque faltan el umbral y
  el TCH (el fallo de resolución); con la 0.9.76 llegarán.
- El **desfase** de este vuelo, con el estimador `radar − agl` en el umbral, es **~46,9 ft** (SKBG
  35), coherente con el 47 de la (16). La muestra más cercana al umbral: `agl = 17`, `radar = 64,9`
  → 47,9; el `radio` en esa muestra es 56,8 (ruedas), y el `agl` 17 es el legado.

  **Y una precisión sobre el eje:** la traza de la 0.9.75 **sí está alineada al umbral legal**, no al
  extremo físico. `FlareDistanceFt` usa el `offset_threshold_ft` del **umbral de aproximación** (421
  en SKBG 35), que sí se resolvió; la muestra de `dist = −12` está 12 ft **pasada el umbral legal**, y
  el 47,9 = 3.901 − 3.854 lo confirma (si fuera el extremo físico, el terreno sería otro). Lo que
  faltó en el payload fue el **campo** `offset_threshold_ft` —null por el fallo de la geometría de la
  toma—, no la alineación. Con la 0.9.76 el campo llega 421 y el eje de phpVMS pasa a `umbral`; la
  traza no cambia de cero.
- La **Capa 3** no se puede calcular en este vuelo: falta `tch_ft`/`threshold` por el fallo; con la
  0.9.76 sí.
- Cuando salga la 0.9.76, el primer vuelo que la lleve es la verificación de los cuatro puntos; el
  caso SKBG 35 debería dar **3.854 / `displaced_threshold` / 3,0** y la distancia de toma distinta de
  0.
