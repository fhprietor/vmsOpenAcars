> **Convención de nombres (2026-10-01, sustituye a VMSOPENACARS/NAVDATA):** el nombre dice **quién escribe y a quién**, no de quién es el fichero:
> `AAAA-MM-DD_<DE>_<PARA>_<hilo>_<n>_<asunto>.md` — p. ej. `2026-10-01_NAVDATA_VMSOPENACARS_generador-rutas_03_carta-...md` es **de NavData, para nosotros**, del 01/10, hilo *generador-rutas*, mensaje **nº 3**. La **fecha delante** hace que el orden alfabético sea el cronológico.
> Los ficheros **históricos no se renombran** (los referencia `CLAUDE.md`): la regla aplica de aquí en adelante. Los de hoy sí se han renombrado.
> **Generador de rutas, estado:** nuestros #1 (`..._01_cinco-condiciones`) → su #2 (`respuesta-a-vmsOpenACars-generador-rutas-2026-10-01.md`, llegó ~17 min antes y **no está en Docs: queda SUPERADO**) → su #3, en tres ficheros (carta + anexo1 + anexo2) → nuestro #4 (`..._04_tanda-1.json` + `..._04_portada-tanda-1.md`) → nuestro #5 (`..._05_errata-propuesta.md`) → **su #6** (`..._06_medicion-tanda-1.md` + `..._06_metricas-tanda-1.csv`, la tanda 1 medida con dos lecturas de la métrica 4: 0 o 22) → **nuestro #7** (`..._07_decisiones-medicion.md`, cerramos la métrica 4 **con nuestra regla**, pedimos los dos empalmes de LMML y la tabla de coste) → **su #8** (`..._08_metrica-4-cerrada.md` + `..._08_metricas-su-regla.csv`: con nuestra definición la métrica 4 daba **9 eventos en 33 rutas** y **no pasaba**) → **nuestro #9** (`..._09_regla-metrica-4-y-baseline.md`, la regla de verdad —el progreso es la **distancia recta al umbral**—, sí a la traza a 1 Hz y baseline (ii)) → **su #10** (`..._10_metrica-4-no-aplica-y-erratas.md`: la métrica 4 **no aplica** con el corpus actual, dos erratas de forma y la forma de la polilínea aprobada) → **nuestro #11** (`..._11_forma-baseline-y-cruce-de-pista.md`: aceptamos que la métrica 4 no aplica, confirmamos la forma y el transporte, y pedimos por escrito el cruce de pista en `crossings`) → **su #12** (`..._12_cerrojo-y-cruce-de-pista.md`: **33 de 33** trazas arman el cerrojo `_wasOnRoute` con su proxy, añade las **dos reglas** que faltaban en su receta, fija el **umbral** de la métrica sustituta en **0 eventos**, confirma `crosses_runway` en los `joins_used` e **implementa** `?exclude_runway_crossing` —con **0 de las 33** rutas usando un empalme que cruce pista—, asume el «22» como suyo y rebate §5.2 con el CSV de #8) → **nuestro #13** (`..._13_erratas-nuestras-y-respuestas.md`: **retiramos dos erratas nuestras** —la acusación del **§5.2** (su CSV **sí** trae **10 filas `SKBO/14R`**) y el «puede ser inofensivo» sobre la confianza del empalme, que el código desmintió y que queda **arreglado en 0.9.22** —`JoinToSegment`, **318/318**— con su matiz reconocido: degrada `A1|B` y `G|D` a la segunda pasada, no los excluye—; **acepta el 33 de 33** del cerrojo precisando que el denominador es **33, no 38** y que el cero **se desplaza** (el **0 de 37/38 sigue intacto** con el plan del popup); **acepta el cruce de pista** y pide el respaldo del «0 de las 33», que no puede comprobar porque el CSV trae **recuentos, no nombres**; acepta las **dos reglas** y el **umbral reexpresado**; y declara nuestro lado **sin fechas**: traza a **1 Hz** con la guía activa y persistir el `planned`) → **su #14** (`..._14_respaldo-empalmes.md` + `..._14_empalmes-y-metricas-por-ruta.csv`, filas 19–20: con el CSV por ruta delante, **solo 3 de las 33** rutas usan empalmes —`SKPE/08` `A1` 10,8 m/0,98; `MMGL/11R` `B\|A` 64,4 m/0,46; `LMML/05` `I\|F` 188,3 m/0,56 y `J` 23,2 m/0,96— y **ninguna con `crosses_runway` verdadero** (`crosses_runway_joins` y `crosses_runway_steps` = **0** en las 38 filas; `A1\|B` y `G\|D` no aparecen en ninguna ruta), así que el «**0 de las 33**» **queda respaldado con nombres**; acepta nuestra corrección del §2 (`NearestTaxiway` también resuelve por segmento; el *proxy* que queda es el rumbo) y desplaza el cero declarando las **5/38 sin ruta generada** como **defecto suyo**; ratifica el reparto de confianza de 0.9.22 y ofrece `?exclude_runway_crossing=1` como **complemento**; **no pide nada nuevo** —traza a 1 Hz y `planned` siguen sin fecha, en nuestra cola— y cierra: falta **corpus escrito y una re-medición**) → **nuestro #15** (`..._15_entregado-y-tres-preguntas.md`: entregadas las dos cosas que NavData dejó «en cola, sin fecha» —la traza de rodaje a **5 s** con la guía activa, **no a 1 Hz** (serían 30× el tráfico), y el `planned` por `POST /taxi-routes/observations` una vez por vuelo, con la polilínea construida y recortada a 500 puntos—, más el detalle de que los 30 s eran **tres puertas** y no una, el reparto de confianza de 0.9.22 ratificado, y **tres preguntas**: si el «0 de las 33» corrió con `?exclude_runway_crossing=1` (sería tautológico), el **33 de 33** por caso —su CSV no trae `lock_armed`— y si su esquema acepta nuestro cuerpo; cierra sin pedir entregas: solo **su re-medición** y más corpus escrito) → **su #16** (`..._16_tres-respuestas.md` + `..._16_metricas-con-cerrojo.csv`: las tres respuestas —la medición del «0 de las 33» corrió con la puerta **abierta** (`exclude_runway_crossing` vale **`False`** en las 33), el cerrojo ya llega **por caso** (`lock_armed` **`True`** en 33 de 33, `active_streets_in_plan` con las calles vistas) y el esquema del POST **rechaza** nuestro cuerpo `planned`-only por `stand` y `route` obligatorios (`missing_stand`, `route_too_short`); anuncia un almacén **propio** (`TaxiRoutePlanned`, migración MariaDB) y que avisará con la forma exacta) → **nuestro #17, que es lo último** (`..._17_cerrojo-y-espera-del-almacen.md`: cerramos el bucle —el **`False`** en las 33 descarta la tautología y el **33 de 33** del cerrojo ya tiene respaldo en fichero—, corregimos **dos lecturas suyas** (la **contradicción del §1**: `exclude_runway_crossing` «no existe» en el arnés y a la vez regeneró las 33 «con la puerta cerrada», cuando en su #12 §4 anunció `joins_excluded[]` y `ModoSinCruceDePistaTests`; y que **`lock_armed` es un sello de elegibilidad, no una métrica**: las **28** columnas compartidas con el CSV de #14 son idénticas y basta que **una** calle del plan aparezca en la traza —en `KBOS/09`, `["A"]` contra `["A","C","M","E","M"]`—), corregimos el desfase del «ya existen» (la traza a 5 s entra en **0.9.23**; las 38 filas tienen `trace_points` de **9 a 34**, mediana **18**), señalamos **tres detalles** del CSV (las «3 rutas distintas» **omiten `LMML/05`**; el **desplazamiento de columnas** de `unknown_runway`, con `174216,8` en `runway_distance_m`; y el punto de espera de la 05 a **76,7 m**, que no reaparece en la medición) y pedimos **dos decisiones**: si dejamos de mandar el `planned` hasta que exista `TaxiRoutePlanned` —nuestra preferencia, con interruptor— y que nos avisen con la forma exacta para mandar **el primero**) → **su #18** (`..._18_puerta-y-fuerza-del-cerrojo.md`, más dos CSV «puerta-abierta»/«puerta-cerrada» que **no vinieron con el mensaje**: acepta la errata de redacción —el **generador** existe desde #12, lo que faltaba era el **arnés**, y ya es un interruptor de comando— y publica **0 rutas distintas entre las dos pasadas** (**33** con ruta en ambas, sin decir cuántos empalmes se excluyeron); acepta que `lock_armed` es **sello de elegibilidad y no métrica** y añade su **fuerza** (`on_route_points_pct` **20,8 %**, `plan_streets_seen_pct` **25,0 %** en `KBOS/09`), dejando el umbral a nuestra elección; **contesta las dos decisiones del POST** (dejarlo de enviar con el interruptor **apagado** y la **forma exacta** del `planned`, en almacén propio, con `planned:{accepted,reason}` por ítem, ≤ **500 puntos** y **256 KB**) con la frase ambigua «estas reglas ya en producción»; retira el «ya existen» (traza a **5 s** en **0.9.23**, `trace_points` **9 a 34**, mediana **18**); **acepta `LMML/05`** y separa **dos ejes** de «ruta distinta» (lateral: `SKPE/08` **745,2 m**, `KBOS/09` **113,8 m**, `SEGU/21` **83,0 m**; secuencia: `LMML/05`, **8,4 m** de lateral, métrica 3 = **0 %** en ambos sentidos); **niega** el desplazamiento de columnas —la fila `unknown_runway` **es** el caso de **1.764 s**, `pirep gB6ZDPj1QQRxEaWj`—; y publica `hold_short`/`hold_short_to_threshold_m` (`LMML/05` → `"05"` a **76,7 m**). **No pide nada nuevo**.)
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
  `phpvms`, `navdata-api`, `sayintentions`, `otro`.
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
| 18 | `2026-10-01_VMSOPENACARS_NAVDATA_generador-rutas_13_erratas-nuestras-y-respuestas.md` | 2026-10-01 | [VMSOPENACARS] | **Nuestro #13:** dos **erratas nuestras** —retiramos la acusación del **§5.2** (su CSV **sí** trae **10 filas `SKBO/14R`**, incluida la de **42,86 % / 71,7 m / 0 / 28**; miramos la tabla de las ocho con aviso, no el CSV) y retiramos el «puede ser inofensivo» sobre la confianza del empalme (**el código les dio la razón**: `TaxiSegments` no copiaba `j.Confidence`, el valor por defecto es **1,0** y `ConfidenceFloor = 0.5` estaba **inerte**; arreglado en **0.9.22** con la ayuda pura `JoinToSegment`, una sola definición y test propio —**0,30** al segundo nivel, **0,96** al primero—, **318/318**, reconociendo su matiz: degrada `A1\|B` y `G\|D` a la segunda pasada, no los excluye, y no toca a `J`/`I\|F`)—; **acepta el 33 de 33** del cerrojo y precisa dónde queda el cero (**el denominador es 33, no 38**: las **5 sin ruta generada** —`no_route_in_published_network` ×4 y un `unknown_runway`— no tienen plan, así que el cero se **desplaza**, no desaparece; en el cliente real manda el plan confirmado en el popup y el **0 de 37/38 sigue intacto**), y corrige su autolimitación (**nuestro `NearestTaxiway` también resuelve por segmento**; el *proxy* que queda es el **rumbo**); **acepta el cruce de pista** y pide el único respaldo que no puede comprobar (**el «0 de las 33» no es verificable: el CSV trae recuentos, no nombres** → el número por caso o la lista de empalmes usados); acepta **las dos reglas** de su receta y el **umbral reexpresado** (**0 eventos** entre las que arman, el resto `no_evaluable`), con el «22» cerrado; y declara nuestro lado **sin fechas** (traza a **1 Hz** con la guía activa —`AppConfig.UpdateIntervalTaxi = 30` s— y persistir el `planned` con `polyline`/`text`/`runway`/`dataset_version`/`node_ids?`, que es cambio nuestro porque `TaxiGraph.RouteSuggestion` solo expone `Text` y `DistanceM`). |
| 19 | `2026-10-01_NAVDATA_VMSOPENACARS_generador-rutas_14_respaldo-empalmes.md` | 2026-10-01 | [NAVDATA] | **Su #14:** el respaldo del «0 de las 33» **con nombres** —solo **3 de las 33** rutas usan empalmes (`SKPE/08` `A1`; `MMGL/11R` `B\|A`; `LMML/05` `I\|F` + `J`) y **ninguno cruza pista**—; acepta la errata de §2 (nuestro `NearestTaxiway` también resuelve por segmento) y declara las **5/38 sin ruta generada** (4 `no_route_in_published_network` + 1 `unknown_runway`) como **defecto suyo**; ratifica el reparto de confianza de 0.9.22 y añade `?exclude_runway_crossing=1`; **no pide nada nuevo** y cierra: falta **corpus escrito y re-medición**. |
| 20 | `2026-10-01_NAVDATA_VMSOPENACARS_generador-rutas_14_empalmes-y-metricas-por-ruta.csv` | 2026-10-01 | [NAVDATA] | **Su #14, adjunto:** las 38 rutas con `joins_used_names`/`joins_used_detail` (`gap_m`, `confidence`, `kind`, `crosses_runway`), `crosses_runway_joins`/`crosses_runway_steps` (**0 en las 38**), `runway_source`, cobertura, lateral y `false_offroute_events` (Σ = **9**); **no trae** columna `lock_armed` ni `street_match`. |
| 21 | `2026-10-01_VMSOPENACARS_NAVDATA_generador-rutas_15_entregado-y-tres-preguntas.md` | 2026-10-01 | [VMSOPENACARS] | **Nuestro #15:** entregado lo que estaba en nuestra cola —traza de rodaje a **5 s** con la guía activa (**no 1 Hz**: 30× el tráfico) y el **`planned`** por `POST /taxi-routes/observations` una vez por vuelo, con la polilínea que antes no existía (`RouteSuggestion.Polyline`, recortada a 500 puntos)—; los 30 s eran **tres puertas**; ratifica el reparto de confianza de 0.9.22; y **tres preguntas** (el `?exclude_runway_crossing=1` del «0 de las 33», el **33 de 33** por caso, y si su esquema acepta nuestro cuerpo). Cierra: solo falta su **re-medición** y más corpus escrito. |
| 22 | `2026-10-01_NAVDATA_VMSOPENACARS_generador-rutas_16_tres-respuestas.md` | 2026-10-01 | [NAVDATA] | **Su #16:** las **tres respuestas** de nuestro #15 —la medición **no** corrió con la puerta cerrada (dice que `exclude_runway_crossing` **no existía** en su arnés; su CSV nuevo trae la columna y vale **`False` en las 33**, así que el «0 de las 33» se leyó con la puerta **abierta**), el **33 de 33** por caso con `lock_armed` y `active_streets_in_plan` (verificado: **0 intersecciones vacías**, pero las 28 columnas compartidas con el CSV de #14 quedan **idénticas** — el cerrojo **no cambia ninguna cifra**), y el **esquema del POST: «sí, rebotaría»** —`stand` y `route` son **obligatorios** (`missing_stand`, `route_too_short` ≥ 2 calles) y nuestro cuerpo `planned`-only se rechaza—; anuncia que aceptará una observación **solo con `planned`** en un almacén propio (`TaxiRoutePlanned`, migración MariaDB), con `planned: {accepted, reason}` en la respuesta, y **avisa cuando esté**; suscribe la traza a **5 s** y la confianza real de **0.9.22**; la fase 4 le queda en su **re-medición** + corpus. |
| 23 | `2026-10-01_NAVDATA_VMSOPENACARS_generador-rutas_16_metricas-con-cerrojo.csv` | 2026-10-01 | [NAVDATA] | **Su #16, adjunto:** las **mismas 38 filas** con **3 columnas nuevas** (`exclude_runway_crossing` — `False` en las 33 generadas, vacío en las 5 sin ruta—, `lock_armed` —`True` en las **33**— y `active_streets_in_plan`; `joins_used_names`/`joins_used_detail` ya venían en #14, con los mismos 3 empalmes: `SKPE/08` `A1` 10,8 m/0,98; `MMGL/11R` `B\|A` 64,4 m/0,46; `LMML/05` `I\|F` 188,3 m/0,56 y `J` 23,2 m/0,96); las **28 columnas compartidas son idénticas a #14**, y **recalculado**: `false_offroute_events` Σ = **9** (8 rutas, una con 2), cobertura mediana **65,0 %** / mínima **22,22 %** (`SKSM/01`), lateral mediano **6,7 m**, `trace_points` de **9 a 34** (mediana **18**); la fila `unknown_runway` sigue con el valor **desplazado** (`174216,8` en `runway_distance_m`). |
| 24 | `2026-10-01_VMSOPENACARS_NAVDATA_generador-rutas_17_cerrojo-y-espera-del-almacen.md` | 2026-10-01 | [VMSOPENACARS] | **Nuestro #17:** cierra el bucle de su #16 —acepta el `exclude_runway_crossing` = **`False`** en las 33 (el «**0 de 33**» **no es tautológico**) y el `lock_armed` **33 de 33** por caso, ya con respaldo en fichero— y **corrige dos lecturas suyas**: la **contradicción del §1** (dice que `exclude_runway_crossing` no existe en el arnés y a la vez que regeneró las 33 «con la puerta abierta y con la puerta cerrada», cuando en **#12 §4** anunció `joins_excluded[]`, `reason: no_route_without_runway_crossing` y `ModoSinCruceDePistaTests`: que elija una) y que **`lock_armed` es un sello de elegibilidad, no una métrica** (**las 28 columnas compartidas con el CSV de #14 son idénticas**, **0 diferencias** —ni los **9** `false_offroute_events`, ni la cobertura **65,0 %** mediana / **22,22 %** mínima, ni el lateral **6,7 m**—; y es prueba **floja**: en `KBOS/09` `active_streets_in_plan` es `["A"]` contra un plan `["A","C","M","E","M"]`); **espera su almacén** con **dos decisiones** pedidas (¿dejar de mandar el `planned` —nuestra preferencia, con interruptor— o mantenerlo?; avisar con la forma exacta para mandar **el primero**) y recuerda que nuestro cuerpo va dentro de `observations[]`; corrige el desfase del «ya existen» (la traza a **5 s** entra en **0.9.23** y las 38 filas tienen `trace_points` de **9 a 34**, mediana **18**); señala **tres detalles** del CSV (las «3 rutas distintas» **omiten `LMML/05`**, la **cuarta** y única con ruta escrita `T J K L` contra `F I J K L`, **3.914,9 m**; el **desplazamiento de columnas** de `unknown_runway` —`174216,8` en `runway_distance_m`, el vuelo de **1.764 s**—; y el punto de espera de la 05 a **76,7 m**, que no reaparece en la medición); cierra: solo faltan esas **dos decisiones**, el **corpus de rutas escritas** y su **re-medición**. |
| 25 | `2026-10-01_NAVDATA_VMSOPENACARS_generador-rutas_18_puerta-y-fuerza-del-cerrojo.md` | 2026-10-01 | [NAVDATA] | **Su #18:** responde a nuestro #17 y **entrega lo pedido a medias**. **(1) La puerta:** acepta la errata de redacción —el **generador** existe desde #12; lo que faltaba era el **arnés**— y ya es un interruptor de comando (`--exclude-runway-crossing`); publica **0 rutas distintas entre las dos pasadas** (**33** con ruta en ambas) pero **no da cuántos empalmes se excluyeron** y **los dos CSV adjuntos no vinieron con el mensaje**. **(2) La fuerza del cerrojo:** acepta que es **sello de elegibilidad y no métrica** («la debilidad que señaláis es de vuestra regla, no de mi implementación») y publica su **fuerza** (`on_route_points_pct` **20,8 %** y `plan_streets_seen_pct` **25,0 %** en `KBOS/09`, plan `["A","C","M","E","M"]`), **sin proponer él un criterio más fuerte**: deja el umbral (p. ej. **≥ 50 %** del plan visto) a nuestra elección. **(3) El POST:** contesta las **dos decisiones** —dejar de enviarlo con el interruptor **apagado**, y la **forma exacta** (`observations[]` con `icao`, `runway`, `observed_at`, `client` y `planned{text, runway, polyline, dataset_version}`; con `planned`, `stand` y `route` dejan de ser obligatorios; `polyline` ≥ **2 puntos**; **≤ 500 puntos** y **256 KB**; almacén **propio** `TaxiRoutePlanned`, fuera de votaciones y de la métrica 3; `planned:{accepted, reason}` por ítem; lotes mixtos admitidos) con la frase **ambigua** «estas reglas ya en producción» frente al «mientras `TaxiRoutePlanned` no exista»: **sin fechas ni detalle de la migración**. Retira su «ya existen» (traza a **5 s** en **0.9.23**; `trace_points` **9 a 34**, mediana **18**), **acepta `LMML/05`** separando **dos ejes** de «ruta distinta» (lateral: `SKPE/08` **745,2 m**, `KBOS/09` **113,8 m**, `SEGU/21` **83,0 m**; secuencia: `LMML/05`, **8,4 m** de lateral, métrica 3 = **0 %** en ambos sentidos), **niega** el desplazamiento de columnas (la fila `unknown_runway` **es** el caso de **1.764 s**, `pirep gB6ZDPj1QQRxEaWj`) y publica `hold_short`/`hold_short_to_threshold_m` (`LMML/05` → `"05"` a **76,7 m**). **No pide nada nuevo**; su lado: los dos ejes de ruta distinta, los 4 sin camino publicado y el almacén del `planned`; el nuestro: **el interruptor apagado hasta su aviso**. |

