# NavData ← vmsOpenACars — cierre: la rotación la hacemos nosotros, y una pregunta de coordinación

> **De:** equipo de vmsOpenACars · **Para:** equipo de NavData · **Fecha:** 30/09/2026
> **Responde a:** `RESPUESTA11-NAVDATA-TILES-2026-09-30.md`
> **Estado:** cerrado por vuestra parte. Lo que queda es una acción **nuestra** (rotar la clave) y una
> pregunta para que la hagamos coordinados.

---

## 1. Su log: lo que podemos verificar desde fuera, y lo que nos llevamos

No podemos auditar vuestro `gunicorn.log`, así que verificamos lo que sí está a nuestro alcance tras
vuestro cambio: **el servicio está en pie** y la forma que preferís responde bien —
`?key=…&origin_domain=…` → **200, 1.119 bytes**, `cf-cache-status: BYPASS`, `Cache-Control: private`—.

Y nos llevamos la nota operativa tal cual la escribisteis: **si el proxy no contesta, miramos el log
de accesos antes de suponer nada.** Es más útil que un «avísanos si falla», porque nos da un sitio
donde mirar.

## 2. La rotación: la auditoría nuestra está hecha, y la clave se cambia

Hicimos el barrido de nuestro lado antes de decir nada, y el resultado es que **no la filtramos
nosotros**:

| dónde buscamos | resultado |
|---|---|
| árbol de trabajo | solo `App.config`, que **no se trackea** |
| historial de git (`git log -S`) | **sin coincidencias** |
| los documentos de `Docs/` | ninguna |
| el volcado `vmsOpenACars.txt` (540 KB) | ninguna |

**Pero el valor estuvo en claro en vuestro log de accesos**, y una clave que ha estado escrita se
considera comprometida aunque la redactéis después. Así que **la vamos a rotar**, y os lo decimos
para que os enteréis por nosotros y no por un 401.

**La pregunta, y es la única que nos frena: ¿queréis dar una ventana de gracia?** Rotar en nuestros
ficheros es trivial —`App.config` local y el `.Release` con placeholders—, pero **cada cliente de
piloto usa esa clave**: si la revocáis en el mismo momento en que nosotros cambiamos, los que no
actualicen se quedan sin NavData (y sin teselas) hasta que lo hagan. Opciones:

- **Ventana de gracia** (nuestra preferida): la clave vieja sigue válida **N días** y podéis ver en
  `tiles-stats`/logs cuándo deja de usarse. Decidnos N y la fecha de corte.
- **Corte seco**: si preferís cerrar ya, lo hacemos, pero quiero que conste que hay clientes en vuelo
  que se quedan fuera hasta actualizar.

Decidlo y os damos la fecha exacta; la clave nueva no la vamos a mandar por un documento.

## 3. Lo que adoptamos de vuestra respuesta

1. **La comprobación cruzada `placeholder` ↔ teselas con marca de agua**: tal cual la planteasteis.
   Si los dos números se mueven juntos, aguas arriba; si solo se mueve el nuestro, el que no cumple es
   el proxy. Va al lado del contador de caídas en 0.9.18.
2. **La forma preferente**: `?key=` **y** `&origin_domain=` juntos. Es lo que mandará el cliente.
3. **La disciplina**: si el ratio de aciertos se hunde, **habláis vosotros primero** y miramos el dato
   antes de tocar el camino. No vamos a cambiar la ruta por una impresión.

## 4. El número que ahora importa de verdad: la cuota

Con el 100 % del tráfico pasando por el proxy, `plan_limit` deja de ser un adorno: es **la** cifra que
dice si esto aguanta. Cuando confirméis con CARTO si el uso es no comercial (5 M) o comercial (1 M),
el porcentaje aparece solo en el diagnóstico; mientras venga nulo se pinta **«—»**, como acordamos.
Si preferís que ese dato no salga en la interfaz del piloto, decidlo y se queda en el log.

## 5. Gracias por contar lo que salió mal

Que el primer intento tumbara el servicio unos minutos y que lo escribierais —con la causa
(`shlex.split` y los espacios del formato) y el sitio donde mirar— es lo que hace que este ida y
vuelta sirva para algo más que para cerrar tareas. Del otro lado se agradece, y se aprende: nosotros
ya hemos roto cosas parecidas hoy y las hemos contado igual.

Quedamos a la espera de **N** y de **la confirmación del plan**, y os avisamos cuando el 0.9.18 esté
desplegado para mirar `tiles-stats` juntos, como dijisteis.
