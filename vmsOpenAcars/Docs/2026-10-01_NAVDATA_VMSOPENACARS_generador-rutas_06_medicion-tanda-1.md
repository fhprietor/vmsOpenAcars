# NavData → vmsOpenACars: la tanda 1, medida — y la definición que la decide

> **Para:** equipo de vmsOpenACars · **De:** equipo de NavData · **Fecha:** 2026-10-01
> **Responde a:** `2026-10-01_VMSOPENACARS_NAVDATA_generador-rutas_04_portada-tanda-1.md`,
> `…_04_tanda-1.json` y `…_05_errata-propuesta.md`
> **Adjunto:** `2026-10-01_NAVDATA_VMSOPENACARS_generador-rutas_06_metricas-tanda-1.csv` (detalle por ruta)
> **Estado:** **medida hecha, y no pasa el criterio.** 3 de 6 umbrales se cumplen, 1 no es evaluable
> y **1 depende de una definición vuestra**: la métrica 4 da **0 eventos** con una lectura y **22**
> con la otra. Hasta que la fijéis, el «0 falsos» no es un dato.

---

## 0. Resumen

1. **La tanda es usable a pesar de los nulos.** `runway: null` en 37 de 38 hacía imposible calcular
   ruta (sin pista no hay destino). La pista **se mide en la traza**: 37 de 38 dan un extremo a
   menos de 300 m (mediana 62 m). Va marcado `runway_source: derived_from_trace`.
2. **3 de 6 criterios se cumplen**: desviación lateral **6,7 m** (umbral 25), **0 falsos
   `FUERA DE RUTA`** con la lectura conservadora, y **100 %** de empalmes declarados.
3. **La cobertura falla** (65 % de mediana, umbral 95 %) y sabemos por qué: **28 de 33 trazas
   acaban más allá del punto de espera** (la traza sigue hasta la pista) y **19 de 33 empiezan en la
   plataforma**. Descontando los puntos que están dentro del rectángulo de pista: 73,3 %.
4. **La métrica 3 no es evaluable**: solo 1 de 38 casos trae ruta escrita (el `T J K L` de LMML).
   Y **la baseline no se puede calcular** en ninguno, como avisasteis.
5. **La métrica 4 es la que decide y su resultado se invierte con la definición**: 0 eventos si
   «acercarse a la pista» anula el aviso con cualquier progreso, **22 eventos** si exige ≥ 50 m
   (vuestro `off_route_progress_m`). Ver §4.

---

## 1. Cómo hemos hecho usable la tanda

**La pista, medida en la traza.** Sin `runway` no hay destino, así que la derivamos del último punto
del rodaje (el avión acabó ahí: es dato, no invención) y lo declaramos por caso. Si preferís darnos
la pista real, se cambia y se re-mide; es una línea.

**Un caso no es un rodaje de salida, y sabemos por qué.** `gB6ZDPj1QQRxEaWj` (SKBO) acaba a **174 km**
del aeropuerto, en Pereira: su traza incluye el vuelo. La causa es de cortes: **su pausa mayor es de
1.764 s = 29,4 min**, y vuestro corte es «la primera pausa de más de 30 min» — no salta por 36 s.
Los que avisasteis como «huecos» eran pausas reales, de acuerdo; este es el único que además arrastra
el vuelo entero. Queda fuera de la medición, no lo hemos recortado a mano.

**`route_written: null` y `baseline_polyline: null`: aceptados.** No se infiere nada, y lo agradecemos
explícitamente: es la decisión correcta, y evita envenenar la métrica 3 y la 4.

**`stand: null`:** usamos vuestro `lat`/`lon` (primer punto real de la traza), que es la forma de
origen que admite el anexo 2.

---

## 2. Resultados de la tanda 1

33 de 38 casos generan ruta (5 no: `SKLT/21`, `SKYP/05` y `SKCG/01` ×2 por
`no_route_in_published_network`, y el caso mal cortado por `unknown_runway`).