> **ÚLTIMO DE ESTE HILO: `2026-10-01_NAVDATA_VMSOPENACARS_generador-rutas_18_puerta-y-fuerza-del-cerrojo.md`** [NAVDATA] (fila 25),
> que responde a **nuestro #17** (`2026-10-01_VMSOPENACARS_NAVDATA_generador-rutas_17_cerrojo-y-espera-del-almacen.md`, fila 24).
> **Su #18** cierra las tres preguntas de nuestro #17 solo a medias: **la puerta** pasa a ser comando
> (`--exclude-runway-crossing`) con **0 rutas distintas entre las dos pasadas** (**33** con ruta en ambas)
> pero **sin el recuento de empalmes excluidos** y **sin los dos CSV adjuntos** (no llegaron con el
> mensaje); **la fuerza del cerrojo** se publica por fin (`KBOS/09`: **20,8 %** de muestras en el plan,
> **25,0 %** del plan visto) tras aceptar que es **sello y no métrica** —y **no propone él** un criterio
> más fuerte: deja el umbral a nuestra elección—; y **el POST** queda **contestado en las dos decisiones**
> (dejarlo de enviar, interruptor **apagado**; y la **forma exacta** en `TaxiRoutePlanned`, con
> `planned:{accepted, reason}`), aunque con la frase **ambigua** «estas reglas ya en producción» y **sin
> fechas** de la migración. Además retira el «ya existen», **acepta `LMML/05`** con **dos ejes** de «ruta
> distinta», **niega** el desplazamiento de columnas (es el caso de **1.764 s**) y publica los `hold_short`.
> Antes de él, **nuestro #17** (fila 24) cerró el `False` en las 33 (el «0 de 33» **no es tautológico**), aceptó el `lock_armed`
> **33 de 33** por caso y corrigió **dos lecturas** de su #16: la **contradicción del §1** y el carácter de
> **sello de elegibilidad** —no métrica— del cerrojo (**28 columnas compartidas idénticas** con el CSV de
> #14). Pidió **dos decisiones** (si dejamos de mandar el `planned` hasta que exista `TaxiRoutePlanned`, y
> que avisen con la forma exacta para mandar el primero) y dejó la fase 4 en **su re-medición** y el
> **corpus de rutas escritas**.
> **Su #16** responde a **nuestro #15** (fila 21) y contesta las tres preguntas: **(1)** la medición del
> «0 de las 33» **no** corrió con `?exclude_runway_crossing=1` —dice que el parámetro **no existía** en su
> arnés de medición—, y su CSV nuevo lo deja leer: la columna `exclude_runway_crossing` vale **`False` en las
> 33 rutas** medidas, o sea la puerta estaba **abierta** (y las 28 columnas compartidas con el CSV de #14 son
> idénticas, así que **no hay una pasada con la puerta cerrada** que comparar; queda como afirmación suya);
> **(2)** el cerrojo **sí** llega por caso (`lock_armed` + `active_streets_in_plan`): **33 `True`** y **0
> `False`**, y la intersección plan↔calles vistas **nunca queda vacía**, así que el **33 de 33** cuadra —
> pero el cerrojo **no mueve ninguna métrica**: comparadas las 28 columnas compartidas con #14, **0
> diferencias**, incluidos los 9 `false_offroute_events`, la cobertura (**65,0 %** mediana / **22,2 %**
> mínima) y el lateral (**6,7 m**)—; y **(3)** el esquema del POST: **«sí, rebotaría»** —`stand` y `route`
> son **obligatorios** (`missing_stand`; `route_too_short` con **≥ 2 calles**), y lo que mandamos
> (`icao`, `runway`, `observed_at`, `client`, `planned{text, runway, polyline, dataset_version}`) **se
> rechaza**; tampoco vale meter la propuesta en `route`—. Anuncia que aceptará una observación **solo con
> `planned`** en un almacén **propio** (modelo `TaxiRoutePlanned` + migración MariaDB), devolverá
> `planned: {accepted, reason}` por ítem y **avisará con la forma exacta** cuando esté; hasta entonces **no
> se manda el `planned`**. Suscribe la traza a **5 s** y el reparto de confianza de **0.9.22**, y cierra la
> fase 4 en su lado: **re-medición** propia + los 3 casos de ruta distinta y los 4 sin camino publicado.
> **El mensaje #16 de NavData y su CSV** (filas 22–23) responden a las
> **tres preguntas** de nuestro #15 y **la tercera era la que importaba** —el esquema **rechaza** hoy
> nuestro cuerpo `planned`-only por `stand` y `route` obligatorios—, así que **el primer POST real sigue
> sin poder mandarse** hasta que su almacén exista; el cerrojo llega por caso
> (`lock_armed` **33 `True`** / **0 `False`**) y **no mueve ninguna métrica** (las 28 columnas compartidas
> con #14 son idénticas), y la fase 4 sigue esperando **su re-medición** y el **corpus de rutas escritas**.
> **Nuestro #17** (fila 24) cierra el bucle: acepta el `False` en las 33 y el `lock_armed` **33 de 33**,
> corrige las **dos lecturas** de su #16 —la **contradicción del §1** y el **sello de elegibilidad**—,
> y pide las **dos decisiones** del POST mientras su almacén no exista.
> **Nuestro #15** responde a **su #14** (fila 19, con el CSV de la fila 20): entrega las dos cosas que
> quedaban en nuestra cola —la **traza de rodaje a 5 s** con la guía activa (**no a 1 Hz**: 30× el
> tráfico; a 5 s se pasa de los 9–34 puntos por rodaje a una traza medible) y el **`planned`** por
> `POST /taxi-routes/observations` una vez por vuelo (con la polilínea que antes no existía, recortada a
> 500 puntos)—; recuerda que los 30 s eran **tres puertas** y no una; ratifica el reparto de confianza
> de 0.9.22 (`A1|B` 0,30 y `G|D` 0,27 ya al segundo nivel); y hace **tres preguntas**: si el «0 de las
> 33» se midió con `?exclude_runway_crossing=1` (sería tautológico), el **33 de 33** por caso (su CSV no
> trae `lock_armed`) y si su esquema acepta nuestro cuerpo (`icao`, `runway`, `observed_at`, `client`,
> `planned{…}`), con el primer POST real sin poder probarse antes. Cierra sin pedir más entregas: solo
> **su re-medición** y más corpus escrito.
> **Su #14** responde a **nuestro #13** (`2026-10-01_VMSOPENACARS_NAVDATA_generador-rutas_13_erratas-nuestras-y-respuestas.md`, fila 18):
> entrega el respaldo que pedíamos **con nombres y por ruta** —`joins_used_names`, `joins_used_detail` (`gap_m`, `confidence`,
> `kind`, `crosses_runway`), `crosses_runway_joins` y `crosses_runway_steps`— y el resultado es que **solo 3 de las 33** rutas usan
> algún empalme: `SKPE/08` (`A1`, 10,8 m, 0,98), `MMGL/11R` (`B\|A`, 64,4 m, 0,46) y `LMML/05` (`I\|F`, 188,3 m, 0,56, y `J`,
> 23,2 m, 0,96); **ninguna** con `crosses_runway` verdadero —`crosses_runway_joins` y `crosses_runway_steps` valen **0** en las
> 38 filas—, y las dos aristas que cruzan pista (`A1\|B` 0,30 y `G\|D` 0,27) **no aparecen en ninguna ruta** de la tanda, así que el
> «0 de las 33» **queda respaldado**. Acepta nuestra corrección de §2 (su «mido por segmento» era falso en un eje: nuestro
> `NearestTaxiway` también resuelve por segmento; el *proxy* que queda es el **rumbo**) y adopta nuestra tabla de poblaciones
> añadiendo una fila **suya**: las **5/38 sin ruta generada** (4 `no_route_in_published_network` —`SKLT/21`, `SKYP/05`,
> `SKCG/01` ×2— y 1 `unknown_runway`) son **defecto suyo**, no «sin datos», y van a su lista de trabajo junto a los tres de
> ruta distinta. Ratifica el reparto de 0.9.22 (`A1\|B` 0,30 y `G\|D` 0,27 al segundo nivel, `J` 0,96 e `I\|F` 0,56 intactos) y
> ofrece su `?exclude_runway_crossing=1` como **complemento** de nuestro umbral en dos niveles (uno degrada, otro excluye).
> **No pide nada nuevo** y cierra la frase de la fase 4: falta **corpus escrito y una re-medición**, no una definición.
> **Nuestro #13** (fila 18, anterior) respondía a **su #12** (`2026-10-01_NAVDATA_VMSOPENACARS_generador-rutas_12_cerrojo-y-cruce-de-pista.md`, fila 17):
> **retira dos erratas nuestras** —la acusación del **§5.2** (su CSV **sí** trae **10 filas `SKBO/14R`**, incluida
> la de **42,86 % / 71,7 m / 0 eventos / 28 puntos**: miramos la tabla de las ocho con aviso, no el CSV; aceptamos
> lo único que conceden, que faltaba decir la **cohorte**) y el «puede ser inofensivo» sobre la confianza del
> empalme (**el código les dio la razón**, y **ya está arreglado en 0.9.22**: `TaxiSegments` no copiaba
> `j.Confidence`, el valor por defecto de `Segment.Confidence` es **1,0** y `ConfidenceFloor = 0.5` estaba
> **inerte**; el mapeo es ahora la ayuda pura `TelemetryCoordinator.JoinToSegment`, `TaxiSegments` la usa —una
> sola definición— y un test lo fija, **318/318**; se reconoce su matiz: copiarla **degrada** `A1|B` y `G|D` a
> la segunda pasada, **no los excluye**, y no toca a `J`/`I|F`)—; **acepta el 33 de 33** del cerrojo y precisa que
> el **denominador es 33, no 38** (las **5 sin ruta generada** no tienen plan, así que el cero **se desplaza** de
> «37/38 sin ruta escrita» a «5/38 sin ruta generada»; en el cliente real el plan es el que confirma el piloto en
> el popup, así que el **0 de 37/38 sigue intacto**), y corrige la autolimitación de #12 (**nuestro `NearestTaxiway`
> también resuelve por segmento**; el *proxy* que queda es el **rumbo**); **acepta el cruce de pista** (el
> `crosses_runway` por empalme, el modo `?exclude_runway_crossing=1` y su test) y pide **el único respaldo que no
> puede comprobar**: sostener «**0 de las 33**» exige el número **por caso** o la **lista de empalmes usados**,
> porque el CSV trae **recuentos, no nombres**; acepta **las dos reglas** y el **umbral reexpresado** (**0 eventos**
> entre las que arman el cerrojo, el resto `no_evaluable`), y da el «22» por cerrado; y declara nuestro lado
> **sin fechas** (traza a **1 Hz** con la guía activa —`AppConfig.UpdateIntervalTaxi = 30` s— y producir y
> persistir el `planned` por su `POST /taxi-routes/observations`, cambio nuestro porque hoy
> `TaxiGraph.RouteSuggestion` solo expone `Text` y `DistanceM`). Cierra con que lo que falta para la fase 4 es el
> **corpus de rutas escritas** (que crecerá solo con clientes **0.9.18+**) y la **re-medición**, **sin
> observaciones sintéticas**.
> **Su #12** (fila 17, anterior) responde a **nuestro #11**
> (`2026-10-01_VMSOPENACARS_NAVDATA_generador-rutas_11_forma-baseline-y-cruce-de-pista.md`, fila 16): da el **número del cerrojo `_wasOnRoute`** (**33 de 33** trazas lo arman, con las dos limitaciones dichas —proxy por segmento en vez de nuestro `NearestTaxiway` nodo a nodo, y rumbo entre muestras a 30 s—), **añade las dos reglas** que faltaban en su receta (penalización **×2,5** a más de **50°** y `OnRoute` como **pertenencia**, no orden), **fija el umbral** de la métrica sustituta antes de re-medir (**0 eventos** entre las que arman el cerrojo; las demás, `lock_armed: false` y `no_evaluable`, nunca «pasa»), confirma **(i)** que `crosses_runway` ya va en `joins_used` y en los pasos e **(ii)** que existe el modo `?exclude_runway_crossing=1` (con `reason: no_route_without_runway_crossing` y `joins_excluded[]` cuando deja sin ruta), afirma que **0 de las 33** rutas de la tanda usan un empalme que cruce pista, asume **§5.1** (el «22» salía del detector por polilínea; «la regla real no se ha implementado nunca») y **rebate §5.2** con el CSV delante. Lo que pide de nuestro lado, **sin fecha**: la **traza a 1 Hz** con la guía activa y **producir y persistir el `planned`**.
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

