# Primeros pasos — vmsOpenACars

Esta guía es para el primer vuelo. Son cinco minutos y no hay que editar ningún fichero a mano.

---

## 1. Qué necesitas

- **Windows** (de 32 o de 64 bits) y **.NET Framework 4.8** (en Windows 10 y 11 ya viene).
- **El simulador con su enlace**: FSUIPC7 para MSFS, o XUIPC para X-Plane.
- **Un usuario y una contraseña de la web de tu aerolínea**. Si vienes de CrewSystem, aún no tienes
  contraseña: mira el punto 3.

## 2. Instálalo

Descomprime `vmsOpenACars.zip` en una carpeta propia —por ejemplo `C:\vmsOpenACars`— y ejecuta
`vmsOpenACars.exe`. **No hace falta bajarse nada aparte**: la configuración viaja dentro del paquete
y se instala sola la primera vez.

Si ya tenías el ACARS de antes, **no borres tu carpeta**: actualiza encima y tu configuración se
conserva. Lo mismo vale si descomprimes en otra carpeta: tu acceso se guarda en tu cuenta de Windows,
así que no te volverá a pedir el login.

## 3. Si vienes de CrewSystem: primero, tu contraseña

Tu cuenta **ya existe** en la web de la aerolínea, con tu mismo usuario y tu mismo email. Lo que no
tienes es una contraseña, porque CrewSystem no la usaba. El cliente del ACARS te la va a pedir, así
que el primer paso es crearla:

1. Abre **https://vholar.co/login**
2. Pulsa **«¿Has olvidado tu contraseña?»**
3. Escribe **el email con el que estás en la aerolínea** y pide el enlace.
4. Abre el correo, pon una contraseña y guárdala.

Con eso ya puedes entrar en la web y en el ACARS con **el email o tu identificador de piloto**
(`VHR017` sirve igual) y esa contraseña.

> Si el correo no llega, mira en la carpeta de **spam** y comprueba que el email es el que tiene la
> aerolínea. Si tampoco, escribe a tu aerolínea: no es algo que resuelva el ACARS.

## 4. El primer arranque: inicia sesión

Lo primero que verás es la ventana de acceso:

1. **URL de phpVMS**: ya viene puesta (`https://vholar.co`). No la cambies salvo que tu aerolínea te
   diga otra cosa. Tiene que empezar por **`https://`**.
2. **Usuario o email**: tu email, o tu identificador de piloto (`VHR017`).
3. **Contraseña**: la que acabas de crear.
4. **ENTRAR**.

Y ya está: el cliente recibe tu clave de acceso y **la guarda cifrada en tu equipo**, atada a tu
cuenta de Windows. **Tu contraseña no se guarda en ningún sitio.** A partir de aquí no vuelve a
pedírtela: arranca directo.

> Si te equivocas de contraseña, el mensaje de debajo te lo dice y puedes reintentar. Si la cuenta no
> está activa, también. Y si fallas muchas veces seguidas, el servidor te bloquea unos minutos:
> **espera, no insistas**, que el bloqueo se alarga con cada intento.

## 5. Ajustes: lo que de verdad importa

Puedes volver aquí cuando quieras con **AJUSTES** (el botón de la ventana principal o el menú de la
bandeja).

**Lo que ya está bien y no tienes que tocar:** la **URL API** y la **API Key** las rellena el cliente
con tu inicio de sesión.

Lo que sí conviene revisar el primer día:

| Campo | Qué poner |
|---|---|
| **Usuario SimBrief** | Tu nombre de usuario de SimBrief, si quieres que el ACARS te traiga los planes de vuelo solo |
| **Aerolínea** | Solo si tu aerolínea te ha dado un identificador; si no, déjalo vacío |
| **Idioma** | `es` o `en`. Cambia los rótulos del programa |
| **Unidades SB** | `lbs` o `kgs`, **las mismas que uses en SimBrief** |
| **Cost Index** | El que use tu aerolínea para los planes |
| **BD Landing** | Dónde se guardan tus aterrizajes. Déjalo como está salvo que sepas lo que haces |
| **OSD** | Los avisos que salen sobre el simulador: duración, opacidad y si suenan |
| **Avisar en el OSD** (espacios aéreos) | Si lo desmarcas, dejas de ver esos avisos en pantalla, pero **se siguen registrando** en el vuelo y en el mapa |
| **Anuncios cabina** | Los avisos de cabina y su volumen |
| **Guía de rodaje** | La guía giro a giro por la calle de rodaje, con su voz |

Pulsa **Guardar**. Algunos cambios (idioma, BD Landing) piden reiniciar; el resto se aplica en
caliente y el propio aviso te lo dice.

## 6. Tu primer vuelo

1. Enciende el simulador **y el enlace** (FSUIPC o XUIPC) antes que el ACARS.
2. Abre el ACARS: arriba tiene que poner la versión y, al conectar, que ha encontrado el simulador.
3. Carga tu plan de SimBrief y elige el vuelo que te toque.
4. Pulsa **START** cuando estés listo para rodar. A partir de ahí el ACARS apunta solo las fases, el
   vuelo y el aterrizaje, y te avisa en pantalla de lo que va pasando.
5. Al aparcar, cierra el vuelo y **envía el PIREP**.

## 7. Si algo va mal

- **«Usuario o contraseña incorrectos»**: si vienes de CrewSystem y no has creado la contraseña,
  hazlo primero en https://vholar.co/login. Prueba también con tu **identificador** (`VHR017`) en vez
  del email.
- **«Tu cuenta no está activa»**: háblalo con tu aerolínea; el ACARS no puede activarla.
- **«Demasiados intentos»**: espera unos minutos. Insistir alarga el bloqueo.
- **«No se pudo conectar» al entrar**: revisa que la URL empiece por `https://` y que sea la de tu
  aerolínea.
- **Error de autorización nada más arrancar (401)**: actualiza el ACARS. En instalaciones nuevas con
  versiones anteriores a la 1.0.3, el cliente mandaba al servidor el texto de ejemplo de la plantilla.
  Con la 1.0.3 o posterior va solo.
- **Cambiaste tu contraseña o tu clave en el perfil de phpVMS**: el ACARS deja de conectar hasta que
  vuelvas a iniciar sesión. Borra `%APPDATA%\vmsOpenACars` y arranca de nuevo.
- **No encuentra el simulador**: casi siempre es que falta el enlace (FSUIPC7/XUIPC) o que el
  simulador está en modo ventana sin foco. El informe de arranque del registro dice por qué.
- **Un aviso tapa la pantalla**: los avisos (OSD) se pueden apagar en Ajustes sin dejar de
  registrarse en el vuelo.

---

## Lo que no tienes que hacer nunca

- **No copies claves a mano** ni las pegues en un fichero: el inicio de sesión se encarga.
- **No edites el `.config`**: lo que necesitas está en Ajustes.
- **No mandes tu contraseña ni tu clave a nadie**, ni al soporte: no las necesitamos para ayudarte.
