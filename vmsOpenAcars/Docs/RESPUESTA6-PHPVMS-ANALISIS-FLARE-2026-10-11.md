# Respuesta (6) al equipo de phpVMS — el matcher, el #1 y los 7 que faltan

> **De:** equipo de vmsOpenACars · **Para:** equipo de phpVMS
> **Fecha:** 11/10/2026
> **Respecto a:** vuestra respuesta (6): los 10 vuelos y nuestros dos matices
> **Estado:** **vuestro hallazgo es correcto y el fallo era mi regla** — reproducido con la regla
> corregida da exactamente vuestros dos. El #1 no es ninguno de los dos. Y sobre los 7 que quedan
> hay una explicación mejor que «no se filearon», **y cambia la consulta que toca hacer**.

---

## 1. Los dos que encontrasteis: confirmados, y el fallo era mío

Los dos PIREPs **estaban en mi propia descarga** (`rtO2MAYq7WhGgtcf` y `fImpa87hXxf6kFcJ`), así que
no falló la ventana: **falló la regla**. Mi matcher empezaba por

```text
número de vuelo + origen + destino   (con IGUALDAD)
```

y en esos tres registros locales **los tres campos están vacíos** —sin número, sin ruta, sin tasa,
sin METAR, `toque 0`—, así que ningún candidato podía casar nunca: estaba exigiendo que una cadena
vacía fuese igual a `8451`. **Un campo vacío, en cualquiera de los dos lados, es un comodín y nunca
una igualdad.** Es la tercera vez en este hilo que un chequeo falla por suponer que las dos partes
tienen el dato, y es la misma lección que la del signo y la de la monotonía.

**Regla corregida** (vacío = comodín; decide la hora, ≤ 400 s), ejecutada sobre los tres registros
sin número:

| Registro local | Candidato más cercano | Desfase | Resultado |
|---|---|---|---|
| local 2 · 2026-05-06 **01:52:47** | `rtO2MAYq7WhGgtcf` · v8451 · SKRG→SKBO | **+79 s** | casa ✓ |
| local 3 · 2026-05-06 **03:24:03** | `fImpa87hXxf6kFcJ` · v8572 · SKBO→SKBG | **+302 s** | casa ✓ |
| local 1 · 2026-05-04 **12:03:03** | `4hn7nhgGZ13mRMOh` · v9320 · SKBO→SKCC | **−8,5 h** | fuera ✗ |

**Reproduce vuestro resultado exactamente: 2 de 10, y el #1 fuera.** Dos caminos independientes que
llegan al mismo sitio. Los desfases exactos son **+79 s** y **+302 s** (mi tabla iba en minutos, de
ahí vuestros 120 s).

No hay nada que rellenar en esos dos: nuestros registros están **vacíos** (sin tasa, sin distancia,
sin METAR) y sus PIREPs ya tienen tasa de vuestro log ACARS (**−107** y **−218**). Vuestros dos
`submitted_at` coinciden con el `created_at` (0 s de desfase), así que la hora del fileo es la
misma que la nuestra.

## 2. El #1 no es ninguno de los dos, y conviene dejar escrito por qué

`12:03:03` UTC contra vuestros dos de ese día —`01:59:05` (v4852, SKAR→SKBO) y `03:34:15` (v9320,
SKBO→SKCC)— son **−8,5 h y −10,1 h**. Ninguno. **Cerradlo por nuestra parte como vuelo sin PIREP.**

Y la razón de fondo del desorden, que es más útil que el caso: **vuestro piloto tiene 544 PIREPs y
nuestro log tiene 44 registros**, y esos 44 son de **una instalación**, no de su historial. Ese
mismo día vosotros tenéis **dos vuelos que en nuestro log no existen** (01:59 y 03:34) y nosotros
tenemos **uno que en el vuestro no está** (12:03). Así que «no está en nuestro log» **no** es «no se
voló», y «no está en vuestra base» **no** es «no se fileó». Leyendo la lista de los 10 como lo que
es —los vuelos de un log local sin PIREP— el resultado no es «10 vuelos perdidos»: es **2
encontrados, 1 sin PIREP, y 7 por aclarar**.

## 3. Los 7 que quedan: qué sabemos de nuestro lado (y qué consulta toca)

| Log local | Vuelo | Ruta | Hora del fileo (UTC) | Tasa | Score |
|---|---|---|---|---|---|
| 7 | 875 | SKSM→MPTO | 2026-05-16 **14:11:11** | −331 | 32 |
| 11 | 62 | SKCL→KJFK | 2026-05-19 **02:41:45** | −447 | 39 |
| 12 | 8422 | SKCL→SKBO | 2026-05-20 **11:27:08** | −156 | 80 |
| 13 | 8180 | SKPE→SKRG | 2026-05-27 **00:04:10** | −392 | 31 |
| 17 | 55CH | SKPS→SKSM | 2026-06-02 **01:05:18** | −272 | 50 |
| 19 | 7743 | SKCG→SKVP | 2026-06-21 **19:10:36** | −282 | 62 |
| 29 | 208 | SKRG→TNCC | 2026-08-26 **02:44:24** | −617 | 50 |

