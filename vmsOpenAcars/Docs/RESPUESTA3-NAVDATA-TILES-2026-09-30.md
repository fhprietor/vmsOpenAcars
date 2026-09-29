# NavData ← vmsOpenACars — cerrado por nuestra parte; y un bloqueante de una línea para adoptar el proxy

> **De:** equipo de vmsOpenACars · **Para:** equipo de NavData · **Fecha:** 30/09/2026
> **Responde a:** `RESPUESTA9-NAVDATA-TILES-2026-09-29.md`
> **Estado:** verificado todo lo vuestro; el cliente ya cumple la obligación de los 30 días. Queda
> **una cosa vuestra** para que podamos pedir las teselas al proxy, y es una línea.

---

## 1. Verificado desde fuera

| | medido |
|---|---|
| **la purga** | las tres URLs que estaban en `HIT`: **`401` sin clave**, `200` con clave, `cf-cache-status: **BYPASS**`, `Cache-Control: max-age=2419200, private` → nuestra §7.3 **pasa** |
| **`criterion_version`** | `version=2565baaee61e` (intacto) + **`criterion_version=c0262c8dd`** en CYUL y SKBO → invalidaremos con **los dos** |
| **`voyager`** | `200`, **1.179 bytes** — vuestra cifra |
| **`tiles-stats`** | `month="2026-09"`, `month_tiles`, `plan_limit`, `month_used_pct` — **vienen dentro de `tiles`**, no en la raíz; lo decimos por si el texto del §2 se lee como si estuviera al mismo nivel. Nuestro parser los lee de ahí |

Y sobre `plan_limit` **a `null`**: nos parece la decisión correcta —«antes nulo que un porcentaje
falso»— y la copiamos en el cliente: mientras venga nulo pintamos **«—»**, nunca un número inventado.
Cuando confirméis el plan, el porcentaje aparece solo.

## 2. Vuestra adenda: aceptada, y gracias por contarla

Que **el token fuera decorativo en esa ruta** y que los dos incidentes los arreglara el
`clear_navdata_cache` que ejecutabais a la vez es exactamente el tipo de cosa que solo aparece
midiendo. Y el detalle del `500` que cazó **la prueba en vivo y no los tests** es el mismo fenómeno
que nos ha mordido a nosotros tres veces el mismo día: **la comprobación que encuentra el fallo es la
de fuera**. Nos lo apuntamos como regla, no como anécdota.

## 3. El bloqueante de una línea: **la clave de la clave, como parámetro**

Para pedir las teselas desde el cliente usamos GMap.NET, y su proveedor de teselas construye la
petición **por dentro** (`GetTileImageUsingHttp(url)`): **no permite añadir cabeceras propias**. Es
decir, **no podemos enviar `X-API-Key`** al pedir una tesela — solo una URL.

Petición: que la **ruta de teselas** acepte la clave **como parámetro** (`?key=…`), que es
exactamente la forma que ya usa CARTO y por eso nos encaja sin inventar nada. Una línea en vuestro
lado (leer el parámetro además de la cabecera). Si preferís otra forma (un token en la ruta, un
`?t=`), nos vale igual: lo que no podemos es una cabecera.

**Mientras tanto el cliente sigue yendo directo a CARTO** y no rompemos nada: no vamos a dejar el
mapa en blanco por servir al proxy.

## 4. Lo que ya está hecho por nuestra parte (v0.9.17)

1. **La obligación de los 30 días**: al abrir el mapa se borran las teselas con más de 30 días, y hay
   una acción **«Borrar caché del mapa»** para el caso de dejar de usar el servicio. GMap.NET no lee
   vuestras cabeceras, así que el TTL lo imponemos nosotros.
2. **Contador de teselas fallidas** (`CartoTileUrl.TileFailures`), que es lo que nos pedisteis: en
   cuanto adoptemos el proxy, ese contador **es** el de las caídas a CARTO directo, y os lo diremos
   con un número en vez de con una impresión. Hoy ya cuenta los fallos contra CARTO.

## 5. Lo que queda

Adoptar el proxy en los dos proveedores **con caída a CARTO directo**, y los campos de URL y clave de
teselas en Ajustes (que necesitábamos para el plan B y no existían). Las dos cosas en cuanto tengamos
el `?key=`; la primera está escrita para que sea un cambio de configuración, no de código.
