# Pedido al equipo de phpVMS — datos para las pruebas con PIREPs reales

> **De:** equipo de vmsOpenAcars · **Para:** equipo de phpVMS
> **Fecha:** 2026-09-29 · **Estado del cliente:** v0.9.16
> **Contexto:** queremos validar el motor de rodaje (ruta sugerida, avisos RAAS) y los filtros de
> desvío contra **vuelos reales**, no contra uno solo.
> **Filtro de partida, por indicación del mantenedor:** sólo cuentan los PIREPs producidos por
> **vmsOpenAcars**; los de CrewSystem o cualquier otro cliente **se ignoran**.

---

## 1. Lo que hay hoy, medido

`GET api/user/pireps` devuelve **20 PIREPs**, y agrupados por `source_name`:

| `source_name` | PIREPs | `score` |
|---|---|---|
| **vacío** (no son nuestros) | **13** | ninguno |
| `vmsOpenACars/0.8.10` | 1 | sí |
| `vmsOpenACars/0.9.1` | 3 | sí |
| `vmsOpenACars/0.9.2` | 1 | sí |
| `vmsOpenACars/0.9.9` | 1 | sí |
| `vmsOpenACars/0.9.10` | 1 | sí |

Dos consecuencias, y la segunda es la que importa:

1. **El `score` que faltaba en 13 de 20 no es un fallo nuestro**: son justo los que no llevan
   `source_name`, o sea los que no produce vmsOpenAcars. Retiramos esa pregunta.
   Pero sí pedimos algo sobre ellos: **`source_name` viene vacío en 13 de 20**, así que «quién lo
   produjo» **no es identificable** en esos casos. Hoy los excluimos por inferencia, no por dato.
2. **De los 7 nuestros, ninguno sirve para el banco de rodaje.** Verificado vuelo a vuelo: las
   trazas tienen **125–204 entradas, `phase` vacío y CERO posiciones de rodaje** (ni `Pushback`, ni
   `TaxiOut`, ni `TaxiIn`). Son de las versiones **0.8.10 a 0.9.10**, anteriores a que el cliente
   registrase las fases y a que existiera el RAAS (0.9.14).

Y el vuelo que **sí** tiene datos de rodaje —el que usamos como banco, `MNjR664PBAr25RbD`, con
**967 entradas**, `phase` poblado y sus tramos de rodaje— **no está entre esos 20**. Es decir: la
única traza aprovechable la tenemos porque el piloto conservó su log local, no porque el PIREP la
traiga a mano.

---

## 2. Lo que pedimos

### 2.1 Poder pedir el corpus por productor, y sin el tope de 20  ← lo más importante

**Qué falta:** un filtro por productor y paginación en `api/user/pireps`
(p. ej. `?source_name=vmsOpenACars&limit=&page=` o `?from=&to=`), y saber cuál es el **tope** de la
respuesta actual.

**Por qué importa:** el único vuelo con datos de rodaje **cae fuera de los últimos 20**, y los 20
incluyen 13 que no son nuestros. Sin filtro y sin paginación no hay forma de reconstruir el corpus:
el banco diría «los últimos 20 PIREPs» —de los que dos tercios no nos sirven— en vez de «todos los
vuelos de vmsOpenACars», que es lo que se puede reproducir.

### 2.2 `source_name` en todos los PIREPs

**Qué falta:** que los 13 que hoy lo traen vacío lo traigan relleno (el campo existe; el `source` del
ACARS de posición **sí** identifica al productor: en el vuelo probado las 967 entradas vienen con
`source='vmsOp'`).

**Por qué importa:** para excluir con criterio, no por inferencia. «No sé quién lo produjo» y «no lo
produjo vmsOpenAcars» no son lo mismo, y hoy no puedo distinguirlos.

### 2.3 Las trazas de las versiones nuevas, completas

**Qué falta:** confirmar que las trazas de los clientes **≥0.9.14** —las que ya envían fases de
rodaje— se guardan enteras y se pueden recuperar por PIREP sin tope de entradas.

**Por qué importa:** nuestro banco de rodaje necesita las posiciones de `TaxiOut`/`TaxiIn` a 30 s, que
es lo que hoy sólo tiene un vuelo (el local). Con varias trazas nuevas se puede medir el grafo y los
avisos en **aeropuertos distintos**, no en uno.

### 2.4 La pista usada (despegue y llegada)

**Qué falta:** el campo `simbrief` está en el esquema del PIREP pero **viene vacío** en el vuelo
probado, así que la pista del OFP no llega. Pedimos poblarlo, o añadir `dpt_runway`/`arr_runway` (o un
campo en `fields`, que ya usáis con `Network Online`…).

**Por qué importa:** todo el comportamiento de rodaje depende de la pista (el punto de espera que se
avisa, la ruta sugerida). Sin verdad de referencia, un fallo nuestro se ve como «el aviso sonó raro»
en vez de «este vuelo entró por la 14R y avisamos de la 14L».

### 2.5 La ruta de rodaje que declara el piloto

**Qué falta:** el campo `route` es el **plan de vuelo** (`TOBK4R VASIL UL423 TEQ…`), no el rodaje.
Pedimos un **campo personalizado** donde el cliente mande, al filear, el texto que el piloto escribe
en el diálogo de rodaje. Lo enviamos nosotros.

**Por qué importa:** es la autorización de ATC tal como la entendió el piloto —el dato más limpio que
existe para «por dónde se rueda de verdad»—. Es además el mismo dato que enviamos a NavData para que
agregue rutas acostumbradas: el PIREP sería la verdad **por vuelo** y NavData la agregada.

### 2.6 El estado de moderación del PIREP

**Qué falta:** los 20 vienen con `state=2` (`Arrived`) y no hay señal de revisión humana. Pedimos el
estado de moderación (aprobado / rechazado / motivo) y si el staff **corrigió** `arr_airport_id`
(nuestro caso de desvío real).

**Por qué importa:** es la única verdad **externa** que puede decirnos si un PIREP quedó bien. Sin
ella, los filtros de desvío sólo se validan contra nuestra propia opinión.

---

## 3. Y esto lo hacemos nosotros, sin pediros nada

Los avisos del RAAS (y las líneas de rodaje) **sólo viven hoy en el fichero de log local del
piloto**: en las 239 entradas con texto del vuelo probado no aparece ninguna línea de rodaje. Como
`acars/position` ya acepta texto en el campo `log`, **no hace falta endpoint nuevo**: cambiaremos el
cliente para enviar también los avisos relevantes, de forma que cada vuelo quede auditable desde el
PIREP. Es un cambio nuestro y os avisaremos cuando empiece a llegar.
