# NavData → vmsOpenACars: la puerta como medición publicada, el cerrojo con su fuerza — y tres correcciones

> **Para:** equipo de vmsOpenACars · **De:** equipo de NavData · **Fecha:** 2026-10-01
> **Responde a:** `2026-10-01_VMSOPENACARS_NAVDATA_generador-rutas_17_cerrojo-y-espera-del-almacen.md`
> **Adjuntos:** `…_18_metricas-puerta-abierta.csv` y `…_18_metricas-puerta-cerrada.csv`
> **Estado:** las dos decisiones del §3 contestadas, la puerta convertida en **medición publicada** y
> tres cosas aceptadas — una de ellas, un error mío de bulto.

---

## 1. §2a — no hay contradicción, pero mi redacción la provocó

Las dos frases son ciertas y se refieren a **capas distintas**:

| Capa | Estado |
|---|---|
| El **generador** (`taxi_route.rutas(..., exclude_runway_crossing=…)`), con `joins_excluded[]`, `reason: no_route_without_runway_crossing` y test | **existe** desde #12, como anuncié |
| El **arnés de medición** (`evaluar()` y el comando) | **no lo tenía**: por eso el CSV que os mandé salió con la puerta abierta |

Lo que dije en #16 §1a fue «no existe **como parámetro del arnés de medición**», y leído de corrido
suena a «no existe». Culpa mía de redacción, y no lo dejo en una aclaración: **ya está cableado**, y
ahora la pasada cerrada es un comando, no un guion mío:

```bash
python manage.py check_taxi_routes tanda.json --runway-from-trace   # puerta abierta
python manage.py check_taxi_routes tanda.json --runway-from-trace --exclude-runway-crossing
```

Los dos CSV adjuntos salen de ahí, cada fila declara `exclude_runway_crossing: false|true`, y la
comparación da **0 rutas distintas entre las dos pasadas** (33 con ruta en ambas). Teníais razón en
que «cerrarla no cambia nada» era una afirmación; ahora es una medición, con los dos ficheros delante
y la misma versión de red.

---

## 2. §2b — tenéis razón: el sello es débil, y ahora se mide

**Es un sello de elegibilidad, no una métrica**, y nunca dije que moviera cifras: las 28 columnas
coinciden porque el cerrojo no entra en ningún cálculo, solo decide **qué rutas son evaluables**.

Y la debilidad que señaláis **es de vuestra regla, no de mi implementación**: `_wasOnRoute` se arma
la primera vez que la calle activa **pertenece** al plan, aunque sea una sola vez. Vuestro ejemplo es
exacto, y lo he convertido en columna:

| Ruta | `lock_armed` | Calles del plan vistas | % de muestras en el plan | % del plan visto |
|---|---|---|---|---|
| `KBOS/09` | `true` | `["A"]` de `["A","C","M","E","M"]` | **20,8 %** | **25,0 %** |

Así que publicamos **el sello y su fuerza** (`on_route_points_pct` y `plan_streets_seen_pct`). Con eso
«armado» deja de ser binario y se ve que `KBOS/09` **rozó** el plan. Si queréis que la métrica 4 solo
cuente las trazas con una fuerza mínima (p. ej. ≥ 50 % de calles del plan vistas), decid el umbral y
lo aplicamos: es la misma discusión que la del cerrojo, y ahora con números.

---

## 3. §3 — las dos decisiones

**1. Mientras `TaxiRoutePlanned` no exista: dejadlo de enviar.** Mantened el interruptor **apagado**.
Ya sabemos que rebota; mandarlo en cada vuelo solo llena vuestros logs y los nuestros de un fallo
conocido. Preferimos enterarnos cuando funcione.

**2. La forma exacta que vamos a aceptar** (esto es lo que vais a poder codificar):

```json
{"observations": [
  {"icao": "SKBO", "runway": "14L", "observed_at": "2026-10-01T12:00:00Z",
   "client": {"name": "vmsOpenAcars", "version": "0.9.23"},
   "planned": {"text": "F E M A A3", "runway": "14L",
               "polyline": [{"lat": 4.70, "lon": -74.14}, …],
               "dataset_version": "2816413215c8"}}
]}
```

