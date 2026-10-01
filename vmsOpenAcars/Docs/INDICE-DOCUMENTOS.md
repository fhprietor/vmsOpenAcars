> **Convención de nombres (2026-10-01, sustituye a VMSOPENACARS/NAVDATA):** el nombre dice **quién escribe y a quién**, no de quién es el fichero:
> `AAAA-MM-DD_<DE>_<PARA>_<hilo>_<n>_<asunto>.md` — p. ej. `2026-10-01_NAVDATA_VMSOPENACARS_generador-rutas_03_carta-...md` es **de NavData, para nosotros**, del 01/10, hilo *generador-rutas*, mensaje **nº 3**. La **fecha delante** hace que el orden alfabético sea el cronológico.
> Los ficheros **históricos no se renombran** (los referencia `CLAUDE.md`): la regla aplica de aquí en adelante. Los de hoy sí se han renombrado.
> **Generador de rutas, estado:** nuestros #1 (`..._01_cinco-condiciones`) → su #2 (`respuesta-a-vmsOpenACars-generador-rutas-2026-10-01.md`, llegó ~17 min antes y **no está en Docs: queda SUPERADO**) → su #3, en tres ficheros (carta + anexo1 + anexo2) → nuestro #4 (`..._04_tanda-1.json` + `..._04_portada-tanda-1.md`) → nuestro #5 (`..._05_errata-propuesta.md`) → **su #6** (`..._06_medicion-tanda-1.md` + `..._06_metricas-tanda-1.csv`, la tanda 1 medida con dos lecturas de la métrica 4: 0 o 22) → **nuestro #7** (`..._07_decisiones-medicion.md`, cerramos la métrica 4 **con nuestra regla**, pedimos los dos empalmes de LMML y la tabla de coste) → **su #8** (`..._08_metrica-4-cerrada.md` + `..._08_metricas-su-regla.csv`: con nuestra definición la métrica 4 daba **9 eventos en 33 rutas** y **no pasaba**) → **nuestro #9** (`..._09_regla-metrica-4-y-baseline.md`, la regla de verdad —el progreso es la **distancia recta al umbral**—, sí a la traza a 1 Hz y baseline (ii)) → **su #10** (`..._10_metrica-4-no-aplica-y-erratas.md`: la métrica 4 **no aplica** con el corpus actual, dos erratas de forma y la forma de la polilínea aprobada) → **nuestro #11** (`..._11_forma-baseline-y-cruce-de-pista.md`: aceptamos que la métrica 4 no aplica, confirmamos la forma y el transporte, y pedimos por escrito el cruce de pista en `crossings`) → **su #12, que es lo último** (`..._12_cerrojo-y-cruce-de-pista.md`: **33 de 33** trazas arman el cerrojo `_wasOnRoute` con su proxy, añade las **dos reglas** que faltaban en su receta, fija el **umbral** de la métrica sustituta en **0 eventos**, confirma `crosses_runway` en los `joins_used` e **implementa** `?exclude_runway_crossing` —con **0 de las 33** rutas usando un empalme que cruce pista—, asume el «22» como suyo y rebate §5.2 con el CSV de #8).
# ÍNDICE DE DOCUMENTOS — `Docs/`

> **Para qué sirve:** saber de un vistazo **de qué hilo es cada documento** y, sobre todo,
> **cuál es el último de cada hilo**. Creado el **2026-10-01**.
> **Autor:** [VMSOPENACARS] = equipo de vmsOpenACars · [PHPVMS] = equipo de NavData / equipo de phpVMS.
> **Alcance:** solo lista; no sustituye a `README.md` (índice por audiencia) ni a `CLAUDE.md`.

---

## Regla de nombres (de aquí en adelante)

```
AAAA-MM-DD_<DE>_<PARA>_<hilo>_<n>_<asunto>.md
```

- **La fecha va delante** → el orden alfabético del gestor de archivos es el orden cronológico.
- `<hilo>`: uno de `tiles`, `conectividad`, `rutas-taxi`, `empalmes`, `generador-rutas`,
  `phpvms`, `navdata-api`, `otro`.
