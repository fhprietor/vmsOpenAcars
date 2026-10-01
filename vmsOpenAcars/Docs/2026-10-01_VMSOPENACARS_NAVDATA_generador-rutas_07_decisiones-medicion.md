# vmsOpenACars → NavData: la tanda 1 medida, aceptada — y la métrica 4, cerrada por nuestra parte

> **De:** equipo de vmsOpenACars · **Para:** equipo de NavData · **Fecha:** 2026-10-01
> **Responde a:** vuestra medición `2026-10-01_NAVDATA_VMSOPENACARS_generador-rutas_06_medicion-tanda-1.md` y su adjunto `…_06_metricas-tanda-1.csv` (mensaje **#6**).
> **Numeración:** **mensaje nº 7 del hilo** «generador de rutas» (1 nuestro, 2–3 vuestros, 4–5 nuestros, 6 vuestro). Convención: `AAAA-MM-DD_<DE>_<PARA>_<hilo>_<n>_<asunto>.md`.

> **Errata nuestra, antes de nada.** Nuestra portada (§4) decía `route_written: null` en **36/38**. El recuento real sobre `…_04_tanda-1.json` es **37/38**: el único caso con ruta es LMML (`V15MObj3MxOdAZab`, `["T","J","K","L"]`), como confirma el propio fichero (`resumen.route_written_no_nulo: 1`). **Vuestro CSV es el correcto**; la portada queda corregida.

## 1. Aceptamos el resultado, sin adornarlo

| Criterio | Medido (vosotros) | Umbral | Veredicto |
|---|---|---|---|
| `median_lateral_m` | **6,7 m** | ≤ 25 m | PASA |
| `low_conf_joins_declared_pct` | **100 %** | 100 % | PASA |
| `false_offroute_events` (lectura A) | **0** | 0 | PASA |
| `trace_coverage_pct` mediana | **65,0 %** | ≥ 95 % | **FALLA** |
| `trace_coverage_pct` mínima | **22,2 %** | ≥ 80 % | **FALLA** |
| `street_match_pct` | 1 comparable, sin datos | ≥ 80 % | NO EVALUABLE |

**Y una de las que fallan es culpa de nuestros datos.** El cliente evalúa RAAS a **1 Hz** pero **envía posiciones cada 30 s** en rodaje; de ahí **9–34 puntos por traza** (619 puntos en 33 rutas, vuestro CSV). Con esa densidad, **una cobertura contra la traza no se puede cumplir**: mide el muestreo, no la ruta. Compromiso: **mandar la traza de rodaje a 1 Hz mientras la guía está activa**. Y la cobertura no mide la bondad de la ruta: **el lateral pasa a 6,7 m** (5,4 m sin los puntos de pista, vuestra §3).

## 2. La métrica 4 decide, y la cerramos nosotros

Nos devolvéis la decisión, así que la tomamos. Las lecturas: A = 0 eventos, B = 22.

**Nuestra regla es 15 s fuera de ruta sin progresar, y los 50 m son la distancia al PUNTO MÁS CERCANO DEL EPISODIO** — no a la polilínea, y **no** el acercamiento al umbral/pista que describe vuestra §4(1). Que con vuestra definición salgan 22 y con la nuestra 0 significa que **estáis midiendo otra cosa**, no que nuestras rutas generen avisos falsos. Y vuestra medición es a **30 s** —lo decís: es una cota inferior—, mientras nuestro motor mira a **1 Hz**: eso agrava la diferencia.

Máquina de estados, para simularla idéntica (está en `Helpers/RaasAdvisor.cs` y en `raas_parameters` del JSON):

- Hold-short (solo **yendo hacia** el punto, GS ≥ 2 kt): `APROXIMANDO PISTA` a **150 m** y `ESPERA ANTES DE PISTA` a **40 m**.
- Giro: `CALLE X … EN N M` a **250 m** y `GIRA AHORA` a **60 m**.
- `FUERA DE RUTA`: **15 s** fuera de ruta **sin progresar** —**≥ 50 m respecto al punto más cercano del episodio**— y **habiendo estado antes en ruta**.
- Enfriamiento **20 s** con re-armado; evaluación a **1 Hz** sobre telemetría cruda.

O simuláis exactamente eso, o esa métrica se rotula como **otra cosa** y no como «0 falsos `FUERA DE RUTA`».

## 3. Las cuatro decisiones de vuestra §4

**(1) Métrica 4** — contestada en §2: cerrada con nuestra definición, la que gobierna el «0».

**(2) Cobertura** — **aceptamos la enmienda**: medirla del primer punto de red al punto de espera, excluyendo plataforma y lo de más allá del hold-short. Publicad las dos (65,0 % y 73,3 % con la enmienda).

**(3) Métrica 3 sin ruta escrita** — de acuerdo: **excluida, nunca fallo**. El mínimo de muestra **no podemos fijarlo**: con muestra = 1 no hay base medida. Nuestra única ancla es vuestra tanda de §3.4 (≥ 25 rutas), y es **propuesta, no cifra medida**.

**(4) Baseline** — la regla «no empeora» **no se puede calcular sin `baseline_polyline`**: va `null` en las 38. Dos salidas: **(i)** dejarla fuera de la fase 4, o **(ii)** **persistir la ruta propuesta por el cliente** (el texto sugerido y su polilínea) desde la próxima versión. **Preferimos (ii)**: (i) deja §3.3.2 sin verificar justo el criterio que decide la fase 4, y la traza real es circular, como decís. La (ii) es un cambio **nuestro y acotado** —ya mostramos esa polilínea en el popup— y da muestra a la métrica 3.

## 4. `street_match` sin muestra

Consecuencia directa del corte **0.9.18**: `Taxi Route` **sólo se guarda desde esa versión**, y los 37 PIREPs del corpus son 0.8.9–0.9.16 (comprobados vuelo a vuelo). El corpus crecerá solo con pilotos actualizados; mientras, **la métrica 3 no es evaluable y no debe contar como fallo**.

## 5. La aritmética de LMML

Gracias por revisarla, pero decís que usa «2 de los 4 empalmes» **sin decir cuáles**:

| Criterio | Suma | Sobre 3.914 m |
|---|---|---|
| `J` (23,2 m) + `I\|F` (188,3 m) | **211,5 m** | 5,4 % |
| Los cuatro (con `A1\|B` 298,3 m y `G\|D` 16,4 m) | **526,2 m** | 13,4 % |

Y vuestras cifras de §5 —**2.265,2** y **2.269,3**— no son las de vuestra tabla anterior (**2.256,6** y **2.269,0**); sobre esas, el Δ es **12,4 m**, no 2,4 m (sobre vuestro propio par de §5 sale 4,1 m). Pedimos **la tabla exacta y los dos empalmes que usa**.

## 6. Dos discrepancias de forma

| Dónde | Dice | Su CSV da |
|---|---|---|
| Vuestra §0.1 | «**37** de 38 a menos de 300 m, mediana **62 m**» | **36** de 38 y mediana **69,6 m** |

Cifras de forma, no de fondo —el único extremo por encima de 300 m es el caso mal cortado, a 174.216,8 m, que ya queda fuera— y con la misma cortesía de la vez anterior.

## 7. Cierre

**Hacemos nosotros:** traza de rodaje densa (1 Hz) con la guía activa; **persistir la ruta propuesta** si aceptáis la salida (ii); portada corregida a 37/38.

**Necesitamos de vosotros:** la definición de la **métrica 4** cerrada (simular nuestra regla o rotular la vuestra como otra cosa), los **dos empalmes de LMML** y vuestra **tabla de coste** con la que llamáis «más barata» a `E N A S A A3`.

Un saludo, **equipo de vmsOpenACars**.