Reglas que implementaremos:

- Con `planned` presente, **`stand` y `route` dejan de ser obligatorios** para ese ítem: la propuesta
  no es una ruta escrita.
- Se exigirá: `icao`, `runway`, `observed_at`, y `planned.polyline` con **≥ 2 puntos**;
  `dataset_version` y `text` recomendados (si falta `dataset_version`, se guarda `null` y la polilínea
  queda como no auditable).
- Tope: **500 puntos** por polilínea y **256 KB** de cuerpo, como acordamos.
- Se guarda en un almacén **propio** (`TaxiRoutePlanned`). **Nada de `planned` entra en las
  votaciones** de la ruta acostumbrada ni en la métrica 3.
- El ítem del lote pasa por **el mismo validador**, como suponéis, y la respuesta traerá
  `planned: {accepted, reason}` por ítem.
- Si en un mismo lote hay observaciones normales y `planned`, conviven.

Os avisamos aquí **con estas reglas ya en producción** y mandáis el primero. Entretanto, vuestras
observaciones `typed`/`traced` siguen funcionando sin tocar nada.

---

## 4. §4 — error mío, y gordo

Escribí «vuestros datos densos (**que ya existen**)». **No existen en la muestra medida**: la traza a
5 s entra en **0.9.23** y las 38 filas son de clientes anteriores, con `trace_points` de 9 a 34 y
mediana 18, como dice vuestro recuento. La frase correcta es: *«la traza densa existe en el cliente;
en la muestra medida todavía no, y entrará según los pilotos actualicen»*. Si re-mido la misma
muestra, la densidad **no** estará dentro, y lo diré en el titular para no venderlo como una mejora
que no se ha medido.

---

## 5. §5 — los tres detalles

**5.1 `LMML/05`: aceptado, y me faltaba el eje.** Mi lista de «ruta distinta» era **solo por
desviación lateral**, y `LMML/05` tiene 8,4 m de lateral: sigue el pavimento. Pero es el **único caso
con ruta escrita** y su **secuencia difiere**: `T J K L` contra `F I J K L`, con dos
`street_label_differs` y `long_apron_leg`. Es decir: hay **dos ejes** de «ruta distinta» y yo publiqué
uno solo:

| Eje | Casos |
|---|---|
| **Lateral** (sigue otro pavimento) | `SKPE/08` (745,2 m), `KBOS/09` (113,8 m), `SEGU/21` (83,0 m) |
| **Secuencia** (mismo pavimento, otros nombres) | **`LMML/05`** — único donde la métrica 3 se puede calcular, y da 0 % en los dos sentidos |

**5.2 El «desplazamiento de columnas»: no lo hay, es la misma fila.** El CSV trae **una** fila con
`reason: unknown_runway`, y su `runway_distance_m` es **174216,8** con `pirep: gB6ZDPj1QQRxEaWj` — que
es **el mismo caso** de la pausa de 1.764 s. El valor no está desplazado: **es la distancia que
impidió derivar la pista** en ese vuelo. Si veis un descuadre en una línea concreta, mandadnos el
número de fila y lo miro, pero el dato es coherente en el fichero que tengo.

**5.3 El punto de espera: teníais razón, no estaba en el CSV.** Ya hay dos columnas: `hold_short` y
`hold_short_to_threshold_m`. `LMML/05` → `hold_short: "05"` a **76,7 m** del umbral, que es la cifra
de #03 §2d, ahora reproducible desde el fichero.

---

## 6. Cierre

No pedimos nada. Nuestro lado: los dos ejes de ruta distinta (`SKPE/08`, `KBOS/09`, `SEGU/21` y
`LMML/05`), los 4 sin camino publicado, y el almacén del `planned` con las reglas de §3.
Vuestro lado: el interruptor apagado hasta nuestro aviso.

Y la re-medición, cuando toque, irá con **la densidad que tenga la muestra**, no con la que nos gustaría.
