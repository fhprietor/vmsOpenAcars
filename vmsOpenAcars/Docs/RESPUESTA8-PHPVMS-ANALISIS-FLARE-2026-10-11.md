# Respuesta (8) al equipo de phpVMS — las siete horas, el `forceDelete` y el tercer campo

> **De:** equipo de vmsOpenACars · **Para:** equipo de phpVMS
> **Fecha:** 11/10/2026
> **Respecto a:** vuestra respuesta (8): la regla del `0`, los cinco opcionales y los 6 números
> **Estado:** **aquí están las siete horas** —era lo único que bloqueaba—, el `forceDelete()` cierra
> el caso por nuestro lado, y hay una corrección nuestra: **`trace_stop_reason` no lo habíamos
> implementado**, solo propuesto el nombre. Ya viaja.

---

## 1. Las siete horas (lo que os faltaba)

No os llegó la respuesta (6) y ahí iban, así que van sueltas. Son del **piloto 55** y son **el
instante del registro local**, que se escribe **antes** del `file`:

| Log local | Vuelo | Ruta | Instante del registro (UTC) | Tasa | Score |
|---|---|---|---|---|---|
| 7 | **875** | SKSM→MPTO | 2026-05-16 **14:11:11** | −331 | 32 |
| 11 | **62** | SKCL→KJFK | 2026-05-19 **02:41:45** | −447 | 39 |
| 12 | **8422** | SKCL→SKBO | 2026-05-20 **11:27:08** | −156 | 80 |
| 13 | **8180** | SKPE→SKRG | 2026-05-27 **00:04:10** | −392 | 31 |
| 17 | **55CH** | SKPS→SKSM | 2026-06-02 **01:05:18** | −272 | 50 |
| 19 | **7743** | SKCG→SKVP | 2026-06-21 **19:10:36** | −282 | 62 |
| 29 | **208** | SKRG→TNCC | 2026-08-26 **02:44:24** | −617 | 50 |

**Cómo leerlas, y es lo que os ahorra media hora de consulta:** ese instante es **anterior** a
`submitted_at`, porque el registro se copia en el touch-and-go del fileo y el `await` que lo manda
viene después. Lo medimos con los dos vuelos de mayo que sí casaron: **+79 s** y **+302 s**. Los dos
positivos, y en el mismo sentido, así que la ventana buena es **[t, t+10 min]**; con ±10 min vais
sobrados.

**Huella secundaria, por si el número vuelve a estorbar:** el `score` de nuestro cliente viaja en el
`file` y coincide con vuestro `pireps.score` cuando está poblado —en octubre, vuestros
`kP9PDeXLPrNlY9me` y `Me7BMgzvAwylvDan` marcan **42** y **43** y nuestro log local también—. En
mayo está a `null`, así que vale solo como confirmación, no como criterio.

**Y un dato de contexto que ayuda a leer el 55CH:** el registro 17 (2026-06-02 01:05) es la pata
**siguiente** a un PIREP vuestro que sí casa —el del 2026-06-01 22:39, SKRG→SKPS— y **anterior** al
del 2026-06-15 (SKSM→SKCG). O sea: el piloto voló la cadena y esa pata quedó sin PIREP entre dos que
están. Es el patrón de «vuelo volado y no guardado», no el de un número tecleado mal.

## 2. El `forceDelete()`: aceptado, y cierra el caso por nuestro lado

Vuestra verificación en `PirepService::delete()` es la pieza que faltaba: **borrado duro**, sin
rastro, ni siquiera en `onlyTrashed`. Con eso la explicación (1) deja de ser una hipótesis y pasa a
ser la única que encaja con nuestro lado:

- Nuestro log lo vio como `filed` **porque lo fue** —el registro se escribe solo dentro de
  `if (filed)`, y `filed` exige 2xx o un PIREP existente en estado procesado—.
- En vuestra base ya no está, y tampoco su telemetría.

**Criterio de cierre que proponemos**: si la consulta por ventana no devuelve nada, los seis se
cierran como **no recuperables** y se quedan así, documentados. No merece más vueltas: si un admin
purgó esos PIREPs, el vuelo **se perdió entero**, no solo el flare, y lo único que queda de él es
nuestra fila local —tasa y score—, que es justo lo que nos ha dejado llegar hasta aquí.

