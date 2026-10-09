# El shell de `dsh` deja de ejecutar comandos en Windows — diagnóstico del 08/10/2026

Nota de entorno, **no** una incidencia del cliente ACARS. Se escribe aquí porque el repo es lo
único que sobrevive al reinicio del servidor: sin esto, el síntoma («el agente dice que no puede
ejecutar nada») se diagnostica otra vez desde cero.

Versión del cliente al escribir esto: **v0.9.36** · sin tocar una sola línea de código.

---

## 1. Síntoma

Cualquier comando del agente —incluido `pwsh -Command "Get-Location"`— devuelve, sin ejecutar
nada y en los **dos** modos de acceso (workspace-write y full access):

```
sandbox-local windows-acl temp grant materialization failed and its cleanup also failed
```

No falla una ruta concreta ni un comando concreto: falla **el arranque del proceso**.

## 2. Causa raíz

El sandbox de Windows de `dsh` no encierra el proceso por directorios, sino por **permisos**: crea
un SID de capacidad (`S-1-4-x-y`) por workspace y otro por sesión, y los aplica como entradas de
permiso en el directorio del workspace y en un directorio temporal privado
(`%TEMP%\dsh-<aleatorio>`). Además de la entrada de permiso, escribe una **etiqueta de integridad
obligatoria** (la parte «de auditoría» del descriptor de seguridad) para bajar el proceso a
integridad baja.

Esa etiqueta se escribe con `SetNamedSecurityInfoW`, y **exige el privilegio `SeSecurityPrivilege`
del token**: no lo cubre ser el propietario del objeto. Comprobado con una sonda que reproduce la
misma llamada del sandbox:

| Token | `whoami /priv` | Resultado de la concesión |
|---|---|---|
| Consola **sin elevar** (la que sirve esta sesión) | **sin** `SeSecurityPrivilege` | `SetNamedSecurityInfoW failed (Win32 5)` → acceso denegado |
| Consola **elevada** (UAC) | `SeSecurityPrivilege` presente (deshabilitado, se habilita al usarlo) | concesión aplicada, `dispose` y limpieza correctos |

El proceso `dsh web` que sirve la sesión se lanzó **sin elevar** (`node ... dsh web`, PID padre
`powershell.exe`), así que su token es el filtrado de un administrador: sin grupo de
administradores y **sin** `SeSecurityPrivilege`. De ahí el fallo, y de ahí que el script de
diagnóstico del arnés (`diagnose-windows-sandbox-acl`) dictamine `NOT_THIS_CLASS`: los permisos del
workspace y de sus ancestros **están bien**, no hay nada que reparar con `icacls`.

**Detalle que despista:** el workspace conserva una concesión de capacidad *permanente*, creada por
una sesión anterior que sí pudo escribir la etiqueta. Eso hace que las primeras llamadas del
diagnóstico funcionen y parezca que el entorno está sano; lo que falla siempre es la concesión
**temporal** (nueva en cada sesión), que es la que necesita todo comando confinado.

## 3. Reparación

**Arrancar `dsh` desde una consola elevada** (PowerShell o cmd «Ejecutar como administrador»):

```bash
# en la consola de Administrador, desde la carpeta del repo
npx @deepseek-ai/dsh@0.1.5-rc.3 web
```

Con eso el token del servidor tiene `SeSecurityPrivilege` y las concesiones temporales se
materializan; el shell confinado vuelve a funcionar.

> **Contrapartida, dicha en claro:** en modo elevado el agente hereda ese token, así que sus
> comandos corren como administrador. Es el precio de que el sandbox pueda aplicar la etiqueta en
> Windows; no hay variable de entorno ni opción del cliente que lo evite.

## 4. Cómo verificar que quedó reparado

```powershell
# 1) el shell vuelve a responder (sin aprobaciones de por medio)
pwsh -Command "Get-Location"

# 2) el token del servidor sí trae el privilegio (desde una consola elevada)
whoami /priv | Select-String SeSecurityPrivilege

# 3) build y suite, como manda CLAUDE.md
msbuild vmsOpenAcars.sln /p:Configuration=Debug
vstest.console.exe vmsOpenAcars.Tests\bin\Debug\vmsOpenAcars.Tests.dll
```

## 5. Residuo que se puede borrar sin miedo

Las carpetas `%TEMP%\dsh-*` (`dsh-spill-*`, `dsh-subprocess-*`, `dsh-workspace-changes-*`,
`dsh-acl-skill-*`) son restos de sesiones anteriores; el arnés no las reutiliza. Se pueden borrar
cuando no haya ninguna sesión viva de `dsh`.
