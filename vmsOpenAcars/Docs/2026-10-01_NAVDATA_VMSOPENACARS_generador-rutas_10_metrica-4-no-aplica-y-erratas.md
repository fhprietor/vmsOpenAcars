# NavData → vmsOpenACars: la métrica 4 no se puede medir todavía — y dos erratas nuestras

> **Para:** equipo de vmsOpenACars · **De:** equipo de NavData · **Fecha:** 2026-10-01
> **Responde a:** `2026-10-01_VMSOPENACARS_NAVDATA_generador-rutas_09_regla-metrica-4-y-baseline.md`
> **Estado:** con vuestra §2 se cae la base de la métrica 4 en esta tanda: **el disparador es una
> comparación de nombres y sin ruta escrita no existe**. Aceptamos las dos erratas de forma, y los
> siete valores por defecto quedan cerrados.

---

## 0. Resumen

1. **Vuestra errata de #7 cambia el número y también mi mecanismo**: con la regla real (reducción de
   la distancia al **umbral de pista** respecto al mínimo del episodio) el resultado es el de mi
   lectura B, **22**, no los 9 de #8. Pero…
2. **…y lo importante es que ninguna de las tres simulaciones vale.** Vuestra §2 dice dos cosas que
   lo invalidan: el disparador es **comparar el nombre de la calle activa con el plan**, no una
   distancia a una polilínea; y **sin ruta escrita no hay `FUERA DE RUTA`**. De los 38 casos, **37 no
   traen ruta escrita**: en esos, vuestro motor no emite nada, y la métrica no mide calidad, mide
   vacío. Y en LMML, el único con plan, vuestro motor de hoy da **0** (vosotros lo medisteis: 15 con
   el de entonces).
3. **Por eso hacemos lo que ofrecisteis**: la métrica 4 se **rotula como otra cosa** —
   «`FUERA DE RUTA` simulados con la ruta generada **como plan**»— y se implementa con vuestra
   máquina de estados (§2) cuando llegue la tanda densa. No publicamos otro número flojo.
4. **Aceptamos vuestras dos correcciones de forma** (§5): la mediana era **67,95 m**, no 62, y la
   lista de «ruta distinta» estaba mal — el peor caso, `SKPE/08` con **745 m**, no aparecía.
5. **Los siete valores por defecto quedan cerrados** tal como los teníais (§4): con eso, los doce
   puntos de la propuesta están cerrados.

---

## 1. Qué número es ahora

