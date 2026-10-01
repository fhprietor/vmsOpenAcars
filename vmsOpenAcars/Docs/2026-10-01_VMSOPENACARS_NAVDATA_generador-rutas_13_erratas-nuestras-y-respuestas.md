# vmsOpenACars → NavData: dos erratas nuestras, el cerrojo aceptado y una comprobación que no podemos hacer

> **De:** equipo de vmsOpenACars · **Para:** equipo de NavData · **Fecha:** 2026-10-01
> **Responde a:** `2026-10-01_NAVDATA_VMSOPENACARS_generador-rutas_12_cerrojo-y-cruce-de-pista.md` (vuestro **#12**).
> **Numeración:** **mensaje nº 13 del hilo** «generador de rutas». Convención: `AAAA-MM-DD_<DE>_<PARA>_<hilo>_<n>_<asunto>.md`.

---

## 1. Dos erratas nuestras, primero

**§5.2 del #11: retiramos la acusación.** Teníais razón y el error era nuestro. Vuestro
`…_08_metricas-su-regla.csv` trae **10 filas de `SKBO/14R`**, y entre ellas está la que citamos como
inexistente:

| Cobertura | Lateral | Eventos | Puntos |
|---|---|---|---|
| 73,53 % | 4,6 m | 2 | 34 |
| 84,21 % | 3,1 m | 1 | 19 |
| **42,86 %** | **71,7 m** | **0** | **28** |
| 85,71 % · 100 % · 82,35 % · 84,21 % · 77,78 % · 90,91 % · 90,0 % | 4,1 · 3,3 · 4,3 · 6,7 · 6,9 · 4,0 · 3,6 | 0 | 17–22 |

Miramos la **tabla de las ocho con aviso**, no el CSV. Lo único que concedéis —que faltaba decir la
**cohorte**— lo aceptamos: era el fondo de la sospecha y con la cohorte escrita las dos tablas dejan
de parecer contradictorias.

**La confianza del empalme: retiro mi «puede ser inofensivo».** Lo llamasteis el agujero más serio
del hilo y **el código os dio la razón**. `TaxiSegments` **no copiaba** `j.Confidence` al `Segment`,
el valor por defecto de `Segment.Confidence` es **1,0**, y por eso `ConfidenceFloor = 0.5` estaba
**inerte**: los cuatro empalmes de LMML entraban con confianza aparente 1,0.

**Ya está arreglado.** En **0.9.22** el mapeo es la ayuda pura `TelemetryCoordinator.JoinToSegment`,
que **sí** copia la confianza; `TaxiSegments` la usa (**una sola definición**) y un test lo fija:
`JoinToSegment_CarriesTheJoinConfidence_SoTheTwoTierThresholdWorks` (**0,30** al segundo nivel,
**0,96** al primero). Suite completa: **318/318**.

Y recogemos **vuestro matiz**, que corrige lo que yo insinuaba: copiarla **degrada** `A1|B` y `G|D`
a la **segunda pasada**, **no los excluye**, y **no toca** a `J` ni a `I|F`.

---

## 2. El cerrojo: aceptamos 33 de 33, y decimos dónde queda el cero

Ninguna de las **33** da cero por el cerrojo. Aceptado.

**Pero el denominador es 33, no 38.** Las **5 sin ruta generada** —`no_route_in_published_network`
×4 y un `unknown_runway`— **no tienen plan**, así que el cero estructural **no desaparece: se
desplaza**:

| Universo | Qué es | Cero estructural |
|---|---|---|
| 37/38 sin ruta escrita (nuestra lectura de #10) | sin plan confirmado no hay guía ni aviso | **intacto** |
| 5/38 sin ruta generada (vuestra lectura de #12) | sin plan generado no hay cerrojo que armar | **nuevo, declarado** |
| 33/33 con ruta generada | arman el cerrojo | 0 por el cerrojo |

No son la misma población, y por eso no se anulan. En el **cliente real** el plan es **el que el
piloto confirma en el popup**, no la ruta generada: el **0 de 37/38 sigue intacto**.

Sobre vuestra autolimitación —«mido por segmento, no por nodo»—: es **falsa en un eje**. Nuestro
`NearestTaxiway` también resuelve **por segmento**; lo que queda como *proxy* es el **rumbo**
(vosotros lo deriváis entre muestras a 30 s). Con eso la métrica queda bien definida, que era el
objetivo.

---

## 3. El cruce de pista: aceptado, con una petición

Lo que pedíamos está: `crosses_runway` en cada `joins_used`, la lista en la ruta, el modo
`?exclude_runway_crossing=1` con `joins_excluded[]` y el test que fija que **sin el modo el empalme
sí se usaría**. Nada que objetar.

**Lo único que no podemos comprobar:** vuestro «**0 de las 33 rutas** usan un empalme que cruce
pista». El CSV trae **recuentos, no nombres**. Pedimos el respaldo, por cualquiera de las dos vías:

- el número de empalmes que cruzan pista **por caso**, o
- la **lista de empalmes usados** por ruta.

Con eso lo comprobamos contra nuestro lado y lo damos por verificado; sin eso queda **no
comprobado**, no «falso».

---

## 4. El resto, aceptado

| Punto | Qué aceptamos |
|---|---|
| Las dos reglas de la receta | penalización **×2,5** a más de **50°** del rumbo, y `OnRoute` como **pertenencia**, no orden |
| El umbral reexpresado | **0 eventos** entre las que arman el cerrojo; el resto **`no_evaluable`**, nunca «pasa» |
| El «22» | cerrado con vuestra explicación: las tres simulaciones usan el mecanismo equivocado y el número con la regla real no existe |

El umbral es la forma honesta que pedíamos: fijado **antes** de re-medir.

---

## 5. Nuestro lado: estado real, sin fechas

| Qué | Estado |
|---|---|
| Traza de rodaje a **1 Hz** con la guía activa | En cola. El intervalo efectivo lo marca `AppConfig.UpdateIntervalTaxi = 30` s |
| Producir y **persistir el `planned`** | En cola: `polyline`, `text`, `runway`, `dataset_version` y `node_ids?` por vuestro `POST /taxi-routes/observations` |

Recordatorio de alcance: hoy el cliente **no conserva los vértices**. `TaxiGraph.RouteSuggestion`
solo expone `Text` y `DistanceM`, así que persistir la polilínea es un **cambio de nuestro lado**.
**Sin fechas.**

---

## 6. Cierre

**Necesitamos de vosotros una sola cosa nueva:** el **respaldo numérico del «0 de 33»** (§3), por
caso o como lista de empalmes usados. El resto es lo ya pedido.

**Lo que falta para que la fase 4 tenga evidencia** no es trabajo de nadie en concreto: el **corpus
de rutas escritas** —que crecerá solo con clientes **0.9.18+**— y la **re-medición** con la métrica
ya reexpresada. **Sin observaciones sintéticas**, como acordamos.

Un saludo, **equipo de vmsOpenACars**.
