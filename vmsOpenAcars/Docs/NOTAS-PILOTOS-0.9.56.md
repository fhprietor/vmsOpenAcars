# Novedades de vmsOpenACARS — versión 0.9.56

**En una frase:** la casilla de avisos de espacio aéreo del OSD ahora calla **todo** lo que debe callar, y
las fronteras de clase E dejan de aparecer en pantalla.

---

## Qué pasaba

En *Settings → OSD* la casilla se llamaba **«Zonas restringidas → Avisar en el OSD»**, y en la práctica
solo silenciaba **tres** avisos: las zonas prohibidas/restringidas/peligrosas y los dos avisos de
`AIRSPACE AHEAD` y `DO NOT DESCEND`. Las **entradas en CTR, TMA, CTA, ATZ y RMZ** se pintaban **siempre**,
con la casilla marcada o no.

Un piloto lo reportó en su llegada a San Juan, y tenía razón: entre las 19:01 y las 19:36 de su vuelo
`nk2a96dl5vO0B2ab` el registro tiene **19 entradas** de espacio aéreo, y **cuatro caen en los últimos tres
minutos de la final** (`CTR SAN JUAN LUIS MUNOZ MARIN … CLASS C`, `TMA SAN JUAN LUIS MUNOZ MARIN`).

## Qué cambia

- **La casilla calla ahora todos los avisos de espacio aéreo en el OSD**, no solo las zonas acotadas. Y se
  llama como lo que hace: **«Espacios aéreos → Avisar en el OSD»**.
- **Las `CTA` (clase E) no se pintan nunca, solo van al registro.** `CTA SAN JUAN LOW`,
  `CTA PUERTO RICO CLASS E5`, `CTA PONCE CLASS E2`… cruzar una de esas fronteras no te pide nada, y eran
  casi todos esos 19 avisos. Las `CTR`/`TMA`/`ATZ`/`RMZ` siguen avisando: llevan frecuencia y autorización.

## Y lo que **no** cambia con la casilla

Apagar los avisos **no** perdona una invasión. Si entras en una zona prohibida, restringida o peligrosa
—por debajo de su techo, que es lo que es invadir— el vuelo **va a revisión**, y el PIREP no se acepta
solo. El registro te lo dice al filear, con el nombre de la zona, y queda también en las notas del vuelo
para quien lo revise.

Hasta ahora eso no funcionaba: el cliente mandaba `state = 2`, que en phpVMS significa «aceptado», en
**todos** los vuelos. O sea que **ningún PIREP pasaba por revisión nunca**. Ahora no se toca el estado
(lo fija el servidor) y se manda la marca de espacio restringido, que es lo que lo detiene.
## Lo que no cambia

El **registro** del vuelo y el **polígono en el mapa** siguen igual: apagar el OSD no te quita ni la línea
del log ni la traza. Y el cambio aplica en vuelo, sin reiniciar.
