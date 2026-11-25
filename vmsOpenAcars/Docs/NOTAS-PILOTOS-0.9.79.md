# Novedades de vmsOpenACARS — versión 0.9.79

**En una frase:** cuatro arreglos que salieron de revisar un vuelo real, todos en el registro y uno
en un aviso que asustaba sin motivo.

---

## 1. Los flaps ya no escriben una línea por cada temblor

Antes, mover la palanca una vez dejaba **siete líneas** en el registro, con saltos que nunca
ocurrieron (`0% → 38% → 13%` en el mismo segundo). El registro contaba las lecturas del simulador, no
lo que hiciste tú.

Ahora solo cuenta **la posición en la que los flaps se quedan**. En el vuelo que lo destapó, la
aproximación pasa de siete líneas a una y el despegue de doce a dos, sin perder ningún cambio real.

## 2. Se acabó el «BELOW MINIMUMS» imposible

Salía esto:

```
⚠️ BELOW MINIMUMS: 49 ft MSL < DA 50 ft
```

…en un aeropuerto cuya elevación es **134 ft**. Un avión a 49 ft MSL estaría bajo tierra. El 50 no era
la altitud de decisión del procedimiento: era el **TCH**, la altura a la que se cruza el umbral.

La altitud de decisión se calcula ahora como **elevación del umbral + 200 ft** (la estándar de una
aproximación de precisión), y **sin el dato de elevación no se avisa de nada**. En ese mismo vuelo el
aviso ya no puede aparecer.

## 3. Se dejó de inventar la pintura

Con el título `737-800 PAX SSW HD`, el cliente anunciaba **«Pintura: PAX»** —que no es una pintura,
es si el avión es de pasajeros—. Ya había pasado con `777` y con `ATR`.

La línea solo se escribe cuando el título contiene un **nombre de aerolínea reconocible**. Si no lo
contiene, no se escribe nada: preferimos una línea menos a una línea que miente.

## 4. El aviso de NavMap sale del registro

`⚠️ NavMap: runway not found for MPTO` venía de la integración con LittleNavMap, que solo funciona si
tienes esa base de datos instalada. Deja de aparecer como aviso; sigue disponible con la casilla de
depuración si alguna vez hace falta mirarlo.

## Sin cambios en el vuelo

No se toca la puntuación, ni los anuncios, ni la guía de rodaje, ni el envío del PIREP. Son cuatro
arreglos del registro y de un aviso.