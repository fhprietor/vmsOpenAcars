namespace vmsOpenAcars.Helpers
{
    /// <summary>
    /// Las banderas crudas del piloto automático, tal como las publica FSUIPC7. Se pasan juntas para
    /// que la interpretación sea una función pura y probable sin simulador.
    ///
    /// Offsets, todos con `Responded` en MSFS según la tabla oficial (`FSUIPC7 Offsets Status.pdf`):
    /// `0x07BC` maestro, `0x0764` disponible, `0x07C4` NAV1 lock, `0x07C8` heading lock, `0x07D0`
    /// altitude lock, `0x07DC` airspeed, `0x07E4` mach, `0x07EC` vertical, `0x07FC` glideslope,
    /// `0x0800` approach, `0x0804` backcourse y `0x2EE0` flight director.
    /// </summary>
    internal struct AutopilotFlags
    {
        public bool Available;       // 0x0764
        public bool Master;          // 0x07BC
        public bool NavLock;         // 0x07C4
        public bool HeadingLock;     // 0x07C8
        public bool AltitudeLock;    // 0x07D0
        public bool AirspeedHold;    // 0x07DC
        public bool MachHold;        // 0x07E4
        public bool VerticalHold;    // 0x07EC
        public bool GlideslopeHold;  // 0x07FC
        public bool ApproachHold;    // 0x0800
        public bool BackCourse;      // 0x0804
        public bool FlightDirector;  // 0x2EE0
    }

    /// <summary>
    /// Decide si el avión va con piloto automático y **en qué modo**, con las banderas de verdad.
    ///
    /// **Lo que estaba mal (v0.9.49 y anteriores).** El respaldo para los addons de MSFS que no
    /// escriben `0x07BC` usaba **`0x07CC`**, que la tabla documenta como
    /// **`AUTOPILOT HEADING LOCK DIR`**: *«Autopilot heading value, as degrees*65536/360»*. Es un
    /// **rumbo**, no una bandera: vale 43 690 con 240°, y solo es 0 si el rumbo es exactamente 0,000°.
    /// Así que `0x07BC != 0 || 0x07CC != 0` era **verdadero prácticamente siempre** y el log anunciaba
    /// `AP ENGAGED` con el avión volado a mano. Peor: los modos se deducían comparando ese rumbo con
    /// `1`, `2` y `3` (`apLoc = bits == 1 || bits == 3`), comparaciones que solo aciertan en rumbos de
    /// 0,005°, 0,011° y 0,016° — de ahí que la línea dijera siempre `HDG ALT`.
    ///
    /// **La regla que queda:** el AP está puesto si el **maestro** o **cualquier bandera de modo** lo
    /// dice. Es la unión que sobrevive a los addons que no escriben el maestro (`PMDG`, `FBW`) y no se
    /// dispara con un rumbo. El **flight director** no cuenta como AP: va encendido en casi todo el
    /// vuelo, también en la final a mano; sirve solo de corroboración en el registro.
    /// </summary>
    internal static class AutopilotPanel
    {
        /// <summary>¿Alguna bandera de modo activa? (lateral o vertical; el FD no cuenta).</summary>
        internal static bool HasAnyMode(AutopilotFlags f) =>
            f.NavLock || f.HeadingLock || f.AltitudeLock || f.AirspeedHold || f.MachHold ||
            f.VerticalHold || f.GlideslopeHold || f.ApproachHold || f.BackCourse;

        /// <summary>¿Va con piloto automático?</summary>
        internal static bool IsEngaged(AutopilotFlags f) => f.Master || HasAnyMode(f);

        /// <summary>Modo lateral, con los nombres que usa la cabina.</summary>
        internal static string NavMode(AutopilotFlags f)
        {
            if (f.GlideslopeHold) return "ILS";
            if (f.ApproachHold) return "APP";
            if (f.NavLock) return "NAV";
            if (f.HeadingLock) return "HDG";
            if (f.BackCourse) return "BC";
            return "----";
        }

        /// <summary>Modo vertical.</summary>
        internal static string VertMode(AutopilotFlags f)
        {
            if (f.GlideslopeHold || f.ApproachHold) return "GS";
            if (f.VerticalHold) return "VS";
            if (f.AltitudeLock) return "ALT";
            return "----";
        }

        /// <summary>
        /// Una línea con **todo** lo que publica el avión, para el vuelo de diagnóstico. Es la que
        /// decide qué addon escribe qué: sin ella, cualquier cambio de umbral es adivinar.
        /// </summary>
        internal static string Describe(AutopilotFlags f) =>
            $"AP: avail={B(f.Available)} master={B(f.Master)} nav={B(f.NavLock)} hdg={B(f.HeadingLock)} " +
            $"alt={B(f.AltitudeLock)} vs={B(f.VerticalHold)} ias={B(f.AirspeedHold)} mach={B(f.MachHold)} " +
            $"appr={B(f.ApproachHold)} gs={B(f.GlideslopeHold)} bc={B(f.BackCourse)} fd={B(f.FlightDirector)} " +
            $"-> {(IsEngaged(f) ? NavMode(f) + "+" + VertMode(f) : "sin AP")}";

        private static int B(bool value) => value ? 1 : 0;
    }
}
