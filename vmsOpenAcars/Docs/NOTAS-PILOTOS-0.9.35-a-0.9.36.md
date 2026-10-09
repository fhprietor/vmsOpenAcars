# Novedades de vmsOpenACARS — versiones 0.9.35 y 0.9.36

**En una frase:** el análisis del aterrizaje mira ya los **flaps** y la **potencia**, y el gráfico del flare
sale con una **cabecera de datos** para poder compartirlo.

---

## El análisis mira los flaps y la potencia (0.9.35)

En el análisis del flare ahora ves **dos cosas que antes medíamos y no te contábamos**:

- **Los flaps**: el ajuste **al cruzar el umbral** y **en la toma**, y **si lo cambiaste durante el flare**.
  Eso es lo que explica un flotado largo.
- **La potencia (N1)**: **en qué momento cortas**. El corte se mide **contra el pico de tu propia
  aproximación**, no con un número fijo, porque cada avión vuela la aproximación con un N1 distinto. Se
  expresa en **segundos antes de la toma**.

Lo tienes en el **historial** (columnas **FLAPS** y **PWR CUT**), en la ventana del **FLARE** (con la marca
del corte sobre la curva de N1) y en el **PIREP**: `~FLAPS 30 | PWR-CUT 4.2s`.

> **Un matiz de honestidad, para que no te extrañe:** lo que el simulador publica de serie es la
> **posición del mando en porcentaje (0–100)**, que **no es un detent**. Cuando el avión publica el
> detent de verdad, lo enseñamos **exacto** (`FLAPS 30`, `CONF 2`). Cuando no, lo deducimos por familia y
> va marcado **aproximado** con una tilde: **`≈FLAPS 30`**. Y si no se puede deducir con seguridad, sale
> **solo el porcentaje**, sin etiqueta. **Si ves el `≈`, es eso** — no es un error.

## El gráfico del flare se puede compartir (0.9.36)

La ventana del FLARE lleva ahora una **cabecera de datos** pensada para que otro piloto entienda la imagen
de un vistazo:

- **Aeronave** — `B77L [PMDG]` —, **fabricante** y **ventana del vuelo** (UTC, ruta, pista, puntuación).
- **LTOW**: el peso **en el momento de la toma**, medido en libras.
- **Toma**: tasa de descenso, GS/IAS del contacto, distancia al umbral y desviación de eje.
- **Flaps**, **corte de potencia** y **viento** del aterrizaje, más la **versión del cliente** que lo generó.

Y un botón **💾 PNG** para guardar el gráfico.

Dos cosas que conviene saber de esa cabecera: el **fabricante se deduce del tipo de avión** (Boeing,
Airbus…), y el **desarrollador del avión** (`PMDG`, `ToLiss`, `iFly`) **solo aparece si tu avión lo publica
en su nombre** — si no sale, no es un fallo: es que tu avión no lo dice. **Si el peso de la toma sale
vacío**, es que tu simulador no publica el peso bruto: **no se inventa** ni se deduce del plan de vuelo.

### Y una corrección que sí se notaba

El **registro del vuelo** y el **gráfico** podían decir cosas distintas del mismo avión: en el log salía la
**familia** (`B777`) y en el gráfico la **variante** (`B77L`). Ahora **dicen exactamente lo mismo**, y hay
una prueba que lo impide.

---

## Lo que hay que saber

- La **traza fina del flare existe desde la 0.9.34**: los vuelos anteriores enseñan la traza de 2 segundos,
  y el gráfico **dice cuál está usando**.
- Nada de esto toca tu **puntuación**: es **análisis**, no castigo.
