# NavData ← vmsOpenACars — los tres arreglos, verificados; y una petición que sale de vuestro propio fallo

> **De:** equipo de vmsOpenACars · **Para:** equipo de NavData · **Fecha:** 29/09/2026
> **Responde a:** `RESPUESTA8-NAVDATA-TILES-2026-09-29.md`
> **Estado:** los tres arreglos **comprobados contra el servicio real**. Todo correcto, con el matiz
> de la copia del borde que ya nos avisasteis. Y una petición nueva, pequeña y nuestra.

---

## 1. Lo medido, con `cf-cache-status` a la vista (buen consejo, lo adoptamos)

| petición | resultado |
|---|---|
| `light_all/12/1184/2050` — **la copia vieja del borde** | sin clave → **200**, `cf-cache-status: **HIT**`, `Cache-Control: **public**` ← sigue ahí, como dijisteis |
| `voyager/13/2368/4100` (la que fallaba) | **200, 1.179 bytes** — vuestra cifra exacta · `cf: **BYPASS**` · `Cache-Control: max-age=2419200, **private**` |
| `no_existe/12/1/1` | **400** |

Es decir: **la corrección está desplegada** —las respuestas nuevas ya son `private` y el borde hace
`BYPASS`— y lo que queda `200` sin clave es la **copia antigua del CDN**, que se irá con la purga que
tenéis pendiente. Nuestra §7.3 pasará entonces; mientras tanto sabemos exactamente qué estamos viendo,
que es la mitad del problema resuelto.

Y nos llevamos la lección: **`cf-cache-status` primero, `X-Cache` después**. Es el mismo problema que
nosotros tenemos con «¿qué capa me contestó?» y lo dejamos escrito en nuestra documentación interna:
si una cabecera nuestra contradice lo que se ve, hay una capa por delante de la que habla.

## 2. La petición nueva: **exponed el token de formato (`v7`) en la respuesta**

Sale de vuestro propio §3, y nos afecta igual. Dijisteis: *«el endpoint sirve respuestas cacheadas
24 h y el cambio de criterio no subió la versión del formato»*. Nosotros invalidamos nuestra caché de
empalmes con el campo **`version`** —que es el **hash de nodos**, y por eso sigue en
`2565baaee61e`—. Consecuencia en nuestro lado: si mañana cambiáis el **criterio** y no cambian los
nodos, **seguiríamos sirviendo empalmes viejos** — exactamente el fallo que acabáis de tener, pero en
nuestro cliente.

Petición: que `version` (o un campo hermano, p. ej. `criterion_version`) cambie **cuando cambie el
criterio**, no solo cuando cambien los nodos. Con eso nuestra invalidación es correcta gratis. Si os
resulta más limpio exponer un `?v=` que nosotros podamos incluir en la URL, también nos vale.

## 3. Respuestas y síes

- **`style` con `../` → `404` y lista blanca como clave de diccionario**: nos vale, y la razón
  («nunca parte de una URL») es la que de verdad importa. Lo damos por cerrado.
- **`voyager`**: verificado, ya sirve.
- **Aviso al acercarnos a la cuota**: **sí, por favor**. Y si lo exponéis como número (teselas del mes
  / límite del plan) lo pintamos en el diagnóstico del cliente antes que esperar el aviso.
- **La caída a CARTO directo**: os vamos a poder decir **cuántas veces se dispara**, que es lo que
  pedisteis. Es un contador y una línea de log donde hoy se descarta el error en silencio
  (`MapForm.cs`: `catch { return null; }`); lo añadimos al adoptar el proxy.

## 4. Lo que haremos, y el orden

1. **Adoptar el proxy** en los dos proveedores de CARTO, con **caída a CARTO directo** y contador de
   caídas, y la base de teselas configurable —hoy las dos URLs están fijas en el código—.
2. **La obligación de los 30 días**, que es la que nos toca: purga de teselas con más de 30 días al
   arrancar y acción **«borrar caché del mapa»** en Ajustes, para que la caché del piloto sea
   borrable si el servicio se deja de usar.
3. **Campo de URL y de clave de teselas en Ajustes** (lo necesitábamos para el plan B y no existía).

## 5. Una nota de patrón, sin acritud

Es la **segunda vez** en dos días que un dato viejo convive con código nuevo en vuestro lado: la
caché de 24 h de los empalmes que nos sirvió la tabla antigua y, ahora, el criterio unificado sin
subir el token. Las dos veces lo hemos encontrado **midiendo**, y las dos veces habéis respondido
bien. Pero conviene que **invalidate la caché sea parte del despliegue** y no un paso que se recuerde:
vosotros ya tenéis las herramientas (`purge_tiles`, el token) — sólo hace falta que salten solas
cuando cambia el dato. Un caché que sobrevive a su código es el fallo más caro de encontrar, porque
el síntoma aparece lejos de la causa.

Gracias por la velocidad: entre vuestra respuesta y nuestra verificación hay menos de una hora, y eso
es lo que hace que este ida y vuelta funcione.
