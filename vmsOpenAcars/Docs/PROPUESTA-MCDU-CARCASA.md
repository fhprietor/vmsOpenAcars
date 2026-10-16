# La ventana como un MCDU del A320 — v0.9.45

Cómo se convirtió la ventana de vmsOpenAcars en el aparato: **carcasa de plástico azuloso gastado** y cada
grupo de información **hundido como una pantalla LCD**. La maqueta que se aprobó antes de escribir código
está en `Tools/McduPreview/out/06-carcasa-mcdu.png` y la captura de la aplicación real, en
`Tools/McduPreview/out/07-app-real.png`.

## 1. La paleta: muestreada, no elegida

Los colores salen de **muestrear la foto de referencia** (413×638) que pasó el mantenedor, contando píxeles
por zona. No son un gusto:

| Zona | Color | Respaldo en la foto |
|---|---|---|
| Pantalla — fondo | `#1B1D20` | 55.122 px, el color dominante del LCD |
| Pantalla — texto | `#F8F4ED` | 604 px (títulos y etiquetas) |
| Pantalla — valores | `#08DA09` | 519 px |
| Pantalla — anotaciones | `#09CFCF` | 374 px (`AIRAC`) |
| Carcasa — reflejo | `#93A6BD` | brillo del canto de arriba |
| Carcasa — claro / medio / bajo | `#859AB2` · `#7D90A8` · `#6A7C92` | la rampa del plástico |
| Carcasa — sucio / desgaste | `#5A6A7E` · `#505F71` | manchas junto a las teclas |
| Carcasa — sombra dura | `#39424D` | huecos y costuras |
| Tecla — cara | `#323233` / `#393839` | 11.900 px: **gris oscuro cálido, no negro** |

**Lo que NO está medido**: el ámbar y el rojo. En esa foto la pantalla solo tiene blanco, verde y cian —no
hay un solo píxel cálido (R>B+25) ni rosado—, así que van por **convención MCDU** (`#FFB020` y `#FF7A6B`,
este último porque el rojo puro de la aplicación se queda en 4,0:1 sobre el LCD y no pasa el umbral).

## 2. Las tres decisiones de diseño

1. **La carcasa va de `#859AB2` a `#5A6A7E`, no de `#93A6BD` a negro.** El tono más claro de la foto es un
   *reflejo del canto*, no el color del panel: un panel entero a `#93A6BD` se ve nuevo y limpio, que es lo
   contrario de lo que se pedía. El reflejo se pinta como una línea de 1 px.
2. **Sobre el plástico el texto va grabado en oscuro** (`Ink #1F2933`). No es estética: el blanco sobre
   `#859AB2` da **2,9:1** y el cian que usaba la aplicación **1,5:1**, los dos por debajo del 4,5:1 de WCAG.
   La tinta grabada da **5,1:1** y es lo que hace un panel de verdad. La información viva (AIRAC, METAR,
   estado) vive **dentro** de las pantallas, que es donde vive en un MCDU.
3. **El verde medido para los valores y uno rebajado para el cuerpo del log** (`#3FCB55`, 8,0:1). El del
   aparato (8,9:1) es auténtico pero cansa en un párrafo, y el log se lee durante minutos.

## 3. Qué es carcasa y qué es pantalla

- **Carcasa**: el fondo de la ventana, el layout (sus huecos son la mayor superficie de plástico), la
  cabecera, la barra de teclas y la tira de estado. Cuatro tornillos en las esquinas.
- **Pantallas**: `FLIGHT INFORMATION` (con sus tarjetas dentro, separadas por una línea y **no** por su
  propio bisel: bisel dentro de bisel es ruido), el `ACARS LOG`, `INCOMING MSG`, `METAR` y el `FMA`. Cada
  una con su **título de página** en blanco, su anotación en cian y su línea de subrayado, como el `ENG` /
  `AIRAC` del aparato.
- **Las rayitas blancas del bisel** sobre cada tecla de la barra: son las de las teclas de línea (LSK) del
  MCDU y son lo que convierte la barra en la columna de línea del aparato.
- El color de la leyenda de cada tecla y su convenio (`McduLegend`) **no cambian**: es pintura, no semántica.

## 4. El interruptor

`mcdu_look` (por defecto `true`) apaga la **textura** —grano, manchas, rayones y líneas de barrido— y deja
los colores y los biseles. Es la válvula para el piloto que lee el log durante horas. El contenido de las
pantallas se pinta **plano** en los dos casos: ver el punto 5.

## 5. Las trampas que costaron horas (y que están en el código)

Estas tres se descubrieron **verificando contra la ventana real**, no razonando:

1. **Nada que se pinte sobre una superficie pintada a mano puede llevar `BackColor = Color.Transparent`.**
   WinForms no sabe qué hay debajo y el rótulo sale con basura detrás —medido: cuadros de color con la
   forma del texto—. Por eso el contenido de las pantallas lleva **fondo sólido** del color del cristal y
   por eso el área de contenido se pinta **plana**, con la textura en la banda del título y en los márgenes.
2. **Un degradado de GDI+ definido sobre un rectángulo *virtual* no da el color que toca.** Medido: la
   barra, al 96 % de la altura, salía con el color del 26 %. Se sustituyó por una **función pura**
   (`McduChassis.RampAt`) que interpola las paradas medidas; así los extremos de cada panel son exactos y
   dos paneles contiguos coinciden en la junta (`LaRampaNoHaceEscalonEntrePanelesContiguos`).
3. **`PointToScreen` no sirve para saber dónde cae un panel**: devolvía **−220** para un control colocado
   en Y=40, y con eso todos los paneles pintaban el mismo trozo de rampa y **la ventana entera se veía
   plana** aunque cada panel pintara bien. Ahora se suma el `Top` de la cadena de padres
   (`UnPanelSabeSuAlturaDentroDeLaVentana`).

Y una cuarta, de composición: **lo que más superficie ocupa manda**. Con los paneles pintando la rampa pero
el layout y el flujo de estado de color plano, la impresión seguía siendo de plástico liso; hubo que darles
la carcasa a ellos también.

## 6. Lo que se verificó y lo que no

- **Verificado con la aplicación en marcha**: la captura de `07-app-real.png` es la ventana real con la piel
  puesta, y de ella salieron los tres fallos del punto 5.
- **Verificado con pruebas** (9 nuevas en `McduPaletteTests`): que los colores son los medidos, que **todo
  color de `Theme` queda legible** sobre el LCD (por reflexión, para que uno nuevo no se cuele), que el
  verde de lectura es más suave que el medido, que la tinta grabada se lee y el blanco no, que la carcasa se
  pinta igual en cada repintado (si no, parpadearía), que el interruptor cambia algo, que la pantalla
  reserva su banda de título, y que la rampa es monótona, continua entre paneles y sabe su altura.
- **No verificado**: cómo se ve en un monitor al 150 % de escalado (aquí no hay), ni el resto de
  formularios, que siguen con la piel anterior: van en una segunda pasada.
