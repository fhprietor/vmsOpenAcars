# Estado del bloque del FLARE — 08/10/2026

Traspaso para continuar el tema del flare en un hilo nuevo. **No es un resumen de la
conversación**: la parte entregada está en `Docs/CHANGELOG.md` (secciones 0.9.31 a 0.9.36, con
la causa raíz y las cifras) y aquí solo se apunta. **Lo que sí vive únicamente aquí es la deuda
de verificación**, que es lo primero que se pierde al cambiar de sesión.

Version al escribir esto: **v0.9.36** · suite **655/655** · idiomas simetricos **460 claves**.

---

## 1. Qué está entregado, y dónde leerlo

| Versión | Qué trajo | Dónde |
|---|---|---|
| 0.9.31 | Closeup del toque en el perfil vertical (pista, umbral, punto de toque, bandas TDZ) | CHANGELOG 0.9.31 |
| 0.9.32 | Traza fina del flare a **10 Hz** (`flare_track`), ventana propia (`FlareAnalysisForm`), eje X del closeup en pies enteros | CHANGELOG 0.9.32 |
| 0.9.33 | La **Y del closeup se autoescala** al tramo visible; el closeup pinta la traza fina cuando existe (la de 2 s como respaldo, avisando cuál usa) | CHANGELOG 0.9.33 |
| 0.9.34 | **La traza fina no se guardaba en ningún vuelo** (se copiaba después del reset): se copian las dos trazas antes del `await` | CHANGELOG 0.9.34 |
| 0.9.35 | **Flaps** y **corte de potencia (N1)** en el análisis; `ChartAxisSafety` para que un gráfico no reviente al pintarse | CHANGELOG 0.9.35 |
| 0.9.36 | **Cabecera de datos** en el gráfico para compartirlo + botón **PNG**; identidad de aeronave **única** para el log y el gráfico | CHANGELOG 0.9.36 |

Módulos del bloque, para situarse rápido: `Helpers/FlareCapturePolicy.cs`, `FlareSampleBuffer`,
`FlareChartLayout`, `CloseupAxis`, `CloseupVerticalAxis`, `CloseupTrackSource`,
`TouchdownCloseupGeometry`, `TouchdownZonePolicy`, `ThresholdToTouchdown`, `PowerCut`,
`FlapSetting`, `FlapTrackSummary`, `LandingHeader`, `AircraftIdentity`, `ChartAxisSafety`;
formularios `UI/Forms/FlareAnalysisForm.cs` y el closeup de `LandingAnalysisForm.cs`;
persistencia en `Services/LandingLogService.cs` (tabla `flare_track` + columnas nuevas en `flights`).

---

## 2. Deuda de verificación (esto es lo importante)

**Nada de este bloque se ha visto funcionar con filas reales de `flare_track`.** La tabla estuvo
**vacía en todos los vuelos** desde que existe (0.9.32–0.9.33) por el fallo del orden de copiado
que arregló la 0.9.34, y **desde entonces no se ha volado**. Consecuencia directa: los tests usan
el **perfil real del vuelo 41 remuestreado a 10 Hz** y los valores que **documenta el offset**, no
una traza medida en vuelo.

Sin verificar, una a una:

1. **La cadencia real de 10 Hz.** El diseño muestrea sobre la lectura de 20 Hz de FSUIPC (no añade
   lecturas), pero el retardo del temporizador de 50 ms con el simulador cargado **no se ha medido**.
2. **El camino completo de `SendPirep`** (snapshot antes del `await` → `SaveFlight` + `SaveFlareTrack`).
   Lo cubre **la lectura del código y un test de la columna**, no un test automático: necesita la
   API, el `FlightManager` y la base. **Pendiente de un vuelo real.**
3. **El radioaltímetro** (`0x31E4`) según addon: no comprobado. Un 0 se guarda como **NULL**.
4. **LTOW** (`FSUIPC.PayloadServices.GrossWeightLbs`, lb, `RefreshData()` una vez por vuelo):
   **compila y degrada a NULL, pero no se le ha visto devolver un peso real**. Si sale vacío en el
   próximo vuelo, es esto — y **no se deriva del plan** para rellenarlo.
