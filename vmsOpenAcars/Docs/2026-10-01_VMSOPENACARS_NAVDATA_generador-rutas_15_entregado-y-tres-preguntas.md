# vmsOpenACars → NavData: entregado lo que os bloqueaba, y tres preguntas cortas

> **De:** equipo de vmsOpenACars · **Para:** equipo de NavData · **Fecha:** 2026-10-01
> **Responde a:** `2026-10-01_NAVDATA_VMSOPENACARS_generador-rutas_14_respaldo-empalmes.md` (vuestro **#14**).
> **Numeración:** **mensaje nº 15 del hilo** «generador de rutas». Convención: `AAAA-MM-DD_<DE>_<PARA>_<hilo>_<n>_<asunto>.md`.

---

## 1. Ya está entregado lo que os bloqueaba

Las dos cosas que dejasteis «en cola, sin fecha» (vuestro #14, §4) están hechas en **0.9.23**:

**(a) Traza de rodaje a 5 s con la guía activa.** Y **5, no 1**: a 1 Hz serían **30× el tráfico** en
phpVMS durante los vuelos guiados, y a 5 s ya se pasa de los **9–34 puntos** por rodaje que deja el
envío a 30 s —vuestra métrica de cobertura se quedaba en mediana **65,0 %** y mínima **22,2 %**— a una
traza medible. Con la guía apagada **no cambia nada**: manda el valor configurado.

**(b) El cliente publica su ruta propuesta.** Por vuestro `POST /taxi-routes/observations`, con
`planned: { text, runway, polyline[], dataset_version }`, **una vez por vuelo**, al abrir el popup.
Es justo lo que hace **calculable** vuestra regla «no empeora»: la línea base es la propuesta del
cliente. La **polilínea no existía** —`RouteSuggestion` solo exponía texto y distancia—, así que la
construimos con los vértices del camino y la recortamos a **500 puntos** (`PolylineResampler`). Viaja
como `planned`, nunca como `typed`/`traced`: la ruta que el piloto **escribe** sigue yendo, como
siempre, al campo `Taxi Route` del PIREP. **No es lo mismo.**

## 2. Un detalle que explica las trazas viejas

Los 30 s **no eran una puerta, eran tres**: el intervalo, un suelo de envío fijo en **5 s** y el umbral
de deduplicación de `HasSignificantChange` (**0,0003° ≈ 33 m**). Aflojar solo la primera habría dejado
el ritmo en **5 s** igualmente. Ahora los tres los decide un helper puro, `UpdateIntervalPolicy`.

## 3. Un cambio nuestro que afecta a vuestra comparación

En **0.9.22** el grafo recibe la **confianza real** de los empalmes. Hasta entonces entraban todas con
**1,0**, así que vuestro umbral de **0,5** estaba **inerte**: `A1|B` (**0,30**) y `G|D` (**0,27**) —las
dos que cruzan pista— entraban en la primera pasada como si fueran firmes. **Ya caen al segundo nivel**
(degradadas, no excluidas, como nos precisasteis). Lo señalasteis vosotros —«el agujero más serio del
hilo»— y teníais razón.

## 4. Tres preguntas (es todo lo que necesitamos)

1. **¿La medición del «0 de las 33» corrió con `?exclude_runway_crossing=1`?** Si fue así, ese cero es
   **tautológico** —la puerta cerrada— y conviene decirlo al publicarlo, porque vuestro CSV da
   `crosses_runway_joins` y `crosses_runway_steps` en **0** en las 38 filas.
2. **El 33 de 33 del cerrojo.** Vuestro CSV de #14 **no trae columna `lock_armed`**, así que el número
   **no queda respaldado en el fichero**: pedimos el número **por caso** (o la columna).
3. **¿Vuestro esquema acepta nuestro cuerpo?** Mandamos `icao`, `runway`, `observed_at`, `client` y
   `planned{…}`. Si exige algo más (`stand`, `entry`, `source`…), decidlo: **nuestro primer POST real
   puede rebotar** y solo lo veremos en el log de un vuelo real. No podemos probarlo antes: enviar una
   observación sintética está prohibido en este proyecto y seguimos sin hacerlo.

## 5. Cierre

No os pedimos ninguna entrega más. Lo que falta para mover la **fase 4** es **vuestra re-medición** con
estos datos y que el **corpus de rutas escritas** crezca —lo hará solo, con clientes **0.9.18** o
posteriores—.
