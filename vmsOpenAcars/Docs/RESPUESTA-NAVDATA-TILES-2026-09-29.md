# NavData ← vmsOpenACars — proxy de teselas: verificado, y tres cosas que devolver

> **De:** equipo de vmsOpenACars (cliente ACARS) · **Para:** equipo de NavData · **Fecha:** 29/09/2026
> **Responde a:** `RESPUESTA7-NAVDATA-TILES-2026-09-29.md` (vuestra respuesta al pedido
> `PEDIDO-NAVDATA-TILES.md`)
> **Estado:** **lo hemos probado contra el servicio real** y funciona; lo que sigue son dos cosas que
> conviene arreglar, una discrepancia entre vuestro texto y vuestro servidor, y lo que asumimos por
> nuestra parte.

---

## 1. Verificado desde nuestro lado (mismos números que vosotros)

Con la clave de nuestra aerolínea y la misma URL de siempre:

| petición | resultado |
|---|---|
| `{navdata_api_url}tiles/light_all/12/1184/2050.png` | `200`, **2.355 bytes**, `X-Cache: HIT` |
| `{navdata_api_url}tiles/dark_all/14/4736/8200.png` | `200`, **1.203 bytes**, `X-Cache: HIT` |
| Cabeceras | `Cache-Control: public, max-age=2419200` (**28 días** ✓ dentro de los 30 de CARTO), `ETag` presente |
| `{navdata_api_url}tiles-stats/` | `served`, `hit`, `miss`, `stale`, `upstream`, `evicted`, `hit_ratio`, `cache_bytes`, tope **3 GB** · `styles = light_all dark_all voyager` |

Coincide byte a byte con lo que nos contasteis, y el contador **`stale`** está ahí: el «servir lo
viejo antes que nada» lo habéis implementado de verdad. Gracias — es lo que hace que un proxy sirva
en la mayoría de los casos en vez de dejar huecos en blanco.

## 2. Lo que hay que arreglar: **las teselas cacheadas se sirven sin credenciales**

Reproducido dos veces, con la **misma** URL y sin ninguna cabecera de autenticación:

```
sin clave, tesela NO cacheada   -> 401   (correcto, y lo probamos con tres tiles nuevos)
sin clave, tesela YA cacheada   -> 200   X-Cache: HIT      ← esto
```

Es decir: **la autenticación se comprueba después de la caché**. Consecuencia: el endpoint se
comporta como un **proxy público** para todo lo que ya esté cacheado, que es exactamente lo que
pedíamos evitar en §6 del pedido y lo contrario de vuestro propio §4 («sin clave válida → `401`»).

Severidad: baja —en una tesela de calles no hay nada sensible—, pero (a) cualquiera puede consumir
vuestro ancho de banda y vuestra caché sin ser piloto, y (b) perdéis la posibilidad de cortar el
servicio por clave. **Recomendación**: validar `X-API-Key` **antes** de mirar la caché, de modo que
el `401` sea siempre `401` y el `X-Cache` solo se decida en la rama autenticada.

## 3. Menor: `voyager` está en `styles` pero un tile válido devuelve `404`

`{navdata_api_url}tiles/voyager/13/2368/4100.png` **con credenciales** → `404`, mientras
`light_all/19/151234/231234.png` a zoom mayor → `200 MISS`. z13 con `x=2368, y=4100` está dentro de
rango (`0..8191`). Puede ser un tile oceánico vacío o que `voyager` no esté cableado al upstream que
toca; lo decimos por si acaso, sin prisa, porque no usamos ese estilo.

## 4. Vuestra tabla de §3 no coincide con lo que sirve el endpoint — y **preferimos que se quede**

Vuestro texto dice **CYUL → 0 empalmes**. El endpoint publica **uno**:

```
kind=component_bridge  confidence=0.14  turn_deg=68  gap=47.7 m  source=computed
```

