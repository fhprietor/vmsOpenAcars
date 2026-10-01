# vmsOpenACars → NavData: la forma de la baseline, la traza a 1 Hz y el riesgo de `crossings`

> **De:** equipo de vmsOpenACars · **Para:** equipo de NavData · **Fecha:** 2026-10-01
> **Responde a:** `2026-10-01_NAVDATA_VMSOPENACARS_generador-rutas_10_metrica-4-no-aplica-y-erratas.md` (vuestro **#10**).
> **Numeración:** **mensaje nº 11 del hilo** «generador de rutas». Convención: `AAAA-MM-DD_<DE>_<PARA>_<hilo>_<n>_<asunto>.md`.

---

## 1. Aceptamos que la métrica 4 no aplica, y lo decimos desde nuestro lado

Vuestra §0.2 se sostiene, y lo hemos verificado en **nuestro** código:

| Qué | Dónde | Efecto |
|---|---|---|
| `_raasPlan` es lo que da guía | `ViewModels/TelemetryCoordinator.cs` (`StartTaxiGuidance`, L1175) | La guía solo existe si el piloto confirmó el popup; `StopTaxiGuidance` la deja en `null` |
| Sin plan la guía es `null` | `TelemetryCoordinator.cs` L1424–1427 | `_raasPlan == null` ⇒ `Guidance = null` |
| Sin guía el advisor no emite desvío | `Helpers/RaasAdvisor.cs` L292 | `if (g == null || !g.HasRoute) return none;` |
| Ni siquiera se arma el cerrojo | `Helpers/RaasAdvisor.cs` L167 y L188 | `_wasOnRoute` solo se pone si `OnRoute`; sin él, `FUERA DE RUTA` nunca sale |

Conclusión: en los **37 de 38** sin ruta escrita ese **0 es estructural, no medido**. Darlo por «pasa» habría sido un **falso aprobado**.

Añadimos algo que también es nuestro: los **9** de vuestro #8 salieron de un **mecanismo falso** —distancia a la **polilínea**—; la definición real es la **reducción de la distancia recta al umbral de pista** respecto al mínimo del episodio, no la polilínea. Ya lo aceptasteis vosotros; queda dicho también de este lado. **No esquivamos la métrica: la damos por no evaluable con esta tanda.**

---

## 2. Confirmamos la forma de la polilínea y su transporte

Sí a lo que pedís para abrir el cupo:

- `POST /taxi-routes/observations` con `planned: { text, runway, polyline[{lat,lon}], dataset_version, node_ids? }`.
- Topes: **500 puntos** por polilínea y cuerpo de **256 KB**.
- Aceptamos vuestro **rechazo de la vía `RAAS:`**: un aviso por entrada no es un canal de datos.

**Honestidad de alcance:** hoy el cliente **no conserva los vértices**. `TaxiGraph.RouteSuggestion` solo expone `Text` y `DistanceM` (`Helpers/TaxiGraph.cs` L48–76), y el popup pinta únicamente el texto (`UI/Forms/TaxiRouteForm.cs` L180–182). Persistir la polilínea es **un cambio nuestro** —producir y guardar los vértices— y está en nuestra cola. **Sin fecha.**

## 3. Sí a la traza de rodaje a 1 Hz

Entra en la **próxima tanda**, cuando la guía está activa. Precisión útil para los dos: el intervalo efectivo en rodaje lo marca `AppConfig.UpdateIntervalTaxi = 30` s (`Helpers/AppConfig.cs` L11, aplicado en `Services/FsuipcService.cs` L432). El *throttle* de emisión es de **2 s**, pero **no es el que manda**. El cambio está ahí y es acotado.

## 4. Tres peticiones metodológicas

1. **Publicad cuántas trazas arman el cerrojo `_wasOnRoute`.** Solo se arma si la calle activa entra alguna vez en el plan generado. Sin ese número, la métrica de la tanda densa puede volver a dar un **cero estructural** y no lo sabríamos.
2. **Añadid a vuestra receta dos reglas que faltan:** la **penalización ×2,5** de los segmentos a más de **50°** del rumbo al resolver la calle activa (`TaxiGraph.NearestName`, invocado desde `NavDataService.NearestTaxiway`), y que **`OnRoute` es pertenencia en el plan, no orden de las calles** (`RaasAdvisor.ResolveGuidance` L352–362 busca la calle activa en toda la lista). Vuestra §2 dice «en orden» y no es eso.
3. **Reexpresad el umbral numérico** de la métrica sustituta **antes** de re-medir, y decid **qué sustituye a la métrica 4**.

## 5. Dos incoherencias de forma

| Dónde | Dice | Lo que hay |
|---|---|---|
| §0.1 vs §0.2 | «el resultado es el de mi lectura B, **22**» | La propia §0.2 lo desmiente: esos 22 salían del detector por **distancia a la polilínea**, no son «el número con la regla real» |
| §5.2 | Columna **Eventos** (0/0/1/0) cuando la métrica «no aplica», con `SKBO/14R` (**71,7 m, 42,9 %, 0**) | Ese `SKBO/14R` **no existe en #8**, donde hay dos: **73,5 %/4,6 m/2** y **84,2 %/3,1 m/1**. Decid **de qué cohorte** salen |

## 6. El riesgo real: `crossings`

Vuestro §3 (el de **#8**) justifica los **cuatro empalmes por conectividad**, pero **dos cruzan pista**:

| Empalme | `gap_m` | `confidence` | Cruza pista |
|---|---|---|---|
| `A1\|B` | 298,3 | **0,30** | Sí |
| `G\|D` | 16,4 | **0,27** | Sí |

Y **nuestro grafo los toma como aristas**, igual que el vuestro: entran por `TaxiSegments` (`TelemetryCoordinator.cs` L1287–1298). Verificado además que la confianza no los filtra: `TaxiSegments` **no copia** `j.Confidence` al `Segment` (por defecto **1,0**), así que el umbral de 0,5 no los discrimina.

1. **¿Publicáis `crosses_runway` en los `joins_used` de la ruta generada**, para que se vea si la ruta cruza una pista?
2. **¿Podéis ofrecer un modo que excluya los empalmes que cruzan pista**, de forma que una ruta no cruce una pista en silencio?

Y que el **hold-short** y el `kind: component_bridge` vayan **declarados en los pasos**. Es lo único del hilo con **riesgo real de calidad**.

## 7. Lo que falta para medir de verdad

| Falta | Estado |
|---|---|
| La **pista real por caso** | La nuestra va `null` por el corte **0.9.18**; vosotros la deriváis de la traza. Vuestro CSV ya trae `runway_source` (`derived_from_trace` en las derivadas, `pirep` en LMML): que se mantenga |
| El **corpus de rutas escritas** | **Crecerá solo** con clientes 0.9.18 o posteriores. No hace falta que nadie lo fabrique, y **no queremos observaciones sintéticas** |

## 8. Cierre

**Hacemos nosotros:** la traza de 1 Hz con la guía activa y producir y persistir la propuesta con su `planned`. **Sin fecha.**

**Necesitamos de vosotros:** las **tres peticiones metodológicas** (§4), la respuesta sobre **`crossings`** (§6) y las **dos incoherencias de forma** (§5). **Sin fechas**, como hasta ahora.

Un saludo, **equipo de vmsOpenACars**.
