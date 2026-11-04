# Novedades de vmsOpenACARS — versión 0.9.63

**En una frase:** si al abrir el OFP en PDF algo falla, el ACARS ya **no se cierra ni te tira el vuelo**:
sale un aviso claro, la aplicación sigue funcionando y el detalle queda en el registro.

---

## Qué pasaba

El visor de PDF se preparaba **antes** de la protección que capturaba el fallo, así que cuando la
librería del PDF no se podía cargar en el equipo, el error se escapaba y aparecía el cuadro genérico
de .NET («Excepción no controlada…»). Pulsar *Salir* ahí cerraba el ACARS **con el vuelo en marcha**.

## Qué cambia

- **Ese fallo ya no cierra nada.** La aplicación muestra el aviso y **sigue viva**; el vuelo continúa
  con normalidad y puedes enviar el PIREP.
- **Cualquier error inesperado**, no solo el del OFP, pasa por el mismo camino: se anota en el registro
  y se avisa sin matar la aplicación.
- El aviso trae ahora **información de diagnóstico**: si estás en 32 o 64 bits y **qué archivo de la
  librería de PDF se esperaba y si está ahí**. Eso convierte un «no funciona» en algo que se puede
  corregir sin adivinar.

## Lo que hay que saber

- **El OFP en PDF puede seguir sin abrirse** en un equipo donde esa librería no cargue. Lo que está
  garantizado es que **no se lleva la aplicación por delante**: el ACARS, la telemetría y el PIREP no
  dependen de ella. El OFP lo puedes ver siempre en la web de SimBrief.
- Si te aparece el aviso, manda la línea del registro con el detalle técnico: con eso se puede saber
  exactamente qué falta en ese equipo.
- Nada que configurar.