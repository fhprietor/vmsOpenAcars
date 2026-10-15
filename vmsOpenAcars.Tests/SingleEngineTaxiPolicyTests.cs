using Microsoft.VisualStudio.TestTools.UnitTesting;
using vmsOpenAcars.Helpers;

namespace vmsOpenAcars.Tests
{
    /// <summary>
    /// El criterio de la bonificación de motor único: **el 50 % del tiempo en movimiento de la fase,
    /// con un solo motor**, y las cifras que se publican son las que deciden.
    ///
    /// El caso que lo obliga es el PIREP real **`lA4ZVAzgxYzlG4vD`** (SCEL→SKBO,
    /// `vmsOpenACars/0.9.42`): el piloto arrancó el **ENG2 a las 15:45:29** —el `ENG2 idle 407s` de la
    /// línea `Motor:` de la entrada en pista, con el `Engines started` de las 15:34:10 validando el
    /// método— y la fase `TaxiOut` había empezado a las **15:40:39**. Son **290 s** de motor único
    /// dentro de un rodaje de **630 s** en movimiento: **46 %**, por debajo del umbral, sin
    /// bonificación… y **sin una sola línea en el log** que se lo dijera. Ese silencio es la mitad del
    /// fallo que se corrigió.
    ///
    /// El segundo caso es el **BE58** del PIREP `RBmrBApLOjWlYmO2`, donde el cliente **nunca** vio los
    /// dos motores en marcha (`DecodeEnginePower` etiqueta «N1» un offset que en ese pistón lee escala
    /// completa, y la categoría por título manda `rpm > 400`): la proporción puede parecer un aprobado
    /// y la bonificación no se puede acreditar. El veredicto tiene que decirlo, no callarlo.
    /// </summary>
    [TestClass]
    public class SingleEngineTaxiPolicyTests
    {
        // ══ El caso real, con sus cifras ══════════════════════════════════════
        private const double CasoRealMovimiento = 630.0;   // s en movimiento durante TaxiOut
        private const double CasoRealMotorUnico = 290.0;   // s de esos, con un solo motor

        [TestMethod]
        public void ElRodajeDeMotorUnicoDelCasoReal_NoLlegaAlUmbralYElVeredictoLoDice()
        {
            var v = SingleEngineTaxiPolicy.Evaluate(CasoRealMovimiento, CasoRealMotorUnico, true);

            Assert.IsFalse(v.Awarded, "290 s de 630 son el 46 %: no llega al mínimo");
            Assert.AreEqual(SingleEngineTaxiOutcome.BelowThreshold, v.Outcome);
            Assert.AreEqual(46, v.Percent, "el porcentaje que se publica es el del vuelo real");
            Assert.AreEqual("SETaxi_Below", SingleEngineTaxiPolicy.OutcomeKey(v.Outcome));
        }

        [TestMethod]
        public void JustoEnElUmbral_SeOtorga()
        {
            // 315 de 630 es exactamente el 50 %. El umbral es «al menos», como la comparación
            // `>= 0.5` que había antes en los dos sitios.
            var v = SingleEngineTaxiPolicy.Evaluate(630.0, 315.0, true);

            Assert.IsTrue(v.Awarded);
            Assert.AreEqual(SingleEngineTaxiOutcome.Awarded, v.Outcome);
            Assert.AreEqual(50, v.Percent);
            Assert.IsTrue(SingleEngineTaxiPolicy.Meets(315.0, 315.0));
        }

        /// <summary>
        /// Un pelo por debajo del umbral **no** se otorga, y el porcentaje que se enseña tampoco puede
        /// decir «50 %»: con redondeo, un 49,6 % se publicaría como «50 % · mínimo 50 % · sin
        /// bonificación», que se lee como una contradicción. Por eso se trunca.
        /// </summary>
        [TestMethod]
        public void MedioPuntoPorDebajoDelUmbral_NoSeOtorgaYNoSeEnseniaComoCincuenta()
        {
            var v = SingleEngineTaxiPolicy.Evaluate(630.0, 312.5, true);   // 49,60 %

            Assert.IsFalse(v.Awarded);
            Assert.AreEqual(SingleEngineTaxiOutcome.BelowThreshold, v.Outcome);
            Assert.AreEqual(49, v.Percent, "redondear hacia arriba mentiría sobre el veredicto");
        }

        [TestMethod]
        public void SinMovimiento_NoHayNadaQueAcreditar()
        {
            var v = SingleEngineTaxiPolicy.Evaluate(0.0, 0.0, true);

            Assert.IsFalse(v.Awarded);
            Assert.AreEqual(SingleEngineTaxiOutcome.NoSingleEngine, v.Outcome);
            Assert.AreEqual(0, v.Percent);
            Assert.IsFalse(SingleEngineTaxiPolicy.Meets(0.0, 0.0),
                "sin movimiento el cociente no significa nada");
        }

        [TestMethod]
        public void RodajeSiempreConDosMotores_NoEsMotorUnico()
        {
            var v = SingleEngineTaxiPolicy.Evaluate(600.0, 0.0, true);

            Assert.IsFalse(v.Awarded);
            Assert.AreEqual(SingleEngineTaxiOutcome.NoSingleEngine, v.Outcome);
            Assert.AreEqual("SETaxi_NoMove", SingleEngineTaxiPolicy.OutcomeKey(v.Outcome));
        }

