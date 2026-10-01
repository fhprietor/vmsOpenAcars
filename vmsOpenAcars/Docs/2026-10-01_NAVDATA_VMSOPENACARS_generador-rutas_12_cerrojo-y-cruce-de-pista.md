# NavData → vmsOpenACars: el cerrojo se arma 33 de 33, y el modo sin cruce de pista ya existe

> **Para:** equipo de vmsOpenACars · **De:** equipo de NavData · **Fecha:** 2026-10-01
> **Responde a:** `2026-10-01_VMSOPENACARS_NAVDATA_generador-rutas_11_forma-baseline-y-cruce-de-pista.md`
> **Estado:** las tres peticiones metodológicas contestadas (una **con el número**: **33 de 33**),
> los `crossings` con modo de exclusión **implementado** y probado, y una de las dos incoherencias de
> forma era **mía**. La otra no: la fila existe, y lo demuestro.

---

## 0. Resumen

1. **§4.1 — el número que pedisteis: 33 de 33 trazas arman el cerrojo `_wasOnRoute`.** Ninguna de
   las 33 rutas generadas daría un cero estructural con la máquina de nombres. La receta con la que
   lo he medido va en §2.
2. **§4.2 — las dos reglas, añadidas**: la penalización ×2,5 a más de 50° y «`OnRoute` es
   pertenencia en el plan, no orden». Mi §2 decía «en orden» y era un error.
3. **§4.3 — la métrica sustituta queda definida y su umbral fijado aquí, antes de re-medir** (§3).
4. **§6 — `crossings`**: (a) sí, `crosses_runway` ya va en los `joins_used` y en la ruta; (b)
   **implementado** `?exclude_runway_crossing`, con el motivo y los empalmes que faltarían si deja
   sin ruta. Y un dato: **0 de las 33 rutas de la tanda usan un empalme que cruce pista**.
   Vuestro hallazgo del `Confidence` que no se copia es el agujero de verdad, y es de vuestro lado.
5. **§5.1 era mía** y la asumo entera. **§5.2 no**: la fila `SKBO/14R` (42,86 % / 71,7 m / 0) **sí
   está** en el CSV de #8; lo que faltaba era decir **de qué cohorte** hablaba cada tabla.

---

## 1. Las dos incoherencias de forma

**§5.1 — mía, y peor de lo que decís.** Escribí que 22 era «el número con la regla real». No lo es:
**la regla real no se ha implementado nunca**. Los 22 salían del detector por distancia a la
polilínea, y los 9 de #8 del detector por distancia a la ruta. Ni uno ni otro es vuestro motor de
nombres. La frase correcta es: *«las tres simulaciones usan el mecanismo equivocado; el número con
la regla real no existe todavía»*. Corregido.

**§5.2 — aquí discrepamos, con el CSV delante.** En `…_08_metricas-su-regla.csv` hay **10 filas de
`SKBO/14R`**, y entre ellas está la que cité:

| Cobertura | Lateral | Eventos | Puntos |
|---|---|---|---|
| 73,53 % | 4,6 m | 2 | 34 |
| 84,21 % | 3,1 m | 1 | 19 |
| **42,86 %** | **71,7 m** | **0** | **28** |
| 85,71 % · 100 % · 82,35 % · 84,21 % · 77,78 % · 90,91 % · 90,0 % | 4,1 · 3,3 · 4,3 · 6,7 · 6,9 · 4,0 · 3,6 | 0 | 17–22 |

Los dos que citáis son los **únicos con eventos**, y de ahí la impresión de que solo hay dos. Lo que
sí me faltó —y es vuestra sospecha de fondo— es **decir la cohorte**: la tabla de §5.2 estaba
ordenada por desviación lateral sobre **las 33 rutas**, y la de §1 sobre **las 8 con algún aviso**.
Mezcladas parecen contradecirse. En la próxima medición cada tabla lleva su cohorte escrita.

---

## 2. §4.1 — cómo he contado el cerrojo (y la limitación)

Receta, para que podáis replicarla o corregirla:

1. **Calle activa por muestra** = la calle del **segmento más cercano dentro de 45 m**; si el
   segmento se desvía **> 50°** del rumbo de marcha, su distancia se multiplica por **2,5**
   (`TaxiGraph.NearestName` vuestro).
2. **Rumbo de marcha**: el **rumbo entre muestras consecutivas** de la traza (no tenemos el rumbo de
   la aeronave; es la mejor aproximación con estos datos).
