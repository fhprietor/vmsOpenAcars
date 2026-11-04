using System;
using System.ComponentModel;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using vmsOpenAcars.Helpers;

namespace vmsOpenAcars.Tests
{
    /// <summary>
    /// **El piloto que reporta «el ACARS no detecta el simulador salvo como administrador».** La causa
    /// no es el simulador: es UIPI. El simulador o FSUIPC7 corre elevado, el ACARS no, y Windows no
    /// deja abrir la tubería entre procesos de distinto nivel de integridad; devuelve **acceso
    /// denegado**, sin ningún aviso de UAC. Hasta v0.9.64 el `catch` de `FsuipcService.TryConnect`
    /// tiraba esa excepción y solo dejaba el estado en «desconectado», así que el piloto no podía
    /// distinguir «el sim no está abierto» de «Windows me bloquea».
    ///
    /// Lo que decide esta regla: cuándo el fallo es de permisos (y merece el aviso de elevación) y
    /// cuándo es cualquier otro. Las excepciones son las que entrega de verdad el runtime; el texto
    /// llega en el idioma del sistema, por eso se prueban las dos cadenas.
    /// </summary>
    [TestClass]
    public class SimLinkFailureTests
    {
        /// <summary>La forma directa: la DLL de FSUIPC envuelve el error 5 de Windows así.</summary>
        [TestMethod]
        public void UnauthorizedAccessExceptionEsAccesoDenegado()
        {
            var ex = new UnauthorizedAccessException("Access is denied.");

            Assert.IsTrue(SimLinkFailure.IsAccessDenied(ex),
                "UnauthorizedAccessException es el acceso denegado de UIPI y tiene que clasificarse");
            Assert.AreEqual(SimLinkFailure.AccessDeniedKey, SimLinkFailure.MessageKeyFor(ex),
                "el aviso tiene que ser el de elevación, no el genérico");
        }

        /// <summary>El caso más habitual en MSFS: FSUIPC7 lanza `IOException` con el texto del sistema.</summary>
        [TestMethod]
        public void IOExceptionConAccessIsDeniedEsAccesoDenegado()
        {
            var ex = new IOException("Unable to connect: Access is denied");

            Assert.IsTrue(SimLinkFailure.IsAccessDenied(ex),
                "IOException con «Access is denied» es el mismo bloqueo de integridad");
            Assert.AreEqual(SimLinkFailure.AccessDeniedKey, SimLinkFailure.MessageKeyFor(ex));
        }

        /// <summary>
        /// **Windows en español traduce el mensaje.** Comparar solo «access is denied» dejaba sin aviso
        /// de elevación a los pilotos con el sistema en español, que es la mayoría de la aerolínea.
        /// </summary>
        [TestMethod]
        public void IOExceptionConAccesoDenegadoEnEspanolTambienClasifica()
        {
            var ex = new IOException("No se puede conectar: Acceso denegado.");

            Assert.IsTrue(SimLinkFailure.IsAccessDenied(ex),
                "«Acceso denegado» es el mismo error 5, solo que traducido por el sistema");
        }

        /// <summary>
        /// La DLL envuelve el error nativo: el `Win32Exception(5)` va dentro y el mensaje de fuera no
        /// dice nada. Sin mirar `InnerException`, este fallo se reportaba como «sim cerrado».
        /// </summary>
        [TestMethod]
        public void ElAccesoDenegadoAnidadoEnInnerExceptionTambienClasifica()
        {
            var ex = new InvalidOperationException(
                "FSUIPC no pudo abrir la tubería",
                new Win32Exception(5, "Access is denied."));

            Assert.IsTrue(SimLinkFailure.IsAccessDenied(ex),
                "el error nativo viaja en InnerException; hay que recorrerla");
        }

        /// <summary>
        /// **No todo `IOException` es de permisos.** Un FSUIPC sin instalar o un simulador cerrado dan
        /// el mismo tipo de excepción, y avisar de «abre el ACARS como administrador» ahí mandaría al
        /// piloto a un callejón sin salida. Tiene que caer en la clave genérica.
        /// </summary>
        [TestMethod]
        public void UnIOExceptionGenericoNoSeClasificaComoPermisos()
        {
            var ex = new IOException("No se encuentra el fichero FSUIPC64.dll");

            Assert.IsFalse(SimLinkFailure.IsAccessDenied(ex),
                "sin la cadena de acceso denegado es un fallo de instalación, no de elevación");
            Assert.AreEqual(SimLinkFailure.UnavailableKey, SimLinkFailure.MessageKeyFor(ex),
                "el texto genérico es el que dice que el sim puede estar cerrado");
        }

        /// <summary>Sin excepción no hay nada que clasificar y el helper no puede lanzar.</summary>
        [TestMethod]
        public void NullNoLanzaYDevuelveFalso()
        {
            Assert.IsFalse(SimLinkFailure.IsAccessDenied(null),
                "null es «no hay fallo que clasificar», no un acceso denegado");
            Assert.AreEqual(SimLinkFailure.UnavailableKey, SimLinkFailure.MessageKeyFor(null));
        }
    }
}