5. **La captura del flare**: el respaldo a **300 ft AGL** (solo cuando no hay distancia) y el
   timeout de 45 s no se han ejercitado. El armado principal es a **1.500 ft del umbral** (con
   1.000 ft AGL se habría armado *antes* del umbral: el vuelo 41 lo habría hecho a 1.653,8 ft).
6. **La distancia de toma** sale de la última muestra en tierra: **±1 muestra ≈ 25 ft a 150 kt**.
7. **Flaps**: lo que se guarda es el **porcentaje del recorrido del mando** (`0x0BDC`, 0–16383 →
   0–100), que **no es un detent**. El detent real (`0x0BFC` → `flare_track.flaps_index`) se
   persiste desde la 0.9.35, pero:
   - la regla que **descarta el `0`** cuando el mando está desplegado (`0x0BFC = 0` se interpreta
     como «el addon no escribe el offset») es una **inferencia razonada**, no una medición;
   - las bandas por familia vienen de los umbrales de `FsuipcService.DecodeFlapsByFamily`, **nunca
     contrastadas con una traza real**.
8. **Potencia**: el N1 es de verdad (`0x2000`/`0x2100`, en por ciento). El **criterio del corte**
   —pico del N1 medio + primera caída sostenida ≥5 puntos durante ≥0,3 s, relativa al pico— es una
   **decisión de diseño**, no un valor validado: puede quedar corto o largo según el avión.
9. **`ChartAxisSafety`**: la excepción `Axis Object - Auto interval does not have proper value`
   **no se consiguió reproducir** (19 sondas). Lo confirmado es que `DrawToBitmap` sobre un `Chart`
   era el **único** camino del código que pasaba por `WM_PRINTCLIENT`, que es la pila que se vio.
   Si vuelve a aparecer, **hay otro camino** y hay que buscarlo.
10. **La identidad de aeronave**: validada con los dos PIREPs reales (`A319 [ToLiss]`,
    `B38M [iFly]`) y el caso PMDG (`B77L [PMDG]`). Los **títulos exactos** de ToLiss e iFly se
    **reconstruyeron** (el log solo guarda tipo y addon), así que las pruebas de esos dos casos
    usan la forma real del título, no una captura literal.
11. **`flights.aircraft_title` / `aircraft_model`**: columnas nuevas. El addon sale del **título**,
    así que un avión que no lo publique no mostrará addon (es el comportamiento buscado).

---

## 3. Abiertos del tema (decisiones, no trabajo mecánico)

- **El bloque de texto no sale en el PNG** (es una franja del formulario): para que la imagen se
  entienda sola hay una **identidad compacta en el título del gráfico**. Falta decidir si el bloque
  debe dibujarse **dentro** de la imagen.
- **El pitch** se captura, pero `FlareLayout.HasPitch` puede faltar y el área sale con **SIN DATOS**:
  decidir si se pinta o se retira el área.
- **Ventana del flare vs. closeup**: hoy enseñan lo mismo en dos sitios. Decidir el reparto.
- **La línea técnica** de `TouchdownCloseupGeometry.Summary` mide **1 505 px** y **ya se cortaba
  antes** de todo esto: está **documentada en un test, no arreglada**.
- **`FsuipcService.GetAircraftDeveloper()`** quedó **sin uso** en el cliente (delega en el helper):
  se dejó para no ampliar el cambio.
- **Métricas que se propusieron y NO se implementaron**: tiempo desde 50 ft, recorrido del flare y
  tasa de descenso en el flare. Salen de la misma traza.

---

## 4. Reglas que este bloque ya rompió una vez (no repetirlas)

- **Copiar los buffers ANTES del `await`** en `SendPirep`. Es el fallo de la 0.9.32–0.9.33: el
  reset vacía el buffer del flare y el guardado va después.
- **Un gráfico no puede reventar al pintarse**, y **cualquier volcado a PNG usa `Chart.SaveImage`,
  nunca `DrawToBitmap`** (eso abrió una ventana de excepción en la pantalla del mantenedor).
- **Degradar sin datos**: `NULL` y la línea se omite; nunca un cero que parezca una medida.
- **Nada de etiquetas inventadas**: si no se puede deducir, se enseña el número en crudo y, si es
  aproximado, se marca (`≈`).
- Antes de dar algo por hecho: **build Debug y Release + la suite completa**, y **ningún diálogo en
  pantalla** durante las pruebas.
