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