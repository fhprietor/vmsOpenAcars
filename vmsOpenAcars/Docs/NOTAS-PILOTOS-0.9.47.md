# Novedades de vmsOpenACARS — versión 0.9.47

**En una frase:** si alguna vez rodaste con un solo motor y el cliente no te lo reconoció, era un error
de medición. Ya está arreglado.

---

## Qué pasaba

Para la bonificación de **motor único** el cliente medía el rodaje con la **velocidad de tierra** que
publica el simulador. Y esa velocidad **miente con el avión parado**: en un vuelo real marcaba 4–7 kt
durante ocho minutos en los que el avión se movió **60 metros**. Ese tiempo se contaba como rodaje y
**restaba** en el porcentaje; además podía cobrarte una penalización de luces con los motores todavía
apagados en el puesto.

## Qué cambia

- **El movimiento se mide con la posición**, no con la velocidad: si el avión no se ha movido, no hay
  rodaje que contar.
- **Sin ningún motor en marcha no hay rodaje.** Ni para sumar ni para restar.
- La línea del registro lo cuenta todo: los segundos con un motor, los segundos descartados y los
  **metros que rodaste de verdad**. Si algo no cuadra, se ve ahí.

## Lo que no cambia

La regla sigue siendo **la mitad del tiempo de rodaje con un solo motor**: no se ha bajado el listón,
se ha dejado de contar tiempo que no era rodaje.

**Y ojo:** un vuelo ya enviado **no cambia de nota** — se puntuó con la regla de entonces. Esto se
aplica a partir de ahora.
