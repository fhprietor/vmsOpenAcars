# Respuesta (4) al equipo de phpVMS — el gráfico del flare, revisado con vuestras cifras

> **De:** equipo de vmsOpenACars · **Para:** equipo de phpVMS
> **Fecha:** 11/10/2026
> **Respecto a:** el análisis del aterrizaje que pintáis en el PIREP (vuelo 44, `kP9PDeXLPrNlY9me`)
> **Estado:** hemos comprobado vuestros chips **muestra a muestra** contra nuestra traza local: **la
> aritmética cuadra**. De las tres cosas que cambiaríamos del gráfico, **una es NUESTRO dato** — y
> tenemos la traza arreglada a mano para reenviárosla si la queréis.

---

## 1. Primero, lo que cuadra (y no es cortesía: está medido)

Ese panel es el **vuelo 44**, cuya traza tenemos en local (81 muestras), así que hemos podido mirar
de dónde sale cada número:

| Chip vuestro | Nuestra traza | |
|---|---|---|
| VS a 50 ft · **−923 fpm** | muestra con `agl_ft` = 51 → **−920 fpm** | 3 fpm |
| VS a 30 ft · **−919 fpm** | muestra con `agl_ft` = 30,0 → **−919 fpm** | exacto |
| Cabeceo en la toma · **6,6°** | muestra de contacto → **6,6°** | exacto |
| Flotación desde 50 ft · **682 ft** | de la muestra de 50 ft (dist −3) al toque (dist −698) → **695 ft** | 13 ft |
| Duración de la traza · **10 s** | **81 muestras en 10,01 s = 8,1 Hz** | |
| Panel derecho | último punto a **dist −1196 ft con 3,6°** | es nuestra traza |

Los 13 ft de la flotación son de método, no de error: vosotros interpoláis el cruce de 50 ft y
nosotros cogemos la muestra más cercana. Con eso, **el renderer está bien**; lo que sigue son cosas
del dibujo y **una de los datos que os mandamos**.

## 2. El cero del eje no está donde dice la etiqueta: 496 ft, y el dato es nuestro

El chip dice **«Toma desde umbral 202 ft»** y el panel derecho pone el contacto en **−698 ft**. La
diferencia son **496 ft**, que es exactamente el `offset_threshold_ft` de la **17 de SKBG** — la
misma pista que declara vuestro chip. **No es un fallo de vuestro gráfico: es una mezcla de
referencias nuestra**, y está documentada en `ESTADO-FLARE-2026-10-08.md` §2:

- `ProjectOnRunway` (el `touchdown_dist_ft` del payload) mide desde el **umbral legal**, restando
  `OffsetThresholdFt`.
- La **traza del flare** proyecta sobre el **extremo físico** de la pista, sin restar.

La aritmética la teníamos apuntada tal cual: `202,3 + 496 ≈ 698`. **El vuelo 44 es anterior al
arreglo** (la 0.9.37 ya suma el offset en `FlareDistanceFt`, así que la marca de toque, las bandas
de la TDZ y la traza comparten cero), así que lo que estáis pintando es una traza **previa al
cambio**.

**Lo que os proponemos, elegid:**

1. **Os reenviamos esa traza alineada** (mismo `POST`, mismo `source_name` de relleno): son las
   mismas 81 muestras con `dist_ft + 496`, que deja el contacto en −202 y cuadra con el chip. Lo
   tenemos preparado.
2. **O la marcáis como anterior al cambio** y lo dejáis como está. Para un vuelo suelto es
   cosmético; el día que pintéis las **bandas de la TDZ** sobre esta traza, no lo será.

**Y una petición de dibujo que sale de aquí:** pintad **la línea del umbral y la del toque** (esta
última desde `touchdown_dist_ft`, que sí viene bien). Un eje rotulado «Distancia al umbral» cuyo
cero no está en el umbral **miente en silencio**, y aquí lo delata que vuestro propio chip se
contradice con vuestro propio dibujo. Con las dos marcas, un desfase así se ve de un vistazo en vez
de descubrirse cruzando números.

## 3. «VS a 10 ft»: no sale de ninguna de las dos alturas que os mandamos

Este nos interesa especialmente, porque **es el chip que decide si el flare se completó**:

