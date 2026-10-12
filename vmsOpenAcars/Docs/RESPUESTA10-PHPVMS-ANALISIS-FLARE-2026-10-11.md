# Respuesta (10) al equipo de phpVMS — el tope de 32, la purga con causa y el rastro que queda

> **De:** equipo de vmsOpenACars · **Para:** equipo de phpVMS
> **Fecha:** 11/10/2026
> **Respecto a:** vuestra respuesta (10): las siete horas, la purga horaria y nuestras tres aclaraciones
> **Estado:** **el caso de los 6 números queda cerrado por los dos lados**, y vuestra respuesta ha
> destapado **un fallo nuestro**: el tope de `trace_stop_reason`. Corregido, con prueba.

---

## 1. El tope de 32: teníais el dato que nos faltaba, y nosotros lo teníamos al revés

Decís que vuestra validación admite **hasta 32 caracteres**. Nosotros recortábamos a **64** —con un
comentario que decía, literalmente, «el margen es para que un motivo nuevo no cueste el envío
entero»—. **Y es exactamente al revés:** vuestra validación es **todo o nada**, así que un motivo de
33 a 64 caracteres **no se recorta en vuestro lado, tumba el envío completo** —traza, métricas y todo
el análisis del vuelo—. El «margen de seguridad» era la trampa.

Corregido a **32**, que es vuestro número, y con una prueba que lo fija
(`ElMotivoDemasiadoLargo_SeRecortaAlTopeDeSuValidacion`). Ahora un motivo nuevo y largo **degrada a
una cadena cortada** en vez de costar el análisis, y el recorte **conserva el prefijo**, que es lo que
parseáis: el más largo del vocabulario (`stopped:post-touchdown`) son **22**, así que en 32 entra
siempre entero. La cadena completa más larga que producimos hoy son **27**.

De paso hemos repasado **los otros siete topes** del payload contra lo que está publicado
(`source_name` 64, pista 16, título 191, ICAO 8, modelo 64, etiqueta de flaps 32, METAR 500): esos
siguen lo que acordamos en el contrato y no han cambiado. **El único desalineado era este**, y solo
porque su número lo hemos sabido hoy: es el tipo de dato que conviene que nos digáis **aunque
parezca obvio**, porque un tope que se cree conocido en un lado y no en el otro no falla al
escribirse, falla al rechazarse.

Y una nimiedad, en el espíritu del hilo: `stopped:post-touchdown 2.0s` son **27**, no 26
(`post-touchdown` tiene 14 y la cadena 22 antes del espacio). Irrelevante para el tope —entra con
cinco de sobra—, pero aquí contamos.

## 2. La purga: cerrado con causa, y es la mejor explicación posible

**Aceptado y entendido, y es un hallazgo vuestro, no una hipótesis nuestra.** Un cron horario con
`delete_cancelled_hours = 12` y `delete_rejected_hours = 0` explica las siete búsquedas vacías, la
ausencia de rutas completas y —lo que lo convierte en demostración— que **sobreviva un PIREP
rechazado del piloto 55**: si esos seis hubieran sido rechazados, seguirían ahí. Fueron **cancelados**,
y cancelado más 12 h es purgado.

La secuencia queda escrita también de nuestro lado: **volado → fileado (nuestro `filed` dijo la
verdad) → cancelado → purgado por el cron**. Y con eso, los seis se cierran como no recuperables.

**Tres cosas que podemos añadir, y una pregunta:**

1. **El dato no se pierde del todo: se pierde el crédito.** El `landing_log.sqlite` del piloto guarda
   el vuelo, su puntuación y —desde la 0.9.32— **la traza del flare entera**, así que de esos seis
   vuelos lo que se fue es la acreditación, no la evidencia. Es también lo que nos ha permitido
   llegar hasta aquí: sin nuestro log local, esta conversación no habría existido.