- `<n>`: número de mensaje dentro del hilo, dos dígitos (`01`, `02`, …).
- `<asunto>`: corto, en minúsculas y con guiones.
- Ejemplo: `2026-10-01_SUYA_generador-rutas_02_cinco-condiciones-aceptadas.md`

**Los ficheros históricos NO se renombran.** Los nombres actuales son confusos (`respuesta2-`,
`RESPUESTA4-`, `vmsOpenACars-a-…`) pero están citados desde la guía y desde otros documentos;
renombrarlos rompería enlaces. La regla aplica **solo a lo que se cree desde 2026-10-01**.

---

## Hilo 1 — Teselas y proxy de CARTO

| # | Fichero | Fecha | Autor | Qué es |
|---|---|---|---|---|
| 1 | `PEDIDO-NAVDATA-TILES.md` | 2026-09-29 | [VMSOPENACARS] | Pedido del proxy de teselas con caché; incluye la pregunta para CARTO. |
| 2 | `RESPUESTA7-NAVDATA-TILES-2026-09-29.md` | 2026-09-29 | [NAVDATA] | El proxy está en marcha y probado de punta a punta. |
| 3 | `RESPUESTA-NAVDATA-TILES-2026-09-29.md` | 2026-09-29 | [VMSOPENACARS] | Verificado en vivo; dos cosas que devolver y una discrepancia texto/servidor. |
| 4 | `RESPUESTA8-NAVDATA-TILES-2026-09-29.md` | 2026-09-29 | [NAVDATA] | Los tres hallazgos corregidos (el `401` era Cloudflare). |
| 5 | `RESPUESTA2-NAVDATA-TILES-2026-09-29.md` | 2026-09-29 | [VMSOPENACARS] | Arreglos verificados + petición de `criterion_version` y consumo. |
| 6 | `RESPUESTA9-NAVDATA-TILES-2026-09-29.md` | 2026-09-30 | [NAVDATA] | `criterion_version`, consumo del mes y purga del borde. |
| 7 | `RESPUESTA3-NAVDATA-TILES-2026-09-30.md` | 2026-09-30 | [VMSOPENACARS] | Cerrado por VMSOPENACARS parte; bloqueante de una línea (clave en la URL). |
| 8 | `RESPUESTA10-NAVDATA-TILES-2026-09-30.md` | 2026-09-30 | [NAVDATA] | La clave en la URL, resuelta (solo en la ruta de teselas). |
| 9 | `RESPUESTA4-NAVDATA-TILES-2026-09-30.md` | 2026-09-30 | [VMSOPENACARS] | Todo el tráfico de teselas al proxy y la caída en tres escalones. |
| 10 | `RESPUESTA11-NAVDATA-TILES-2026-09-30.md` | 2026-09-30 | [NAVDATA] | Teníamos razón: la clave estaba en claro en su log; corregido. |
| 11 | `RESPUESTA5-NAVDATA-TILES-2026-09-30.md` | 2026-09-30 | [VMSOPENACARS] | Cierre: la rotación de la clave la hacemos nosotros. |
| 12 | `RESPUESTA6-NAVDATA-TILES-2026-09-30.md` | 2026-09-30 | [VMSOPENACARS] | Cerrado: 28 días, sí al `expires_at` y sí al porcentaje. |

> **ÚLTIMO DE ESTE HILO: `RESPUESTA6-NAVDATA-TILES-2026-09-30.md`** [VMSOPENACARS].
> Responde a `RESPUESTA12-NAVDATA-TILES-2026-09-30.md` [NAVDATA], **que no está en `Docs/`**.

---

## Hilo 2 — Rutas de rodaje acostumbradas (base de conocimiento compartida)

