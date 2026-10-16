# Novedades de vmsOpenACARS — versión 0.9.45

**En una frase:** los botones ahora son **teclas encendidas de MCDU** y la ventana tiene **carcasa y
pantallas** — y de paso, 28 rótulos que seguían en inglés ya están en tu idioma.

---

## Los botones se encienden

Cada botón es una **tecla negra con la leyenda retroiluminada** en el color de su función, en vez de un
rectángulo de color con letra blanca encima. El color ya no rellena: **enciende** (lavado dentro del
capuchón, halo en la leyenda y una fuga de luz por la junta de abajo). Pulsada, la tecla se enciende.

Eso obligó a **aclarar los colores**: usados como letra sobre negro, los de antes se quedaban entre
**1,75:1** y **4,29:1** de contraste, por debajo del **4,5:1** que se pide para leer texto normal. El rojo
puro se quedaba en 4,39:1 y ahora es `rgb(255,118,110)`. **Cada botón conserva su familia de color**: un
`OFP` apagado sigue siendo azul y se distingue de un `MAPA` apagado — ninguno se vuelve gris.

## La ventana es un MCDU

Carcasa de plástico gastado y cada grupo hundido como una pantalla, con la paleta **muestreada de la foto
de un MCDU real**. Si no te convence, se apaga con `mcdu_look = false` en `vmsOpenACars.exe.config`
(viene **encendido**).

## Y 28 rótulos que seguían en inglés

Estaban escritos a mano en el código **y su traducción ya existía sin usarse**: `PLAN IN SIMBRIEF` es
ahora **`SIMBRIEF`**, `FETCH OFP` es **`OBTENER OFP`**, y así `ACEPTAR`, `VER ANÁLISIS`, `COMPARAR`…
También se arregló que **los emoji salieran como cuadros vacíos** (⚙️, 💾, 🗑, 📋, 🔄): se pintaban con una
librería que no enlaza fuentes, y algunos se partían en **dos** cuadros.

---

## Lo que hay que saber

- **Nada de esto toca tu puntuación ni tu vuelo**: es aspecto.
- El **aspa de cerrar es roja sobre negro**, y `CANCELAR` ya no se oscurece cuando hay vuelo activo: el
  rótulo ya dice lo que hace (`CANCELAR` contra `SALIR`).
- ¿No te gusta la carcasa? `mcdu_look = false` en `vmsOpenACars.exe.config` y vuelves a la ventana de
  siempre.