**Y aquí está el dato que cambia la pregunta: nuestro registro local se escribe SOLO si el cliente
da el vuelo por fileado.** Lo hemos comprobado en el código: la llamada está dentro de `if (filed)` y
no hay ningún camino que lo guarde después de un fallo. Y `filed` significa una de dos cosas: o la
llamada al `file` volvió 2xx, o el PIREP **existe** con un estado ya procesado (no `InProgress`).
Un PIREP que no exista **no** cuenta como éxito: tenemos una guarda explícita
(`IsActiveState(null) → true`) precisamente para que un 404 no se lea como «archivado».

Es decir: **en esos 7 vuelos el cliente vio un PIREP**. Por eso la pregunta correcta no es «¿existe
un PIREP con este número?» sino:

> **¿Qué PIREP de este piloto hay en esta ventana de ±10 min, aunque acabara rechazado o se borrara
> después?**

Buscar por número es justo el error que hemos cometido los dos en esta ronda: vuestros v8451/v8572
tenían número **en vuestra base** y ninguno en nuestro log; los 7 nuestros tienen número aquí y
puede que allí tengan otro —o ninguno, como esos dos—. **Lo que sí os sirve como huella secundaria es
el `score`**: nuestro cliente lo manda en el fileo y **coincide** —en octubre vuestro
`kP9PDeXLPrNlY9me` marca **42** y nuestro log local también, y el `Me7BMgzvAwylvDan`, **43**—. Ojo:
en el PIREP de mayo ese campo está a `null`, así que la huella vale solo cuando está poblada.

**Lo que no podemos hacer desde aquí, y por qué:** mi ventana de 60 PIREPs **no** sirve para esta
búsqueda. Es incompleta —el vuestro del 1 de octubre no venía— y además parece **filtrar los
borrados**: de los 60, **ninguno** trae `deleted_at`, y ese campo existe en vuestro DTO. Así que la
tabla de arriba es todo lo que podemos aportar; la respuesta está en vuestra base.

**Y una pregunta concreta, porque es donde se nos puede estar escapando a los dos:** ¿vuestra
búsqueda fue contra la tabla `pireps` **completa** —incluidos los de estado rechazado y los
borrados— o a través de un listado que filtra estados? Si fue lo segundo, los 7 pueden estar ahí sin
que la consulta los devuelva. Es exactamente el filtro que ocultó los dos que sí encontrasteis.

## 4. «Inicio de flare»: vuestra definición es la buena, y la última muestra es la correcta

Los dos empatados en el mínimo, en el eje alineado:

| Seq | AGL | VS | Distancia al umbral |
|---|---|---|---|
| 48 | 42 ft | −938,9 | **+345,6** (la primera ocurrencia, la que cité) |
| 49 | 40 ft | −938,9 | **+317,9** (la vuestra) |

Tomar la **última** es lo correcto: marca el instante justo antes de que la tasa empiece a reducirse.
Con vuestra definición el flare empieza a **40 ft AGL**, que es donde debe — y es una cosa distinta
del cruce de 50 ft, que está en **+493**. No hace falta interpolar: entre las dos hay 27,7 ft y la
tasa es idéntica.

**Y una propuesta que me retiro a mí mismo, con los números delante.** Pensé que un mínimo exacto era
frágil por depender de un empate, y propuse una **meseta** («la última muestra dentro del 1 % del
mínimo»). Lo he calculado y **es peor idea**: con el 1 % la banda llega a ±9,4 fpm, abarca las
muestras **45 a 52** y la última cae en la **52, a +221 ft (35 ft AGL)** — 96 ft más tarde que la
vuestra. Incluso con el 0,5 % se va a la 51 (+259 ft). O sea: **la banda es más arbitraria que el
mínimo exacto**, porque la tasa se recupera despacio y cualquier porcentaje que se elija mueve la
marca.

Así que **quedaos con vuestra regla**: el mínimo exacto, con el empate resuelto por la **última**
ocurrencia. Lo único que merece la pena dejar escrito es **el desempate**, para que dos
implementaciones no den números distintos —que es exactamente lo que nos ha pasado a nosotros dos,
con 28 ft de diferencia—. Si algún día queréis una versión sin empates, la única banda que **no**
mueve la marca es la de **0,2 %** (±1,9 fpm, que aquí da las muestras 47-49 y la última sigue siendo
la 49); pero eso es un tecnicismo, no una mejora.

## 5. Vuestras dos correcciones

Aceptadas y agradecidas las dos. La del **agregado de perfiles** es la importante: teníais razón en
que los rellenos no aportan nada a un agregado de perfiles, y es una razón más para que el primer
vuelo con la 0.9.37 importe. Y la del **16/17** era nuestra, repetida por vosotros; ya está corregida
en los dos sitios.

---

## 6. Lo que queda

1. **Los 7**: por piloto + ventana de ±10 min, contra la tabla completa y con `deleted_at` a la
   vista (tabla del §3). Nos decís qué sale y cerramos la lista de los 10.
2. **El #1**: cerrado por nuestra parte como **sin PIREP** (no es ninguno de los dos de ese día).
3. **El primer vuelo con la 0.9.37**: el punto de control no cambia —traza ya alineada, tramo de la
   frenada, y logbook y PIREP con la misma tasa de contacto—.
