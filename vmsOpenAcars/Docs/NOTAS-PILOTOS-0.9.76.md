# Novedades de vmsOpenACARS — versión 0.9.76

**En una frase:** la pista de la toma se resuelve en el **primer contacto** —no 33 segundos después,
ya rodando— y la traza del flare **incluye el toque**, así que el análisis ya puede usar el umbral, el
TCH y la senda de esa pista.

---

## La pista de la toma

El primer vuelo de la 0.9.75 llegó con el nombre de la pista (`35`) pero **sin el resto**: sin largo,
sin umbral desplazado, sin elevación del umbral, sin TCH y sin senda. El análisis no podía usar la
pista y la distancia de toma salía **0**.

La causa era el momento en que se buscaba la pista: el aviso de «toque» del simulador llegó **33 s
después** del contacto, cuando el avión ya estaba **pasado el extremo** de la pista. Ahora la pista se
busca con la **posición del primer contacto**, la que se guarda en el instante del toque.

En una pista como la 35 de SKBG eso significa: umbral **3.854**, extremo **3.901**, senda **3,0°**,
umbral desplazado **421 ft** y, cuando NavData no puede derivar el TCH (esta pista), el motivo
`displaced_threshold` en vez de un número inventado.

## La traza ya incluye el toque

La traza terminaba **1,2 ft sobre la pista**: el cierre por «en tierra» era correcto, pero la muestra
que lo disparaba no se guardaba. Sin una muestra en tierra, el análisis no podía situar el toque y su
respaldo lo inventaba **484 ft antes**.

Ahora la muestra del toque se guarda: la traza acaba con `on_ground = true`, el radar de ruedas a
**≈ 0** y la distancia del toque. La maniobra se puede medir donde ocurrió.

## El G del toque

El mismo aviso tardío se llevaba el G: la ventana de 1 s se evaluaba 33 s después del contacto y
quedaba a 0, así que el registro decía «— G» y el criterio no se aplicaba. Ahora, si la ventana viene
vacía, se usa el G del **frame del toque**.

## Lo que hay que saber

- **No hay nada que configurar.**
- **Los vuelos anteriores no se reescriben.** El análisis nuevo usa los campos nuevos; las trazas
  viejas conservan su aviso.
- **Los umbrales de puntuación no cambian.** Lo que cambia es que la pista y el toque están donde
  deben para poder medirlos.
- La verificación definitiva es el siguiente vuelo con la 0.9.76: debe traer **3.854**,
  `displaced_threshold`, **3,0°** y una distancia de toma distinta de 0.