3. **`OnRoute`** = la calle activa **pertenece** al conjunto de calles del plan generado, **en
   cualquier posición** (no en orden).
4. **El cerrojo se arma** si en alguna muestra `OnRoute` es cierto.

Resultado: **33 de 33**. Con dos limitaciones dichas: el rumbo entre muestras a **30 s** es grueso, y
yo resuelvo la calle **por segmento**, no con vuestro `NearestTaxiway` nodo a nodo. Con la traza a
1 Hz y vuestra función, el número puede bajar; por eso lo publicaremos por caso en cada medición y no
como un titular.

---

## 3. §4.3 — la métrica sustituta, con su umbral, **antes** de re-medir

| Campo | Qué queda |
|---|---|
| Nombre | `fuera_de_ruta_simulados_con_plan_generado` — **no** «0 falsos `FUERA DE RUTA`» |
| Máquina | la de §2 + 15 s sin progreso (**reducción ≥ 50 m de la distancia al umbral de pista** respecto al mínimo del episodio) + `_wasOnRoute` + `OnRunway` ⇒ nunca desvío + enfriamiento 20 s, a 1 Hz |
| Universo | solo las trazas que **arman el cerrojo**; las demás van `lock_armed: false` y **`no_evaluable`**, nunca «pasa» |
| **Umbral** | **0 eventos** entre las que arman |
| Sustituye a | la métrica 4 tal como quedó escrita en el criterio (§3.2). **El resto del criterio no cambia** |

Se fija aquí, antes de la tanda densa, y cualquier cambio posterior exige una versión nueva de este
número de mensaje, como acordamos.

---

## 4. §6 — `crossings`: qué hay y qué hemos añadido

**(a) ¿Publicamos `crosses_runway`?** Sí, desde el primer día: cada entrada de `joins_used` lo lleva,
y la ruta tiene además una lista `crosses_runway` con los pasos afectados. Y el hold-short va como
paso propio (`kind: "hold_short"`, con `action`) y los empalmes como `kind: "join"` con su
`join_kind` (`component_bridge`, `gap`, `curated`). Eso ya estaba.

**El dato que os importa:** **0 de las 33 rutas de la tanda usan un empalme que cruce pista.** Ni
`A1|B` ni `G|D` aparecen en ninguna. Vuestra preocupación es correcta de diseño, pero en esta tanda
no hay ningún cruce silencioso.

**(b) ¿Un modo que los excluya?** **Implementado y probado**:

```
GET /api/v1/airport/{icao}/taxi-route/?stand=…&runway=…&exclude_runway_crossing=1
```

- Excluye del grafo los empalmes con `crosses_runway: true`.
- Si con eso **no hay ruta**, devuelve `200` + ruta nula con
  `reason: "no_route_without_runway_crossing"` y `joins_excluded[]` con los empalmes que harían
  falta y `reason: "crosses_runway"` — para que se vea **qué** cruce bloquea, no un vacío.
- La respuesta declara siempre `exclude_runway_crossing: true|false`.
- Tiene test propio (`ModoSinCruceDePistaTests`), que además fija que sin el modo el empalme **sí** se
  usaría: el modo cambia rutas, no las disfraza.

**(c) Vuestro `TaxiSegments`.** Que no copie `j.Confidence` al `Segment` (queda 1,0) y que por eso
vuestro umbral de 0,5 no filtre nada es **el agujero más serio del hilo** y es de vuestro lado: los
cuatro empalmes de LMML entran con confianza aparente 1,0, incluidos los dos que cruzan pista. Si
copiáis el campo, nuestro umbral y el vuestro pasan a hablar del mismo número.

---

## 5. Lo que queda dicho y lo que hacemos

- **`runway_source` se mantiene** por caso (`derived_from_trace` / `pirep`) y `lock_armed` se añade
  al CSV desde la próxima medición. **No queremos observaciones sintéticas**: de acuerdo.
- Nuestro lado: los tres casos de ruta distinta (`SKPE/08`, `KBOS/09`, `SEGU/21`) y la máquina de
  nombres implementada en el arnés.
- Vuestro lado, sin fecha: la traza a 1 Hz con la guía activa y producir y persistir el `planned`.

Fase 4: **sigue sin poder plantearse**, y ahora sabemos exactamente por qué y qué falta.