O sea que la **regla unificada de ≤60° no es lo que está sirviendo**, y vuestra oferta de relajarla
«solo para puentes (≤90°)» no hace falta: el puente **ya está publicado**.

**Y para nosotros debe seguir estándolo.** CYUL tiene **2 componentes**: sin ese puente, nuestra
política por `node_id` **no encontraría ruta** en ese aeropuerto. Lo que hace que un empalme dudoso
sea inofensivo ya es nuestro trabajo: el grafo prueba **primero sin los empalmes por debajo de 0,5**
y **solo si con eso no hay ruta** repite con todos, dejándolo dicho (`UsedLowConfidence`). Por eso
un puente de **0,14** es aceptable publicado: no ensucia una ruta que ya se puede hacer bien, y salva
la que sin él no existe.

Si en algún momento cambiáis el criterio, **preferimos que sea subiendo o bajando `confidence`** —que
es un dato que podemos leer y ponderar— antes que aplicando un listón en silencio: un empalme que
desaparece sin aviso nos cambia las rutas que sugiere el cliente.

## 5. Lo que asumimos por nuestra parte

1. **La obligación de los 30 días nos toca, y hoy no la cumplimos.** Nuestra caché de teselas es la
   de GMap.NET por defecto: **no fijamos ubicación, ni expiración, ni purga**, y no lee vuestras
   cabeceras. Vamos a implementar (a) **purga de teselas con más de 30 días** al arrancar y (b) una
   acción **«borrar caché del mapa»** en Ajustes, para que la caché local sea borrable si el servicio
   se deja de usar. Os lo decimos porque es una obligación que habéis puesto por escrito y no está
   cubierta todavía.
2. **El cliente pasará a pedir al proxy**, con **caída a CARTO directo** si el proxy falla o no
   responde —no queremos añadir un punto único de fallo al mapa— y manteniendo el satélite de ESRI
   fuera del proxy, como acordamos.
3. **Corrección de nuestro propio pedido, para que no os despiste**: en el plan B escribimos que
   había un campo de URL de teselas en Ajustes. **Era falso**: las dos URLs están fijas en el código
   y solo la clave se lee de la configuración. Ya está corregido en `PEDIDO-NAVDATA-TILES.md`, y
   significa que ese plan B también exige añadir el campo. Perdón por el ruido.

## 6. Lo que necesitamos de vosotros

1. **Confirmad la URL base**: la estamos usando como `{navdata_api_url}tiles/{style}/{z}/{x}/{y}.png`.
   Si es otra, decidlo y la ajustamos nosotros.
2. **El `401` en los aciertos** (§2): ¿lo vais a corregir?
3. **`style` inválido**: probamos una ruta con `../` y devolvió **404**; en el pedido escribimos
   `400`. Nos da igual cuál de los dos, pero **que no descargue de otro sitio** es lo que hay que
   poder afirmar — y con 404 lo damos por bueno.
4. **`voyager`** (§3): ¿está cableado?
5. **`tiles-stats`**: ¿podemos leerlo con la misma clave, para vigilar desde nuestro lado el ratio de
   aciertos y el consumo aguas arriba?

## 7. Lo que verificaremos nosotros

1. `200` + `image/png` + `Cache-Control` + `ETag` con `X-Cache: MISS`, y `HIT` en la segunda.
2. `ETag` + `If-None-Match` → `304`.
3. Sin clave → `401` **también en los aciertos** (hoy no, §2).
4. `style` desconocido o fuera de lista → `400`/`404`, nunca descarga de otro host.
5. **Con CARTO caído o devolviendo `5xx`**, una tesela ya cacheada se sigue sirviendo (vuestro
   contador `stale` sugiere que sí; lo probaremos en cuanto podamos inducirlo).
6. La misma tesela pedida desde N claves de piloto distintas produce **1** petición aguas arriba.
7. Nuestro lado: la caché del piloto **no pasa de 30 días** y se puede **borrar**.
