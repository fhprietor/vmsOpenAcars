# Pedido al proyecto NavData — proxy de teselas de CARTO con caché

> **De:** equipo de vmsOpenAcars (cliente ACARS, WinForms/.NET 4.8, GMap.NET)
> **Para:** equipo del proyecto NavData
> **Estado del cliente al escribir esto:** v0.9.16
> **Origen:** la API key de CARTO ya está implementada en el cliente (v0.9.16) para poder seguir
> usando los mapas; **este pedido es el paso siguiente**, para dejar de distribuir esa clave.

---

## 1. Lo que pedimos, en una frase

Un endpoint en NavData que sirva las teselas de las basemaps de CARTO, **con la clave guardada en
vuestro servidor** y **una caché propia**, para que el cliente no tenga que distribuir la clave ni
que cada piloto consuma cuota.

## 2. Por qué (contexto que necesitáis)

CARTO retiró el acceso sin clave a `basemaps.cartocdn.com`: ahora exige un parámetro `key` y, sin él,
las teselas llegan con la marca de agua «API key required». Consecuencias para un cliente de
escritorio distribuido a N pilotos:

- La clave tendría que viajar en el `.config` de **cada piloto** (o cada piloto tendría que pedir la
  suya). Es un token publicable, pero se distribuye a mucha gente y **el consumo de todos va contra
  la misma cuenta**.
- Por eso la cuota **se multiplica por piloto** en vez de compartirse: 20 pilotos mirando el mismo
  aeropuerto son 20× las mismas teselas contra la cuenta.
- Además, una clave **con restricciones de web** devuelve **403** a un cliente de escritorio, porque
  no envía la cabecera `Referer`; hoy obliga a crearla sin restricción.
- Y cuando CARTO cambie algo, lo arreglamos publicando **una versión nueva del cliente**.

Con el proxy: la clave vive solo en vuestro servidor, **la caché sirve una vez lo que muchos piden**
y un cambio de CARTO se resuelve en el backend sin tocar el cliente.

**Lo que esto NO es**: no elimina el tráfico a CARTO (lo **centraliza y amortigua**), y no es un
servicio de teselas para terceros.

## 3. Requisito bloqueante, antes de escribir código

Confirmad por escrito con CARTO (`support-basemaps@carto.com`) que **cachear y reservir** las teselas
a vuestros propios usuarios entra en los términos de vuestro plan, y si un proxy que sirve a N
pilotos cuenta como **una sola aplicación**. Datos que os van a pedir (verificado hoy en su FAQ):

- Uso **no comercial**: gratis hasta **5 millones de teselas por mes natural**.
- Uso **comercial**: gratis hasta **1 millón**; por encima, planes de pago ($500/mes o $5.000/año
  hasta 10 M, y $1.500/mes o $15.000/año hasta 50 M).
- El consumo se cuenta **entre todas las claves de la cuenta y en ambos servicios** (raster y
  vector), y los meses son naturales en UTC.

Si la respuesta fuese que no, este pedido muere aquí — y preferimos saberlo **antes** de invertir
trabajo.

## 4. El endpoint

```
GET /api/v1/tiles/{style}/{z}/{x}/{y}.png
```

- **Autenticación**: la misma que ya usa el resto de la API (`X-API-Key` + `X-Origin-Domain`).
  Sin clave válida → `401`.
- **`{style}` es lista blanca cerrada**: `light_all`, `dark_all` (y los que decidáis, p. ej.
  `voyager`). **Nunca un valor libre, una ruta compuesta ni una URL**: sería un **proxy abierto
  (SSRF)** y os expondría a servir contenido de terceros con vuestra cuenta. Se compara contra el
  conjunto permitido y punto.
- **`{z}`**: entero **2–19** (nuestro mapa va de 2 a 19). **`{x}` y `{y}`**: enteros dentro de
  `[0, 2^z − 1]`. No numérico o fuera de rango → `400`. Válido pero inexistente aguas arriba → `404`.
- **Respuesta**: `image/png` con
  `Cache-Control: public, max-age=2592000` (30 días), `ETag`, `Content-Length`, y **`304`** ante
  `If-None-Match`.
- **Cabecera de diagnóstico `X-Cache: HIT | MISS`** (opcionalmente `X-Cache-Age`). Nos permite
  verificar desde el cliente que la caché hace su trabajo, y a vosotros operarla.
- **Upstream**: `https://{a|b|c}.basemaps.cartocdn.com/{style}/{z}/{x}/{y}.png?key=<CLAVE_SERVIDOR>`,
  con la clave en configuración/entorno del servidor — **nunca** en la respuesta, en los logs ni en
  el repositorio.
- **Errores aguas arriba**: no se cachean como si fueran teselas. `404` se puede propagar con TTL
  negativo corto; `429`, `5xx` o timeout → `502`/`504` con `Retry-After`, y cortacircuitos para no
  aporrear a CARTO.