| # | Criterio | Valor medido | Umbral | Veredicto |
|---|---|---|---|---|
| 1 | `trace_coverage_pct` (mediana) | **65,0 %** | ≥ 95 % | **FALLA** |
| 1b | `trace_coverage_pct` (mínima) | **22,2 %** | ≥ 80 % | **FALLA** |
| 2 | `median_lateral_m` | **6,7 m** | ≤ 25 m | **PASA** |
| 3 | `street_match_pct` | **1 caso comparable**, 0 % | ≥ 80 % | **SIN DATOS** |
| 4 | `false_offroute_events` | **0** (lectura A) / **22** (lectura B) | 0 | **PASA / FALLA** |
| 5 | `low_conf_joins_declared_pct` | **100 %** | 100 % | **PASA** |

Con la lectura conservadora de la métrica 4 el criterio queda «3 pasa, 2 falta, 1 sin datos» → **no
se puede plantear la fase 4**. El detalle por ruta va en el CSV adjunto.

---

## 3. Por qué falla la cobertura (y qué parte no es culpa de la ruta)

Con 9–34 puntos por traza (muestreo de 30 s), un par de puntos malos hunden un porcentaje. Mirado
punto a punto:

| Medición | Valor |
|---|---|
| Distancia del **último** punto a la ruta, mediana | **103 m** — y **> 45 m en 28 de 33** |
| Distancia del **primer** punto a la ruta, mediana | **63 m** — y **> 45 m en 19 de 33** |
| Puntos que caen dentro del rectángulo de pista | 49 de 619 (**7,9 %**) |
| Cobertura **descontando los puntos de pista** | **73,3 %** |
| Lateral **descontando los puntos de pista** | **5,4 m** |

El patrón es claro y no es aleatorio: **la ruta termina en el punto de espera a propósito** (es lo que
acordamos), y la traza sigue después hasta la pista; y al principio está la plataforma, donde el
escenario no siempre modela calles. En el tramo de rodaje propiamente dicho la ruta va pegada:
**5,4 m de mediana**.

Quedan **3 casos** que sí son ruta distinta y no extremos — `KBOS/09` (24 %, 114 m), `SEGU/21`
(40 %, 83 m) y `SKRG/19` (42 %, 58 m) —; esos los miramos nosotros, no son de definición.

**Y un error nuestro, que la tanda destapó.** El primer intento daba cifras absurdas (cobertura
22 %, lateral 528 m) por un bug en nuestra derivación de pista: devolvía el nombre del **extremo
opuesto** de la fila (para una salida por `14R` decía `32L`) y mandaba el generador al otro lado del
aeropuerto. Corregido, con test que fija los cuatro extremos de SKBO. Las cifras de esta carta son
las de después del arreglo.

---

## 4. Las cuatro decisiones que bloquean la re-medición

**(1) La métrica 4: ¿qué significa «sin acercarse a la pista»?** Es vuestra pregunta y es la que
decide, con números:

| Lectura | Eventos en 33 rutas |
|---|---|
| A: cualquier acercamiento al umbral anula el aviso | **0** |
| B: solo lo anula un acercamiento ≥ 50 m (vuestro `off_route_progress_m`) | **22** |

El umbral «0 falsos» **pasa o falla según cuál se implemente**. Decidnos cuál es la vuestra (o
pasádnoslo del `RaasAdvisor`) y publicamos esa; mientras, damos las dos.

**(2) Cobertura: ¿sobre toda la traza o sobre el rodaje?** Proponemos medirla en la ventana
**desde el primer punto de red hasta el punto de espera**, excluyendo la plataforma y lo que hay más
allá del punto de espera (que es pista). Es una enmienda al criterio y la pedimos **antes** de
re-medir: con la definición de hoy el número es 65 %, con la enmienda 73,3 %, y las dos se publican.

**(3) La métrica 3 sin ruta escrita.** Nuestra propuesta: **excluida del recuento, no contada como
fallo** (un `False` sería una acusación falsa). Hoy la muestra es **1**, así que no decide nada: hace
falta que crezca el corpus desde 0.9.18. ¿Fijamos un mínimo de muestra para darla por evaluable?

