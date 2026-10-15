# Novedades de vmsOpenACARS — versión 0.9.43

**En una frase:** un vuelo ya **no se puede quedar sin poder enviarse**, y cuando el motor único no te da
los +5 el log te dice **exactamente por qué**.

---

## El vuelo que no se podía enviar

Un piloto aterrizó, llevó el avión al parking y lo apagó. El ACARS **nunca le dio el ON BLOCK**: lo único
que le ofrecía la aplicación era **cancelar**, y cancelar **borra el PIREP**. El vuelo lo tuvo que cerrar
un administrador a mano.

Esto es lo que pasaba, en orden:

1. **Un rebote en la toma se leyó como un touch-and-go.** El cliente daba por hecho un despegue solo con
   ver **velocidad**: 5 segundos después de tocar, el avión iba a 88 kt, pero **descendiendo** a −178 fpm.
   Eso no es despegar, es la carrera del aterrizaje.
2. **La fase se fue a «ascendiendo» y ya no volvió.** El ON BLOCK solo se alcanza desde el rodaje de
   llegada, y desde una fase de vuelo no había camino de vuelta al suelo.
3. **El simulador decía que el avión estaba en el aire** mientras rodaba y aparcaba: durante casi tres
   minutos, con el avión entre 6 y 21 kt y la altitud clavada en el mismo valor.
4. La altitud se medía contra el **aeropuerto de salida**, así que el log publicaba **264 ft** de altura
   con el avión aparcado. Un número falso que hacía que todo lo demás pareciera coherente.

**Lo que cambia:**

- **Un touch-and-go tiene que demostrarlo:** además de la velocidad, exige que el avión **suba de verdad**
  (200 fpm sostenidos 3 segundos).
- **La señal de «en el suelo» se confirma en los dos sentidos.** El cliente ya sabía que el simulador la
  parpadea al tocar (por eso esperaba antes de dar la toma por buena); ahora también espera para dar por
  hecho un despegue.
- **Si estás en una fase de vuelo pero el avión está rodando por el suelo, la fase vuelve al suelo** en
  15 segundos. Es la garantía de que el ON BLOCK siempre sea alcanzable.
- **Con la toma registrada, la altitud se mide contra el aeropuerto de llegada**, en cualquier fase.

Con esto el vuelo se habría cerrado solo. Y por si algo se vuelve a torcer:

> ## ON BLOCK manual
>
> En el **menú del icono de la bandeja** (junto al reloj) y en el **diálogo de CANCEL**. Hace el mismo
> cierre que la detección automática, así que **después puedes enviar el PIREP con normalidad**.
>
> Si algún día te quedas sin ON BLOCK, **no canceles**: cierra a mano y envía. Cancelar **elimina** el
> PIREP y el vuelo no se acredita — el aviso de CANCEL ahora lo dice con esas palabras.

---

## Motor único: si no te da los +5, ahora sabes por qué

El otro vuelo que destapó trabajo: un piloto rodó **290 segundos de 630 con un solo motor**, y no tuvo
**ninguna forma** de saber por qué no cobró la bonificación. El motivo era que el **46 %** se quedaba por
debajo del **50 %** que pide el criterio… pero eso no lo decía nadie en ningún sitio.

Ahora el log lo dice **siempre**, con las cifras:

```
🚶 Motor único en salida: 290 de 630 s con un solo motor (46 %) · mínimo 50 % · sin bonificación
```

Y al cerrar el vuelo se publica también el **rodaje completo**, con el rodaje de salida y el de llegada
sumados en un solo porcentaje. **Ojo con esto:** ese porcentaje **informa, no aprueba**. La bonificación
sigue necesitando que **una** de las dos fases llegue al 50 %, porque sumar las dos **nunca** puede dar
más de lo que ya daba una sola. Si lo ves en el log, es para que tengas el número completo, no un criterio
nuevo.

Los contadores se guardan además en tu registro local de aterrizajes, así que quedan para consultarlos.

**El umbral no se ha tocado: sigue en el 50 % del tiempo en movimiento, por fase.** Lo que cambia es que
ahora lo puedes ver.

---

## Otras tres cosas que te quitaban puntos sin motivo

Van en el mismo paquete, porque son del mismo tipo: el cliente cobraba o avisaba **antes de tener motivo**.

- **Las luces de taxi del pushback (0.9.40).** A un piloto se le cobró el −5 de «luces de taxi apagadas
  durante el rodaje» **en el mismo instante** en que se declaró el rodaje… con el avión todavía
  **remolcado** y los motores apagados. Ahora esa comprobación **espera 5 segundos** y mira las luces
  **de verdad**, en vivo. Si de verdad ruedas más de 5 segundos con la luz de taxi apagada, **sigue
  penalizando**. El −5 de IVAO se aplaza igual.
- **El tope del pushback (0.9.41).** El tiempo máximo que el cliente aguantaba en «remolcando» eran 90
  segundos, y con eso **se disparaba a mitad de todos los pushbacks reales** que medimos (**142** y **148**
  segundos). Ahora son **300**: el tope es solo el rescate de quien nunca pone el freno de parqueo, que
  sigue siendo la señal buena.
- **El aviso de «has aterrizado en otro aeropuerto» (0.9.42).** La comprobación previa preguntaba por la
  **altura equivocada** (la del destino planeado, no la real), así que podía dispararse de más. Ahora
  pregunta por la **altitud real** del avión.

---

## Lo que hay que saber

- **Nada de esto ha volado todavía.** Está verificado contra los datos grabados de los dos vuelos reales
  que lo destaparon —muestra a muestra—, y con 804 pruebas automáticas, pero **ningún piloto ha volado con
  la 0.9.43**. Si en tu próximo vuelo el ON BLOCK llega solo, o si te sale el aviso de «fase de vuelo con
  el avión en el suelo», cuéntalo: es exactamente lo que hay que confirmar.
- **Hay un aviso nuevo en el log** para ese caso: si el cliente cree que estás volando y el avión lleva 15
  segundos rodando por el suelo, lo escribe. Es la línea que habría delatado el problema del principio en
  **una sola línea**.
- **La barra de botones se ha aligerado.** Se han quitado **MENU** y **ATIS**, que no hacían nada (lo del
  menú ya está en el engranaje de la cabecera y en Settings; el ATIS se pinta en su panel). La barra es más
  estrecha y **ya cabe en el ancho mínimo de la ventana**: hasta ahora, si encogías la ventana, **START y
  CANCEL se quedaban fuera del panel y no se podían pulsar**. De paso, los rótulos de la barra están
  traducidos (MAPA, METEO, BITÁCORA, INICIAR…).
- **Nada de esto cambia tu puntuación**, salvo las tres cosas de arriba, que la cambian **a tu favor**.
- Si te quedas sin ON BLOCK: **ON BLOCK manual** desde la bandeja. **No canceles.**