- **Timeout corto aguas arriba (2–3 s)** y **coalescencia de peticiones idénticas (single-flight)**:
  si 20 pilotos piden la misma tesela a la vez, **una sola** llamada a CARTO.

## 5. La caché — lo que pedimos concretamente

- **Clave**: `style/z/x/y`. En la práctica es inmutable.
- **Almacén**: disco (o lo que ya uséis) en la misma máquina que sirve el endpoint.
- **TTL**: **30 días**. CARTO actualiza sus basemaps cada semanas; para un mapa de calles de
  simulador la frescura no es crítica. Con purga manual disponible.
- **Tope de tamaño con desalojo LRU.** Y esto es importante: **la caché no puede ser un espejo**.
  La pirámide mundial completa hasta z19 son decenas de miles de millones de teselas. Se cachea **lo
  que piden los pilotos**, con un tope que dimensionéis vosotros. Para tamaño: una tesela de 256×256
  en PNG pesa ~5–20 KB; un aeropuerto navegado a z12–16 toca unos cientos de teselas; el centenar de
  aeropuertos de la aerolínea son del orden de **cientos de miles de teselas y unos pocos GB**.
- **Métricas**: teselas servidas, ratio de acierto (global y por piloto/clave), y contador de consumo
  aguas arriba. Es lo que os deja defender la cuota y detectar abuso.
- **Precalentado (opcional, muy deseable)**: el cliente ya os dice qué aeropuertos tiene el plan de
  vuelo (pide `/airport/{icao}/…`). Si al cargar el OFP precalentáis las teselas de **origen y
  destino** en los zooms de rodaje (12–16), **el mapa del aeropuerto aparece listo al abrirlo** en
  vez de descargarse a trozos. Eso hoy no podemos hacerlo desde el cliente.

## 6. Atribución — obligación de CARTO, no opcional

El plan gratuito exige acreditar **CARTO y OpenStreetMap** en cada mapa. El proxy no sustituye esa
obligación: mantened en la respuesta (o documentad) el texto de atribución que hay que mostrar, y
**no permitáis que el endpoint sirva a terceros fuera del cliente de la aerolínea**. Nosotros tenemos
pendiente pintarla en el mapa y lo haremos.

## 7. Lo que implementaremos en el cliente (contrato)

- Usaeremos la clave actual del host de `navdata_api_url`.
- Los dos proveedores de CARTO (Street y Dark) pedirán a `{proxy}/{style}/{z}/{x}/{y}.png` con
  `X-API-Key`, y **caerán a CARTO directo si el proxy falla o no responde**: no queremos añadir un
  punto único de fallo al mapa.
- El satélite (ESRI) **no** pasa por aquí: no necesita clave y hoy funciona directo.
- Del lado del cliente el cambio es pequeño: la URL de teselas se construye en un único sitio
  (`Helpers/CartoTileUrl.cs`, ya probado con tests).

## 8. Criterios de aceptación — cómo lo vamos a verificar

1. Una petición devuelve `200`, `image/png`, `Cache-Control` con `max-age`, `ETag` y
   `X-Cache: MISS`; **la segunda** devuelve `X-Cache: HIT`.
2. `ETag` + `If-None-Match` responde `304`.
3. Sin `X-API-Key` → `401`. `style` desconocido o `z` fuera de rango → `400`.
4. **Prueba de que no es un proxy abierto**: `style` con `../`, con una URL, o con el host de otro
   dominio → `400`; nunca debe descargar de otro sitio.
5. La tesela servida por el proxy es **la misma imagen** que CARTO entrega con clave válida.
6. Consumo aguas arriba medible: pedir la misma tesela N veces desde N claves de piloto distintas
   produce **1** petición a CARTO.

## 9. Lo que NO pedimos

- Un CDN global ni replicación multi-región (aunque poner Cloudflare delante reduzca vuestro ancho de
  banda, bienvenido sea).
- Servir el satélite de ESRI.
- Editor de estilos, teselas vectoriales ni nada que no esté en los apartados 4 y 5.

## 10. Preguntas que necesitamos respondidas

1. ¿Entra en los términos de CARTO? (apartado 3 — **bloqueante**)
2. ¿Qué URL final usaremos y con qué autenticación exacta?
3. ¿Qué TTL y qué tope de caché fijáis?
4. ¿Vais a hacer el precalentado del apartado 5?
5. ¿A qué cuenta va asociada la clave y cómo llevamos el contador del mes?

---

### Fuentes consultadas para los datos de CARTO

- FAQ de basemaps de CARTO — <https://docs.carto.com/faqs/carto-basemaps.md>
  (formato de la clave, plan gratuito, límites y la exigencia de atribución)
- Basemap de CARTO — <https://docs.carto.com/carto-for-developers/key-concepts/carto-for-deck.gl/basemaps/carto-basemap.md>
- Clave gratuita — <https://carto.com/basemaps/apikey/>
