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

        // ══ El vuelo que obligó a separar «moverse» de «rodar» ═════════════════

        /// <summary>
        /// **Sin un motor en marcha no hay rodaje.** El vuelo que lo obliga es el PIREP
        /// `29Z3A8r748rGPnzq` (SKRG→KSEA, `vmsOpenACars/0.9.46`, 20/10/2026), reclamado por el piloto
        /// **en vuelo**: entre las 19:46:53 y las 19:54:24 el avión hizo **60 m en ocho minutos** con
        /// **IAS 0**, pero el simulador publicaba **GS 4-7 kt** y hasta 500 fpm de `vs` con el avión
        /// aparcado en el puesto. Ruido de datos de tierra.
        ///
        /// Esas filas entraban en el criterio **con los dos motores apagados** —cero motores no es
        /// «uno», así que no sumaban— y engordaban el denominador: **restaban** igual que un rodaje real
        /// con los dos motores. La regla que queda: sin un motor en marcha, ese tiempo no se cuenta.
        /// </summary>
        [TestMethod]
        public void SinNingunMotorEnMarchaNoSeCuentaComoRodaje()
        {
            // 29 m en la ventana de 5 s son ~11 kt: rodando. 3 m son el ruido de un avión parado.
            Assert.IsFalse(SingleEngineTaxiPolicy.CountsAsMoving(29.0, false, false),
                "se movió con todo apagado: eso es remolque, no rodaje");
            Assert.IsTrue (SingleEngineTaxiPolicy.CountsAsMoving(29.0, true,  false), "29 m con ENG1");
            Assert.IsTrue (SingleEngineTaxiPolicy.CountsAsMoving(29.0, false, true),  "29 m con ENG2");
            Assert.IsTrue (SingleEngineTaxiPolicy.CountsAsMoving(29.0, true,  true),  "rodando con los dos");
            Assert.IsFalse(SingleEngineTaxiPolicy.CountsAsMoving(3.0,  true,  false),
                "3 m en 5 s no es moverse, y ése es justo el ruido que este umbral deja fuera");
            Assert.IsFalse(SingleEngineTaxiPolicy.CountsAsMoving(0.0,  true,  true), "parado con los dos en marcha");
        }

        // ══ El movimiento, medido por desplazamiento ══════════════════════════

        /// <summary>
        /// **Con el avión aparcado, la GS del simulador miente.** Es el tramo real del PIREP
        /// `29Z3A8r748rGPnzq`: de las 19:46:53 a las 19:54:24 —**451 s**— el avión se desplazó **60 m**
        /// mientras el sim publicaba **GS 4-7 kt** e **IAS 0**. En una ventana de 5 s eso son décimas de
        /// metro: no se mueve, por mucho que la GS diga que sí.
        /// </summary>
        [TestMethod]
        public void ElAvionAparcadoNoSeMueveAunqueLaGsDigaQueSi()
        {
            var motion = new GroundMotion();
            const double lat0 = 6.17319, lon0 = -75.42833;
            const double dLat = (6.17372 - lat0) / 451.0;    // las posiciones reales del puesto
            const double dLon = (-75.42822 - lon0) / 451.0;

            for (int i = 0; i <= 451; i++)
                motion.Observe(lat0 + dLat * i, lon0 + dLon * i, 1.0);

            Assert.IsTrue(motion.WindowMetres < SingleEngineTaxiPolicy.MinMovingMetres,
                $"en la ventana sólo se han recorrido {motion.WindowMetres:F1} m: no llega al umbral");
        }

        /// <summary>
        /// Y el tramo que sí era rodaje: de las 19:58:53 a las 19:59:24 el avión pasó de 6,17381/−75,42813
        /// a 6,17260/−75,42626 —unos **250 m en 31 s**, 15 kt—. Ahí la ventana de 5 s lleva ~40 m.
        /// </summary>
        [TestMethod]
        public void RodandoDeVerdadLaVentanaLoVe()
        {
            var motion = new GroundMotion();
            const double lat0 = 6.17381, lon0 = -75.42813;
            const double dLat = (6.17260 - lat0) / 31.0;
            const double dLon = (-75.42626 - lon0) / 31.0;

            for (int i = 0; i <= 31; i++)
            {
                motion.Observe(lat0 + dLat * i, lon0 + dLon * i, 1.0);
                if (i >= 5)
                    Assert.IsTrue(motion.WindowMetres >= SingleEngineTaxiPolicy.MinMovingMetres,
                        $"a los {i} s la ventana lleva {motion.WindowMetres:F1} m");
            }

            Assert.IsTrue(motion.StepMetres > 4.0,
                $"un paso de un segundo ronda los 8 m, y salió {motion.StepMetres:F1}");
        }

        /// <summary>
        /// Y lo que ese descarte cambia, con las cifras del mismo vuelo: **228 s** con un motor dentro de
        /// **691 s** que el simulador dio por movimiento → **sin bonificación** (el log enseñó 33 %); los
        /// mismos **228 s** dentro de **448** —descontados los **243 s** que se hicieron con los motores
        /// apagados— → **con bonificación**.
        ///
        /// Se comprueba el **veredicto** y no el entero exacto a propósito: el porcentaje se **trunca**
        /// (para que un 49,6 % no se lea como «50 % · mínimo 50 % · sin bonificación») mientras el log
        /// redondea los dos segundos para enseñarlos, así que la cuenta con las cifras redondeadas da un
        /// punto menos. Lo que no puede pasar —y es lo que se afirma— es que el número publicado y el
        /// veredicto se contradigan.
        /// </summary>
        [TestMethod]
        public void ElTiempoSinMotorDescartadoEsLaDiferenciaDelCasoReal()
        {
            var conRuido = SingleEngineTaxiPolicy.Evaluate(691.0, 228.0, true);
            var sinRuido = SingleEngineTaxiPolicy.Evaluate(448.0, 228.0, true);

            Assert.IsFalse(conRuido.Awarded, "con el ruido dentro no llega al mínimo");
            Assert.IsTrue(conRuido.Percent < SingleEngineTaxiPolicy.MinPercent,
                $"sin bonificar, el número publicado no puede llegar al mínimo (salió {conRuido.Percent} %)");

            Assert.IsTrue(sinRuido.Awarded, "descontado el tiempo sin motor, el mismo rodaje sí llega");
            Assert.IsTrue(sinRuido.Percent >= SingleEngineTaxiPolicy.MinPercent,
                $"con bonificación, el número publicado tiene que llegar al mínimo (salió {sinRuido.Percent} %)");
        }
    }
}
