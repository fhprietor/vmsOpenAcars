# Novedades de vmsOpenACARS — versión 0.9.67

**En una frase:** los avisos de rodaje dicen cada cosa **una vez**, y
desaparece el aviso de «deberías estar en…» que salía después de enviar el PIREP.

---

## Qué cambia

**Los avisos de rodaje ya no se repiten.** Antes, si seguías acercándote al mismo cruce,
el aviso volvía a sonar cada veinte segundos. En un rodaje largo llegó a decir **seis veces**
«calle K1 a la derecha en 80 metros». Ahora cada maniobra se dice **una sola vez**, y solo se
repite si la situación cambia de verdad: cuando pasas el cruce, cuando cambias de calle o de
pista, o cuando te alejas y vuelves a acercarte.

**Se acabó el aviso de base después de aterrizar.** Al enviar el PIREP aparecía un
«deberías estar en …» señalando el aeropuerto base, con el vuelo ya terminado y estando donde
tenías que estar. Ahora, una vez cerrado el vuelo, ese aviso no sale; y si sales con el vuelo
en marcha, compara con el aeropuerto de **llegada**, no con tu base.

**La pista se guarda en el análisis del aterrizaje.** En algunos vuelos el registro se
guardaba sin pista aunque el cliente la conocía. Ahora se conserva la del toque y, si no se
pudo resolver, la que ya tenía la aproximación.

**Ya no se inventa una pintura.** Con aviones cuyo título solo dice el fabricante y el modelo
—`ATR 72-600 Passenger`— se imprimía «Pintura: ATR». Ahora, si el nombre no es una pintura de
verdad, la línea no se muestra.

## Lo que hay que saber

- Nada que configurar: se aplica solo.
- Si vuelas con la guía de rodaje apagada en Ajustes, los dos primeros cambios no te afectan.
- Los avisos siguen siendo los mismos y en el mismo orden; solo dejan de repetirse.