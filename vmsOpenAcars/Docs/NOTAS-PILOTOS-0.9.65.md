# Novedades de vmsOpenACARS — versión 0.9.65

**En una frase:** si el cliente no te detecta el simulador, ahora te dice **por qué** — y en nueve de
cada diez casos es que el simulador o FSUIPC está abierto **como administrador** y el ACARS no.

---

## Qué cambia

- **El registro ya no esconde el motivo.** Antes solo se veía «simulador desconectado»; ahora queda la
  excepción real, con su texto: *acceso denegado*, *no encontrado*, etc.
- **Aviso con la solución concreta.** Cuando el problema es de permisos, el cliente lo dice sin rodeos:
  Windows bloquea la conexión entre un programa elevado y otro que no lo está. **Lo preferible es abrir
  el simulador SIN elevación**; si tu instalación exige administrador, abre el ACARS como administrador
  para igualar el nivel.
- **La línea de arranque dice si vas elevado:**

  `🖥️ Proceso: 64 bits · Windows: 64 bits · elevado: no`

  Con eso, si algún día reportas que no detecta el simulador, esa línea responde en un segundo.

## Lo que hay que saber

- **El vuelo no se bloquea nunca por esto**: el ACARS sigue intentando conectar y puedes enviar el PIREP.
- **No hace falta ejecutar el ACARS como administrador siempre.** Solo es necesario si el simulador
  está elevado y no quieres (o no puedes) quitárselo. Si el simulador corre normal, el ACARS también.
- Si el aviso de permisos no aparece y aun así no detecta el sim, mira el registro: ahora dice la causa
  exacta, y con eso se puede pedir ayuda sin adivinar.
- No hay nada que configurar.