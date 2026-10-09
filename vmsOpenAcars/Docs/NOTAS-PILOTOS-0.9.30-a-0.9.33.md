# Novedades de vmsOpenACARS — versiones 0.9.30 a 0.9.33

**En una frase:** la puntuación de la zona de toma pasa a medirse contra la pista que usas de verdad, el
aterrizaje guarda ya la meteorología del momento del contacto, y el análisis del aterrizaje —incluido el
**flare**— se puede mirar al detalle.

---

## Lo que cambia para ti

### La zona de toma se mide contra tu pista (0.9.30)

Los puntos siguen siendo **0, 3 y 7**, pero **las rayas dependen de la longitud de la pista**:

- **0 puntos** hasta `1.500 ft` **o el 15 % de la pista**, el mayor de los dos, **con techo de 3.000 ft**.
- **3 puntos** hasta `3.000 ft` **o la mitad de la pista**, el menor.
- **7 puntos** por encima, **siempre**.

En pistas cortas no cambia nada; en las largas se concede entre 150 y 650 ft. **El techo de 3.000 ft no se
toca**: en una pista larga, tocar a 3.200 ft sigue penalizando. *El detalle está en el comunicado de la
zona de toma.*

### La guía de rodaje se puede apagar (0.9.31)

Hay un **checkbox nuevo en Ajustes** (`Guía de rodaje`). Al desmarcarlo **no hay avisos RAAS, ni voz, ni
ventana de confirmación de ruta**, y la traza de posición vuelve a su ritmo normal. Aplica **sin reiniciar**.
Una instalación que ya tengas configurada **la mantiene encendida**: no hay que hacer nada.

### Cada aterrizaje guarda la meteorología del momento del contacto (0.9.31)

En el mismo instante del toque se guardan el **METAR del destino** y el **viento** que había, con sus
**componentes**: **en cara o en cola** y **cruzado**, con el lado. En el **logbook** lo ves en una franja
(`WIND 010/03 kt · RWY 14R · TAILWIND 1.4 kt · CROSSWIND 2.7 kt`) y el historial tiene su columna **Wind**.
También **va al PIREP**, así que las condiciones del aterrizaje quedan registradas para analizarlas.

Si el viento no se puede leer, no se inventa: se guarda el METAR y ya está.

### El análisis del aterrizaje se puede mirar al detalle (0.9.31 – 0.9.33)

- **Closeup del toque** en el perfil vertical: la **pista** dibujada, el **umbral**, tu **punto de toque** y
  las **bandas de la zona de toma**, para ver de un vistazo si tocaste dentro o fuera. Con varias escalas
  (5.000 ft, 2.500 ft y, si hay traza fina, **1.000 ft**).
- **El eje vertical se ajusta a lo que estás mirando**: antes se quedaba en la escala del perfil entero
  (miles de pies) y el tramo final se veía como una raya aplastada.
- **Ventana del FLARE** (botón **FLARE**): un gráfico propio con **altitud, velocidad y pitch** frente a la
  distancia al umbral, más la pista, el umbral y el punto de toque.
- **Traza fina del flare (0.9.32).** Hasta ahora el análisis se hacía con **una muestra cada 2 segundos**
  —a 150 kt, casi 550 ft entre puntos—, lo cual no permite juzgar un flare. Ahora se guarda una traza
  **diez veces por segundo** desde **1.500 ft antes del umbral** hasta **2 segundos después del toque**:
  entre 60 y 80 muestras en vez de 6. **No añade lecturas nuevas al simulador** (ya se consultan todos los
  datos 20 veces por segundo) y no ocupa nada apreciable.

> **Aviso honesto:** la traza fina **empieza a existir con esta versión**. Los vuelos anteriores siguen
> enseñando la traza de 2 segundos —que es el respaldo— y **el gráfico dice cuál está usando**, para que
> nadie confunda una resolución con otra.

---

*El resto del ACARS no cambia: ni el registro de vuelo, ni las puntuaciones, ni la conexión con la web de
la aerolínea.*
