# Novedades de vmsOpenACARS — versión 0.9.74

**En una frase:** la nota del aterrizaje deja de contradecirse, los avisos de pista solo
suenan cuando vas hacia el punto de espera, y la **Vapp** ya no te aconseja por debajo de tu Vref.

---

## El G del aterrizaje: una sola tabla

La línea de datos decía **«1,72g (Normal)»** y el vuelo cobraba **los 15 puntos** de la banda
máxima. El motivo era que el rótulo y la puntuación usaban dos umbrales distintos —1,8 frente a
1,7—. Ahora los dos salen de **la misma tabla**, así que no pueden contradecirse: se acabó ver
«Normal» mientras te descuentan.

## Los avisos de pista

«Aproximándote a la pista» y «espera antes de pista» saltaban al **rodar en paralelo** a la
pista, aunque estuvieras lejos del punto de espera: en un rodaje largo llegó a avisar tres veces
de la misma pista. La causa era que la condición de rumbo no filtraba nada. Ahora se exige
**rumbo hacia el punto de espera** —cono de morro de 45°— además de la distancia. Rodando de
través o en paralelo, no suena.

## La reversa que no existía

El registro llegó a decir **«máximo del rodaje ENG1 100 % / ENG2 100 %»** y con eso anunciaba un
cool-down de 180 s. Un 100 % de reversa rodando no es un despliegue: es un dato erróneo del
avión. Ahora solo cuenta como reversa lo que ocurre **en el suelo, por encima de 40 kt y en los
30 s siguientes al toque**; lo demás se ignora en silencio. El despliegue real del toque sigue
contando igual.

## `ON BLOCK` a tiempo

Con el **freno de parqueo puesto y todos los motores parados** el vuelo está terminado: ahora se
cierra en **5 segundos** en vez de esperar más de un minuto. Si falta el freno o hay hotel mode,
se mantiene la detección de siempre.

## La Vapp y el Vref del plan

El avión calculaba **Vref 150** y el cliente esperaba **139**, así que publicaba «Vapp 144». Eso
no era solo una observación injusta: era un consejo **por debajo de tu Vref real**.

- La **Vapp nunca baja del Vref** que se usa como referencia, y cuando ese Vref viene del plan y no
  del avión, la línea lo dice: **«Vapp 144 kt (del plan)»**.
- Si la velocidad está **8 kt o más por encima** del Vref del plan y fuera de la ventana, se
  entiende que el Vref de referencia no es el tuyo (por ejemplo, otra configuración de flaps) y
  **el criterio de velocidad no se aplica** en vez de penalizarte.
- **Ir lento sí se sigue penalizando**: con un Vref real más alto, ir corto es peor.

## Detalles menores

- La línea del toque ya no dice `FLAPS FLAPS 25 (75%)`.
- El aviso de retroceso por la pista sale **una vez** por maniobra, no dos.
- Sobraba un punto delante de «Diversión a … rechazada».

## Lo que hay que saber

- No hay nada que configurar.
- Los umbrales de puntuación **no han cambiado**: lo que cambia es de dónde salen los números y
  cuándo un criterio no se aplica por falta de un dato fiable.
- Si tu avión publica su propio Vref, ese mandará en cuanto se conecte esa vía. Mientras tanto, lo
  que viene del plan se marca como tal.