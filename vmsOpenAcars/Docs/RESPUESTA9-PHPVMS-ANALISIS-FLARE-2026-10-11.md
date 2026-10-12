# Respuesta (9) al equipo de phpVMS — las horas, la semántica del offset y el tercer campo

> **De:** equipo de vmsOpenACars · **Para:** equipo de phpVMS
> **Fecha:** 11/10/2026
> **Respecto a:** vuestra respuesta (9): el primer vuelo de la 0.9.37
> **Estado:** **la corrección del offset aceptada y escrita en nuestro repo**; las **siete horas** que
> pedís, aquí abajo; y una aclaración: `trace_stop_reason` **ya está implementado** y los tres campos
> viajan en **0.9.38**, no en la 0.9.37.

> **Nota de canal:** por lo que decís, **no os han llegado ni la respuesta (6) ni la (8)** —las dos
> llevaban las siete horas—. Así que esta respuesta **lleva lo que iba en las dos** y es autosuficiente.

---

## 1. Las siete horas (lo que bloquea cerrar los 6 números)

Del **piloto 55**, y son **el instante del registro local**, que se escribe **antes** del `file`:

| Log local | Vuelo | Ruta | Instante del registro (UTC) | Tasa | Score |
|---|---|---|---|---|---|
| 7 | **875** | SKSM→MPTO | 2026-05-16 **14:11:11** | −331 | 32 |
| 11 | **62** | SKCL→KJFK | 2026-05-19 **02:41:45** | −447 | 39 |
| 12 | **8422** | SKCL→SKBO | 2026-05-20 **11:27:08** | −156 | 80 |
| 13 | **8180** | SKPE→SKRG | 2026-05-27 **00:04:10** | −392 | 31 |
| 17 | **55CH** | SKPS→SKSM | 2026-06-02 **01:05:18** | −272 | 50 |
| 19 | **7743** | SKCG→SKVP | 2026-06-21 **19:10:36** | −282 | 62 |
| 29 | **208** | SKRG→TNCC | 2026-08-26 **02:44:24** | −617 | 50 |

**Cómo leerlas, y es lo que os ahorra media hora:** ese instante es **anterior** a `submitted_at`,
porque el registro se copia antes del `await` que lo manda. Calibrado con los dos vuelos de mayo que
sí casaron: **+79 s** y **+302 s**, los dos en el mismo sentido. La ventana buena es **[t, t+10 min]**;
±10 min va sobrado.

**Huella secundaria:** el `score` de nuestro cliente viaja en el `file` y coincide con vuestro
`pireps.score` cuando está poblado —en octubre, vuestros `kP9PDeXLPrNlY9me` y `Me7BMgzvAwylvDan`
marcan **42** y **43** y nuestro log local también—. En mayo está a `null`: vale como confirmación,
no como criterio.

**Y un dato de contexto del 55CH:** el registro 17 (2026-06-02 01:05) es la pata **siguiente** a un
PIREP vuestro que sí casa —2026-06-01 22:39, SKRG→SKPS— y **anterior** al del 2026-06-15
(SKSM→SKCG). El piloto voló la cadena y esa pata quedó sin PIREP entre dos que están.

**Sobre el borrado duro:** vuestra verificación de `forceDelete()` es la pieza que faltaba, y encaja
con nuestro lado —lo vimos como `filed` porque lo fue, y en vuestra base ya no está—. Si la consulta
por ventana sale vacía, los seis se cierran como **no recuperables** y se quedan documentados. Una
idea, y no es petición: una línea en el `activity_log` dentro de ese `delete()` —piloto, número y
fecha— habría hecho diagnosticable el próximo caso en vez de invisible.

## 2. La corrección del offset: aceptada, y es la nuestra la que estaba incompleta

**Tenéis razón, y el matiz importa.** En la respuesta (7) escribí que con el campo «la alineación
pasa a deducción → resta», y eso vale **solo para la convención histórica**, la que tiene el extremo
**físico** como cero. Desde la 0.9.37 **alineamos en cliente**, así que la traza nueva ya viene al
umbral legal y aplicarle el offset la correría **dos veces** —en la 17 de SKBG, 496 ft, sobre una
traza que estaba perfecta—. El supuesto estaba mal en nuestro propio mensaje, no en vuestra
implementación.

Vuestra regla **desplazamiento = −residual**, con `residual = dist_toma_en_la_traza +
touchdown_dist_ft`, es la buena, y por una razón que se ve en nuestro código: **los dos caminos
comparten el cero por construcción**. `ProjectOnRunway` **resta** el offset al calcular
`touchdown_dist_ft` y `FlareDistanceFt` lo **suma** al proyectar la traza, así que los dos miden desde
el umbral legal y el residual tiene que salir ~0. **No es que salga 0 por suerte: no puede salir de
otra forma** cuando la traza está alineada.

Y hay un detalle que os ahorra un campo futuro: vuestra regla **ya detecta la convención**. El
residual vale ≈0 en las trazas alineadas y ≈−offset en las históricas, así que comprobar que está en
`{0, −offset}` distingue las dos sin que nosotros mandemos nada más. Por eso **no proponemos un campo
de convención**: sería un campo que envejece para decir algo que el residual ya dice.

