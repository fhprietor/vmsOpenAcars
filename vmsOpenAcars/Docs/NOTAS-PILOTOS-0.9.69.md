# Novedades de vmsOpenACARS — versión 0.9.69

**En una frase:** la actualización **ya no se atasca** cuando el cliente tiene una librería en uso.

---

## Qué pasaba

Al actualizar podía aparecer:

```
Esperando que la aplicación cierre...
Error durante la actualización: The process cannot access the file
'…\fsuipcClient.dll' because it is being used by another process.
```

El actualizador intentaba **sobrescribir** una librería que el cliente tenía abierta. Windows no
permite reemplazar una librería en uso, y no era cuestión de insistir: el bloqueo no se iba solo.
Además, si el cliente tardaba en cerrarse, seguía adelante igualmente y fallaba.

## Qué cambia

- **Se espera de verdad a que el cliente cierre.** Si tras un tiempo prudencial sigue abierto, la
  actualización **se detiene con un aviso claro** que dice qué hacer, en vez de fallar a medias.
- **Si una librería está en uso, se aparta y se copia la nueva.** El archivo viejo se renombra con la
  extensión `.old` y la versión nueva entra en su sitio. Esos `.old` **se borran solos** en la
  siguiente actualización.
- **Si algo falla, el mensaje dice qué archivo y qué programa lo tiene**, no un error genérico.
- **El relanzamiento también avisa**: si el archivo se copió pero no se pudo abrir el cliente, lo dice
  en vez de quedarse callado.

## Si la actualización anterior te falló

1. Abre el **Administrador de tareas** (Ctrl + Mayús + Esc) y comprueba que **no queda ningún
   `vmsOpenACars.exe`**. Si queda alguno, *Finalizar tarea*.
2. Vuelve a lanzar la actualización.

El actualizador viene **dentro del paquete**, así que esta versión trae ya el bueno: no hay que
instalar nada aparte. Y si el archivo se resiste incluso con el cliente cerrado, un reinicio lo
libera seguro.

## Lo que hay que saber

- Esta versión es la que **arregla las actualizaciones**: si la 0.9.68 te falló, salta directamente a
  esta.
- No cambia nada del vuelo, de los anuncios ni del METAR: es solo el actualizador.