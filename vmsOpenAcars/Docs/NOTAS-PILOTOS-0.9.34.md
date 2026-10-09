# Novedades de vmsOpenACARS — versión 0.9.34

**En una frase:** la traza fina del flare **se medía en vuelo pero se tiraba antes de guardarse**, así que el
análisis del flare nunca llegó a tener datos. Ya se guarda.

---

## Qué pasaba

En la 0.9.32 se anunció que cada aterrizaje guardaría una traza del flare **diez veces por segundo** desde
1.500 ft antes del umbral. El cliente **sí la medía** —el registro del vuelo lo decía: *196 muestras
capturadas*, en el vuelo del 7 de octubre—, pero **la descartaba justo antes de escribirla en el logbook**.

Consecuencia: si has volado con la **0.9.32** o la **0.9.33**, tu análisis del flare o no tenía datos, o
mostraba la traza gruesa de 2 segundos. **No era culpa de tu vuelo.**

## Qué NO se vio afectado

- **La puntuación, el PIREP y el registro del vuelo: nada.** El vuelo se guardaba y se puntuaba igual que
  siempre; lo único que se perdía era el detalle del análisis.
- **Tampoco había nada que configurar**, ni antes ni ahora.

Era un fallo silencioso: el vuelo se guardaba perfecto, solo faltaba la traza fina, y la ventana del flare se
abría para decir que no había datos.

## Qué cambia desde la 0.9.34

- **La traza fina se guarda de verdad**, así que el análisis del aterrizaje tendrá los **60–80 puntos** por
  aterrizaje en vez de los 6 de la traza de 2 segundos: la ventana del **FLARE** y la escala fina de
  **1.000 ft** del closeup pasan a tener datos con los que trabajar.
- **El registro del vuelo anota si la captura se armó.** Así una toma sin traza fina se puede distinguir de
  un vuelo antiguo, en vez de parecer siempre lo mismo.

## Lo que ya no se puede recuperar

**Los aterrizajes anteriores no se arreglan**: esa traza no llegó a escribirse. El primero con datos finos
será el próximo que hagas con esta versión.

---

*El resto del ACARS no cambia. Y una nota honesta: el arreglo está verificado con compilación y pruebas, pero
el camino completo —vuelo, envío y guardado— solo se prueba de verdad volando. El próximo aterrizaje es la
prueba.*