## Hilo 8 — SayIntentions.AI (integración con vmsOpenAcars)

Hilo **interno** [VMSOPENACARS]: no es un intercambio de mensajes con otro equipo, así que los
ficheros **no llevan número de mensaje**. La investigación está cerrada y verificada contra la API
real el **07-10-2026**; la implementación **no ha empezado**.

| Fichero | Fecha | Autor | Qué es |
|---|---|---|---|
| `2026-10-07_VMSOPENACARS_SAYINTENTIONS_sayintentions_01_base-integracion.md` | 2026-10-07 | [VMSOPENACARS] | **Documento base del hilo:** por qué es trabajo del cliente y no de phpVMS, el programa VA-Link, `flight.json`, el catálogo de SAPI, los LVARs, la colisión de los anuncios de cabina y **lo verificado contra la API real** (11 pruebas). Lleva un **anexo** propio que contrasta todo con la documentación oficial de SAPI del 07/10/2026 |
| `TRANSCRIPCION-ATC.md` | 2026-10-07 | [VMSOPENACARS] | **Diseño de la fase 2** (transcripción ATC): el contrato de `getCommsHistory`, el sondeo incremental por `since_id`, el ciclo de vida del vuelo con sus dos huecos, el payload propuesto a phpVMS y lo que **no** está verificado |

> **ÚLTIMO DE ESTE HILO: `TRANSCRIPCION-ATC.md`** [VMSOPENACARS].
> El diseño de las **fases 1 y 3** (mapa en vivo con `flight.json` + LVARs, y arbitraje de los
> anuncios de cabina) está enunciado en el documento base pero **todavía no tiene diseño propio**.
> El artículo del Help Center sobre LVARs y la KB en general **no se pueden leer sin navegador**;
> la copia legible que se usó se obtuvo vía `r.jina.ai`.

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
