# Preparar otro equipo para desarrollar vmsOpenAcars

Checklist para pasar el desarrollo a un equipo nuevo (por ejemplo el portátil), con
**DeepSeek Harness (`dsh`)** en lugar de Claude Code. Verificado sobre el equipo actual
(`master` en **v0.9.12**, Node v24.21.0, `dsh` 0.1.5-rc.3).

> **Recorrido entero en el portátil el 11/10/2026 (`master` en v0.9.37, VS2017 Professional,
> `dsh` en el navegador).** Los tres tropiezos que aparecieron y que ya están incorporados abajo:
> el shell del agente no ejecutaba **nada** (era un permiso que faltaba en la raíz del workspace, ver
> `2026-10-08-DSH-SHELL-BLOQUEADO-WINDOWS.md` §6), faltaba `App.config` y faltaban los dos paquetes
> de MSTest en `packages\`. Con eso resuelto: **Debug y Release compilan y la suite da 655/655**.

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
| **MSBuild 15.0** | `…\Microsoft Visual Studio\2017\Professional\MSBuild\15.0\Bin\MSBuild.exe`. La guía dice compilar desde el IDE; la verificación automatizada usa este `MSBuild.exe` (mismo toolchain, sin interfaz). **Hay que pasarle también `/p:Platform="Any CPU"`**: sin la plataforma, MSBuild no coge la de la solución y falla con `MSB4126 … la configuración "Debug|MCD" no es válida` (comprobado en el portátil el 11/10/2026) |
| **`vstest.console.exe`** | `…\Common7\IDE\CommonExtensions\Microsoft\TestWindow\vstest.console.exe`. Es el runner de la suite |
| **NuGet** | Los paquetes se referencian desde `..\packages\…` (`packages.config`): **sin restaurar, no compila**. Ojo: **`nuget.exe` no está en el `PATH`** ni dentro de VS2017; si falta, se baja de `https://dist.nuget.org/win-x86-commandline/v5.11.6/nuget.exe` y se ejecuta desde donde se guarde. Y una carpeta `packages\` copiada a mano puede estar **incompleta**: el proyecto de tests necesita `MSTest.TestAdapter` y `MSTest.TestFramework` **1.3.2**, que son los dos que no usa nadie más — si faltan, el error es `This project references NuGet package(s) that are missing on this computer` |
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

> **En Windows, `dsh` se lanza desde una consola de Administrador.** El sandbox necesita el
> privilegio `SeSecurityPrivilege` para escribir la etiqueta de integridad de la carpeta temporal,
> y un servidor sin elevar no lo tiene: el síntoma es que **ningún** comando del agente se ejecuta
> (`sandbox-local windows-acl temp grant materialization failed`). El diagnóstico completo, con la
> reproducción y la verificación, está en `2026-10-08-DSH-SHELL-BLOQUEADO-WINDOWS.md`.

### 3.1. Trampas del `git` en esta máquina (medidas el 11/10/2026)

**Un pathspec relativo exacto puede no casar aunque el nombre sea idéntico, en silencio.** Medido:
`git add -- vmsOpenACars/CLAUDE.md` **no** hace nada (ni error), mientras que el comodín
`'*CLAUDE.md*'` sí casa el mismo fichero y `git add -u` lo añade. Los bytes del nombre en disco y en
el índice son **idénticos** (`76 6d 73 4f 70 65 6e 41 63 61 72 73 2f …`), así que no es un problema
de mayúsculas ni de acentos. Consecuencias reales: un `git add` que parece correcto deja el commit
**con menos ficheros de los que crees** (pasó: un commit de documentación salió con **1** de los 5),
y `git checkout -- <ruta>` y `git show HEAD:<ruta>` fallan con «did not match any file(s) known to
git» / «exists on disk, but not in 'HEAD'».

**Qué hacer:** usar **`git add -u`** para los modificados (o comodines), y **comprobar siempre con
`git diff --cached --stat` antes de comitear** que están todos. Para leer un fichero commiteado sin
que PowerShell estropee los acentos, sacarlo **en binario**:

```powershell
cmd /c "git -C <repo> cat-file blob <hash-del-indice> > salida.md"   # git ls-files -s da el hash
```

**Y `CLAUDE.md` no tiene 64 KB de presupuesto, tiene ~65 218 bytes.** El 11/10/2026 el arnés truncó
un fichero de 65 407 a 65 218 y **se comió la última línea de "Próximas áreas"** (el bullet de
`landing_rate = 0`), que es justo lo que la regla del techo quiere evitar. Se detecta porque el
aviso del arnés pasa de *omitted* a **truncated**; se arregla reconstruyendo el fichero desde el
blob commiteado (`cat-file`, arriba) y aplicando **solo** el cambio pretendido, nunca editando el
fichero dañado a mano.


---

## 4. Checklist en el equipo nuevo

```bash
git clone https://github.com/fhprietor/vmsOpenAcars.git   # o git pull si ya está clonado
```

1. **Copiar `vmsOpenAcars/App.config`** (y `Updater/App.config`) desde el equipo viejo. Sin el
   principal, el build **no falla por código**: falla con `MSB3249 … El archivo de configuración de
   la aplicación "App.config" no es válido`, que se lee como un problema del proyecto y no lo es.
2. **Restaurar NuGet**: `nuget restore vmsOpenAcars.sln` (o dejar que VS2017 lo haga al abrir).
3. **Build Debug y Release** y pasar la suite:
   ```bash
   msbuild vmsOpenAcars.sln /p:Configuration=Debug /p:Platform="Any CPU"
   vstest.console.exe vmsOpenAcars.Tests\bin\Debug\vmsOpenAcars.Tests.dll
   ```
   Esperado: **655/655** (a 11/10/2026, en el portátil). En Release el proyecto de tests no se
   compila (es a propósito).
4. **Comprobar la versión**, que es el error que ya nos mordió una vez:
   `(Get-Item bin\Release\vmsOpenAcars.exe).VersionInfo` → **`ProductVersion` = 0.9.37**. Si el
   título de la ventana dice una versión anterior, falta subir `AssemblyInformationalVersion`
   (ver `CLAUDE.md` → **Versionado**, los tres atributos van juntos).
5. **Abrir la app y revisar Settings**: con `App.config` presente la app arranca con la URL y la
   clave de phpVMS. **La sección *NavData API* ya no existe**: desde v0.9.27 no hay campos de URL ni
   de clave de NavData —la clave llega **cifrada en el sobre de phpVMS** y vive solo en memoria—,
   así que **no** es un fallo que no aparezcan. Lo que sí conviene mirar es `carto_api_key` en el
   `.config`: vacía, el mapa sale con la marca de agua `API KEY REQUIRED`.
6. **Lanzar el agente desde la raíz del repo** para que el workspace sea el proyecto:
   `npx @deepseek-ai/dsh@0.1.5-rc.3 web`.

---

## 5. Cuando vuelvas al equipo viejo

- `git pull` y reconstruir: los `bin/` y `obj/` no viajan, y **`App.config` tampoco**, así que si
  el equipo viejo se limpió hay que volver a ponerlo.
- Si el otro equipo generó artefactos (`landing_log.sqlite`, `NavData_cache.sqlite`), no se
  commitean: están ignorados a propósito.
