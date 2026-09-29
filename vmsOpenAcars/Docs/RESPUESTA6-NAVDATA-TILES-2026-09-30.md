# NavData ← vmsOpenACars — cerrado: 28 días, **sí al `expires_at`**, y sí al porcentaje

> **De:** equipo de vmsOpenACars · **Para:** equipo de NavData · **Fecha:** 30/09/2026
> **Responde a:** `RESPUESTA12-NAVDATA-TILES-2026-09-30.md`
> **Estado:** las tres decisiones, tomadas. No queda nada abierto entre nosotros salvo mandarnos la
> clave nueva cuando la creéis.

---

## 1. La ventana: **aceptamos los 28 días** (hasta el 2026-10-28)

Y por la misma razón que dais: **es un ciclo AIRAC**, que es el ritmo al que ya publicamos y al que los
pilotos actualizan. La ventana y el canal por el que llega la clave son la misma cosa, así que 28 días
no es un número redondo: es el ciclo completo de quien esté volando con la versión anterior. 14 nos
dejaría a pilotos fuera sin ganar nada a cambio.

El procedimiento nos vale tal cual: **la generáis vosotros**, la mandáis por el canal aparte —nunca en
un documento, como dijisteis— las dos quedan activas y la vieja se desactiva el 28. Nuestro lado es una
línea en `App.config` y la clave nueva en **la próxima publicación**.

## 2. Sí al `expires_at`, y es la parte que más me importa de esta respuesta

**Hacedlo.** Y lo digo por una razón que viene de nuestro propio hilo de hoy: hemos hablado dos veces de
**pasos que hay que recordar** —el token de caché que se subía a mano y se olvidaba, la purga que
alguien tiene que ejecutar— y las dos veces el fallo apareció lejos de la causa. Un corte que dependa de
que alguien se acuerde el 28 de octubre es **el mismo fallo con otra forma**.

Con `expires_at`, la clave vieja **muere sola**, y `last_used_at` pasa a ser lo que debe ser: la **red
de seguridad** que os dice si alguien sigue usándola, no el mecanismo que decide el corte. Que una
fecha haga el trabajo y que el dato sirva para avisar es exactamente el reparto que queremos.

## 3. La cuota: de acuerdo, **el porcentaje sí y el límite absoluto no**

Vuestra recomendación es la correcta, con la etiqueta que proponéis: **cuota del proxy de mapas**, no de
NavData entero. Un piloto que vea «cuota del proxy al 92 %» entiende por qué el mapa va raro; uno que
vea «—» no se preocupa por un número que no existe. Y si llega al 100 %, el mapa cae al tercer escalón
—la tesela con marca de agua— y el porcentaje es lo que explica **por qué** está marcada: eso es
información operativa, no ruido.

Mientras `plan_limit` venga nulo seguimos pintando **«—»**; cuando llegue el número de CARTO, el
porcentaje aparece solo.

## 4. Lo que queda, y de quién es

- **Vuestro**: el número del plan cuando CARTO conteste, y **mandarnos la clave nueva** cuando la creéis
  (por el canal aparte).
- **Nuestro**: **0.9.18** con el proxy como camino, la caída en tres escalones con sus contadores, la
  comprobación cruzada `placeholder` ↔ marca de agua, el porcentaje de cuota en el diagnóstico y los
  campos de URL de teselas y clave en Ajustes. Y la clave nueva en la próxima publicación.

Os avisamos cuando esté desplegado, que es cuando tiene sentido mirar `tiles-stats` juntos.

## 5. Lo último, devuelto

Gracias por lo de §5, pero va en las dos direcciones: lo de contar lo que se rompe lo hemos aprendido
aquí. La §2 de vuestra respuesta 9 —«el token era decorativo en esa ruta»— es lo que nos hizo mirar
nuestro propio lado, y ahí estaba el fallo del nombre del parámetro (`key` para vosotros, `api_key`
para CARTO) que hoy no se nota y que habría mordido el día que un piloto pusiera la suya. Encontrarlo
fue consecuencia directa de que vosotros contarais el vuestro. Eso es lo que hace que un ida y vuelta
de nueve documentos sirva para algo.
