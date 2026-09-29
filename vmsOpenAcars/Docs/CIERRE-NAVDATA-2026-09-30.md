# NavData → vmsOpenACars: cierre de hilo (estado de todo lo hablado)

> **Para:** equipo de vmsOpenACars · **De:** equipo de NavData · **Fecha:** 2026-09-30
> **Asunto:** cerramos los hilos abiertos. Aquí está todo lo entregado, lo vuestro y lo único que
> nos queda a nosotros.

Han sido nueve documentos en dos días. Este es el estado final, para que nada quede a medias por
olvido y no haya que releerlos todos.

---

## 1. Entregado y verificado desde vuestro lado

| Tema | Qué quedó |
|---|---|
| **`mag_var`** en `/airport/` | Publicado; vuestro desfase de rumbo 7,8° → 0,7° |
| **Espacios aéreos** | Índice local de OpenAIP: 0 errores, `source`, `capped`, `count...`; sin 503 |
| **AIRAC desde el fichero** | El ciclo y la validez se leen de la propia base; subir el fichero basta |
| **Rutas de rodaje (KB)** | Ingesta con límites, agregación con peso y decaimiento, `customary`/`alternativas`, `taxiway-stats`; validada en vivo |
| **Puntos de espera** | Desde los tipos de nodo del escenario (26/6/1/11/8 en SKBO), `runway_names`, muñones de entrada fuera |
| **`node_id`** | 52 bits (precisión de JS), mismo id = mismo nodo |
| **Empalmes** | Calculados desde los datos + curados, con `confidence`, `kind`, `version` y **`criterion_version`** |
| **Teselas** | Proxy con caché, `private`, `?key=`, `voyager` corregido, marca de agua detectada, consumo del mes |
| **`expires_at`** | Las claves pueden caducar solas: la rotación no depende de que nadie se acuerde |

## 2. Lo vuestro (no os bloquea nada nuestro)

- **v0.9.18**: el proxy como camino, la caída en tres escalones con sus contadores, la comprobación
  cruzada `placeholder` ↔ marca de agua, el porcentaje de cuota y los campos de URL/clave en Ajustes.
- **El contador de caídas a CARTO**: cuando lo tengáis, mandadnos el número; es lo que nos dirá si el
  proxy cumple o si os cuesta más de lo que ahorra.
- **La clave nueva de VHR**: la creamos nosotros y os la hacemos llegar **fuera de banda**; la vieja
  lleva `expires_at = 2026-10-28` y muere sola ese día. Nada que recordar por ninguna de las dos
  partes.
- **El grafo exacto con `node_id`** (§5.1): cuando lo midáis, si necesitáis el emparejamiento de
  extremos que estamos validando, decidlo — sería la señal de que los cruces os hacen falta como dato.

## 3. Lo único que nos queda a nosotros: `runway-crossings`

Sigue pendiente y **no está olvidado**: el emparejamiento de extremos a cada lado de la pista
necesita validarse en varios aeropuertos antes de publicarse, y de ahí que no haya salido con el
resto. Lo que necesitáis de él ya lo tenemos claro por vuestra parte: **el caso que os falta es
`has_hold_short: false`** —cruzar pavimento sin nodo de espera—, porque con nodo ya avisáis vosotros.

Cuando lo validemos, lo publicamos con las cifras por aeropuerto. Si en vuestro §5.1 veis que lo
necesitáis antes, decidlo y sube de prioridad; si no, sale cuando esté bien, como acordamos.

## 4. Lo que queda de nuestro lado operativo

- **El número del plan de CARTO** (5 M no comercial / 1 M comercial): en cuanto contesten,
  `plan_limit` deja de ser nulo y el porcentaje aparece solo en vuestro diagnóstico. Es lo único que
  os debemos de las teselas.
- **AIRAC 2610**: se sube con el ciclo; no requiere nada de vuestro lado (`/status/` lo refleja).

Gracias por el ida y vuelta. Cuatro bugs reales los encontrasteis vosotros midiendo —`V` que no está
en la carta, `voyager` en la ruta equivocada, el `401` que servía Cloudflare y la clave en nuestro
log— y dos los cazaron nuestros tests por cosas que nos contasteis. Eso es lo que ha hecho que esto
sirva para algo más que para cerrar tareas.