        /// <summary>
        /// El caso del BE58: la proporción puede dar un aprobado, pero si el cliente nunca vio los dos
        /// motores en marcha no hay nada que premiar — y hay que decirlo, porque si no el log estaría
        /// afirmando un aprobado que no se cobró.
        /// </summary>
        [TestMethod]
        public void SiNuncaSeVieronLosDosMotoresEnMarcha_NoSePuedeAcreditarAunqueLaProporcionLlegue()
        {
            var v = SingleEngineTaxiPolicy.Evaluate(300.0, 240.0, bothEnginesRunning: false);

            Assert.AreEqual(80, v.Percent, "la proporción sí llega…");
            Assert.IsFalse(v.Awarded, "…pero sin haber visto los dos motores no se acredita");
            Assert.AreEqual(SingleEngineTaxiOutcome.EnginesNotSeen, v.Outcome);
            Assert.AreEqual("SETaxi_NoEngines", SingleEngineTaxiPolicy.OutcomeKey(v.Outcome));
        }

        /// <summary>
        /// **El rodaje completo del caso real, en un solo porcentaje.** El vuelo `lA4ZVAzgxYzlG4vD` hizo
        /// **290 s de 630** con un motor en la salida (**46 %**) y **nada** con un motor en la llegada
        /// (`0 s de 455`), así que el rodaje entero da **290 de 1085 = 26 %**. Es la cifra que explica
        /// por qué no cobró —y la que se publica en el log, porque el veredicto por fases ya se publica
        /// y esta lo cierra—.
        /// </summary>
        [TestMethod]
        public void ElRodajeCompletoDelCasoReal_DaVeintiseisPorCiento()
        {
            var v = SingleEngineTaxiPolicy.EvaluateWholeTaxi(
                taxiOutMovingSeconds: 630.0, taxiOutSingleEngineSeconds: 290.0,
                taxiInMovingSeconds:  455.0, taxiInSingleEngineSeconds:   0.0,
                bothEnginesRunning:   true);

            Assert.AreEqual(1085.0, v.MovingSeconds, 1e-6);
            Assert.AreEqual(290.0,  v.SingleEngineSeconds, 1e-6);
            Assert.AreEqual(26, v.Percent);
            Assert.IsFalse(v.Awarded);
        }

        /// <summary>
        /// **Y aquí está por qué el porcentaje total no es un camino a la bonificación.** Con `a/w1 < 0,5`
        /// y `b/w2 < 0,5` se tiene `a + b < 0,5·(w1 + w2)`, así que es **imposible** que la suma llegue al
        /// umbral si ninguna fase llega sola: aprobar por el total obliga a haber aprobado por alguna
        /// fase, y ésa ya premia antes. Se comprueba sobre una tabla en vez de sobre un caso, porque es
        /// una propiedad de la aritmética y no de este vuelo.
        ///
        /// Queda fijado para que nadie —yo mismo, en una versión futura— crea que sumar las fases
        /// **relaja** el criterio y vuelva a poner un tercer camino que no puede aprobar nunca.
        /// </summary>
        [TestMethod]
        public void LaSumaNoPuedeAprobarLoQueNingunaFaseApruebaSola()
        {
            var casos = new[]
            {
                // (movimiento salida, motor único salida, movimiento llegada, motor único llegada)
                (630.0, 290.0, 455.0,   0.0),   // el caso real
                (600.0, 300.0, 400.0, 100.0),   // la salida justo en el umbral
                (100.0,  40.0, 500.0, 300.0),   // una salida corta y una llegada larga
                (500.0, 240.0, 500.0, 240.0),   // las dos iguales y por debajo
                (  0.0,   0.0, 400.0, 400.0),   // sólo llegada, y con un motor todo el rato
                (600.0, 600.0,   0.0,   0.0),   // sólo salida, y con un motor todo el rato
                (600.0, 310.0, 100.0,   0.0),   // la salida sólo un poco por encima
            };

            foreach (var (outMoving, outSingle, inMoving, inSingle) in casos)
            {
                bool porFase = SingleEngineTaxiPolicy.Meets(outMoving, outSingle)
                            || SingleEngineTaxiPolicy.Meets(inMoving, inSingle);
                bool porTotal = SingleEngineTaxiPolicy.Meets(outMoving + inMoving, outSingle + inSingle);

                if (porTotal)
                    Assert.IsTrue(porFase,
                        $"la suma aprobó ({outSingle}+{inSingle} de {outMoving}+{inMoving}) sin que " +
                        "ninguna fase llegue al umbral, y eso no puede pasar");
            }
        }

        /// <summary>
        /// Y la otra cara, que es la razón de que el aprobado **por fase** se quede: una fase que aprueba
        /// sola puede quedar por debajo del umbral al sumarla. Si la suma fuera la única regla, este
        /// piloto —que sí rodó con un motor más de la mitad de la salida— **perdería** la bonificación.
        /// Quitar lo que ya se paga no es relajar el criterio, es lo contrario.
        /// </summary>
        [TestMethod]
        public void UnaFaseQueApruebaSola_PuedeNoLlegarAlUmbralSumada()
        {
            const double outMoving = 600.0, outSingle = 310.0;   // 51,7 % con un motor
            const double inMoving  = 1000.0, inSingle = 0.0;     // la llegada, con los dos

            Assert.IsTrue(SingleEngineTaxiPolicy.Meets(outMoving, outSingle), "la salida aprueba sola");

            var total = SingleEngineTaxiPolicy.EvaluateWholeTaxi(outMoving, outSingle, inMoving, inSingle, true);

            Assert.IsFalse(total.Awarded, "sumada no llega: el 52 % de la salida se diluye en el rodaje entero");
            Assert.AreEqual(19, total.Percent, "310 de 1600");
        }
    }
}