2. **Pero el piloto no se entera, y ahí tenéis razón en lo que decís.** Lo único que podemos hacer
   nosotros es no mentirle: nuestro LOGBOOK lee la base **local**, así que seguirá enseñando ese vuelo
   como volado mientras en phpVMS ya no existe. Es una divergencia silenciosa más, del mismo tipo que
   la del toque que congelamos, y la única forma de que el piloto la vea es que **alguien la enseñe**:
   el sitio barato es vuestro (una marca en la lista de PIREPs), porque nosotros solo vemos el estado
   **en el momento de filear**, y una cancelación llega después. Si queréis que estudiemos una
   comprobación desde el cliente —«¿siguen estando mis últimos PIREPs?»—, decidlo y lo miramos como
   cosa aparte; no lo damos por hecho.
3. **Vuestra línea de log es la correcta, y el matiz de `activity_log` es un buen hallazgo.** Que
   `AppServiceProvider` lo tenga desactivado y solo lo reactive un middleware por el que el cron **no
   pasa** significa que la entrada no se habría escrito justo en el camino que causó el problema; el
   log sí. Y pasar de «solo el id» a `{pirep_id, pilot_id, flight_number, dpt, arr, state,
   created_at, submitted_at}` es la diferencia entre un rastro y un identificador huérfano.
   - **Sugerencia de un campo, si os encaja**: añadir **`source_name`**. Distingue un vuelo de nuestro
     cliente de uno metido a mano o de otro ACARS, y una purga se atribuye sin cruzarla con nada.
   - **Y una pregunta, que sí nos importa**: ¿el `forceDelete` alcanza también a la fila del
     **análisis de aterrizaje** —la que recibe este hilo—? Si la arrastra, la traza que subimos se va
     con el PIREP y **vuestra línea de log pasa a ser el único rastro de que existió**. No cambia nada
     de lo nuestro; cambia lo que se puede reconstruir después.

## 3. Vuestras tres aplicaciones, confirmadas

**`trace_stop_reason`:** los dos sitios donde lo leéis están como debe. Contar el prefijo hasta el
primer espacio es exactamente para lo que sirve el formato, y **«ausente» = la captura no se cerró**
—distinto de que se cortara— es la lectura que le dimos al `null`. Con el tope ya en 32, no hay
ninguna forma de que un motivo nuestro os llegue fuera de vuestra validación.

**La versión:** correcto, los dos vuelos que medisteis **no pueden** traer los campos y eso no es un
fallo. El próximo con `0.9.38` es el que los estrena.

**El aviso del offset:** me alegra que sirviera, y me quedo con vuestra formulación —«un cero no puede
mover nada y las dos convenciones coinciden»—, que es más corta que la mía.

## 4. Y sobre el cero compartido

Gracias por llamarlo propiedad y no coincidencia: es lo que es. `ProjectOnRunway` **resta** el offset
y `FlareDistanceFt` lo **suma**, así que la igualdad de los dos ceros es una consecuencia de la
construcción y no de que las cuentas salgan. Por eso el residual de una traza alineada **no puede**
salir de ~0, y por eso vuestra validación `{0, −offset}` es un detector y no una comprobación de
buena fe.

Y coincidimos en el campo de convención: no hace falta. Un campo para decir algo que el dato ya dice
envejece sin que nadie lo mire.

## 5. El canal

Confirmado por los dos lados y con la lección aprendida: **si un documento no llega, pedimos la parte
que falta**. Las siete horas eran siete líneas; la ronda se fue en no tenerlas.

---

## 6. Lo que queda

1. **El primer vuelo con `0.9.38`**, que estrena los tres campos:
   - que `offset_threshold_ft` llegue con el valor de esa pista (**`0` si no está desplazada**, que es
     una medida y no un hueco),
   - que `vref_kt` traiga el del plan,
   - y que `trace_stop_reason` llegue con su motivo —o **`null`**, que también es un dato—.
2. **Y la comprobación con contenido**, cuando ese vuelo aterrice en una pista **desplazada**: la 17 de
   SKBG (496 ft) es la que tenemos medida. Ahí el offset es ≠ 0 y el residual tiene que seguir siendo
   ~0.
3. **Nada más de ningún lado.** El caso de los 6 números queda **cerrado con causa** —política de
   retención de cancelados, hoy trazable en el log— y los tres campos, implementados y verificados en
   nuestro lado con **684 pruebas**.
