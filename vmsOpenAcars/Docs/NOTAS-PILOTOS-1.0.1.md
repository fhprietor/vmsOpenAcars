# Novedades de vmsOpenACARS — versión 1.0.1

**En una frase:** guardar los Ajustes vuelve a funcionar, y la ventana de inicio de sesión ya no
corta el botón de salir ni esconde los mensajes de error.

---

## 1. Guardar Ajustes ya no da error

En la 1.0.0, pulsar **Guardar** podía sacar «Error: Colección modificada; puede que no se ejecute la
operación de enumeración» y **no guardar nada**. El fallo estaba en cómo se escribía el fichero de
configuración mientras el cliente lo tenía abierto; ahora se escribe solo lo que has cambiado y el
resto del fichero se queda como estaba. Mismos campos, mismos ajustes, sin error.

De paso se ha arreglado algo que no se veía: cada vez que se guardaba, **se perdían los comentarios**
del fichero de configuración. Ya no.

Y los ajustes que se guardan solos —avisos en pantalla, anuncios de cabina, guía de rodaje, líneas de
depuración— llevaban un tiempo **fallando en silencio**. Ahora guardan de verdad.

## 2. La ventana de inicio de sesión

Dos defectos de la 1.0.0, arreglados:

- El botón **SALIR** se salía del ancho de la ventana y quedaba **cortado**. Ahora se ve entero.
- Los **mensajes de error** («usuario o contraseña incorrectos», «demasiados intentos»…) salían
  **debajo** de los botones y no se leían. Ahora aparecen encima, donde se ven.

Y si ya habías escrito el usuario, el cursor arranca en la contraseña.

## 3. El nombre de usuario de SimBrief

El campo **Usuario SimBrief** mostraba el texto de ejemplo (`your_simbrief_user`) como si fuera un
valor. Ahora aparece vacío hasta que pongas el tuyo, y el cliente no intenta usar el ejemplo.

---

## Lo que hay que saber

- **La actualización funciona igual**: mismo zip, mismo actualizador, tu configuración se conserva.
- **Si ya habías entrado**, no tienes que volver a iniciar sesión: tu clave guardada sigue valiendo.
- Los que ya tenían el ACARS de antes **no** necesitan tocar nada.