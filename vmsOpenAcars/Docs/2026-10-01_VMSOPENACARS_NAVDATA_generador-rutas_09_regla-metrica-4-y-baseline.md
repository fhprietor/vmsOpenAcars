# vmsOpenACars → NavData: la regla de verdad —con nuestra errata—, la traza a 1 Hz y la baseline (ii)

> **De:** equipo de vmsOpenACars · **Para:** equipo de NavData · **Fecha:** 2026-10-01
> **Responde a:** `…_08_metrica-4-cerrada.md` y su adjunto `…_08_metricas-su-regla.csv` (vuestro **#8**).
> **Numeración:** **mensaje nº 9 del hilo** «generador de rutas» (nuestros el 1, 4–5 y 7; vuestros el 2–3, 6 y 8). Convención: `AAAA-MM-DD_<DE>_<PARA>_<hilo>_<n>_<asunto>.md`.

## 1. Aceptamos el 9, y la primera corrección es nuestra

**9 eventos de `FUERA DE RUTA` en 33 rutas**, 8 con al menos uno, umbral 0: **la métrica 4 falla**, y es **cota inferior** por el muestreo a 30 s, como decís.

Simulasteis otra cosa en parte por culpa nuestra:

| Nuestra frase | La verdad, en el código |
|---|---|
| **#7 §2:** los 50 m son «respecto al **punto más cercano del episodio**» | la **reducción de la distancia recta al umbral de pista** respecto al mínimo del episodio |
| **#5 §4(3):** equiparamos el **radio de pavimento de 45 m** con una distancia a la polilínea | los 45 m resuelven **qué calle pisa el avión** (`TaxiGraph.OnTaxiwayM`); no son un umbral contra la polilínea |

Gracias por re-medir: destapasteis la ambigüedad.

## 2. La regla de verdad, punto por punto

Está en `Helpers/RaasAdvisor.cs` y `ViewModels/TelemetryCoordinator.cs`. El disparador **no es una distancia**:

1. **Comparación de nombres:** `ResolveGuidance` busca la **calle activa** en el plan; si no está, `OnRoute = false`. Sin calle activa —o sin ruta escrita— no hay `FUERA DE RUTA`.
2. **15 s** fuera de ruta (`OffRoutePersistSec`) **sin progreso de ≥50 m** (`OffRouteProgressM`): la **distancia recta al umbral de pista** (`DistanceToRunwayM`, de `ResolveRunwayThreshold`) frente al **mínimo del episodio**. Sin dato, manda la insistencia.
3. **En pista nunca hay desvío:** `OnRunway` → `RUTA COMPLETA` (una vez por guía).
4. **Cerrojo `_wasOnRoute` (0.9.19):** sin haber estado una vez dentro de la ruta no hay aviso.
5. **Enfriamiento 20 s con re-armado** y **1 Hz** (`RaasInterval`) sobre telemetría cruda. Hold-short y giros siguen en 150/40 m y 250/60 m.

| Constante | Valor | Símbolo | Desde |
|---|---|---|---|
| Persistencia fuera de ruta | **15 s** | `OffRoutePersistSec` | 0.9.15 |
| Progreso que reinicia | **50 m** | `OffRouteProgressM` | 0.9.15 |
| Enfriamiento / re-armado | **20 s** | `RepeatCooldownSec` | 0.9.14 |
| Cerrojo de entrada en ruta | — | `_wasOnRoute` | **0.9.19** |
| Radio de pavimento (calle activa) | **45 m** | `TaxiGraph.OnTaxiwayM` vía `NavDataService.NearestTaxiway` | **0.9.20** (antes **300 m**) |
| Evaluación | **1 Hz** | `RaasInterval` · TelemetryCoordinator.cs | 0.9.14 |

## 3. Versiones: los 9 son lo que diría el motor de hoy

Las trazas son de **0.8.9–0.9.16** (37 PIREPs, nuestro #7 §4) más el LMML de **0.9.18**: ninguna pasó por 0.9.19/0.9.20; los 9 son una **simulación hacia atrás**, no lo que oyó el piloto. Medido en LMML (`V15MObj3MxOdAZab`): **15 avisos** con el motor de entonces, **0** con 0.9.19 o posterior. Y el resolutor de calle activa usaba **300 m** hasta **0.9.20** —hoy 45 m—, que devolvía `F` a un puesto a 64,2 m de `F` y 262 m de `T`.

## 4. Sí a la traza a 1 Hz

Entra en **la próxima tanda**: **densa (1 Hz) con la guía de rodaje activa**, en **salida (`TaxiOut`) y llegada (`AfterLanding` + `TaxiIn`)**; hoy son **30 s** (`update_interval_taxi`, `update_interval_other`). Cambio **nuestro y acotado**: si el volumen os preocupa, lo limitamos a la guía activa.

## 5. Baseline (ii): confirmada — forma y alcance

Confirmamos la salida **(ii)**. Forma propuesta, cambiable:

| Campo | Contenido |
|---|---|
| `text` | la ruta propuesta, calles separadas por espacios |
| `runway` | la pista |
| `polyline` | la **polilínea ordenada** `[{lat, lon}, …]` del camino que usó el grafo |
| `dataset_version` | el `version`/hash del dataset con el que se generó |
| `node_ids` | **opcional**: `node_id` de los extremos |

Alcance, con el código delante: `Taxi Route` es hoy **texto libre** (`Helpers/PirepFields.cs`) y **`TaxiGraph.RouteSuggestion` solo devuelve `Text` y `DistanceM`**, sin vértices. Es un **cambio de cliente**: que el grafo devuelva la polilínea y un soporte por el que viaje —**campo nuevo** en `fields` del PIREP, **vuestro endpoint de observaciones** (ya admite `POST`, con `typed`/`traced`) o una entrada **`RAAS:`** por `/acars/logs`, que es por aviso y no preferimos—. **Pedimos visto bueno a la forma antes de cerrarla**; sin fecha.

## 6. Dos cosas de forma

| Dónde | Dice | En vuestro CSV |
|---|---|---|
| §0.1 y §4 | «37 de 38 … mediana **62 m**»; en §4, 36 filas con **69,6 m** | **36** filas con `runway_distance_m` < 300 m y mediana **67,95 m**; con los **92 m** de LMML, 37 filas y **69,6 m**. La mediana de **62 m** no sale de ahí |
| §1 vs §2 | §1 llama «ruta distinta» a **SKRG/01 (49,3 m)** | §2 y #6 dicen **SKRG/19 (58 m)**; CSV: SKRG/19, 57,8 m y **0** eventos; SKRG/01, 49,3 m y **1** |

## 7. Vuestros siete valores por defecto

Siguen abiertos y **afectan a lo que medimos**. Cerradlos o confirmad cuáles aplican:

| # | Punto | Vuestro defecto |
|---|---|---|
| 3 | Sentido de marcha | bidireccional; `?one_way=1` en v2 |
| 4 | Nivel de detalle | **los tres**: pasos con giro, **polilínea** y `node_id` |
| 5 | Unidades y referencia | metros y rumbo **magnético** |
| 6b | Entrada a pista | alternativas válidas y la más barata; `?entry=` |
| 8 | Reencaminamiento | sí en v1, `?lat=&lon=` |
| 9 | Caché | TTL 24 h + `version` |
| 12 | Plan y slug | `taxi_route` |

El **#4** nos da la forma de la polilínea; el **#6b**, contra qué ruta comparamos; el **#5** toca nuestra geometría.

## 8. Cierre

**Hacemos nosotros:** traza densa a 1 Hz con la guía activa y persistir la ruta propuesta.

**Necesitamos:** visto bueno a la **forma de la polilínea** (§5), la **mediana de 62 m** y la **inconsistencia SKRG/01 vs SKRG/19** (§6) y los **siete valores por defecto** (§7).

Un saludo, **equipo de vmsOpenACars**.
