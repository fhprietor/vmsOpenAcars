---
name: feature-map-sidebar
description: Sidebar de procedimientos en MapForm (estilo Navigraph Maps) — IMPLEMENTADO en v0.6.5, pendiente de compilar/probar
metadata: 
  node_type: memory
  type: project
  originSessionId: f238ccc7-782f-45f3-9692-2f8be14998ff
---

## Feature: MapForm Sidebar de Procedimientos (estilo Navigraph Maps)

**Estado:** IMPLEMENTADO — código escrito, pendiente compilar y probar en el IDE.

**Why:** El usuario quiere un panel lateral en el mapa que permita cambiar pista, SID, STAR y aproximación en tiempo real, igual que en Navigraph Maps.

---

## Versión: v0.6.5 (en desarrollo)

---

## Archivos modificados

| Archivo | Cambios |
|---|---|
| `UI/Forms/MapForm.cs` | Campos sidebar (Edit 1), BuildLayout (Edit 2), InitMap _approachOverlay (Edit 3), LoadRoute inicio estado (Edit 4), LoadRoute final PopulateSidebar (Edit 5), todos los métodos nuevos (Edit 6) |
| `UI/Forms/MainForm.cs` | Suscripción OnProcedureChanged + SetMetarData en UpdateMetarPanel |
| `ViewModels/MainViewModel.cs` | Método UpdateProcedureOverrides |

---

## Lo que se implementó (sesión 2026-05-24)

### Campos añadidos (Edit 1 — sesión anterior)
```csharp
private GMapOverlay _approachOverlay;
private Panel _sidebarPanel, _sidebarContent;
private Button _btnToggleSidebar;
private bool _sidebarExpanded = true, _populatingSidebar;
private ComboBox _cmbOriginRwy, _cmbSid, _cmbSidTrans;
private ComboBox _cmbDestRwy, _cmbStar, _cmbStarTrans, _cmbApproach;
private Label _lblOriginAirport, _lblDestAirport, _lblOriginWind, _lblDestWind, _lblApproachCount;
private string _selOriginRunway, _selDestRunway, _selSidName, _selSidTransition;
private string _selStarName, _selStarTransition, _selApproachKey;
private List<NavRunway> _sbOriginRunways, _sbDestRunways;
private List<NavProcedure> _sbSids, _sbStars;
private List<NavApproach> _sbApproaches;
private List<NavIls> _sbIls;
private IList<SimbriefWaypoint> _currentWaypoints;
private string _currentOriginIcao, _currentDestIcao, _currentAltIcao;
private int? _metarOriginWindDir, _metarOriginWindSpd, _metarDestWindDir, _metarDestWindSpd;
private static readonly Color _clrApproach = Color.FromArgb(255, 0, 200);
private static readonly Color _clrMissed = Color.FromArgb(0, 200, 255);
public event Action<string, string, string, string> OnProcedureChanged;
```

### BuildLayout — docking order
```csharp
Controls.Add(_map);         // Fill — primero
Controls.Add(bar);          // Bottom
BuildSidebar();
Controls.Add(_sidebarPanel); // Left
Controls.Add(titleBar);     // Top — mayor prioridad, último
```

### InitMap — approachOverlay
```csharp
_approachOverlay = new GMapOverlay("approach");
// ... añadido entre _waypointOverlay y _aircraftOverlay
_map.Overlays.Add(_approachOverlay);
_map.Overlays.Add(_aircraftOverlay);
```

### LoadRoute — estado del sidebar
Al inicio de LoadRoute (después de la guarda null):
- Guarda `_currentWaypoints`, `_currentOriginIcao`, `_currentDestIcao`, `_currentAltIcao`
- Si `airportChanged` → resetea selecciones del sidebar a los valores del plan de SimBrief

Al final del Task.Run (antes del BeginInvoke):
- Recopila `sbOrgRwys, sbDstRwys, sbSids, sbStars, sbApps, sbIls, sbOrgInfo, sbDstInfo`
- Dentro del BeginInvoke (después de _map.Refresh): llama `PopulateSidebar(...)`

