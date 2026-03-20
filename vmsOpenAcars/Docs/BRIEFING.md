# ✈️ vmsOpenAcars - Guía de Operación para Pilotos

Bienvenido a **vmsOpenAcars**, tu asistente personal de vuelo para aerolíneas virtuales basadas en phpVMS 7. Esta guía te explicará paso a paso cómo utilizar todas las funcionalidades del sistema, desde el inicio de sesión hasta la finalización de tu vuelo.

## 📋 Índice
1. [Primeros Pasos](#primeros-pasos)
2. [Interfaz Principal](#interfaz-principal)
3. [Selección de Vuelo](#selección-de-vuelo)
4. [Planificación con SimBrief](#planificación-con-simbrief)
5. [Inicio del Vuelo](#inicio-del-vuelo)
6. [Durante el Vuelo](#durante-el-vuelo)
7. [Finalización del Vuelo](#finalización-del-vuelo)
8. [Configuración](#configuración)
9. [Solución de Problemas](#solución-de-problemas)

---

## 🚀 Primeros Pasos

### Requisitos Previos
- **Simulador compatible**: FSX, Prepar3D, MSFS 2020/2024, o X‑Plane (con XUIPC)
- **FSUIPC** instalado y funcionando (la aplicación lo detectará automáticamente)
- **Conexión a internet** para comunicarse con phpVMS y SimBrief
- **Credenciales** de tu aerolínea virtual (API Key) y cuenta de SimBrief

### Configuración Inicial
Antes de volar, asegúrate de tener tu archivo `App.config` correctamente configurado (o usa el botón de configuración ⚙️ en la interfaz):

```xml
<appSettings>
    <!-- URL de tu phpVMS (con slash al final) -->
    <add key="vms_api_url" value="https://tu-aerolinea.com/" />
    <!-- Tu API Key personal (generada en tu perfil phpVMS) -->
    <add key="vms_api_key" value="tu-api-key-32-caracteres" />
    <!-- Tu usuario de SimBrief -->
    <add key="simbrief_user" value="tu-usuario-simbrief" />
    <!-- Nombre de tu aerolínea (aparece en el encabezado) -->
    <add key="airline" value="Mi Aerolínea Virtual" />
    <!-- Idioma por defecto (es, en, etc.) -->
    <add key="language" value="es" />
</appSettings>
🖥️ Interfaz Principal
La pantalla principal está dividida en varias secciones inspiradas en una cabina ECAM:

Sección	Descripción
Header	Logo, título y nombre de la aerolínea. También contiene el botón de configuración (⚙️).
FLIGHT INFORMATION	Datos del plan de vuelo actual (número, ruta, aeronave, combustible, etc.).
FMA (Flight Mode Annunciator)	Panel superior con información de fase, velocidad, altitud y estado (AIR/GROUND).
INCOMING MSG	Registro de eventos y mensajes del sistema (con colores según tipo).
STATUS	Estado de conexiones ACARS, posición actual, validación y nombre del simulador.
Botones	Panel inferior con las acciones principales: LOGIN, SIMBRIEF, START/ABORT/SEND PIREP, CANCEL/EXIT.
🔐 Inicio de Sesión
Haz clic en el botón LOGIN.

La aplicación se conectará a phpVMS usando tu API Key.

Verás un mensaje de éxito en el panel de logs y tu nombre de piloto aparecerá en el área de STATUS.

El aeropuerto donde te encuentras asignado se mostrará en APT: XXX.

💡 Nota: Si el simulador ya está conectado, se validará automáticamente tu posición contra el aeropuerto asignado.

🗺️ Selección de Vuelo
Una vez autenticado, haz clic en SIMBRIEF. Se abrirá el Flight Planner, que tiene dos pestañas:

📌 Mis Reservas (My Bids)
Muestra los vuelos que tienes reservados en phpVMS. Solo aparecerán aquellos que salgan desde tu aeropuerto actual.

🌍 Vuelos Disponibles (Available Flights)
Lista todos los vuelos que salen de tu aeropuerto actual, filtrados automáticamente para mostrar solo aquellos que puedan ser operados con los aviones disponibles en dicho aeropuerto.

Columnas informativas:

Flight: Número de vuelo (ej. VHR1111 o 55CH para vuelos charter)

From → To: Aeropuerto origen y destino

Aircraft: Tipo(s) de aeronave permitido(s) (ej. BE58/AT46)

Distance (NM): Distancia en millas náuticas

Flight Time: Duración estimada

Route: Ruta resumida

Selección de Aeronave
Selecciona un vuelo de cualquiera de las dos pestañas.

Automáticamente se cargará la lista de aeronaves disponibles en tu aeropuerto que puedan operar ese vuelo.

Elige una aeronave de la lista.

📡 Planificación con SimBrief
Una vez seleccionado vuelo y aeronave:

Haz clic en PLAN IN SIMBRIEF. Se abrirá tu navegador con la página de dispatch de SimBrief, con todos los datos pre‑cargados:

Aerolínea y número de vuelo

Origen y destino

Tipo de aeronave y matrícula

Ruta (si está disponible)

Nombre del piloto

Hora de salida (UTC actual +30 min)

En SimBrief, ajusta los parámetros que desees y genera el plan de vuelo.

Vuelve a vmsOpenAcars y haz clic en FETCH OFP.

El sistema descargará tu último plan de SimBrief y lo validará contra:

Origen y destino seleccionados

Tipo de aeronave

Matrícula

Fecha de generación (máximo 2 horas de antigüedad)

Fecha de salida programada (no puede ser anterior a hoy)

Si todo es correcto, el botón ACCEPT se habilitará. Haz clic para cargar el plan completo en el ACARS.

✅ El panel FLIGHT INFORMATION se actualizará con todos los datos del plan.

🛫 Inicio del Vuelo
Cuando el plan esté cargado y se cumplan todas las condiciones:

✅ Simulador conectado

✅ Posición GPS válida (estás en el aeropuerto correcto)

✅ ICAO del plan coincide con tu aeropuerto asignado

El botón START se habilitará.

Haz clic en START.

La aplicación enviará un prefile a phpVMS, creando un PIREP en estado BOARDING.

El botón cambiará a ABORT (rojo) por si necesitas cancelar el vuelo.

La fase de vuelo se actualizará automáticamente según tus acciones en el simulador.

✈️ Durante el Vuelo
Detección Automática de Fases
El sistema detecta las siguientes fases sin intervención del piloto:

Fase	Descripción
Boarding	Pasajeros embarcando
Pushback	Retroceso desde puerta
TaxiOut	Rodaje a pista
Takeoff	Despegue
Climb	Ascenso inicial
Enroute	Crucero
Descent	Descenso
Approach	Aproximación
Landing	Aterrizaje
AfterLanding	Inmediatamente después del aterrizaje
TaxiIn	Rodaje a puerta
OnBlock	Llegada a puerta
Completed	Vuelo completado
Telemetría en Tiempo Real
Posición GPS: Se actualiza constantemente en el panel STATUS.

Velocidad y altitud: Se reflejan en el FMA.

Fase actual: Cambia de color según el estado (verde para en ruta, naranja para aproximación, etc.).

Envío de Datos a phpVMS
Las posiciones se envían al servidor con frecuencia variable según la fase:

En tierra: cada 30 segundos

Takeoff/Landing: cada 2 segundos

Climb/Descent/Approach: cada 5 segundos

Crucero: cada 15 segundos

Los eventos importantes (cambios de fase, alertas) se envían instantáneamente.

Cancelación del Vuelo
Si necesitas abortar el vuelo en cualquier momento:

Haz clic en ABORT.

Confirma en el diálogo ECAM.

El PIREP se cancelará en el servidor y el sistema volverá al estado inicial.

🏁 Finalización del Vuelo
Cuando el avión llegue a puerta y la fase cambie a Completed:

El botón ABORT se transformará en SEND PIREP (verde).

Haz clic en SEND PIREP.

La aplicación enviará el informe final a phpVMS con:

Combustible utilizado

Tasa de aterrizaje

Tiempo de vuelo

Distancia recorrida

Notas del vuelo

Tras la confirmación del servidor, el sistema se reseteará y el botón volverá a START (deshabilitado hasta que se cargue un nuevo plan).

⚙️ Configuración
Haz clic en el icono de engranaje (⚙️) en la esquina superior derecha para abrir el diálogo de configuración.

Parámetros editables
Campo	Descripción
URL API	Dirección base de tu phpVMS
API Key	Tu clave personal (se genera en tu perfil)
Usuario SimBrief	Tu nombre de usuario en SimBrief
Aerolínea	Nombre que aparecerá en el encabezado
Idioma	Selecciona entre los idiomas disponibles (es, en, etc.)
⚠️ Importante: Los cambios en URL, API Key y usuario SimBrief requieren reiniciar la aplicación. El idioma se aplica inmediatamente.

❓ Solución de Problemas
El botón SIMBRIEF no se habilita después del login
Verifica que tu API Key sea correcta.

Asegúrate de tener conexión a internet.

Revisa los logs en el panel INCOMING MSG.

El botón START no se habilita aunque tenga plan cargado
Revisa el panel de validación: debe mostrar ICAO ✅ y GPS ✅.

Asegura que el simulador esté conectado y en el aeropuerto correcto.

La posición GPS debe estar a menos de 5 km del aeropuerto.

SimBrief no pre‑carga todos los datos
Verifica que el vuelo seleccionado tenga número de aerolínea y ruta.

Comprueba que la aeronave tenga tipo ICAO válido (ej. BE58, B738).

Los logs aparecen duplicados
Es normal durante la validación inicial; si persiste, reinicia la aplicación.

La ventana no recuerda su posición
El sistema guarda automáticamente la posición al cerrar. Si no funciona, verifica permisos de escritura en la carpeta de la aplicación.

🎯 Resumen del Flujo de Vuelo















📞 Soporte
Si encuentras algún problema o tienes sugerencias, puedes:

Abrir un issue en el repositorio de GitHub.

Consultar los logs (panel INCOMING MSG) para obtener pistas sobre errores.

Verificar que todos los requisitos estén cumplidos.

¡Gracias por volar con vmsOpenAcars! ✈️ Que tengas un excelente vuelo.
