# Guía de Primeros Pasos — VHOLAR (phpVMS) + vmsOpenAcars

## Parte 1 — Tu cuenta en el nuevo sistema (phpVMS)

### 1.1 Accede a la página

La nueva plataforma de VHOLAR está en:

### 👉 https://vholar.co

Ábrela con tu navegador (Chrome, Edge, Firefox — cualquiera funciona).

### 1.2 Tu usuario ya existe — no te registres de nuevo

Si ya eras piloto activo en el sistema anterior (Crewsystem), **no necesitas crear una cuenta nueva**. Todos los usuarios activos ya fueron migrados al nuevo sitio, con su rango actual incluido. Solo necesitas recuperar tu contraseña (siguiente paso).

### 1.3 Recupera tu contraseña

Como es un sistema nuevo, tu contraseña anterior no va a funcionar aquí. Para entrar por primera vez:

1. Entra a **https://vholar.co**
2. En la pantalla de inicio de sesión, busca el enlace que dice **"Forgot password?"** (normalmente justo debajo del botón de entrar)
3. Haz clic ahí e ingresa tu **correo electrónico** — el mismo que tenías registrado en el sistema anterior
4. Revisa tu correo: vas a recibir un mensaje con un enlace para crear una contraseña nueva
   - 💡 Si no aparece en la bandeja de entrada en unos minutos, revisa la carpeta de **spam / correo no deseado**
5. Abre el enlace del correo y crea tu contraseña nueva
6. Listo — ya puedes iniciar sesión en https://vholar.co con tu correo y tu contraseña nueva

---

## Parte 2 — Configura tu perfil

Una vez dentro del sitio, hay **dos cosas obligatorias** que debes hacer antes de poder volar.

### 2.1 Edita tu perfil

1. Busca tu nombre de usuario o tu avatar (normalmente arriba, a la derecha de la pantalla) y entra a tu **perfil**
2. Busca la opción para **editar el perfil**
3. Completa estos datos:

| Campo | ¿Obligatorio? | Notas |
|---|---|---|
| Avatar (foto de perfil) | Opcional | Puedes subir una imagen si quieres — no afecta tus vuelos |
| País de residencia | Recomendado | Ayuda a identificar tu ubicación dentro de la aerolínea |
| **IVAO ID** | **⚠️ Obligatorio** | Escribe tu número de piloto de IVAO exactamente como aparece en tu cuenta de IVAO |

4. Guarda los cambios (busca un botón de **Guardar** / **Save** al final del formulario)

> ⚠️ **No te saltes el IVAO ID.** Es el error más común en pilotos nuevos: vuelan sin haberlo configurado y su reporte de vuelo (PIREP) queda rechazado automáticamente.

### 2.2 Genera tu API Key

El **API Key** es un código secreto que le permite a tu programa de ACARS (vmsOpenAcars, que instalamos en la Parte 4) comunicarse con VHOLAR en tu nombre, para reportar tus vuelos automáticamente. Lo vas a necesitar más adelante, así que consíguelo ahora mismo:

1. Dentro de tu perfil, busca un **botón amarillo** que dice algo como **"Generate API Key"**
2. Haz clic en ese botón
3. Va a aparecer un código largo de letras y números — **cópialo completo**
   - Selecciona todo el texto con el mouse y presiona `Ctrl + C` en el teclado, o usa el botón de copiar si aparece uno al lado del código
4. Pégalo en un lugar donde no lo vayas a perder — por ejemplo, abre el **Bloc de notas** de Windows (búscalo escribiendo "bloc de notas" en el menú de Inicio) y pega el código ahí con `Ctrl + V`, guardando el archivo. Lo vamos a usar en la Parte 5.

> 💡 No compartas tu API Key con nadie — es como una contraseña. Cualquiera que la tenga puede reportar vuelos a tu nombre.

---

## Parte 3 — Descarga los programas necesarios

Ahora vamos a descargar dos archivos, ambos desde el mismo sitio de VHOLAR.

### 3.1 Ve a la sección de descargas

1. En https://vholar.co, busca en el menú principal la opción **Descargas**
2. Dentro de Descargas, entra a **Acars**

### 3.2 Descarga el archivo de configuración (solo la primera vez)

Descarga el archivo de **configuración por defecto** usando este enlace directo:

### 👉 https://vholar.co/vmsOpenDownloads/file/59

Solo necesitas descargar este archivo **una sola vez** — no hace falta volver a bajarlo en el futuro, aunque actualices vmsOpenAcars más adelante.

### 3.3 Descarga vmsOpenAcars

