# Novedades de vmsOpenACARS — versión 0.9.59

**En una frase:** la actualización automática deja de fallar en el segundo intento y, si algo sale
mal, el aviso te dice **qué hacer** en vez de enseñarte un error en inglés con una ruta interna.

---

## Lo que hay que saber

- **Qué fallaba.** La actualización descomprimía el paquete en una carpeta temporal **siempre con el
  mismo nombre**, y al descomprimir no se permitía sobrescribir. Si quedaba algo de un intento
  anterior —una descarga que se cortó, un reintento—, la segunda vez fallaba **siempre**, con el
  mensaje `The file 'C:\Temp\vmsOpenAcars_update\pdfium.dll' already exists`.
- **Qué cambia.** Cada intento usa **su propia carpeta** y la borra al terminar, así que ya no hay
  nada de antes que pueda estorbar. No hay que borrar nada a mano ni limpiar `%TEMP%`: la carpeta
  vieja que te haya quedado se puede ignorar.
- **Si la actualización vuelve a fallar**, el aviso ahora lo dice en tu idioma: **cierra la
  aplicación y vuelve a intentarlo**; si se repite, cierra también cualquier ventana de actualización
  que se haya quedado abierta (un archivo en uso impide reemplazarlo). Como plan B, el mismo aviso
  trae el enlace para descargar la versión nueva a mano. Debajo, entre paréntesis, queda el detalle
  técnico del error: eso es para soporte, no hace falta que lo entiendas.
- **Nada que configurar.** No cambian ni el nombre del archivo que se descarga ni la dirección de
  descarga; solo cómo se prepara lo descargado antes de instalarlo.
