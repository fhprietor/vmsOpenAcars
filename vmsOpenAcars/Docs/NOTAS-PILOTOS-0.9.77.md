# Novedades de vmsOpenACARS — versión 0.9.77

**En una frase:** el registro del vuelo deja de enseñarte los volcados técnicos, y hay un ajuste
para volver a verlos cuando los necesites.

---

## Qué cambia

**El log se ve mucho más limpio.** Se ocultan las líneas que solo sirven para diagnosticar:

- el volcado del panel del avión (`🛠 Enroute: PMDG: powered=1 cmd=10 …`), que ocupaba media línea de
  números por cada fase;
- el aviso de calle repetido del guiado (`↳ TWY A, Next B4`), que salía cada pocos segundos;
- los mensajes de diagnóstico del inversor (`Reversa: monitoreando…`, `Reversa: no detectada…`);
- el estado de motores para depurar (`Motor: ENG1 idle 291s … OAT 16°C`);
- el eco del código de estado que devuelve la aerolínea (`📊 PIREP Status: TXI`).

**Nada de lo importante desaparece.** Penalizaciones, cambios de fase, el registro del toque, el
combustible, los avisos de voz, el freno de aparcamiento y el cierre del vuelo siguen ahí siempre.
Hay una comprobación que lo impide: ninguna línea de aviso o de resultado puede ocultarse.

**Si los necesitas, se encienden.** En Ajustes hay una casilla nueva, **«Mostrar líneas de
depuración»**, apagada de serie. Se aplica al momento, sin reiniciar la aplicación.

## Lo que hay que saber

- **La aerolínea los sigue recibiendo**, marcados con `[DBG] ` al principio, para que su gestor pueda
  filtrarlos o verlos cuando haga falta. Ocultarlos en tu pantalla no significa que se pierdan: si
  algún día hay que revisar un vuelo, están.
- Si nunca tocas la casilla, no aparece ni una línea de depuración y no hay que configurar nada.
- El resto del cliente —anuncios, avisos de pista, puntuación, análisis del aterrizaje— no cambia.