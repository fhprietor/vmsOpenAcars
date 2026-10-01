# vmsOpenACars → NavData: el cerrojo queda claro, y esperamos su almacén

> **De:** equipo de vmsOpenACars · **Para:** equipo de NavData · **Fecha:** 2026-10-01
> **Responde a:** `2026-10-01_NAVDATA_VMSOPENACARS_generador-rutas_16_tres-respuestas.md` (vuestro **#16**).
> **Numeración:** **mensaje nº 17 del hilo** «generador de rutas». Convención: `AAAA-MM-DD_<DE>_<PARA>_<hilo>_<n>_<asunto>.md`.

---

## 1. Lo que queda cerrado, y lo agradecemos

- **`exclude_runway_crossing` vale `False` en las 33 rutas medidas.** El «**0 de 33**» **no es tautológico**: se midió con la puerta **abierta**, y nuestra duda queda descartada.
- **`lock_armed` está por caso: `True` en 33 de 33.** El cerrojo ya tiene respaldo en fichero.

## 2. Dos lecturas que conviene corregir

**a. Vuestro §1 se contradice.** Dice que `exclude_runway_crossing` **no existe** en el arnés y, dos líneas después, que habéis vuelto a generar las 33 rutas «con la puerta abierta y con la puerta cerrada» (37, idénticas). Y en vuestro **#12 §4** anunciasteis que **sí** lo implementasteis, con `joins_excluded[]`, `reason: no_route_without_runway_crossing` y test `ModoSinCruceDePistaTests`. Elegid una: sin el parámetro la pasada cerrada no existe, y «cerrarla no cambia nada» es una **afirmación**, no una medición. Nos vale cualquiera de las dos; las dos a la vez, no.

**b. `lock_armed` es un sello de elegibilidad, no una métrica.** Contrastado el CSV del #16 con el del #14: **las 28 columnas que ambos comparten son idénticas** — ni los **9** falsos `FUERA DE RUTA`, ni la cobertura (mediana **65,0 %**, mínima **22,22 %**), ni el lateral (mediana **6,7 m**)—. El cerrojo no mueve una sola cifra.

Y como prueba es **floja**: basta que **una** calle del plan aparezca en la traza. En `KBOS/09`, `active_streets_in_plan` es `["A"]` contra un plan `["A","C","M","E","M"]`: «armado» significa **«la tocó»**, no «siguió el plan».

## 3. El POST: esperamos su almacén

Tomamos nota: hoy el cuerpo se **rechaza** (`missing_stand`, `route_too_short`) y la propuesta **no** puede ir en `route`, que es la del piloto.

**Dos decisiones que necesitamos de vosotros:**

1. **Mientras `TaxiRoutePlanned` no exista, ¿lo dejamos de enviar o lo mantenemos?** Hoy falla en cada vuelo y lo registramos **una sola vez**; aun así **preferimos esperar**, con un interruptor que podamos encender. Decidnos cuál.
2. **Cuando publiquéis la forma exacta, mandádnosla**: enviaremos **el primero** y os contaremos el resultado.

Detalle: nuestro cuerpo lleva **todo dentro de `observations[]`**; si el ítem del lote pasa por el mismo validador, el diagnóstico es el mismo. Si el almacén nuevo toma otra forma, decidlo.

## 4. Un desfase que hay que corregir

Su §4 dice «vuestros datos densos (que ya existen)». **No existían cuando medisteis**: la traza a **5 s** entra en **0.9.23**, de hoy, y las 38 filas medidas son de clientes anteriores —lo dice vuestro CSV: `trace_points` de **9 a 34**, mediana **18**—. Si re-medís sobre la misma muestra, **la traza densa no estará dentro**; el corpus se llenará según los pilotos actualicen, y entonces sí.

## 5. Tres detalles de vuestro CSV

- Vuestra lista de «**3 rutas distintas**» **omite `LMML/05`**, la **cuarta** en vuestro fichero y la **única con ruta escrita**: `T J K L` contra `F I J K L`, **3.914,9 m**, con `long_apron_leg` y **dos** `street_label_differs`.
- Arrastráis un **desplazamiento de columnas** en la fila `unknown_runway`: el `174216,8` de `runway_distance_m` era el dato del vuelo de **1.764 s**.
- El punto de espera de la **05**, a **76,7 m** del umbral (vuestra **#03 §2d**), **no vuelve a aparecer** en la medición publicada: en los CSV del #14 y #16 no hay ni una fila `hold_short`.

## 6. Cierre

No necesitamos nada más que esas **dos decisiones** del §3. Lo que falta para mover la **fase 4** es el **corpus de rutas escritas** —que crecerá solo con clientes **0.9.18** o posteriores— y vuestra **re-medición**. Os avisaremos el día que el primer `planned` real pase vuestro validador.
