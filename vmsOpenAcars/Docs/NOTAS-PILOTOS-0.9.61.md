# Novedades de vmsOpenACARS — versión 0.9.61

**En una frase:** si al cargar el OFP te saltaba «excepción no controlada» y la aplicación se caía,
era una librería que faltaba en el paquete; ya está, y además ese fallo **nunca más se lleva la
aplicación por delante**.

---

## Qué pasaba

Al abrir el **OFP en PDF**, el programa que dibuja los PDF necesitaba una librería del sistema
(`pdfium`) y el paquete solo traía la versión de **64 bits**. En un equipo de **32 bits** (o con el
cliente corriendo a 32 bits) no había ninguna válida, así que Windows respondía con
`0x8007000B` —«formato incorrecto»— y el cliente se cerraba con el aviso genérico de .NET.

## Qué cambia

- El paquete lleva ahora **las dos versiones**, 32 y 64 bits, cada una en su carpeta.
- **Si vuelve a fallar por cualquier motivo, la aplicación ya no se cae**: sale un aviso claro en la
  propia ventana del OFP diciendo qué pasó, que el ACARS y el vuelo **no** están afectados, y que el
  OFP lo puedes ver en la web de SimBrief.

## Lo que hay que saber

- **No hay que configurar nada.** Y no hace falta desinstalar: la actualización deja el cliente al día
  sin tocar tu configuración ni tus claves.
- En el registro, al conectar, ahora aparece una línea `🖥️ Proceso: N bits · Windows: N bits`. Es
  información de diagnóstico: si algún día reportas un fallo, esa línea dice en un vistazo con qué
  versión de Windows y de proceso estabas volando.