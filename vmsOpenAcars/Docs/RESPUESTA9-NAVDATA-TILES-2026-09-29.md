# NavData → vmsOpenACars: `criterion_version`, consumo del mes, y la causa raíz de los datos viejos

> **Para:** equipo de vmsOpenACars · **De:** equipo de NavData · **Fecha:** 2026-09-30
> **Responde a:** `RESPUESTA2-NAVDATA-TILES-2026-09-29.md`
> **Estado:** vuestra petición de §2 entregada, la de §3 también, y el patrón de §5 **arreglado en
> la raíz**: la caché ya no puede sobrevivir a su código.

---

## 0. Antes de nada: la purga está hecha

Escribisteis con la copia vieja todavía en el borde. Purgada: las tres URLs que estaban en `HIT`
devuelven **`401`** con `cf-cache-status: BYPASS`, con credenciales siguen dando `200` y —lo que
importa— **otra vez sin credenciales vuelven a dar `401`**. Vuestra §7.3 ya pasa.

## 1. §2 — `criterion_version`, y calculada de su propio código

Teníais razón y el agujero era real: `version` es el hash de los **nodos**, así que un cambio de
criterio no os invalidaba nada. La respuesta ahora lleva los dos campos:

```json
{ "icao": "CYUL", "version": "2565baaee61e", "criterion_version": "c0262c8dd", ... }
```

- **`version`** sigue siendo lo que era (hash de nodos): no cambiamos su significado, así que vuestra
  lógica actual sigue valiendo para los cambios de dato.
- **`criterion_version`** es la huella del **fuente de las funciones que deciden el criterio**.
- **Invalidad con los dos** (`version + criterion_version`). Con eso, ni un cambio de nodos ni uno de
  algoritmo se os escapa.
- No hace falta `?v=`: el campo va en el cuerpo, y la URL se queda como está.

## 2. §3 — El consumo del mes, como número

En `GET /tiles-stats/`, junto a los contadores que ya leíais:

```json
{ "month": "2026-09", "month_tiles": 0, "plan_limit": null, "month_used_pct": null }
```

- **`month_tiles`** son las peticiones **aguas arriba** del mes natural (UTC): lo que de verdad cuenta
  contra la cuota, no lo que servimos de caché.
- **`plan_limit`** sale de `CARTO_PLAN_TILES_MONTH`. Está **sin declarar** (`null`) hasta que
  confirmemos con CARTO si nuestro uso es no comercial (5 M) o comercial (1 M): preferimos dejarlo
  nulo que inventar un número y que pintéis un porcentaje falso.
- Además, el servidor deja un aviso en el log al **70 %** y al **90 %**, una sola vez cada umbral
  por mes. Cuando pintéis el dato, el aviso os sobra; mientras tanto, está.

## 3. §5 — El patrón: teníais razón, y ya no depende de que nadie se acuerde

Es la crítica más útil que nos habéis hecho, y la aceptamos entera: **un caché que sobrevive a su
código es el fallo más caro de encontrar**, porque el síntoma aparece lejos de la causa. Las dos veces
lo encontrasteis midiendo, y las dos veces la causa era la misma: un token que había que **subir a
mano** al cambiar el código, y olvidarlo no daba ningún error.

Arreglado así: **el token de caché ya no es una constante, se calcula del fuente de los módulos que
producen las respuestas** (`views.py`, `db.py`, `taxi.py`, `airac.py`, `models.py`). Cambiar
cualquiera de ellos **invalida la caché solo**, sin que nadie tenga que acordarse. Hoy vale
`v7-ba5c2d494e`; mañana valdrá otra cosa en cuanto toquemos el código, que es exactamente lo que
queremos. Y `criterion_version` se calcula igual, del fuente del propio criterio.

Coste: tras cada despliegue, la primera petición de cada clave se recalcula (un fallo de caché
limpio). Es el precio correcto por no volver a servir datos viejos.

## 4. Vuestro plan (§4), anotado

1. Adoptar el proxy con caída a CARTO directo y **contador de caídas**: sí, y ese contador es
   justo lo que necesitamos para saber si el proxy está cumpliendo. Cuando lo tengáis, decidnos el
   número de vez en cuando.
2. La obligación de los 30 días (purga al arrancar y «borrar caché del mapa»): la nuestra ya está
   cubierta con `purge_tiles`; la vuestra es la que queda y la dais por asumida.
3. Campo de URL y clave en Ajustes: adelante. Si queréis que la URL base sea configurable, el
   endpoint no cambia.

---

## Adenda — Vuestra crítica de §5 era más profunda de lo que pensábamos

Al desplegar lo de arriba, el endpoint de empalmes seguía sirviendo la respuesta vieja **aunque el
token había cambiado**. Investigado: **el token no estaba en la clave de caché de los endpoints de
rodaje**. `views_taxi.py` tenía su propia función de clave, con el esquema de la base de conocimiento
pero **sin** el token de formato de la respuesta.

Consecuencia incómoda: los dos incidentes que nos señalasteis **no los arregló el token**, los
arregló el `clear_navdata_cache` que yo ejecutaba a la vez. El token era decorativo en esa ruta. Es
decir: teníais razón, y el fallo era peor de lo que os contamos — la invalidación no era «un paso que
se olvida», es que **en esos endpoints no existía**.

Corregido: la clave incluye el token (leído en cada llamada, no al importar), con dos tests —uno de
ellos con partes no textuales, porque la primera versión de mi arreglo devolvía un `500` que cazó la
prueba en vivo y no los tests—. Verificado: `/airport/CYUL/taxiway-joins/` ya devuelve
`criterion_version` **sin vaciar la caché**.
