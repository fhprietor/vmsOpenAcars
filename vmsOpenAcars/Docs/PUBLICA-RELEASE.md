# «PUBLICA RELEASE» — receta de publicación

Lo que hace el agente cuando el mantenedor dice **«PUBLICA RELEASE»**, en el orden que funciona.
Ejecutada entera en la **0.9.44** (15/10/2026): de ahí salen las cifras y las trampas de abajo.

## 0. Antes de tocar nada

- La versión nueva es **la siguiente** a la que hay en `Properties/AssemblyInfo.cs`.
- Si hay cambio de código: sus tests y la **suite en verde antes de versionar**. Si solo cambia
  documentación, se salta al paso 3.
- `CLAUDE.md` tiene techo duro y **trunca en silencio por el final**: mirar
  `(Get-Item CLAUDE.md).Length` antes de añadirle una sección (lo largo va a `Docs/`).

## 1. El cambio

Rama `master`, sin dejar nada a medias. Las rutas, desde la raíz del repo (`vmsOpenACars/...`).

## 2. Versión

Tres atributos en `vmsOpenACars/Properties/AssemblyInfo.cs` (`AssemblyVersion`, `AssemblyFileVersion`,
**`AssemblyInformationalVersion`**), los **dos** del proyecto de tests y la línea `**Versión actual:**`
de `CLAUDE.md`. El informativo es el que se pinta en la ventana y el que viaja a phpVMS.

## 3. Documentación, en el mismo commit

- `Docs/CHANGELOG.md`: entrada nueva arriba, con **causa raíz y cifras**.
- `Docs/NOTAS-PILOTOS-<versión>.md`: **lo más compacto posible** —una frase, qué cambia para el
  piloto y `## Lo que hay que saber`—. Es lo que va al cuerpo de la Release.
- `Docs/README.md`: su fila en el índice, o el `.md` queda huérfano.

## 4. Build y suite

```powershell
& $msb vmsOpenAcars.sln /p:Configuration=Release /p:Platform="Any CPU" /v:m /nologo /t:Rebuild /m:1
& $msb vmsOpenAcars.sln /p:Configuration=Debug   /p:Platform="Any CPU" /v:m /nologo
(Get-Item vmsOpenACars\bin\Release\vmsOpenACars.exe).VersionInfo.ProductVersion   # la nueva
& $vstest vmsOpenACars.Tests\bin\Debug\vmsOpenACars.Tests.dll
```

El proyecto de tests **no se compila en Release**: si algo no compila, se ve en el build de Debug.

## 5. El paquete

`bin\Release` entero **menos `vmsOpenACars.exe.config`** (y cualquier `.zip` suelto que haya dentro,
que se borra). Ese `.config` lleva la `vms_api_key` **real** y, sobre todo, **no puede viajar**: el
`Updater` copia fichero a fichero sin borrar nada, así que el config del piloto sobrevive **porque no
va**; meterlo lo pisaría en cada actualización. El detalle, en `Docs/architecture.md` →
«Configuración de build».

El zip se llama **`vmsOpenACars.zip`** (el Updater busca ese nombre de asset) y en la 0.9.44 llevaba
**68 entradas**: los ficheros de `bin\Release`, con `Languages/`, `nl/`, `x64/` y `x86/`. Se comprueba
que el `.exe` de dentro dice la versión nueva y que **no** hay ningún `.config` del cliente.

## 6. Commit y push

`git add -A` y mensaje en el estilo del repo (`tipo(alcance): …`, sin acentos). Ver la trampa de
pathspecs del paso 9. Después, `git push origin master`.

## 7. Etiqueta, Release y binario

```powershell
git tag v<versión>                          # ligera, como las anteriores
git push origin v<versión>
gh release create v<versión> --repo fhprietor/vmsOpenACars `
   --title "vmsOpenACars v<versión>" `
   --notes-file vmsOpenACars\Docs\NOTAS-PILOTOS-<versión>.md `
   D:\Desarrollo\proyectos\VMS\release\vmsOpenACars.zip
```

Sin `--draft`: el mantenedor pide **publicar**. El cuerpo de la Release son las notas del piloto.

## 8. Verificación

```powershell
gh release view v<versión> --repo fhprietor/vmsOpenACars --json name,isDraft,tagName,body,assets
git ls-remote --tags origin | Select-String "v<versión>"
git status -sb                              # master y origin/master, el mismo commit
```

Se comprueba que el **sha256 del asset coincide con el del zip local** —es lo que garantiza que el
binario publicado es el que se probó— y que la etiqueta apunta al commit de la release.

## 9. Trampas del entorno (agente en Windows con `dsh`)

- **`git add <ruta>` no stagea nada**: sale con 0 y el índice queda igual. Tampoco
  `--pathspec-from-file`. Lo único que funciona es **`git add -A`**. La misma rareza afecta a
  `git show <rev>:<ruta>` y a `git ls-tree <rev> -- <ruta>`: **para leer un fichero de un commit, ir
  por el SHA del blob** (`git ls-tree -r <rev>` → `git cat-file -p <sha>`).
- **`vmsOpenACars` contra `vmsOpenAcars`**, y C# distingue mayúsculas: el **namespace** es
  `vmsOpenAcars` (solo la `O` y la `A` en mayúscula) mientras que el **directorio** es
  `vmsOpenACars` (con `C`). Escribirlo mal en un `using` o en un `namespace` da
  `CS0234: no existe 'UI' en el namespace 'vmsOpenACars'`, y el mensaje no dice que el problema sea
  una mayúscula.
- **GitHub Desktop no arranca** desde la sesión del agente (recibe pid y muere). El push va por `git`,
  con la credencial que dejó `gh auth login`; `gh release download --pattern <nombre>` tampoco casa el
  nombre del asset, así que para bajar uno se usa su URL pública o su id.
- **Ningún artefacto al repo**: el zip se deja **fuera** (p. ej. `D:\Desarrollo\proyectos\VMS\release\`)
  para que no acabe dentro de sí mismo ni en el commit.
