# NavData → vmsOpenACars: los tres hallazgos, arreglados (y uno era peor de lo que parecía)

> **Para:** equipo de vmsOpenACars · **De:** equipo de NavData · **Fecha:** 2026-09-29
> **Responde a:** `RESPUESTA-NAVDATA-TILES-2026-09-29.md`
> **Estado:** los tres puntos de vuestra §2, §3 y §4 corregidos y verificados. El de §2 tenía una
> causa que no era la que parecía, y os la contamos porque cambia cómo hay que verificarlo.

Gracias por probarlo contra el servicio real. Los tres hallazgos eran ciertos, y el de §2 no era un
descuido de orden: era una caché **por delante** de nuestro servidor.

---

## 1. §2 — El `401` en los aciertos: era Cloudflare, y ya no puede pasar

**Nuestra comprobación interna daba `401` correctamente**, así que reproducimos vuestra prueba tal
cual, contra la URL pública:

```
con clave, misma URL -> 200  cf-cache-status: MISS   (llega a nuestro servidor; X-Cache: MISS)
sin clave, misma URL -> 200  cf-cache-status: HIT    ← servido por el borde, sin tocar el origen
```

La causa: la respuesta iba con **`Cache-Control: public`** y la URL **no lleva credencial**, así que
Cloudflare guardó la tesela en su borde y la sirvió a quien la pidiera **sin que la petición llegara
a nuestro servidor** — por eso nuestra autenticación no se ejecutaba: no había nada que autenticar.
El `X-Cache: HIT` que visteis era **nuestro**, horneado en la copia cacheada por el borde.

Arreglado: la respuesta va ahora **`Cache-Control: private, max-age=2419200`**.

- **`private`** permite que la caché **del propio piloto** (su dispositivo) guarde la tesela 28 días,
  que es lo que queremos y lo que respeta el límite de CARTO, pero **prohíbe** que la guarde una
  caché compartida (Cloudflare, cualquier proxy).
- Además vamos a dejar una regla en el borde para que **`/api/v1/*` no se cachee en Cloudflare**:
  una API autenticada no debe cachearse en un CDN, con teselas o sin ellas.
- **Las copias que ya están en el borde** siguen ahí hasta que caduquen, así que hay que **purgar
  `/api/v1/tiles/*`** una vez. Después de eso, vuestra §7.3 (sin clave → `401` **también en los
  aciertos**) debe pasar.

**Para vuestra verificación:** mirad `cf-cache-status` además de `X-Cache`. Si `cf-cache-status` es
`HIT`, lo que estáis viendo no lo sirvió nuestro servidor, y ninguna cabecera nuestra de esa copia
dice nada del estado actual.

## 2. §3 — `voyager`: teníais razón, era un bug nuestro

No era una tesela oceánica vacía: **CARTO sirve `voyager` en `rastertiles/voyager`, no en la raíz**.
Comprobado contra el upstream, con la misma clave y el mismo tile:

```
.../voyager/13/2368/4100.png            -> 404 (162 bytes)
.../rastertiles/voyager/13/2368/4100.png -> 200 (1.179 bytes)
```

Nuestro constructor de URL daba por hecho que todos los estilos cuelgan de la raíz. Corregido con
una ruta por estilo, y verificado por el proxy: `voyager/13/2368/4100.png` → **200, 1.179 bytes**.
Buen ojo: era un defecto real, no una impresión.

## 3. §4 — El puente de CYUL: teníais razón y ya está publicado con su `confidence`

Vuestra medición era correcta y nuestra tabla estaba **desactualizada**, por un fallo nuestro que
ya conocemos bien: el endpoint sirve respuestas cacheadas 24 h y **el cambio de criterio no subió la
versión del formato**, así que seguía sirviendo el resultado anterior. Es exactamente el fallo que
nos señalasteis en el hilo de las rutas, en otra forma: **un dato viejo conviviendo con código
nuevo**. Subido el token (`v7`) y verificado:

```
CYUL -> 1 empalme: kind=component_bridge  confidence=0.14  turn_deg=68.0  gap=47.7 m  taxiway=G|A4
```

**Y se queda publicado**, con vuestro argumento, que nos parece el correcto: un puente no se
descarta por el giro, porque sin él la red está partida y no hay ruta; el giro **sólo baja la
`confidence`** (0,14 en este caso) y vosotros la ponderáis. El corte por giro (≤ 60°) se mantiene
sólo para los huecos dentro de una misma componente, donde «seguir la misma calle» sí es el
criterio. Dicho de otro modo: **ningún empalme va a desaparecer por un listón silencioso**; si algún
día cambia el criterio, cambia la `confidence` y lo leéis.

## 4. Respuestas a vuestra §6

1. **URL base**: confirmada, `{navdata_api_url}tiles/{style}/{z}/{x}/{y}.png`. Sin cambios.
2. **`401` en los aciertos**: corregido (§1), más purga del borde pendiente de un clic.
3. **`style` con `../` → `404`**: correcto y lo dejamos así: la ruta no llega a nuestro código (la
   resuelve el enrutador de Django), y si llegara, la lista blanca responde `400`. En ningún caso se
   descarga de otro host: el `style` es una **clave de un diccionario**, nunca parte de una URL.
4. **`voyager`**: cableado ya, con su ruta correcta (§2).
5. **`tiles-stats`**: sí, misma clave y mismas cabeceras (mismo slug `tiles`). Usadlo para vigilar el
   ratio de aciertos y el consumo aguas arriba; si queréis que os avisemos nosotros al acercarnos a
   la cuota del plan, decidlo y lo añadimos.

## 5. Lo vuestro, anotado

§5.1 (purga de 30 días al arrancar y acción «borrar caché del mapa»): perfecto, y es la obligación
que de verdad importa, porque la del servidor ya está cubierta. §5.2 (proxy con caída a CARTO
directo, satélite de ESRI fuera): de acuerdo, y avisadnos si veis que la caída se dispara, porque
será señal de que el proxy no está cumpliendo. §5.3 (la corrección de vuestro plan B): anotada, sin
problema — mejor saberlo antes de necesitarlo.
