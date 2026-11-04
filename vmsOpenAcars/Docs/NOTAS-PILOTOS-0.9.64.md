# Novedades de vmsOpenACARS — versión 0.9.64

**En una frase:** si el mapa falla, el ACARS ya no se cierra; y el aviso te dice **qué archivo**
está mal, en vez de un error genérico.

---

## Qué cambia

- **El mapa ya no se lleva la aplicación por delante.** Si la ventana del mapa no puede abrirse, se
  queda con un aviso y el detalle, y el resto del cliente —la telemetría, el vuelo, el PIREP— sigue
  funcionando.
- **El aviso identifica el archivo.** Antes decía «algo falló»; ahora dice **qué librería** falló, su
  tamaño real y para qué sistema está compilada, y si falta. Con eso se puede arreglar sin adivinar.
- **Una línea nueva al conectar** muestra el estado de las cuatro librerías del cliente:

  `🧩 Nativas: x86 pdfium.dll OK · x64 pdfium.dll OK · x86 SQLite.Interop.dll OK · x64 SQLite.Interop.dll OK`

  Si alguna aparece como `falta` o `bitness cruzada`, ese es el problema y ya viene señalado.

## Lo que hay que saber

- **Si el mapa no se abre, el vuelo no se pierde**: el ACARS sigue conectado y el PIREP se envía
  igual. El mapa es una ayuda, no un requisito para volar.
- **Si esa línea del arranque señala una librería**, la solución es **reinstalar el cliente en una
  carpeta limpia**. Y si en la carpeta del cliente (junto al `.exe`) hay un archivo suelto llamado
  `SQLite.Interop.dll`, bórralo: puede ser una copia vieja de 32 bits que impide cargar la buena.
- No hay nada que configurar.