| | Altura más cercana a 10 ft | VS de esa muestra |
|---|---|---|
| Por `agl_ft` | 15,0 ft | **−812 fpm** |
| Por `radar_alt_ft` | 12,6 ft (nunca baja de ahí) | **−137 fpm** |
| **Vuestro chip** | — | **−587 fpm** |

**Y el motivo de fondo: en esa traza no hay ninguna muestra a 10 ft AGL.** La última en el aire
está a **15 ft** y la siguiente ya está en tierra, con **32 ft de pista entre las dos** — la altura
pasa de 15 a 0 **en un solo intervalo de muestreo**, lo que implicaría varios miles de fpm. Ese
último tramo por altura no es una medida: el AGL se cuantiza cerca del suelo y el flag de tierra lo
fuerza a 0 (de hecho, `radar_alt_ft` se queda en 12,6 ft con el avión ya rodando).

**¿De dónde sale el −587?** Lo preguntamos en serio, no como reproche: si sale de **vuestro log
ACARS** en vez de nuestra traza, es una definición distinta y hay que decirlo en el chip; y si sale
de una interpolación, con 8 Hz y 15 ft en el último intervalo el número está en el límite de lo que
el dato sostiene.

**Lo que haríamos:**

- **Medir esas puertas en distancia al umbral** (o en tiempo antes del toque) en vez de en altura:
  la distancia es continua y **sí** tiene resolución —8 ft a 150 kt—, la altura no.
- Si se dejan en AGL, decir **la columna y la tolerancia** («la muestra más cercana a 10 ft, ±5»).
- Y poner en el chip **la muestra y la tasa**: `10 s · 81 muestras · 8,1 Hz`. Con eso el lector sabe
  qué puede concluir y qué no, que es la mitad del valor de un informe de entrenamiento.

## 4. Tres cosas más del dibujo (por orden de impacto)

**a. El mismo eje X en los dos paneles, y las fases marcadas.** **17 de las 81 muestras tienen
`agl_ft = 0`** (todo el rodaje): dibujadas contra altura **caen todas en x = 0** y salen como **una
línea vertical**. Eso es el «pico» del final del panel izquierdo, no un dato, y con él se pierde el
20% de la traza. Y como un panel va contra altura y el otro contra distancia, **no se pueden leer
juntos**: la pregunta que un piloto quiere contestar —*¿el flare empezó antes o después de la
cabecera?*— no se responde mirando la gráfica. Con distancia en los dos, y las cuatro fases
sombreadas (aproximación · flare · toma · rodadura), se responde de un vistazo. Aquí hace falta: la
curva naranja **baja de 6,6° en el toque a 3,6° a −1196 ft**, y eso es *nose-hold* y derotación, no
un flare malo — pero en un panel titulado «flare» se lee como un flare malo.

**b. Escalas fijas por tipo, y bandas de referencia.** Con los ejes autoescalados (cabeceo 3-7°,
IAS 146-154) **cualquier aterrizaje parece dramático** y, sobre todo, **dos vuelos no se pueden
comparar**. Para las líneas base que estáis montando eso es crítico: con una **banda P50 por tipo**
en vez de un número suelto, el chip deja de ser un dato y pasa a ser enseñanza. Es exactamente lo
que habilitan los rellenos que os mandamos: la banda no sale de un criterio nuestro, sale de lo que
vuela la flota.

**c. Estilo de línea además del color, y una nota del eje invertido.** Las escalas teñidas por serie
están bien puestas (es lo primero que se mira), pero con daltonismo o impresión en blanco y negro se
pierde una de las dos series. Y el eje Y del panel izquierdo va invertido (−100 arriba): el pie
explica el orden del eje X, no eso.

**Y una rectificación nuestra, por honestidad:** sospechamos que el rango −100/−1000 estaba
**recortando** el pico del toque y lo comprobamos — el mínimo real de la traza es **−939 fpm**, así
que el rango está bien elegido y no recorta nada. El consejo de «no dejéis que el transitorio fije
la escala» aplica solo si recorta; aquí no.

---

## 5. Lo que nos queda

1. **El aviso de publicación de la 0.9.37** (sigue en pie por nuestra parte).
2. **La lista de los 10 vuelos sin PIREP**: la miráis en vuestro lado, que es donde está la
   información buena.
3. **La traza del 44 alineada**: dicha la palabra, os la mandamos con las mismas 81 muestras.
