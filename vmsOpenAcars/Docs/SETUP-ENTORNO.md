# Preparar otro equipo para desarrollar vmsOpenAcars

Checklist para pasar el desarrollo a un equipo nuevo (por ejemplo el portátil), con
**DeepSeek Harness (`dsh`)** en lugar de Claude Code. Verificado sobre el equipo actual
(`master` en **v0.9.12**, Node v24.21.0, `dsh` 0.1.5-rc.3).

> **Lo que no está en el repositorio, no viaja.** El repo es la única memoria que sobrevive al
> cambio de máquina: el historial de conversación del agente se queda en el equipo viejo, y las
> reglas de trabajo, el changelog y las decisiones están en archivos justamente por eso.

---

## 1. Qué viaja por `git` y qué no

| Cosa | ¿Viaja? | Qué hacer |
|---|---|---|
| Código, solución, tests, `Docs/`, `CLAUDE.md`, `Languages/*.json` | **Sí** | Nada, viene en el `clone` |
| `vmsOpenAcars/App.config` | **No** (`.gitignore`) | **Copiarlo a mano** desde el equipo viejo. Lleva `vms_api_url`, `vms_api_key`, `navdata_api_url`, `navdata_api_key`, `simbrief_user`, `landing_log_path`. Sin él la app arranca sin credenciales |
| `Updater/App.config` | **No** (`.gitignore`) | Copiarlo también si se va a tocar el Updater |
| `packages/` (NuGet) | **No** (`.gitignore`) | Restaurar: `nuget restore vmsOpenAcars.sln` o dejar que VS2017 lo haga al primer build |
| `bin/`, `obj/`, `.vs/`, `TestResults/` | **No** | Se regeneran |
| `landing_log.sqlite`, `NavData_cache.sqlite` | **No** | Se regeneran junto al `.exe`. Copiarlos solo si interesa conservar el log de aterrizajes local |
| `%TEMP%\vmsacars\briefing\` (audios de cabina) | **No** | Se descargan otra vez al iniciar un vuelo |
| `vmsOpenAcars.txt` | **Sí** (trackeado) | Volcado de código desactualizado; su destino está pendiente de decisión del mantenedor |

**El historial de git incluye las claves de phpVMS y NavData de versiones anteriores.**
`App.config` ya no se trackea, pero las que se comitearon antes siguen ahí: rotarlas es una
decisión pendiente del mantenedor (ver `CLAUDE.md` → Próximas áreas).

---

## 2. Toolchain en el equipo nuevo

| Necesita | Detalle |
|---|---|
| **.NET Framework 4.8** | Developer Pack (y 4.8.1, que es lo que declara `App.Release.config` en `supportedRuntime`) |
| **Visual Studio 2017** (15.x) | El proyecto es C# 7.3 / net48. Con el IDE se compila y depura; las rutas documentadas abajo son las de VS2017 |
| **MSBuild 15.0** | `…\Microsoft Visual Studio\2017\Professional\MSBuild\15.0\Bin\MSBuild.exe`. La guía dice compilar desde el IDE; la verificación automatizada usa este `MSBuild.exe` (mismo toolchain, sin interfaz) |
| **`vstest.console.exe`** | `…\Common7\IDE\CommonExtensions\Microsoft\TestWindow\vstest.console.exe`. Es el runner de la suite |
| **NuGet** | Los paquetes se referencian desde `..\packages\…` (`packages.config`): **sin restaurar, no compila** |
| **Simulador + FSUIPC/XUIPC** | Solo para **volar**; compilar y correr los tests no lo necesita |

Dependencias principales que trae el restore: `FSUIPCClientDLL 3.3.16`, `NAudio 2.3.0`,
`Newtonsoft.Json`, `GMap.NET 2.1.7` (+ `GMap.NET.WinForms`, `GMap.NET.Core`),
`System.Data.SQLite 1.0.119` (con su `SQLite.Interop.dll` x86/x64), `MSTest.TestAdapter`/
`TestFramework 1.3.2`, `PdfiumViewer`.

---

## 3. DeepSeek Harness (`dsh`)

El equipo viejo lo ejecuta tal cual, a través de `npx`, sin instalación global:

```bash
# desde la carpeta del repo (esa carpeta es el workspace por defecto)
npx @deepseek-ai/dsh@0.1.5-rc.3 web     # alias de --profile web; abre la GUI en http://127.0.0.1:3080
```

| Pieza | Dónde | Nota |
|---|---|---|
| Node.js | v24.21.0 en el equipo viejo | Cualquier versión reciente de Node 20+ sirve; conviene usar la misma para no pelearse con el caché de `npx` |
| Paquete | `@deepseek-ai/dsh` **0.1.5-rc.3** | Binario `dsh` |
| `DSH_HOME` | `%USERPROFILE%\.dsh` | Ahí viven `settings.yaml`, `.credentials.yaml`, `profiles/`, `sessions/`, `storages/` |
| `.credentials.yaml` | `%USERPROFILE%\.dsh\` | **Es un secreto** (clave del proveedor del modelo). Copiarlo por un canal privado o volver a autenticarse; **nunca** al repositorio |
| `settings.yaml` | `%USERPROFILE%\.dsh\` | Perfil, modelo y proveedor por defecto; el mismo archivo evita reconfigurar |
| `sessions/` | `%USERPROFILE%\.dsh\` | Historial de conversaciones. **No viaja** y no hace falta: lo durable está en el repo |

**La memoria del proyecto sí viaja**: `CLAUDE.md` en la raíz del repo se carga solo como
instrucciones del workspace — es el mismo archivo que lee Claude Code, así que el contexto del
proyecto (reglas de trabajo, arquitectura, decisiones pendientes) es idéntico en los dos agentes.
Por eso las reglas viven ahí y no en el hilo de conversación.

---

## 4. Checklist en el equipo nuevo

```bash
git clone https://github.com/fhprietor/vmsOpenAcars.git   # o git pull si ya está clonado
```

1. **Copiar `vmsOpenAcars/App.config`** (y `Updater/App.config`) desde el equipo viejo.
2. **Restaurar NuGet**: `nuget restore vmsOpenAcars.sln` (o dejar que VS2017 lo haga al abrir).
3. **Build Debug y Release** y pasar la suite:
   ```bash
   msbuild vmsOpenAcars.sln /p:Configuration=Debug
   vstest.console.exe vmsOpenAcars.Tests\bin\Debug\vmsOpenAcars.Tests.dll
   ```
   Esperado: **229/229**. En Release el proyecto de tests no se compila (es a propósito).
4. **Comprobar la versión**, que es el error que ya nos mordió una vez:
   `(Get-Item bin\Release\vmsOpenAcars.exe).VersionInfo` → **`ProductVersion` = 0.9.12**. Si el
   título de la ventana dice una versión anterior, falta subir `AssemblyInformationalVersion`
   (ver `CLAUDE.md` → **Versionado**, los tres atributos van juntos).
5. **Abrir la app y revisar Settings**: con `App.config` presente deben aparecer la URL y la API
   Key de NavData y la sección con las cinco líneas (separador, URL, Key, resultado del test,
   botones **REFRESH** y **TEST**).
6. **Lanzar el agente desde la raíz del repo** para que el workspace sea el proyecto:
   `npx @deepseek-ai/dsh@0.1.5-rc.3 web`.

---

## 5. Cuando vuelvas al equipo viejo

- `git pull` y reconstruir: los `bin/` y `obj/` no viajan, y **`App.config` tampoco**, así que si
  el equipo viejo se limpió hay que volver a ponerlo.
- Si el otro equipo generó artefactos (`landing_log.sqlite`, `NavData_cache.sqlite`), no se
  commitean: están ignorados a propósito.
