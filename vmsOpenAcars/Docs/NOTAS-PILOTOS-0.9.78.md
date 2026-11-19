# Novedades de vmsOpenACARS — versión 0.9.78

**En una frase:** el informe técnico que salía al **abrir** el ACARS ya no aparece en el registro;
queda detrás de la casilla de depuración.

---

## Qué cambia

Al arrancar, el cliente escribía siete líneas de diagnóstico: el sistema operativo, el procesador, la
tarjeta gráfica, si el proceso es de 64 bits y si va elevado, el estado de las librerías nativas, y el
aviso cuando no encuentra el simulador con el detalle técnico del error. **Nada de eso es un evento de
vuelo**, así que pasa a formar parte del registro de depuración.

- Con la casilla **«Mostrar líneas de depuración»** apagada —como viene de serie—, el registro
  empieza limpio y solo cuenta lo del vuelo.
- Encendida, vuelven a verse, sin reiniciar.

## Lo que hay que saber

- **El estado del simulador sigue a la vista en la interfaz**: si no conecta, lo sigue diciendo; lo que
  ya no se escribe es el volcado técnico.
- **Estas líneas de arranque no se envían a la aerolínea**, a diferencia de las de depuración que
  ocurren **durante** el vuelo, que sí llegan marcadas con `[DBG] `. Al arrancar todavía no hay vuelo
  abierto, así que se quedan solo en tu pantalla. Es el precio de tener el registro limpio.
- Si alguna vez necesitas ese informe para reportar un problema, enciende la casilla y lo tendrás.

## Sin cambios en el vuelo

El resto del cliente no se toca: ni anuncios, ni avisos de pista, ni puntuación, ni análisis del
aterrizaje, ni el envío del PIREP.