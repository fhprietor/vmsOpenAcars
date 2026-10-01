# Formato de la tanda 1 (validación del generador de rutas de rodaje)

> **Para:** equipo de vmsOpenACars · **De:** equipo de NavData · **Fecha:** 2026-10-01
> **Estado:** el arnés que consume este formato **ya está implementado** y probado. Es lo único que
> falta para correr el criterio pre-registrado (fase 2).

No hace falta endpoint nuevo: es un fichero de validación, no un dato de producción.

## El fichero

JSON con una lista de casos. Un caso por ruta del PIREP:

```json
{
  "tanda": "1",
  "ejemplo_ilustrativo": true,
  "routes": [
    {
      "icao": "SKBO",
      "stand": "G74",
      "runway": "14L",
      "route_written": ["F", "E", "M", "A", "A3"],
      "trace": [
        {"lat": 4.698871, "lon": -74.144562, "t": 1759298591.0},
        {"lat": 4.698900, "lon": -74.144501, "t": 1759298597.0}
      ],
      "baseline_polyline": [[4.698871, -74.144562], [4.698900, -74.144501]]
    }
  ]
}
```

| Campo | Obligatorio | Qué es |
|---|---|---|
| `icao`, `runway` | sí | aeropuerto y pista de salida |
| `stand` | una de las dos | designación del puesto (`"G74"`); lo resolvemos contra `/parkings/` |
| `lat`, `lon` | formas de origen | posición del PIREP, si no hay `stand` (es el caso de LMML) |
| `route_written` | sí | la secuencia de calles que el piloto escribió, en orden |
| `trace` | sí | la traza de rodaje: `lat`, `lon` y **`t` en segundos epoch** (número, no ISO) |
| `baseline_polyline` | no | la ruta que usa hoy el cliente para ese caso. Si la mandáis, calculamos las métricas 1, 2 y 4 **también** contra ella y la comparación «generador contra estado actual» sale sola |

Notas de formato:

- **`t` en segundos epoch** y no ISO8601: la métrica 4 (la regla de 15 s y el enfriamiento de 20 s)
  hace aritmética con el tiempo y así no hay que interpretar zonas horarias.
- Si una traza viene con huecos de más de 60 s, decidnoslo: hoy no se interpola y un salto cuenta
  como una racha más.
- `baseline_polyline` es **opcional pero importante**: sin ella no podemos evaluar el criterio
  «el generador no empeora lo que ya tenéis» (§3.1 de la respuesta). Si os es más fácil mandarla
  como lista de calles, decidlo y añadimos ese modo.

## Cómo se mide

```bash
python manage.py check_taxi_routes tanda.json --csv tanda-metricas.csv
python manage.py check_taxi_routes tanda.json --min-join-confidence 0.6   # condición (d)
```

La salida trae el detalle por ruta (cobertura, desviación lateral, eventos de `FUERA DE RUTA`,
coincidencia de secuencia) y el resumen con los **seis criterios** en `PASA`/`FALLA`, más el hash
`version` de la red con la que se midió. El CSV es el que se discute: no la impresión de nadie.

## El ejemplo de este repositorio

`docs/tanda-1-ejemplo.json` es **ilustrativo**: lleva dos casos reales (SKBO `G74` → 14L con la ruta
`F E M A A3`, y LMML con la ruta `T J K L`) con **trazas sintéticas** generadas a lo largo de la
ruta calculada, para que el arnés se pueda correr de punta a punta hoy mismo. Está marcado con
`"ejemplo_ilustrativo": true` y **no sirve para decidir nada**: las trazas no son de nadie.
