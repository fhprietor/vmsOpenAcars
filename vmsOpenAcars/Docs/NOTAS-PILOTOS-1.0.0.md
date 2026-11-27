# Novedades de vmsOpenACARS — versión 1.0.0

**En una frase:** el cliente sale de beta. Si ya vuelas, **no cambia nada** —mismos avisos, misma
puntuación, mismo archivo de vuelo y la misma actualización de siempre—; si lo **instalas por primera
vez**, ahora se configura solo y no tienes que bajarte nada aparte.

---

## 1. Qué significa la 1.0.0

Es una **declaración de estabilidad**. Salta desde la 0.9.81 y, para quien ya vuela, no hay cambios
de comportamiento: si vienes de la 0.9.81 no vas a notar ninguna diferencia salvo el número de versión
en la cabecera de la ventana.

Lo que sí ha cambiado es lo que hay detrás: el análisis del aterrizaje y el flare cierra el circuito
completo —del cliente a phpVMS y vuelta al gráfico—, la ruta se revisa **antes de despegar** contra
los espacios aéreos restringidos, y la suite de pruebas ha pasado de 684 a **1 298**.

## 2. Si acabas de instalar el cliente

Antes, una instalación nueva llegaba **sin configuración**: el ACARS arrancaba pero no sabía a qué
servidor hablarle hasta que te descargabas un fichero aparte y lo ponías al lado del `.exe`. Ya no.

- **No copias ninguna clave.** Entras con el **usuario y la contraseña** que ya usas en la web de tu aerolínea, y el cliente recibe la suya: se guarda **cifrada** en tu equipo, atada a tu cuenta de Windows, y la contraseña no se guarda en ningún sitio.`r`n- **El paquete trae la plantilla** y el cliente la instala solo en el primer arranque.
- **Se te abren los Ajustes** para que pongas tu **URL** y tu **clave** de phpVMS, que son lo único que
  no puede venir en la plantilla.
- **Tu configuración no se toca nunca**: si ya tenías una, el cliente no la pisa ni al instalar ni al
  actualizar. Es lo que hace que sobreviva a las actualizaciones.

## 3. Si vienes de una versión anterior a la 0.9.80

Dos cosas que sí notarás, y que no son de la 1.0.0 sino de las versiones intermedias:

- **La puntuación de algunos vuelos cambia.** Los mínimos se miden con una banda de 15 ft y el exceso
  de descenso con una de 50 fpm, lo que deja los umbrales efectivos en 192 ft y −1 050 fpm. Un
  aterrizaje que antes no penalizaba al mínimo puede ser que ahora sí, o al contrario.
- **Los avisos en pantalla (OSD) tienen otro aspecto**, con una tarjeta propia para el toque.

## Lo que hay que saber

- **La actualización funciona igual**: el mismo zip, el mismo actualizador, y el `.config` del piloto
  se conserva porque se sigue sin distribuir.
- **Windows de 32 y de 64 bits siguen soportados**: el paquete trae las dos versiones de las librerías
  nativas y el cliente usa la que toque según el sistema.
- **Requiere .NET Framework 4.8**, como hasta ahora.
- Lo que queda pendiente (decisiones de diseño, no trabajo a medias) está en `Docs/DEUDA-Y-DECISIONES.md`;
  la 1.0.0 no significa que no haya deuda, significa que lo que hay es estable.
