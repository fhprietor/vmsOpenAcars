using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// General Information about an assembly is controlled through the following
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.
[assembly: AssemblyTitle("vmsOpenAcars")]
[assembly: AssemblyDescription("Open source ACARS client for phpVMS 7")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("vmsOpenAcars")]
[assembly: AssemblyProduct("vmsOpenAcars")]
[assembly: AssemblyCopyright("Copyright © 2026 Franklin Prieto. Released under MIT License")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

// Setting ComVisible to false makes the types in this assembly not visible
// to COM components.  If you need to access a type in this assembly from
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible(false)]

// The following GUID is for the ID of the typelib if this project is exposed to COM
[assembly: Guid("736c6469-9933-4021-84c0-dbb054dc8cb8")]

// Permite que el proyecto de tests (vmsOpenAcars.Tests) acceda a los tipos `internal`
// —los helpers de geometría y los valores de respaldo de TA/TL— sin tener que hacerlos
// públicos solo para poder probarlos.
[assembly: InternalsVisibleTo("vmsOpenAcars.Tests")]

// Version information for an assembly consists of the following four values:
//
//      Major Version
//      Minor Version
//      Build Number
//      Revision
//
// You can specify all the values or you can default the Build and Revision Numbers
// by using the '*' as shown below:
// [assembly: AssemblyVersion("1.0.*")]
[assembly: AssemblyVersion("0.9.11.0")]
[assembly: AssemblyFileVersion("0.9.11.0")]
// OJO: AppInfo.Version (Core/Helpers/AppInfo.cs) prefiere ESTE atributo sobre AssemblyVersion.
// Es lo que se pinta en el título de la ventana y lo que se envía a phpVMS. Si no se sube
// junto con los otros dos, el cliente muestra la versión anterior aunque el binario sea nuevo.
[assembly: AssemblyInformationalVersion("0.9.11")]