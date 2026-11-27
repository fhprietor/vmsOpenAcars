# Novedades de vmsOpenACARS — versión 1.0.2

**En una frase:** guardar los Ajustes ya no da error. El fallo estaba en el reinicio que hace después
de guardar, no en el guardado.

---

## 1. El error al guardar, resuelto de verdad

En la 1.0.0 y la 1.0.1, al pulsar **Guardar** podía salir «Error: Colección modificada» **después**
de los ajustes se habían guardado. El guardado siempre funcionó: lo que fallaba era el **reinicio**
posterior, que recorría las ventanas abiertas mientras cerraba los avisos en pantalla y el icono de la
bandeja. Ahora el cliente arranca la instancia nueva y cierra la vieja sin pasar por ese recorrido.

## 2. Algo que conviene saber: tu acceso se guarda por usuario, no por carpeta

La clave que recibes al iniciar sesión se guarda **en tu cuenta de Windows**. Si descomprimes el
cliente en otra carpeta, **no te volverá a pedir el login**: te reconoce, porque el piloto eres tú.
Y sigue sin poder usarse en otro equipo: está atada a tu cuenta y a esa máquina.

## 3. Un detalle menor

Si reinicias desde Ajustes, la **posición de la ventana** no se guarda en ese reinicio (en un cierre
normal, sí). Es consecuencia de la forma de reiniciar y se ha preferido a que el reinicio falle.

---

## Lo que hay que saber

- **Mismo zip, mismo actualizador, tu configuración y tu acceso intactos.**
- Si ya habías entrado, no tienes que volver a iniciar sesión.
- Lo demás es igual que en la 1.0.1: los auto-guardados de OSD, cabina y guía funcionan, la ventana de
  acceso se ve entera, y el campo de SimBrief ya no muestra el texto de ejemplo.