| # | Fichero | Fecha | Autor | Qué es |
|---|---|---|---|---|
| 1 | `PEDIDO-NAVDATA-RUTAS-TAXI.md` | 2026-09-29 | [VMSOPENACARS] | Pedido de la base compartida: contrato, umbrales y moderación experta. |
| 2 | `RESPUESTA-NAVDATA-RUTAS-TAXI-2026-09-29.md` | 2026-09-29 | [VMSOPENACARS] | Respuesta y verificación en vivo de sus tres puntos. |
| 3 | `RESPUESTA2-NAVDATA-RUTAS-TAXI-2026-09-29.md` | 2026-09-29 | [VMSOPENACARS] | Segunda respuesta (banco de pruebas corrido); lleva corrección posterior en cabecera. |
| 4 | `RESPUESTA3-NAVDATA-RUTAS-TAXI-2026-09-29.md` | 2026-09-29 | [VMSOPENACARS] | Tercera respuesta; corrige la lectura de las 3 observaciones (eran filas de prueba). |

> **ÚLTIMO DE ESTE HILO: `RESPUESTA3-NAVDATA-RUTAS-TAXI-2026-09-29.md`** [VMSOPENACARS].
> Sus `respuesta-a-…`, `respuesta2-a-…` y `respuesta3-a-…` de este hilo **no están en `Docs/`**.

---

## Hilo 3 — Empalmes y `node_id`

| # | Fichero | Fecha | Autor | Qué es |
|---|---|---|---|---|
| 1 | `PEDIDO-NAVDATA-EMPALMES-2026-09-29.md` | 2026-09-29 | [VMSOPENACARS] | Empalmes y criterios para encender `node_id` en cualquier aeropuerto. |
| 2 | `RESPUESTA5-NAVDATA-EMPALMES-2026-09-29.md` | 2026-09-29 | [NAVDATA] | Empalmes calculados, confianza y estadísticas; cruces pendientes. |

> **ÚLTIMO DE ESTE HILO: `RESPUESTA5-NAVDATA-EMPALMES-2026-09-29.md`** [NAVDATA].

---

## Hilo 4 — Conectividad de la red publicada (LMML / `unions_missing`)

| # | Fichero | Fecha | Autor | Qué es |
|---|---|---|---|---|
| 1 | `PEDIDO-NAVDATA-CONECTIVIDAD-2026-09-30.md` | 2026-09-30 | [VMSOPENACARS] | El criterio común para los huecos de empalme; origen, el rodaje real de LMML. |
| 2 | `respuesta-a-vmsOpenACars-conectividad-2026-09-30.md` | 2026-09-30 | [NAVDATA] | La cobertura medida y un bug suyo que LMML destapó; piden el corpus. |
| 3 | `vmsOpenACars-a-NAVDATA-conectividad-2026-09-30.md` | 2026-09-30 | [VMSOPENACARS] | Los tres descuadres, el hallazgo que confirma el criterio y el corpus. |
| 4 | `respuesta2-a-vmsOpenACars-conectividad-2026-09-30.md` | 2026-09-30 | [NAVDATA] | Los tres descuadres eran suyos y están corregidos. |
| 5 | `respuesta3-a-vmsOpenACars-conectividad-2026-09-30.md` | 2026-09-30 | [NAVDATA] | Prueba de aceptación construida; `LMML/05 "T J K L"` pasa. |
| 6 | `vmsOpenACars-a-NAVDATA-conectividad-2-2026-09-30.md` | 2026-09-30 | [VMSOPENACARS] | Cierre del hilo: lo verificado, lo arreglado y el corpus en camino. |
| 7 | `respuesta4-a-vmsOpenACars-conectividad-2026-09-30.md` | 2026-09-30 | [NAVDATA] | Las uniones que faltan, con nombre y distancia (LEMD era un hueco de 1,3 m). |
| 8 | `vmsOpenACars-a-NAVDATA-conectividad-3-2026-09-30.md` | 2026-09-30 | [VMSOPENACARS] | Decisión sobre `A1`↔`B` (opción 1) con dos condiciones. |
| 9 | `respuesta5-a-vmsOpenACars-conectividad-2026-09-30.md` | 2026-09-30 | [NAVDATA] | Los siete aeropuertos conectados: 7 de 7 con `components_after_joins: 1`. |
| 10 | `vmsOpenACars-a-NAVDATA-conectividad-4-2026-09-30.md` | 2026-09-30 | [VMSOPENACARS] | Entrega el corpus (una ruta) y explica por qué el `Taxi Route` de los 37 está vacío. |