En la misma sección, descarga **vmsOpenAcars**. Si ves varias versiones en la lista, elige siempre la **más alta / más reciente** (por ejemplo, v0.9.0 es más nueva que v0.8.7 — mientras más grande el número, más reciente es).

Ambos archivos se van a guardar, por defecto, en tu carpeta **Descargas** (en inglés, *Downloads*) — así es como Windows los organiza automáticamente, a menos que hayas cambiado esa opción antes.

---

## Parte 4 — Instala vmsOpenAcars

vmsOpenAcars **no se "instala" como un programa normal** (no tiene un asistente de instalación con botones de "Siguiente, Siguiente, Finalizar"). Es lo que se llama un programa **portable**: solo hace falta descomprimirlo (extraerlo) dentro de una carpeta y ya queda listo para usarse desde ahí. Funciona igual sin importar en qué carpeta lo pongas.

### 4.1 Elige o crea una carpeta para vmsOpenAcars

Te recomendamos crear una carpeta específica para esto — idealmente una subcarpeta dentro del mismo lugar donde ya tengas otras utilidades de tu simulador de vuelo (FSUIPC, addons, etc.), para mantener todo organizado.

**Cómo crear una carpeta nueva en Windows:**

1. Abre el **Explorador de archivos** (el ícono de una carpeta amarilla en la barra de tareas, abajo en tu pantalla) — o presiona al mismo tiempo las teclas `Windows` + `E`
2. Navega hasta el lugar donde quieres crear la carpeta
3. Haz **clic derecho** (el botón derecho del mouse) en un espacio vacío de esa carpeta
4. En el menú que aparece, selecciona **Nuevo** → **Carpeta**
5. Escríbele un nombre — por ejemplo `vmsOpenAcars` — y presiona `Enter`

### 4.2 Descomprime (extrae) los dos archivos descargados

Los archivos que bajaste en la Parte 3 vienen comprimidos en formato **.zip** — una especie de "paquete" que ocupa menos espacio y hay que "abrir" antes de poder usarlo. Windows ya trae todo lo necesario para hacer esto, sin instalar nada adicional.

**Cómo descomprimir un archivo .zip:**

1. Abre tu carpeta **Descargas**
2. Busca el archivo de vmsOpenAcars (el nombre incluye algo como `vmsOpenAcars_v0.9.0.zip`)
3. Haz **clic derecho** sobre ese archivo
4. En el menú, selecciona **"Extraer todo..."** (en inglés puede aparecer como *"Extract All..."*)
5. Windows te va a preguntar en qué carpeta quieres guardar los archivos extraídos — elige la carpeta que creaste en el paso 4.1 (busca el botón **Examinar** para navegar hasta ella)
6. Haz clic en **Extraer** (o *Extract*)
7. Repite exactamente los mismos pasos con el segundo archivo, el de **configuración** que descargaste del enlace `file/59` — asegúrate de extraerlo **en la misma carpeta** que el anterior

Al terminar, dentro de tu carpeta `vmsOpenAcars` deberías ver varios archivos, incluyendo uno llamado **`vmsOpenAcars.exe`** (ese es el programa) junto con los archivos de configuración.

> 💡 Si Windows te pregunta si confías en el archivo o si quieres "ejecutar de todas formas" al abrir `vmsOpenAcars.exe` por primera vez, es normal — acepta y continúa.

---

## Parte 5 — Continúa con la configuración

¡Ya tienes todo lo necesario descargado e instalado! Desde aquí, sigue la **Guía del Usuario** (`BRIEFING.md`, incluida junto a vmsOpenAcars) para:

- Configurar tu **API Key** dentro del programa (la que copiaste en la Parte 2.2)
- Conectar vmsOpenAcars con tu simulador de vuelo
- Entender cómo funciona un vuelo completo, el sistema de puntuación, el mapa en movimiento, y el resto de funciones

📖 Si no encuentras el archivo `BRIEFING.md`, pídeselo a tu administrador de la virtual.

---

## Resumen rápido

1. ✅ Entra a **https://vholar.co** con tu correo registrado
2. ✅ Recupera tu contraseña con **"Forgot password?"**
3. ✅ Edita tu perfil: País (recomendado) + **IVAO ID (obligatorio)**
4. ✅ Genera y **copia tu API Key** (botón amarillo)
5. ✅ Descarga desde **Descargas → Acars**: el archivo de configuración (una sola vez, [file/59](https://vholar.co/vmsOpenDownloads/file/59)) + vmsOpenAcars (versión más reciente)
6. ✅ Crea una carpeta y **descomprime ahí** los dos archivos `.zip`
7. ✅ Sigue la **Guía del Usuario** (`BRIEFING.md`) para terminar de configurar y hacer tu primer vuelo

¡Buenos vuelos! ✈️