Queda **escrito en nuestro repo** —`Docs/CHANGELOG.md` y `Docs/ESTADO-FLARE-2026-10-08.md`, con
vuestras cifras— justo para lo que pedís: que nadie lo «arregle» en la dirección contraria más
adelante. Las dos funciones y el porqué están en el mismo sitio, así que el que lo lea ve de dónde
sale el ~0.

## 3. `trace_stop_reason`: ya está implementado, y os debo la aclaración

En la (7) **propuse el nombre** y dije «decid si os vale». Vosotros lo aceptasteis, pusisteis la
columna… y nosotros **no lo estábamos enviando**. Es una errata nuestra, y la cuarta vez en este hilo
que falla el lado del que menos se sospecha. **Ya viaja**, y esto es literalmente lo que vais a
recibir:

```text
stopped:post-touchdown 2.0s     ← final normal (el margen tras el toque)
stopped:on-ground               ← final normal (toma suave, sin VS de toque)
stopped:cap                     ← TRUNCADA: tope de 400 muestras
stopped:timeout                 ← TRUNCADA: seguro de 45 s
```

**Y `null`, que es un caso con significado propio y no un hueco**: la captura **no llegó a cerrarse**
—no se armó, o el vuelo terminó con ella abierta—. Por eso publicamos el motivo **solo cuando el
cierre está sellado**: un `armed:threshold` sin cerrar no es un motivo de cierre, y mandarlo como tal
diría que la traza se cortó cuando lo que pasó es que el vuelo se acabó antes. Conviene que el prompt
lo lea así: **`null` no es un final, es «aquí no hubo cierre»**.

Los motivos van con **`CultureInfo.InvariantCulture`**, así que el número es `2.0s` y no `2,0s` según
el idioma del equipo del piloto. El prefijo hasta los dos puntos es estable; lo de después lo podéis
ignorar.

Sobre vuestra duda del vuelo que ya voló: con **166 muestras en 20,7 s** y el cierre en el margen
posterior al toque, el candidato natural es `stopped:post-touchdown`; pero tenéis razón en que **hoy
no se puede saber**, y ese es exactamente el agujero que el campo tapa.

## 4. Una corrección de versión, y también es nuestra

Los tres campos **no van en la 0.9.37**: esa versión **ya estaba publicada** cuando los añadimos, así
que viajan bajo **`vmsOpenACars/0.9.38`**. Es el motivo por el que subimos versión: `source_name` es
la única marca de procedencia que guardáis, y dejar una capacidad nueva bajo una cadena ya distribuida
la convertiría en una mentira justo en vuestro vuelo de control. Un vuelo con `0.9.37` —como el que
habéis medido— **no lleva los campos**, y eso no es un fallo: es la versión anterior.

## 5. Vuestro vuelo: la alineación queda verificada con el dato

Es el punto de control que llevábamos esperando desde la §2 del `ESTADO-FLARE`, y lo cerráis vosotros
mejor de lo que lo podríamos haber cerrado nosotros: **primer `on_ground` en −218,8 ft contra un
`touchdown_dist_ft` de 218,82 → residual +0,003**. Eso es la alineación en cliente **medida**, no
prometida, y coincide con la construcción de nuestro código (los dos ceros son el mismo).

Tres cosas más de las que coincidimos:

- **La puerta de 10 ft no aparece, y hace bien**: el dato no la sostiene, y era el caso que quitamos
  por insostenible. Que el gráfico no la pinte es el arreglo funcionando en producción.
- **166 muestras frente a 81 y 97** de los históricos, a la misma frecuencia: la captura cubre ahora
  más recorrido, y encaja con que el armado y el cierre cambiaron en la 0.9.37.
- **El detector con 32 cifras y 0 avisos**: el falso positivo por medio pie en el límite de redondeo
  (`<` contra `<=`) es exactamente la clase de ruido que había que quitar, y quitarlo en el comparador
  en vez de en el umbral es lo correcto.

## 6. Lo que queda

1. **Vosotros:** la consulta por ventana de la §1 y, con los tres campos ya en camino, la
   verificación del **siguiente vuelo** —que el offset no mueva una traza alineada y que
   `trace_stop_reason` llegue poblado—.
   - **Y un aviso sobre esa comprobación, porque solo muerde en un caso**: si el vuelo aterriza en
     una pista **sin umbral desplazado**, el offset llega **`0`** y verificar que no mueve la traza
     **no prueba nada** —un cero no puede mover nada, y las dos convenciones coinciden—. El caso que
     sí lo prueba es una pista **desplazada**, y tenemos una identificada: la **17 de SKBG, 496 ft**.
     Ahí el offset es ≠ 0, el residual tiene que seguir saliendo ≈0, y **se vería el doble
     desplazamiento** si alguien lo aplicara: el toque de la traza aparecería ~496 ft más allá de lo
     que dice `touchdown_dist_ft`. No hace falta ir a buscarla —el campo llega igual y vuestra
     validación `{0, −offset}` lo cubre—, pero es el vuelo en el que la comprobación tiene contenido.
2. **Nosotros:** nada pendiente. Los tres campos están implementados con su columna, su validación y
   sus pruebas —**683 en la suite**, tres nuevas para el motivo de cierre—, y el pie forzado no lo
   hemos tocado.
3. **Y una petición de proceso, por si sirve:** si algún documento nuestro vuelve a no llegaros,
   decidlo y reenviamos solo la parte que falte —las horas, en este caso, son siete líneas—. Nos
   habría ahorrado esta ronda a los dos.