> **ÚLTIMO DE ESTE HILO: `vmsOpenACars-a-NAVDATA-conectividad-4-2026-09-30.md`** [VMSOPENACARS].
> No hay respuesta NAVDATA posterior a este documento; el estado global lo recoge
> `CIERRE-NAVDATA-2026-09-30.md` (ver «Cierre general»). Su `RESPUESTA-NAVDATA-CONECTIVIDAD-2026-09-30.md`
> (citado por nuestro #3) **no está en `Docs/`**.

---

## Hilo 5 — Generador de rutas de rodaje (endpoint nuevo)

| # | Fichero | Fecha | Autor | Qué es |
|---|---|---|---|---|
| 1 | `2026-10-01_VMSOPENACARS_NAVDATA_generador-rutas_01_cinco-condiciones.md` | 2026-10-01 | [VMSOPENACARS] | Respuesta a su propuesta: aprobamos el contrato con **cinco condiciones**. |
| 2 | `respuesta-a-vmsOpenACars-generador-rutas-2026-10-01.md` | 2026-10-01 | [NAVDATA] | **Recibido hoy:** acepta las cinco condiciones, aplica la errata y pre-registra el criterio de la fase 3. |
| 3 | `2026-10-01_NAVDATA_VMSOPENACARS_generador-rutas_03_carta-cinco-condiciones-aceptadas.md` | 2026-10-01 | [NAVDATA] | Carta de la respuesta: acepta las cinco condiciones, reproduce LMML y pre-registra las cinco métricas de la fase 3. |
| 4 | `2026-10-01_NAVDATA_VMSOPENACARS_generador-rutas_03_anexo1-errata-propuesta.md` | 2026-10-01 | [NAVDATA] | **Anexo 1:** corrige la propuesta (el giro de 45° inventado, `joins_used: []`, la cita de §4.3) sin tocar el original, con su `sha256` verificado. |
| 5 | `2026-10-01_NAVDATA_VMSOPENACARS_generador-rutas_03_anexo2-formato-tanda-1.md` | 2026-10-01 | [NAVDATA] | **Anexo 2:** formato JSON de la tanda 1 que hay que entregar para arrancar la fase 2. |
| 6 | `2026-10-01_VMSOPENACARS_NAVDATA_generador-rutas_04_tanda-1.json` | 2026-10-01 | [VMSOPENACARS] | **Nuestro #4:** la tanda 1 entregada (38 rodajes) en el JSON del anexo 2, para arrancar la fase 2. |
| 7 | `2026-10-01_VMSOPENACARS_NAVDATA_generador-rutas_04_portada-tanda-1.md` | 2026-10-01 | [VMSOPENACARS] | **Nuestro #4, portada:** qué contiene la tanda y las decisiones de entrega (nulos declarados, no inferidos). |
| 8 | `2026-10-01_VMSOPENACARS_NAVDATA_generador-rutas_05_errata-propuesta.md` | 2026-10-01 | [VMSOPENACARS] | **Nuestro #5:** errata de su propuesta (el «más barata» sin coste publicado, la frase «ninguno por encima de 10°» y la aritmética de LMML). |
| 9 | `2026-10-01_NAVDATA_VMSOPENACARS_generador-rutas_06_medicion-tanda-1.md` | 2026-10-01 | [NAVDATA] | **Su #6:** la tanda 1 medida — 3 de 6 umbrales, cobertura 65 %, lateral 6,7 m y la métrica 4 con **dos lecturas (0 o 22)** según la definición. |
| 10 | `2026-10-01_NAVDATA_VMSOPENACARS_generador-rutas_06_metricas-tanda-1.csv` | 2026-10-01 | [NAVDATA] | **Su #6, adjunto:** el detalle por ruta de la medición, 38 filas. |
| 11 | `2026-10-01_VMSOPENACARS_NAVDATA_generador-rutas_07_decisiones-medicion.md` | 2026-10-01 | [VMSOPENACARS] | **Nuestro #7:** aceptamos la tanda 1 y cerramos la métrica 4 con **nuestra** definición (15 s fuera de ruta sin progresar → 0 eventos), aceptamos la enmienda de cobertura, excluimos la métrica 3 y pedimos los dos empalmes de LMML y la tabla de coste. Preferimos la salida (ii) para la baseline. |
| 12 | `2026-10-01_NAVDATA_VMSOPENACARS_generador-rutas_08_metrica-4-cerrada.md` | 2026-10-01 | [NAVDATA] | **Su #8:** con **nuestra** definición la métrica 4 da **9 eventos en 33 rutas** (8 rutas con al menos un aviso) y **no pasa** el umbral 0; cierra las cuatro decisiones y explica las dos discrepancias de forma. |
| 13 | `2026-10-01_NAVDATA_VMSOPENACARS_generador-rutas_08_metricas-su-regla.csv` | 2026-10-01 | [NAVDATA] | **Su #8, adjunto:** el CSV de #6 con **una sola columna re-medida** (`false_offroute_events`: 0 → 9); las otras 23 columnas son idénticas. |
| 14 | `2026-10-01_VMSOPENACARS_NAVDATA_generador-rutas_09_regla-metrica-4-y-baseline.md` | 2026-10-01 | [VMSOPENACARS] | **Nuestro #9:** la **regla de verdad** de la métrica 4 en el código (el disparador es comparar **nombres**, y el progreso es la **distancia recta al umbral**), la traza a 1 Hz y la **baseline (ii)** confirmada. |
| 15 | `2026-10-01_NAVDATA_VMSOPENACARS_generador-rutas_10_metrica-4-no-aplica-y-erratas.md` | 2026-10-01 | [NAVDATA] | **Su #10:** la **métrica 4 no aplica** con el corpus actual (37/38 sin ruta escrita: sin plan el motor no emite `FUERA DE RUTA`; en LMML, **0**), las **dos erratas de forma** (mediana **67,95 m**, `SKRG/01` vs `SKRG/19`), la **forma de la polilínea** aprobada —transporte por su endpoint de observaciones con un campo `planned`, 500 puntos y cuerpo a 256 KB— y los **siete valores por defecto cerrados**. |
| 16 | `2026-10-01_VMSOPENACARS_NAVDATA_generador-rutas_11_forma-baseline-y-cruce-de-pista.md` | 2026-10-01 | [VMSOPENACARS] | **Nuestro #11:** aceptamos que la métrica 4 **no aplica** (el 0 de 37/38 es **estructural**, no medido), confirmamos la **forma y el transporte** de la polilínea, decimos sí a la **traza a 1 Hz**, pedimos **tres reglas metodológicas**, señalamos **dos incoherencias de forma** de #10 y pedimos por escrito el tratamiento del **cruce de pista** (`crossings`). |
| 17 | `2026-10-01_NAVDATA_VMSOPENACARS_generador-rutas_12_cerrojo-y-cruce-de-pista.md` | 2026-10-01 | [NAVDATA] | **Su #12:** responde a nuestro #11 con **el número del cerrojo** — **33 de 33** trazas arman `_wasOnRoute` (medido con un proxy: calle por segmento a ≤45 m con ×2,5 a >50°, rumbo entre muestras a 30 s, `OnRoute` por pertenencia)—; **añade las dos reglas** que faltaban en su receta; **fija el umbral** de la métrica sustituta (**0 eventos** entre las que arman el cerrojo; las demás quedan `lock_armed: false` y `no_evaluable`); confirma que **`crosses_runway` va en los `joins_used`** y en los pasos (`kind: hold_short`/`join`), e **implementa** `?exclude_runway_crossing` con `reason: no_route_without_runway_crossing`, `joins_excluded[]` y test `ModoSinCruceDePistaTests`, sosteniendo que **0 de las 33** rutas usan un empalme que cruce pista; asume **§5.1** (el «22» nunca fue «la regla real») y **rebate §5.2** con el CSV de #8 delante (**10 filas `SKBO/14R`**, incluida la de **42,86 % / 71,7 m / 0 eventos / 28 puntos**); y señala que el `j.Confidence` que no se copia en nuestro `TaxiSegments` es el agujero de verdad, **de nuestro lado**. |

> **ÚLTIMO DE ESTE HILO: `2026-10-01_NAVDATA_VMSOPENACARS_generador-rutas_12_cerrojo-y-cruce-de-pista.md`** [NAVDATA] (fila 17).
> Responde a **nuestro #11** (`2026-10-01_VMSOPENACARS_NAVDATA_generador-rutas_11_forma-baseline-y-cruce-de-pista.md`, fila 16): da el **número del cerrojo `_wasOnRoute`** (**33 de 33** trazas lo arman, con las dos limitaciones dichas —proxy por segmento en vez de nuestro `NearestTaxiway` nodo a nodo, y rumbo entre muestras a 30 s—), **añade las dos reglas** que faltaban en su receta (penalización **×2,5** a más de **50°** y `OnRoute` como **pertenencia**, no orden), **fija el umbral** de la métrica sustituta antes de re-medir (**0 eventos** entre las que arman el cerrojo; las demás, `lock_armed: false` y `no_evaluable`, nunca «pasa»), confirma **(i)** que `crosses_runway` ya va en `joins_used` y en los pasos e **(ii)** que existe el modo `?exclude_runway_crossing=1` (con `reason: no_route_without_runway_crossing` y `joins_excluded[]` cuando deja sin ruta), afirma que **0 de las 33** rutas de la tanda usan un empalme que cruce pista, asume **§5.1** (el «22» salía del detector por polilínea; «la regla real no se ha implementado nunca») y **rebate §5.2** con el CSV delante. Lo que pide de nuestro lado, **sin fecha**: la **traza a 1 Hz** con la guía activa y **producir y persistir el `planned`**.
> **Nuestro #11** (fila 16, anterior) acepta que la métrica 4 **no aplica** en esta tanda —el 0 de 37/38 es
> **estructural**, no medido—, confirma la **forma y el transporte** de la polilínea (con el tope a
> **500 puntos** y cuerpo a **256 KB**), dice sí a la **traza a 1 Hz**, pide **tres reglas metodológicas**,
> señala **dos incoherencias de forma** de #10 y pide por escrito el tratamiento del **cruce de pista**
> (`crossings`).
> **Su #10** respondía a **nuestro #9** (`2026-10-01_VMSOPENACARS_NAVDATA_generador-rutas_09_regla-metrica-4-y-baseline.md`,
> fila 14): declara la métrica 4 **no aplicable** en esa tanda —su número queda como cota/inferencia y no
> como dato—, acepta las **dos erratas de forma**, aprueba la **forma de la polilínea** con su transporte
> y su tope, y cierra los **siete valores por defecto**. Queda como
> **anterior**: su #8 (`2026-10-01_NAVDATA_VMSOPENACARS_generador-rutas_08_metrica-4-cerrada.md` +
> `…_08_metricas-su-regla.csv`, filas 12–13), que respondía a **nuestro #7** (fila 11) y cerraba las
> **cuatro decisiones** de #6: métrica 4 con nuestra regla (**9**, cota inferior: la traza va
> a 30 s por punto), enmienda de cobertura aceptada (65,0 % y 73,3 %), métrica 3 excluida **sin** mínimo
> de muestra, y baseline por la **salida (ii)** —persistir la polilínea y el texto que propone el
> cliente—, cuya versión de entrada **nos piden**. Da ya los **dos empalmes** de LMML (`J` 23,2 m e
> `I|F` 188,3 m → **211,5 m**, el 5,4 % de 3.914 m) y la tabla de coste con `k = 0,5`.
> **La medición anterior (#9–#10)**, su #6, respondía a nuestros **#4** (`…_04_tanda-1.json` +
> `…_04_portada-tanda-1.md`) y **#5** (`…_05_errata-propuesta.md`): rebatía la cobertura, confirmaba
> lateral y empalmes, y dejaba la métrica 4 y la de ruta escrita pendientes de una definición nuestra
> —que es justo lo que cerró **#8**—.
> Los tres ficheros de arriba (#3–#5) llegaron el **2026-10-01** como **un solo mensaje** —su carta es
> #3 y #4–#5 son sus anexos 1 y 2, en ese orden de lectura—, así que **su mensaje #3 es la carta**, la del
> `_03_` `cinco-condiciones-aceptadas`: era la que llevaba el criterio de la fase 3 pre-registrado y la
> que había que contestar. Los tres comparten el `_03_` del nombre porque quien los bautizó los numeró
> como un único mensaje.
>
> **Tres avisos sobre #3–#5, que se copiaron con el nombre que traían (no se renombran aquí):**
> 1. **Su contenido los firma el equipo de NavData** («Para: equipo de vmsOpenACars · De: equipo de
>    NavData», cierre «equipo de NavData»), no nosotros: por contenido son [NAVDATA], aunque el nombre
>    diga `VMSOPENACARS`. Se listan con la etiqueta del nombre, y el mantenedor decide si la corrige.
> 2. **El nombre no sigue la convención de arriba**: por contenido serían
>    `2026-10-01_NAVDATA_VMSOPENACARS_generador-rutas_03_<asunto>.md` (el hilo tiene hoy los mensajes #1 y #2).
>    Los tres comparten el `_03_`, así que tampoco se numeran entre sí.
> 3. **No son los mismos ficheros que #2.** #2, `respuesta-a-vmsOpenACars-generador-rutas-2026-10-01.md`,
>    llegó **antes** (misma fecha, misma respuesta de NavData) y **no está en `Docs/`**: se lista aquí
>    porque pertenece al hilo. Los #3–#5 son la versión que NavData reenvió después, ya con la errata
>    **como anexo aparte** en vez de marcada dentro del original.
>
> Su propuesta inicial, `propuesta-a-vmsOpenACars-generador-rutas-taxi-2026-10-01.md`, **no está en
> `Docs/`** (su `sha256` es `e005abce691468b4ae658298234b3fe191e667c662807f97613932f89c2a5bb5`,
> verificado contra el adjunto el 2026-10-01).

---

## Hilo 6 — phpVMS: pruebas con PIREPs reales

| # | Fichero | Fecha | Autor | Qué es |
|---|---|---|---|---|
| 1 | `PEDIDO-PHPVMS-PRUEBAS.md` | 2026-09-29 | [VMSOPENACARS] | Pedido de datos para validar con PIREPs reales (solo de vmsOpenACars). |
| 2 | `RESPUESTA-PHPVMS-PRUEBAS-2026-09-29.md` | 2026-09-29 | [VMSOPENACARS] | Respuesta: dos premisas nuestras eran falsas (la fase va en `status`). |
| 3 | `ANADIDO-PHPVMS-CIERRE-2026-09-29.md` | 2026-09-29 | [VMSOPENACARS] | Añadido al cierre, con prueba en vivo (`type = 2`, `fuel`). |

> **ÚLTIMO DE ESTE HILO: `ANADIDO-PHPVMS-CIERRE-2026-09-29.md`** [VMSOPENACARS].
> Sus `RESPUESTA-PHPVMS-PRUEBAS.md` y `CIERRE-PHPVMS-PRUEBAS.md` **no están en `Docs/`**.

---

## Hilo 7 — Aviso de cambios de la API de NavData (29/09)

| # | Fichero | Fecha | Autor | Qué es |
|---|---|---|---|---|
| 1 | `RESPUESTA-NAVDATA-2026-09-29.md` | 2026-09-29 | [VMSOPENACARS] | Respuesta al aviso: los seis puntos, verificados en vivo. |
| 2 | `CONFIRMACION-NAVDATA-2026-09-29.md` | 2026-09-29 | [VMSOPENACARS] | Confirmación y verificación del arreglo, punto por punto. |

> **ÚLTIMO DE ESTE HILO: `CONFIRMACION-NAVDATA-2026-09-29.md`** [VMSOPENACARS].
> Su `aviso-vmsOpenACars-…` original **no está en `Docs/`**.

---

## Cierre general

| # | Fichero | Fecha | Autor | Qué es |
|---|---|---|---|---|
| 1 | `CIERRE-NAVDATA-2026-09-30.md` | 2026-09-30 | [NAVDATA] | Cierra **todos** los hilos abiertos (mag_var, espacios aéreos, rutas, puntos de espera, `node_id`, empalmes, teselas, `expires_at`); único pendiente suyo: `runway-crossings`. |

> **ÚLTIMO DOCUMENTO GLOBAL DEL INTERCAMBIO: `CIERRE-NAVDATA-2026-09-30.md`** [NAVDATA].
> Es anterior al hilo del generador de rutas (hilo 5), que es lo más reciente.

---

## Otros

Documentos del repositorio que no pertenecen a ninguno de los hilos anteriores. Todos son
**internos** ([VMSOPENACARS]) salvo donde se indique.

| Fichero | Fecha | Autor | Qué es |
|---|---|---|---|
| `README.md` | 2026-09-29 | [VMSOPENACARS] | Índice de la documentación por audiencia (pilotos / desarrolladores / equipos externos). |
| `BRIEFING.md` | 2026-09-29 | [VMSOPENACARS] | Guía del usuario/piloto (su cabecera dice «Versión 0.9.3»). |
| `PRIMEROS_PASOS.md` | 2026-09-19 | [VMSOPENACARS] | Alta en phpVMS (VHOLAR), API key, descarga e instalación. |
| `architecture.md` | 2026-09-29 | [VMSOPENACARS] | Arquitectura, módulos, esquemas de BD y notas de build (documento largo). |
| `SETUP-ENTORNO.md` | 2026-09-24 | [VMSOPENACARS] | Montar el proyecto en otro equipo: qué no viaja por git, toolchain y `dsh`. |
| `CHANGELOG.md` | 2026-09-29 | [VMSOPENACARS] | Changelog **canónico** (última entrada: `[0.9.20] — 30/09/2026`). |
| `CHANGELOG-0.9.2-0.9.9.md` | 2026-09-24 | [VMSOPENACARS] | Resumen breve del rango v0.9.2 → v0.9.9. |
| `api_vms.md` | 2026-09-29 | [VMSOPENACARS] (copia) | Copia del contrato de la API REST de phpVMS v7 (endpoints, tipos de `acars`). |
| `MEMORY.md` | 2026-09-28 | [VMSOPENACARS] | Índice de mantenimiento; **no es fuente de verdad** (lo dice el propio fichero). |
| `project_vmsOpenAcars.md` | 2026-09-20 | [VMSOPENACARS] | Estado del proyecto (desfasado: dice v0.9.2); **no es fuente de verdad**. |
| `feature_map_sidebar.md` | 2026-09-15 | [VMSOPENACARS] | Nota de memoria: sidebar de procedimientos en `MapForm` (v0.6.5–v0.8.7). |
| `INDICE-DOCUMENTOS.md` | 2026-10-01 | [VMSOPENACARS] | **Este índice.** |

Las fechas de esta sección son las del fichero en disco; donde el documento declara otra fecha en
su cabecera, manda la cabecera en el resto de secciones de este índice.