**(4) La baseline.** Sin `baseline_polyline` la regla «no empeora» de §3.3.2 **no se puede evaluar**.
Tres salidas: (i) la dejamos fuera de la fase 4 hasta que tengáis polilíneas; (ii) nos dais la
polilínea del cliente; (iii) se sustituye por «la traza real», que es lo que ya hacemos. La (iii) es
circular, así que proponemos (i) o (ii).

**Aviso de muestreo, para que no se confunda con un resultado:** evaluáis RAAS a **1 Hz** y estas
trazas vienen a **30 s por punto**. Una excursión de 15 s entre dos muestras es invisible, así que la
métrica 4 de esta tanda es una **cota inferior**. Para el número bueno hace falta la telemetría de
1 Hz —o el contador de avisos que emitió vuestro cliente en esos vuelos—.

---

## 5. Vuestra errata: lo que aceptamos y lo que aclaramos

| Punto vuestro | Nuestra respuesta |
|---|---|
| «Más barata» no se sostiene **sin publicar el coste** | **Tenéis razón, y por partida doble.** Con el perfil que sirve (`k = 0,5`): `E N A S A A3` = 2.269,3 m + 0,5·338,0° = **2.438,3**; la alternativa sin `N` = 2.265,2 m + 0,5·346,9° = **2.438,6**; la de solo distancia = 2.220,9 m + 0,5·839,7° = **2.640,7**. Es decir: la nuestra es **2,4 m más larga** y su ventaja de coste es **0,3 (0,01 %)**, un empate. Se reformula a «**menos giros** (338° frente a 840°), misma distancia», y el perfil queda publicado aquí para que sea comprobable |
| «Total: 3 giros > 25°, y **ninguno más por encima de 10°**» se contradice con la tabla | **Mala redacción nuestra.** Queda: «3 giros, **todos** > 25°». El anexo 1 se corrige así |
| La aritmética de LMML (5,4 % frente a 13,4 %) | **No hay contradicción**: una ruta usa **los empalmes que cruza** (2 de los 4); los 4 publicados son los que la red necesita para quedar en 1 componente. Y el `gap_m` de `A1|B` **es nuestro**, medido aquí: coincide con el vuestro al decimal (298,3 m) |
| Pregunta 2: ¿`I|F` calculado o curado? | **Calculado**: `kind: component_bridge`, `source: computed`, `crosses_runway: false`. Y confirmamos: **`G|D` (16,4 m) sí cruza pista** — dato que se os escapaba |
| Pregunta 3: los 50 m, ¿a la polilínea o al punto más cercano? | **Es justo la ambigüedad que hay que cerrar**: ver §4(1), donde los dos criterios dan 0 y 22. Nuestra implementación usaba «distancia a la polilínea» para detectar el episodio y «acercamiento al umbral» para anularlo; su `off_route_progress_m` es lo segundo |
| Vuestro «LMML de 300 posiciones» no cuadra: son 29 | **Aceptado y anotado.** La cifra la escribimos nosotros en §3.1 y era mala. Se corrige a **29 puntos reales** |

---

## 6. Qué necesitamos para cerrar la fase 2

1. **Las cuatro decisiones de §4** (la métrica 4 es la que más pesa).
2. **Re-cortar `gB6ZDPj1QQRxEaWj`** con un umbral de pausa menor (o confirmar que se excluye).
3. **La telemetría a 1 Hz** de esos rodajes, si queréis la métrica 4 fiable; o el contador de avisos
   que emitió el cliente en cada vuelo.
4. **La pista de salida real**, cuando la tengáis (hoy vamos con la derivada de la traza).
5. **El corpus de rutas escritas**, que crecerá solo desde 0.9.18: es lo único que puede dar muestra
   a la métrica 3.

Con (1) y (2) re-medimos y publicamos el número definitivo de la tanda 1; sin ellas, cualquier cifra
de la métrica 4 es una interpretación, no un dato.
