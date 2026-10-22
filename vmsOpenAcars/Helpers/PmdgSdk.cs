namespace vmsOpenAcars.Helpers
{
    /// <summary>Familia de PMDG cuyo bloque SDK hay que leer. `None` = no es PMDG.</summary>
    internal enum PmdgFamily
    {
        None = 0,
        B737 = 1,
        B777 = 2
    }

    /// <summary>
    /// Lo que el bloque SDK de PMDG publica, en común para las dos familias. El 737 y el 777 no
    /// comparten direcciones, así que el mismo campo se rellena desde offsets distintos según la
    /// familia (ver <see cref="PmdgSdk"/>).
    /// </summary>
    internal struct PmdgSdkRaw
    {
        public PmdgFamily Family;

        /// <summary>`0x65E9 MCP_indication_powered` (solo 737): el bloque está alimentado.</summary>
        public bool IndicationPowered;

        // Piloto automático
        public bool CmdA, CmdB, CwsA, CwsB;  // 737
        public bool Ap1, Ap2;                // 777 (`MCP_annunAP[2]`)
        public bool Fd1, Fd2;

        // Modos (anunciadores del MCP; los dos aviones los publican uno por bandera)
        public bool Lnav, Vnav, Flch, HdgSel, AltHold, Vs, Loc, App, N1, Speed, AtArm;

        /// <summary>Selector de autobrake, -1 si no se leyó.</summary>
        public int AutobrakeRaw;
        /// <summary>`COMM_FastenBeltsSelector` / `SIGNS_SeatBeltsSelector`: 0 OFF, 1 AUTO, 2 ON.</summary>
        public int SeatBeltsRaw;
        /// <summary>0 OFF, 1 AUTO, 2 ON.</summary>
        public int NoSmokingRaw;
    }

    /// <summary>
    /// El **bloque SDK de PMDG**, que es donde están de verdad el piloto automático, el autobrake y los
    /// carteles de cabina de un PMDG.
    ///
    /// **Lo que descubrió el vuelo de prueba `4Vz9nwaKKgQpYrEl` (738, v0.9.51).** Las doce banderas
    /// estándar de MSFS leídas en vuelo salieron **todas a 0** —`AUTOPILOT MASTER`, `AUTOPILOT
    /// AVAILABLE`, los modos y hasta el flight director— con el avión en crucero manteniendo 23 990 ft y
    /// 240°: iba en AP. La explicación es que esos offsets son del **AP del simulador**, y el 738 de
    /// PMDG vuela con su propio AFDS, así que el AP del sim nunca se engancha. Lo mismo con los
    /// carteles (`0x341C`/`0x341D`) y el selector de autobrake (`0x2F80`), que el piloto **sí movió** en
    /// ese vuelo: PMDG no los escribe.
    ///
    /// **Donde sí están** —`Offset Mapping for PMDG 737.pdf` y `…777.pdf`, «included with kind permission
    /// of PMDG»—:
    ///
    /// | 737 | 777 | Qué es |
    /// |---|---|---|
    /// | `0x65E5`/`0x65E7` `MCP_annunCMD_A/B` | `0x6584`/`0x6585` `MCP_annunAP[2]` | AP enganchado |
    /// | `0x65E6`/`0x65E8` `MCP_annunCWS_A/B` | — | CWS (737) |
    /// | `0x65D8`/`0x65D9` `MCP_annunFD[2]` | `0x6565`/`0x6566` `MCP_FD_Sw_On[2]` | flight director |
    /// | `0x65E0`/`0x65DD`/`0x65DE`/`0x65DF`/`0x65E3`/`0x65E4` | `0x6587`…`0x658C` | LNAV, VNAV, LVL CHG/FLCH, HDG, ALT HOLD, VS |
    /// | `0x65E1`/`0x65E2` | `0x658D`/`0x658E` | VOR LOC / APP |
    /// | `0x6607` `MAIN_AutobrakeSelector` | `0x64FE` `BRAKES_AutobrakeSelector` | **misma tabla: 0 RTO, 1 OFF, 2..5** |
    /// | `0x64FB` `COMM_FastenBeltsSelector` | `0x6483` `SIGNS_SeatBeltsSelector` | 0 OFF, 1 AUTO, 2 ON |
    /// | `0x64FA` `COMM_NoSmokingSelector` | `0x6482` `SIGNS_NoSmokingSelector` | 0 OFF, 1 AUTO, 2 ON |
    /// | `0x65E9` `MCP_indication_powered` | — | **prueba de vida** del bloque |
    ///
    /// **El requisito**, que está en la primera página de esos PDF: sin estas líneas en
    /// `737_Options.ini` / `777_Options.ini` el bloque **no se puebla** y todo lee 0 —
    /// indistinguible de «el avión no publica nada».
    /// <code>
    /// [SDK]
    /// EnableDataBroadcast=1
    /// </code>
    /// Eso no lo puede escribir el cliente (es config del avión), así que lo que hace es
    /// **detectarlo y decírselo al piloto una vez** en el registro.
    /// </summary>
    internal static class PmdgSdk
    {
        internal const int NoData = -1;

        /// <summary>Nombre del fichero de opciones donde se enciende el bloque.</summary>
        internal static string OptionsFileName(PmdgFamily family)
        {
            switch (family)
            {
                case PmdgFamily.B737: return "737_Options.ini";
                case PmdgFamily.B777: return "777_Options.ini";
                default:              return "";
            }
        }

        /// <summary>La línea exacta que hay que añadir al final de ese fichero.</summary>
        internal const string EnableLine = "[SDK] EnableDataBroadcast=1";

        /// <summary>
        /// ¿De qué familia es el PMDG cargado? Se mira el título primero (`PMDG 737-800`,
        /// `PMDG 777-200LR`) y el designador ICAO después (`B738`/`B77W`). Cualquier otra cosa —A320,
        /// C172— no es PMDG y el bloque no se toca.
        /// </summary>
        internal static PmdgFamily FamilyFrom(string title, string icao)
        {
            string t = (title ?? "").Trim().ToUpperInvariant();
            string i = (icao ?? "").Trim().ToUpperInvariant();

            // El 777 antes que el 737: «777» no contiene «737», pero conviene dejar el orden explícito.
            if (t.Contains("777") || i.StartsWith("B77") || i.StartsWith("777")) return PmdgFamily.B777;
            if (t.Contains("737") || i.StartsWith("B73") || i.StartsWith("737")) return PmdgFamily.B737;

            return PmdgFamily.None;
        }

        /// <summary>
        /// Una huella de todo lo que publica el bloque, para saber si **cambia** alguna vez. Sirve de
        /// prueba de vida en el 777, que no tiene bandera de alimentación: si algo se mueve, el bloque
        /// está vivo (`SignalAvailability`).
        /// </summary>
        internal static int Fingerprint(PmdgSdkRaw r)
        {
            int h = 17;
            h = h * 31 + (r.CmdA ? 1 : 0);
            h = h * 31 + (r.CmdB ? 2 : 0);
            h = h * 31 + (r.CwsA ? 4 : 0);
            h = h * 31 + (r.CwsB ? 8 : 0);
            h = h * 31 + (r.Ap1 ? 16 : 0);
            h = h * 31 + (r.Ap2 ? 32 : 0);
            h = h * 31 + (r.Fd1 ? 64 : 0);
            h = h * 31 + (r.Fd2 ? 128 : 0);
            h = h * 31 + (r.Lnav ? 1 : 0);
            h = h * 31 + (r.Vnav ? 2 : 0);
            h = h * 31 + (r.Flch ? 4 : 0);
            h = h * 31 + (r.HdgSel ? 8 : 0);
            h = h * 31 + (r.AltHold ? 16 : 0);
            h = h * 31 + (r.Vs ? 32 : 0);
            h = h * 31 + (r.Loc ? 64 : 0);
            h = h * 31 + (r.App ? 128 : 0);
            h = h * 31 + (r.N1 ? 1 : 0);
            h = h * 31 + (r.Speed ? 2 : 0);
            h = h * 31 + (r.AtArm ? 4 : 0);
            h = h * 31 + r.AutobrakeRaw;
            h = h * 31 + r.SeatBeltsRaw;
            h = h * 31 + r.NoSmokingRaw;
            return h;
        }

        /// <summary>
        /// ¿Va con piloto automático? `CMD A/B` o `CWS A/B` en el 737, `AP` en el 777. **El flight
        /// director no cuenta**: va encendido también en la final a mano, igual que en la tabla estándar.
        /// </summary>
        internal static bool IsEngaged(PmdgSdkRaw r) =>
            r.CmdA || r.CmdB || r.CwsA || r.CwsB || r.Ap1 || r.Ap2;

        /// <summary>Modo lateral, con los nombres del MCP.</summary>
        internal static string NavMode(PmdgSdkRaw r)
        {
            if (r.App) return "APP";
            if (r.Loc) return "LOC";
            if (r.Lnav) return "LNAV";
            if (r.HdgSel) return "HDG";
            return "----";
        }

        /// <summary>Modo vertical. En el 737 el `APP` engancha también la senda, así que sin otro modo
        /// vertical activo se publica `GS`.</summary>
        internal static string VertMode(PmdgSdkRaw r)
        {
            if (r.Vnav) return "VNAV";
            if (r.Flch) return "FLCH";
            if (r.Vs) return "VS";
            if (r.AltHold) return "ALT";
            if (r.App) return "GS";
            return "----";
        }

        /// <summary>Etiqueta del autobrake: **la misma tabla del documento** que `0x2F80`.</summary>
        internal static string AutobrakeLabel(PmdgSdkRaw r) =>
            r.AutobrakeRaw == NoData ? AutobrakeSelector.NoData : AutobrakeSelector.PanelLabel(r.AutobrakeRaw);

        /// <summary>`SeatBeltsSelector` → ¿el cartel está encendido? `AUTO` (1) **no es un estado**: lo
        /// enciende el avión según la fase, así que no se cuenta como encendido ni como apagado.</summary>
        internal static bool SeatbeltsOn(PmdgSdkRaw r) => r.SeatBeltsRaw == 2;

        /// <summary>`NoSmokingSelector` → mismo criterio.</summary>
        internal static bool NoSmokingOn(PmdgSdkRaw r) => r.NoSmokingRaw == 2;

        /// <summary>El selector de carteles como texto, para el diagnóstico (`OFF`/`AUTO`/`ON`).</summary>
        internal static string SignLabel(int raw)
        {
            switch (raw)
            {
                case 0:  return "OFF";
                case 1:  return "AUTO";
                case 2:  return "ON";
                default: return $"RAW={raw}";
            }
        }

        /// <summary>Una línea con todo lo que publica el bloque, para el vuelo de afinamiento.</summary>
        internal static string Describe(PmdgSdkRaw r) =>
            $"PMDG: powered={B(r.IndicationPowered)} cmd={B(r.CmdA)}{B(r.CmdB)} cws={B(r.CwsA)}{B(r.CwsB)} " +
            $"ap={B(r.Ap1)}{B(r.Ap2)} fd={B(r.Fd1)}{B(r.Fd2)} " +
            $"lnav={B(r.Lnav)} vnav={B(r.Vnav)} flch={B(r.Flch)} hdg={B(r.HdgSel)} alt={B(r.AltHold)} " +
            $"vs={B(r.Vs)} loc={B(r.Loc)} app={B(r.App)} at={B(r.AtArm)} " +
            $"-> {(IsEngaged(r) ? NavMode(r) + "+" + VertMode(r) : "sin AP")}";

        private static int B(bool value) => value ? 1 : 0;
    }
}
