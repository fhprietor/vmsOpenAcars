# Novedades de vmsOpenACARS — versión 0.9.48

**En una frase:** los anuncios de cabina ya no dependen de que NavData tenga la grabación —si no la tiene,
los dice la voz de Windows—, hay tres anuncios nuevos y el avión avisa cuando entra y cuando sale de
turbulencia.

---

## Qué cambia

- **Anuncios que antes no existían:** puerta cerrada al empujar, «ya pueden usar sus dispositivos» al dejar
  los 10 000 ft, **desarmar toboganes y chequeo cruzado** al dejar la pista, y llegada a puesto.
- **Turbulencia:** «estamos atravesando una zona de turbulencia, abróchense los cinturones» al entrar, y
  «hemos salido de la zona, pueden moverse por la cabina» al quedar liso. Una vez por zona.
- **Si el briefing no responde** (sin red, sin clave o un 503), las siete fases de siempre ya **no se quedan
  en silencio**: se locutan igual.
- **El destino se dice por su nombre completo** —«Aeropuerto Internacional El Dorado de Bogota»— en vez de
  solo la ciudad, y el anuncio de llegada dice la **hora local**.
- El registro de vuelo anota cada anuncio y en qué modo salió (`🔊 PA taxi_in (grabación)` / `(voz es)`).

## Lo que hay que saber

- **Hace falta una voz instalada.** El cliente usa la voz de Windows del idioma de la aerolínea; si ese
  idioma no está instalado, **locuta el anuncio en inglés** en vez de leer español con fonética inglesa.
  Se comprueba en unos segundos desde **Ajustes → anuncios de cabina → TEST** (13 fases).
- **Las grabaciones siguen teniendo prioridad.** Donde NavData tiene clip, suena el clip; la voz solo entra
  cuando no hay grabación.
- **La hora local es aproximada.** No hay zona horaria en los datos: se deduce de la longitud del
  aeropuerto, así que en países con horario de verano puede ir una hora desviada.
- **La turbulencia se mide, no se lee el letrero.** El aviso salta por lo que el avión está sintiendo
  (factor de carga y tasa vertical), no por la señal de cinturones, así que funciona igual en cualquier
  avión.
- **El volumen es el de siempre:** el deslizador de anuncios de cabina de Ajustes.