### Métodos nuevos en MapForm
- `BuildSidebar()` — panel 230px, toggle ◀/▶, ItemW=200px, controles posicionados absolutamente
- `MakeSectionHeader/MakeSideLabel/MakeSideCombo` — factories
- `PopulateSidebar(...)` — rellena combos con `_populatingSidebar = true/false`
- `FillRunwayCombo/FillProcBaseCombo/FillProcTransCombo/FillApproachCombo` — helpers
- `GetProcBaseNames(procs, runwayFilter)` — nombres base únicos de procedimientos filtrados por pista
- `RunwayMatchesApproach/SelectOrDefault/SelectedRunwayName` — helpers
- clase `ApproachItem` — key `"{Type}{Suffix}_{Runway}"` + label para display en combo
- `UpdateWindLabel(lbl, runways, runwayName, windDir, windSpd)` — HW/TW + XW
- `SetMetarData(orgDir, orgSpd, dstDir, dstSpd)` — public, thread-safe via BeginInvoke
- `RedrawRoute()` — dispara OnProcedureChanged + LoadRoute con estado actual
- `OnOriginRunwayChanged` — EcamDialog si SID incompatible con nueva pista
- `OnDestRunwayChanged` — EcamDialog si STAR incompatible; approach siempre cleared
- `OnSidChanged/OnSidTransChanged/OnStarChanged/OnStarTransChanged/OnApproachChanged`
- `ClearApproachOverlay()` — limpia _approachOverlay, thread-safe
- `DrawApproachOverlay(app, rwy, ils)` — legs con coords → polyline, extended centerline 5NM, missed approach

### MainForm.cs
```csharp
// En BtnMap_Click, al crear _mapForm:
_mapForm.OnProcedureChanged += (orgRwy, sid, dstRwy, star) =>
    _viewModel.UpdateProcedureOverrides(orgRwy, sid, dstRwy, star);

// En UpdateMetarPanel, al final:
if (_mapForm != null && !_mapForm.IsDisposed)
    _mapForm.SetMetarData(
        _metarDataArray[0]?.WindDir, _metarDataArray[0]?.WindSpeedKt,
        _metarDataArray[1]?.WindDir, _metarDataArray[1]?.WindSpeedKt);
```

### MainViewModel.cs
```csharp
public void UpdateProcedureOverrides(
    string originRwy, string sidName, string destRwy, string starName)
{
    var plan = _flightManager?.ActivePlan;
    if (plan == null) return;
    if (originRwy != null) plan.OriginRunway      = originRwy;
    if (sidName   != null) plan.SidName           = sidName;
    if (destRwy   != null) plan.DestinationRunway = destRwy;
    if (starName  != null) plan.StarName          = starName;
}
```

---

## Detalles de implementación importantes

### Transiciones en NavProcedure
`NavProcedure` no tiene campo de transición. Se infieren del campo `Name`:
- `"MGN3C.MGN"` → base = `"MGN3C"`, transición = `"MGN"`
- Sin punto → solo opción "Direct" en combo

### Approach overlay (independiente)
`_approachOverlay` dibuja solo en esa capa, NO llama `LoadRoute`.
- Approach path (magenta, 2.5px) + sombra
- Extended centerline (5NM antes / 0.5NM después del umbral, punteado semitransparente)
- Missed approach (cian, 1.5px, dash)
- Marcador de umbral ("rwy")

### Compatibilidad de pistas
- Cambio pista ORIGEN: EcamDialog si el SID activo NO está en `GetProcBaseNames(_sbSids, newRwy)` → YES=borrar SID+trans+redraw | NO=revertir combo
- Cambio pista DESTINO: EcamDialog si el STAR activo no aplica → YES=borrar STAR+trans+approach+redraw | NO=revertir combo. Approach siempre se borra al cambiar pista destino (sin EcamDialog).

### Metar indices
- `_metarDataArray[0]` = ORIG
- `_metarDataArray[1]` = DEST

### PopulateSidebar preserva selección
Solo resetea cuando `airportChanged == true` (par ICAO diferente). En redibujados sucesivos (cambio de pista dentro del mismo aeropuerto) conserva los combos ya seleccionados.

---

## Pendiente (próxima sesión)

- Compilar y probar en el IDE
- Verificar que el sidebar se muestra correctamente al abrir el mapa
- Verificar que PopulateSidebar se llama correctamente tras LoadRoute
- Verificar chips de viento HW/TW con datos METAR reales
- Verificar DrawApproachOverlay con datos de NavData reales
- Si hay errores de compilación, revisar tipos de NavApproachLeg (Lat/Lon son double? nullable)
- Subir versión a v0.6.5 una vez verificado
