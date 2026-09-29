# NavData ← vmsOpenACars — todo el tráfico de teselas al proxy, y la caída en tres escalones

> **De:** equipo de vmsOpenACars · **Para:** equipo de NavData · **Fecha:** 30/09/2026
> **Responde a:** `RESPUESTA10-NAVDATA-TILES-2026-09-30.md`
> **Estado:** con la clave admitida en la URL, **el proxy pasa a ser el camino principal** del cliente.
> Esto es el plan, con lo que cambia para vosotros.

---

## 1. La clave en la URL: verificado, y por qué nos vale

Comprobado desde fuera y **sin ninguna cabecera**:

| petición | resultado |
|---|---|
| `tiles/light_all/12/1184/2050.png?key=…` | **200**, 2.355 bytes · `BYPASS` · `max-age=2419200, private` |
| lo mismo en `dark_all` y en `voyager` | **200** (1.203 y 1.179 bytes) |
| `?key=…&origin_domain=vholar.co` | **200** — mantenemos la comprobación de dominio |
| **sin** clave | **401** |
| **la clave en otra ruta** (`/airport/SKBO/`) | **401** |
| estilo inventado con clave | **400** |

Lo que hace aceptable la concesión no es el 200: es el **401 en cualquier otra ruta**, fijado con un
test. Han abierto un agujero del tamaño exacto de una tesela. Y como la respuesta va `private`,
ninguna caché compartida puede guardar una URL con la clave dentro.

## 2. La decisión: el proxy es el camino, no la opción

Hasta ahora el plan era «proxy si se configura, CARTO si no». **Cambia**: los dos proveedores de
CARTO del cliente piden **siempre** al proxy, y la caída a CARTO directo es la excepción. El motivo
es el que nos contasteis y ahora asumimos: la clave de la aerolínea no debería viajar en cada
instalación, y la cuota se divide por el número de pilotos en vez de multiplicarse por ellos.

**Cadena de la caída, en tres escalones**, y el tercero es nuevo:

1. **El proxy.** El camino normal.
2. **CARTO directo, con la clave del piloto** — si la tiene configurada.
3. **CARTO directo, sin clave: la tesela con marca de agua.** Hoy `carto_api_key` viene **vacío** en
   la distribución y va a seguir vacío **hasta que cada piloto obtenga la suya**, así que este es el
   escalón que se usará de verdad si el proxy falla.

Preferimos una tesela con marca de agua a un hueco gris: el mapa sigue sirviendo para orientarse y la
marca dice de dónde viene lo que se ve. Es información, no ruido. (La atribución en pantalla
—«© OpenStreetMap contributors · © CARTO»— la seguimos pintando en los tres escalones.)

## 3. Lo que esto significa para vosotros

1. **El 100% del tráfico de teselas de nuestros pilotos pasa por vuestro proxy.** Vuestro
   `tiles-stats` deja de ser un diagnóstico y pasa a ser el instrumento: aciertos, consumo aguas
   arriba y `placeholder`.
2. **El proxy pasa a ser punto único de fallo del mapa.** De ahí la caída, y de ahí el contador: os
   avisamos del **número de caídas a CARTO** y del **número de teselas con marca de agua**, que es la
   señal de que el proxy no está cumpliendo o de que no hay clave que usar.
3. **Si el ratio de aciertos se hunde, os lo decimos antes de tocar nada.** No vamos a cambiar el
   camino por una impresión: preferimos daros el dato y mirarlo juntos, como propusisteis.

## 4. Un detalle que os afecta: la clave no se registra

En nuestro lado la clave **no va a aparecer en ningún log**: el diagnóstico y el contador de caídas
llevan la URL **enmascarada**. Os pedimos lo mismo en el vuestro, porque un
`request.get_full_path()` en un log de depuración, o el log de accesos por defecto de Django,
imprimen la URL **con la clave entera** — y ahora esa URL es la que construye cada piloto.

## 5. Qué queda por nuestra parte, y cuándo

**v0.9.18**: los dos proveedores al proxy con la clave y `origin_domain`, la caída en los tres
escalones con sus contadores, los campos de URL de teselas y de clave en Ajustes, y `tiles-stats` en
el diagnóstico con `month`/`plan_limit` (y **«—»** mientras venga nulo, como acordamos).

Os avisamos en cuanto esté desplegado, que es cuando tiene sentido mirar los números juntos. Gracias
por la concesión y, sobre todo, por acotarla donde la acotasteis.

---

## Corrección (30/09/2026, medida) — el tercer escalón no es una tesela con marca de agua

En nuestra §2 escribimos que, sin clave, CARTO sirve la tesela **con marca de agua**. **Es falso, y lo
comprobamos al mirar la imagen en vez de los bytes**: `https://a.basemaps.cartocdn.com/dark_all/14/4736/8200.png`
sin clave devuelve **200 con una imagen que dice `API KEY REQUIRED · carto.com/basemaps/apikey`** — un
placeholder de 2.513 B, no un mapa marcado. La del proxy con clave, para el mismo tile, son **1.203 B**
de mapa de verdad, y los dos ficheros no se parecen en nada (sha256 distintos).

Consecuencia para lo que os contamos: **sin clave de CARTO no hay mapa**, solo un cartel. O sea que la
caída al tercer escalón **no mantiene el mapa utilizable** y el proxy deja de ser una comodidad para
ser lo único que hace que el mapa funcione en la distribución. El escalón 3 solo sirve para un piloto
que tenga su propia clave; para el que no la tenga, si el proxy cae, el mapa cae.

Se corrige aquí y no reescribimos lo ya enviado: el error es nuestro y la medida también.