**Una idea, y la dejamos a vuestro criterio, no como petición:** una línea en el `activity_log`
dentro de ese `delete()` —piloto, número y fecha del PIREP— convertiría el próximo caso de estos en
**diagnosticable** en vez de invisible. Nos habría ahorrado esta ronda entera a los dos.

## 3. `trace_stop_reason`: teníais razón en esperarlo, y **no lo habíamos hecho**

**Corrección nuestra, y es la tercera vez en el hilo que el lado que menos se sospecha es el que
falla.** En la respuesta (7) **propuse el nombre** y dije «decid si os vale»; vosotros lo aceptasteis,
pusisteis la columna… y nosotros **no lo estábamos enviando**. Ya viaja, con `StopReason` en la
política de captura. Lo que vais a recibir, literal:

```text
stopped:post-touchdown 2.0s     ← final normal (el margen tras el toque)
stopped:on-ground               ← final normal (toma suave, sin VS de toque)
stopped:cap                     ← TRUNCADA: tope de 400 muestras
stopped:timeout                 ← TRUNCADA: seguro de 45 s
```

Y **`null`, que es un caso con significado propio**: la captura **no llegó a cerrarse** —no se armó,
o el vuelo terminó con ella abierta—. Por eso no mandamos el `Reason` en crudo sino solo lo que
**selló** el cierre: un `armed:threshold` sin cerrar no es un motivo de cierre, y mandarlo como tal
diría que la traza se cortó cuando lo que pasó es que el vuelo se acabó antes. **`null` no es un
final: es «aquí no hubo cierre»**, y conviene que el prompt lo diga así (ya lo hacéis con `cap` y
`timeout`).

Un detalle que os ahorra un parseo: los motivos se componen con **`CultureInfo.InvariantCulture`**,
así que el número es `2.0s` y no `2,0s` según el idioma del equipo del piloto. El prefijo hasta los
dos puntos es estable; lo de después ya lo podéis ignorar.

## 4. Los cinco opcionales: vuestros veredictos, aceptados

1. **TDZ — de acuerdo en no mandarla ahora.** Vuestro argumento es mejor que el nuestro: guardar hoy
   dos campos que no usáis es un campo que envejece sin que nadie lo mire. Cuando pintéis las bandas,
   os damos **los dos cortes y el `tdz_policy_version`**, y para el histórico también, porque la
   longitud ya la tenéis.
2. **A/P — mejor para todos que lo derivéis.** Y un dato para que la derivación sea fiable: ese campo
   **se escribe siempre**, como `true` o `false`, **nunca `null`** —así que vuestros 139 de 314 son
   «comprometido», y el punto donde cambia a `false` es la desconexión—. El único matiz es que
   `AutopilotEngaged` tiene un **debounce de unas décimas de segundo** (se confirma que está
   comprometido antes de publicarlo), despreciable a la resolución de una posición, pero está ahí.
3. **A/T — investigación descartada**, y de acuerdo: la técnica de potencia ya se lee del
   `corte_potencia_s`.
4. **Viento — gracias por aceptar la corrección**, y de acuerdo en no llevar el perfil por muestra:
   la traza es lo único que guardáis verbatim y validáis entero, y no merece dos campos por muestra
   por una deriva que aún no es un tema.
5. **CG — descartado**, y no perseguimos el LVar.

## 5. Una corrección de versión, y es nuestra

Vuestro §6.3 dice «el primer vuelo con la **0.9.37**», y ya no es así: **la 0.9.37 estaba publicada
cuando añadimos los campos**, así que los tres viajan bajo **`vmsOpenACars/0.9.38`** —el
`offset_threshold_ft`, el `vref_kt` y el `trace_stop_reason`—. Es exactamente el motivo por el que
subimos versión: `source_name` es la única marca de procedencia que guardáis, y dejar una capacidad
nueva bajo una cadena ya distribuida la convertiría en una mentira justo en vuestro vuelo de control.
Un vuelo con `0.9.37` **no lleva los campos**, y eso no es un fallo: es la versión anterior.

## 6. Lo que queda

1. **Vosotros:** la consulta por ventana de la §1. Si sale vacía, los seis se cierran como no
   recuperables y no volvemos sobre ellos.
2. **Nosotros:** nada pendiente. Los tres campos están implementados, con su columna, su validación y
   sus pruebas —**683 en la suite**, tres nuevas para esto—.
3. **El primer vuelo**, ya con **0.9.38**, y con las tres casillas: que lleguen los dos campos, que
   `trace_stop_reason` venga poblado y **que la traza llegue ya alineada**.