| Simulación | Mecanismo | Eventos |
|---|---|---|
| A (#6) | distancia a la **polilínea**, acercarse a la **pista** anula | 0 |
| B (#6) | ídem, con umbral de 50 m | 22 |
| C (#8) | progreso respecto al **punto más cercano de la ruta** | 9 |
| **Real** | **nombre de calle contra el plan** + reducción de distancia al umbral | **no aplica**: 37/38 sin ruta escrita; LMML, **0** con 0.9.19+ |

Mi error de #8 no fue el número: fue el **mecanismo** (distancia en vez de nombre). Lo asumimos.

---

## 2. Cómo simularemos la métrica 4 de verdad (para la próxima tanda)

Reproducimos vuestra máquina de estados, y pedimos que nos corrijáis si algo no encaja:

| Paso | Cómo lo implementamos |
|---|---|
| Calle activa | la calle del segmento más cercano **dentro de 45 m** (`NearestTaxiway`): nuestra `taxiway-stats` ya resuelve eso por nodo y segmento |
| `OnRoute` | `True` si la calle activa está **en la secuencia del plan**, en orden; si no, `False` |
| Plan | la **ruta generada**, que es la pregunta de la fase 4 (¿aguanta como plan?) |
| Progreso | reducción ≥ **50 m** de la distancia **al umbral de pista** respecto al **mínimo del episodio** (`DistanceToRunwayM`) |
| Persistencia | **15 s** fuera de ruta sin progreso |
| Cerrojo | `_wasOnRoute`: sin haber estado en ruta alguna vez, no hay aviso |
| Pista | `OnRunway` → `RUTA COMPLETA`, nunca desvío |
| Enfriamiento | **20 s** con re-armado, a **1 Hz** |

Lo que hace falta para que sea fiel, y no lo tenemos: **la traza a 1 Hz** (la prometisteis), **la
pista** por caso (hoy la derivamos de la traza) y **una ruta escrita** para que el plan exista.

---

## 3. Baseline (ii): forma aprobada, con un transporte y un tope

Aprobamos los cinco campos (`text`, `runway`, `polyline`, `dataset_version`, `node_ids`), con tres
precisiones:

1. **`dataset_version` = nuestro `version`** (el hash de nodos que ya publicamos). Así la polilínea y
   el dataset con el que se generó son auditables juntos, que es justo lo que faltaba para §3.3.2.
2. **Transporte: nuestro endpoint de observaciones** (`POST /taxi-routes/observations`), con un campo
   nuevo opcional `planned: {text, runway, polyline, dataset_version, node_ids}`. Ese canal ya
   valida, deduplica y alimenta la base de conocimiento; una entrada `RAAS:` en `/acars/logs` sería
   por aviso y tampoco nos gusta.
3. **Tope de tamaño, que sí es un problema real:** el cuerpo está limitado a **64 KB** y el lote a
   200 observaciones. Con polilíneas de ~100 puntos nos pasamos. Proponemos **500 puntos por
   polilínea** y, si hace falta, subimos el tope del cuerpo a **256 KB** — es decisión nuestra y la
   hacemos antes de que lo necesitéis.

---

## 4. Los siete valores por defecto: cerrados

| # | Punto | Queda |
|---|---|---|
| 3 | Sentido de marcha | **Bidireccional** en v1; `?one_way=1` en v2 con el `start_dir`/`end_dir` del escenario |
| 4 | Nivel de detalle | **Los tres**: pasos con giro + rumbo, **polilínea** y `node_id`s |
| 5 | Unidades y referencia | **Metros** y rumbo **magnético** (rumbos de entrada/salida incluidos, para que cuadren con vuestra geometría) |
| 6b | Entrada a pista | Devolvemos las alternativas válidas y elegimos la más barata; fijable con `?entry=` |
| 8 | Reencaminamiento | **Sí** en v1: `?lat=&lon=` |
| 9 | Caché | **TTL 24 h** + `version`, con `Cache-Control: private` |
| 12 | Plan y slug | Slug **`taxi_route`**; activo en todos los planes durante el piloto |

El **#4** ya está implementado (la polilínea va en la respuesta), el **#5** también (metros y
magnético) y el **#6b** está en el generador (`entry=`). Los otros cuatro son de contrato y entran
con el endpoint.

---

## 5. Nuestras dos erratas de forma

**5.1 La mediana.** Publicamos **62 m** y no sale de ningún cálculo reproducible: es un resto de la
**ejecución anterior al arreglo** de la derivación de pista (la que devolvía el extremo opuesto), y
no la volví a calcular al escribir #8. Las cifras buenas, contadas sobre el CSV:

| Conjunto | Filas | Mediana |
|---|---|---|
| `runway_distance_m` < 300 m en el CSV | 36 | **67,95 m** |
| Añadiendo los 92 m de LMML (que no se deriva) | 37 | **69,6 m** |

**5.2 Los casos de ruta distinta.** Me contradije entre §1 y §2 de #8 (dije `SKRG/01` en uno y
`SKRG/19` en el otro) y, peor, **la lista estaba mal por los dos lados**: el peor caso no aparecía.
Ordenados por desviación lateral:

| Caso | Lateral | Cobertura | Eventos |
|---|---|---|---|
| **`SKPE/08`** | **745,2 m** | 27,3 % | 0 |
| `KBOS/09` | 113,8 m | 24,0 % | 0 |
| `SEGU/21` | 83,0 m | 40,0 % | 1 |
| `SKBO/14R` | 71,7 m | 42,9 % | 0 |

Los «3 casos de ruta distinta» son **`SKPE/08`, `KBOS/09` y `SEGU/21`** — `SKPE/08` con 745 m no es
un caso de extremos, es una ruta que va a otro sitio, y es el primero que vamos a mirar. `SKRG/01`
(49,3 m, 1 evento) y `SKRG/19` (57,8 m, 0 eventos) son de segunda fila.

---

## 6. Lo que cierra vuestra §3, y os lo agradecemos

El resolutor de calle activa usaba **300 m** hasta **0.9.20**: con eso, un puesto a 64,2 m de `F` y
262 m de `T` se resolvía como **`F`**, que es exactamente la discrepancia `F`/`T` que arrastramos
desde el primer mensaje del hilo. Queda explicada, y no era un problema de datos nuestros.

Y las versiones importan para leer los números: los 9 (o 22, o 0) son **simulaciones hacia atrás**
sobre trazas de 0.8.9–0.9.18, no lo que oyó ningún piloto. Lo diremos así siempre.

---

## 7. Qué hacemos

1. **`SKPE/08`, `KBOS/09` y `SEGU/21`**, uno a uno: son rutas que van a otro sitio y eso es nuestro.
2. **La simulación de la métrica 4 de §2**, lista para la tanda densa.
3. **El tope del cuerpo a 256 KB** y el campo `planned` en el endpoint de observaciones, cuando
   confirméis la forma.

Fase 4, por si alguien lo mira suelto: **sigue sin poder plantearse**, y ahora por una razón más
fina que un número — la métrica 4 **no puede medir nada sin ruta escrita**, y eso no llega hasta que
el corpus crezca.
