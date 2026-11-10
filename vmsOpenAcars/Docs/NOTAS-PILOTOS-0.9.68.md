# Novedades de vmsOpenACARS — versión 0.9.68

**En una frase:** el METAR ya se actualiza cuando toca, y el botón de refrescar **refresca de
verdad**.

---

## Qué cambia

**El botón de refrescar ahora sí refresca.** Antes pulsabas y no cambiaba nada: el cliente se
guardaba el informe cinco minutos y el botón volvía a leer lo guardado sin preguntar al servidor.
Ahora el refresco manual va siempre a la red.

**El METAR caduca cuando hay uno nuevo**, no cuando pasa el reloj. Si ya han dado las 03:00 y el
informe que tenías era de las 02:00, se descarta aunque lo hayas pedido hace veinte segundos. Y si
no hay informe nuevo publicado, se conserva el que tienes en lugar de quedarse en blanco.

**Y el motivo de fondo, que era un fallo concreto:** en aeropuertos con **viento variable** —el
informe lo escribe como `VRB`, por ejemplo `VRB01KT`— el cliente **no podía leer el informe nuevo**.
Lo intentaba, fallaba al interpretar ese campo y se quedaba con el anterior para siempre. Por eso
pasaba sobre todo en sitios como SKRG y no en otros. Ya se interpreta bien: `VRB` significa «sin
dirección fija» y así se muestra, no como si el viento viniera del norte.

**Y cada 3 minutos se comprueba**, en vez de cada 5, para que un informe recién publicado no tarde
de más en aparecer.

## Lo que hay que saber

- No hay nada que configurar.
- Si el aeropuerto aún no ha publicado el informe de la hora nueva, seguirás viendo el anterior: es
  lo correcto, no un fallo.
- La hora que aparece en el METAR (`ddHHMMZ`) es la del **informe**, no la de tu reloj: una METAR de
  las 03:00 puede seguir siendo válida a las 03:50.