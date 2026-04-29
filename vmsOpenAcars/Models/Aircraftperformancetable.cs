using System.Collections.Generic;

namespace vmsOpenAcars.Models
{
    /// <summary>
    /// Performance envelope limits for a given aircraft type.
    /// All speed values are in knots (IAS/CAS), except MmoKts which is the
    /// knot-equivalent of Mmo at typical cruise altitudes (used only as a ceiling).
    /// </summary>
    public class AircraftPerformance
    {
        /// <summary>
        /// Maximum Operating Speed (Vmo) in knots indicated airspeed.
        /// Used by CheckViolations() for overspeed detection at lower altitudes.
        /// </summary>
        public int VmoKts { get; set; }

        /// <summary>
        /// Human-readable category label for logging.
        /// Example: "Narrow-body jet", "Regional turboprop"
        /// </summary>
        public string Category { get; set; }
    }

    /// <summary>
    /// Static lookup table mapping ICAO type designators to aircraft performance data.
    ///
    /// <para>
    /// Resolution order in <see cref="Get"/>:
    ///   1. Exact ICAO type match (case-insensitive)        e.g. "B738" → Boeing 737-800
    ///   2. Prefix family match (first 3 chars)             e.g. "A32P" → A320 family
    ///   3. Category fallback via known prefix groups        e.g. "DHC" → turboprop fallback
    ///   4. Generic default (320 kts) if nothing matches
    /// </para>
    ///
    /// <para>
    /// To add a new type: insert an entry in <see cref="_table"/> with the ICAO
    /// designator as key and an <see cref="AircraftPerformance"/> as value.
    /// Families share a single entry via a 3-char prefix key (see prefix aliases below).
    /// </para>
    /// </summary>
    public static class AircraftPerformanceTable
    {
        // ─── Full ICAO type table ─────────────────────────────────────────────────
        // Sources: FCOM / AFM published Vmo values.
        // Keys are uppercase ICAO type designators.

        private static readonly Dictionary<string, AircraftPerformance> _table =
            new Dictionary<string, AircraftPerformance>(System.StringComparer.OrdinalIgnoreCase)
        {
            // ── Piston / Light GA ────────────────────────────────────────────────
            { "C172",  new AircraftPerformance { VmoKts = 163,  Category = "Light piston (Cessna 172)" } },
            { "C182",  new AircraftPerformance { VmoKts = 175,  Category = "Light piston (Cessna 182)" } },
            { "C208",  new AircraftPerformance { VmoKts = 175,  Category = "Turboprop single (Caravan)" } },
            { "PA28",  new AircraftPerformance { VmoKts = 148,  Category = "Light piston (Piper PA-28)" } },
            { "PA44",  new AircraftPerformance { VmoKts = 169,  Category = "Light piston (Piper Seminole)" } },
            { "BE58",  new AircraftPerformance { VmoKts = 195,  Category = "Light twin piston (Baron 58)" } },
            { "BE20",  new AircraftPerformance { VmoKts = 260,  Category = "Turboprop (King Air 200)" } },
            { "BE30",  new AircraftPerformance { VmoKts = 260,  Category = "Turboprop (King Air 300/350)" } },
            { "PC12",  new AircraftPerformance { VmoKts = 210,  Category = "Turboprop single (Pilatus PC-12)" } },

            // ── Regional turboprops ──────────────────────────────────────────────
            { "AT42",  new AircraftPerformance { VmoKts = 250,  Category = "Regional turboprop (ATR 42)" } },
            { "AT43",  new AircraftPerformance { VmoKts = 250,  Category = "Regional turboprop (ATR 42-300)" } },
            { "AT44",  new AircraftPerformance { VmoKts = 250,  Category = "Regional turboprop (ATR 42-400)" } },
            { "AT45",  new AircraftPerformance { VmoKts = 250,  Category = "Regional turboprop (ATR 42-500)" } },
            { "AT46",  new AircraftPerformance { VmoKts = 250,  Category = "Regional turboprop (ATR 42-600)" } },
            { "AT72",  new AircraftPerformance { VmoKts = 250,  Category = "Regional turboprop (ATR 72)" } },
            { "AT73",  new AircraftPerformance { VmoKts = 250,  Category = "Regional turboprop (ATR 72-200)" } },
            { "AT75",  new AircraftPerformance { VmoKts = 250,  Category = "Regional turboprop (ATR 72-500)" } },
            { "AT76",  new AircraftPerformance { VmoKts = 250,  Category = "Regional turboprop (ATR 72-600)" } },
            { "DH8A",  new AircraftPerformance { VmoKts = 220,  Category = "Regional turboprop (Dash 8-100)" } },
            { "DH8B",  new AircraftPerformance { VmoKts = 220,  Category = "Regional turboprop (Dash 8-200)" } },
            { "DH8C",  new AircraftPerformance { VmoKts = 260,  Category = "Regional turboprop (Dash 8-300)" } },
            { "DH8D",  new AircraftPerformance { VmoKts = 285,  Category = "Regional turboprop (Dash 8-400/Q400)" } },
            { "SB20",  new AircraftPerformance { VmoKts = 290,  Category = "Regional turboprop (Saab 2000)" } },
            { "SF34",  new AircraftPerformance { VmoKts = 250,  Category = "Regional turboprop (Saab 340)" } },
            { "E120",  new AircraftPerformance { VmoKts = 255,  Category = "Regional turboprop (Embraer 120)" } },

            // ── Regional jets ────────────────────────────────────────────────────
            { "CRJ2",  new AircraftPerformance { VmoKts = 320,  Category = "Regional jet (CRJ-200)" } },
            { "CRJ7",  new AircraftPerformance { VmoKts = 320,  Category = "Regional jet (CRJ-700)" } },
            { "CRJ9",  new AircraftPerformance { VmoKts = 320,  Category = "Regional jet (CRJ-900)" } },
            { "CRJX",  new AircraftPerformance { VmoKts = 320,  Category = "Regional jet (CRJ-1000)" } },
            { "E135",  new AircraftPerformance { VmoKts = 320,  Category = "Regional jet (ERJ-135)" } },
            { "E145",  new AircraftPerformance { VmoKts = 320,  Category = "Regional jet (ERJ-145)" } },
            { "E170",  new AircraftPerformance { VmoKts = 320,  Category = "Regional jet (E170)" } },
            { "E175",  new AircraftPerformance { VmoKts = 320,  Category = "Regional jet (E175)" } },
            { "E190",  new AircraftPerformance { VmoKts = 320,  Category = "Regional jet (E190)" } },
            { "E195",  new AircraftPerformance { VmoKts = 320,  Category = "Regional jet (E195)" } },

            // ── Narrow-body jets ─────────────────────────────────────────────────
            { "A318",  new AircraftPerformance { VmoKts = 350,  Category = "Narrow-body jet (A318)" } },
            { "A319",  new AircraftPerformance { VmoKts = 350,  Category = "Narrow-body jet (A319)" } },
            { "A320",  new AircraftPerformance { VmoKts = 350,  Category = "Narrow-body jet (A320)" } },
            { "A321",  new AircraftPerformance { VmoKts = 350,  Category = "Narrow-body jet (A321)" } },
            { "A20N",  new AircraftPerformance { VmoKts = 350,  Category = "Narrow-body jet (A320neo)" } },
            { "A21N",  new AircraftPerformance { VmoKts = 350,  Category = "Narrow-body jet (A321neo)" } },
            { "B731",  new AircraftPerformance { VmoKts = 340,  Category = "Narrow-body jet (B737-100)" } },
            { "B732",  new AircraftPerformance { VmoKts = 340,  Category = "Narrow-body jet (B737-200)" } },
            { "B733",  new AircraftPerformance { VmoKts = 340,  Category = "Narrow-body jet (B737-300)" } },
            { "B734",  new AircraftPerformance { VmoKts = 340,  Category = "Narrow-body jet (B737-400)" } },
            { "B735",  new AircraftPerformance { VmoKts = 340,  Category = "Narrow-body jet (B737-500)" } },
            { "B736",  new AircraftPerformance { VmoKts = 340,  Category = "Narrow-body jet (B737-600)" } },
            { "B737",  new AircraftPerformance { VmoKts = 340,  Category = "Narrow-body jet (B737-700)" } },
            { "B738",  new AircraftPerformance { VmoKts = 340,  Category = "Narrow-body jet (B737-800)" } },
            { "B739",  new AircraftPerformance { VmoKts = 340,  Category = "Narrow-body jet (B737-900)" } },
            { "B37M",  new AircraftPerformance { VmoKts = 340,  Category = "Narrow-body jet (B737 MAX 7)" } },
            { "B38M",  new AircraftPerformance { VmoKts = 340,  Category = "Narrow-body jet (B737 MAX 8)" } },
            { "B39M",  new AircraftPerformance { VmoKts = 340,  Category = "Narrow-body jet (B737 MAX 9)" } },
            { "B3XM",  new AircraftPerformance { VmoKts = 340,  Category = "Narrow-body jet (B737 MAX 10)" } },
            { "MD82",  new AircraftPerformance { VmoKts = 340,  Category = "Narrow-body jet (MD-82)" } },
            { "MD83",  new AircraftPerformance { VmoKts = 340,  Category = "Narrow-body jet (MD-83)" } },

            // ── Wide-body jets ───────────────────────────────────────────────────
            { "A332",  new AircraftPerformance { VmoKts = 330,  Category = "Wide-body jet (A330-200)" } },
            { "A333",  new AircraftPerformance { VmoKts = 330,  Category = "Wide-body jet (A330-300)" } },
            { "A339",  new AircraftPerformance { VmoKts = 330,  Category = "Wide-body jet (A330-900neo)" } },
            { "A342",  new AircraftPerformance { VmoKts = 330,  Category = "Wide-body jet (A340-200)" } },
            { "A343",  new AircraftPerformance { VmoKts = 330,  Category = "Wide-body jet (A340-300)" } },
            { "A345",  new AircraftPerformance { VmoKts = 330,  Category = "Wide-body jet (A340-500)" } },
            { "A346",  new AircraftPerformance { VmoKts = 330,  Category = "Wide-body jet (A340-600)" } },
            { "A359",  new AircraftPerformance { VmoKts = 330,  Category = "Wide-body jet (A350-900)" } },
            { "A35K",  new AircraftPerformance { VmoKts = 330,  Category = "Wide-body jet (A350-1000)" } },
            { "A388",  new AircraftPerformance { VmoKts = 330,  Category = "Wide-body jet (A380-800)" } },
            { "B752",  new AircraftPerformance { VmoKts = 350,  Category = "Narrow-to-wide (B757-200)" } },
            { "B753",  new AircraftPerformance { VmoKts = 350,  Category = "Narrow-to-wide (B757-300)" } },
            { "B762",  new AircraftPerformance { VmoKts = 350,  Category = "Wide-body jet (B767-200)" } },
            { "B763",  new AircraftPerformance { VmoKts = 350,  Category = "Wide-body jet (B767-300)" } },
            { "B764",  new AircraftPerformance { VmoKts = 350,  Category = "Wide-body jet (B767-400)" } },
            { "B772",  new AircraftPerformance { VmoKts = 330,  Category = "Wide-body jet (B777-200)" } },
            { "B77L",  new AircraftPerformance { VmoKts = 330,  Category = "Wide-body jet (B777-200LR)" } },
            { "B773",  new AircraftPerformance { VmoKts = 330,  Category = "Wide-body jet (B777-300)" } },
            { "B77W",  new AircraftPerformance { VmoKts = 330,  Category = "Wide-body jet (B777-300ER)" } },
            { "B779",  new AircraftPerformance { VmoKts = 330,  Category = "Wide-body jet (B777X)" } },
            { "B787",  new AircraftPerformance { VmoKts = 330,  Category = "Wide-body jet (B787-8)" } },
            { "B788",  new AircraftPerformance { VmoKts = 330,  Category = "Wide-body jet (B787-8)" } },
            { "B789",  new AircraftPerformance { VmoKts = 330,  Category = "Wide-body jet (B787-9)" } },
            { "B78X",  new AircraftPerformance { VmoKts = 330,  Category = "Wide-body jet (B787-10)" } },
            { "B744",  new AircraftPerformance { VmoKts = 365,  Category = "Wide-body jet (B747-400)" } },
            { "B748",  new AircraftPerformance { VmoKts = 365,  Category = "Wide-body jet (B747-8)" } },

            // ── Freighters (same type designators, listed separately for clarity) ─
            { "B74F",  new AircraftPerformance { VmoKts = 365,  Category = "Freighter (B747-400F)" } },
            { "B74S",  new AircraftPerformance { VmoKts = 365,  Category = "Freighter (B747SP)" } },
        };

        // ─── 3-char prefix fallbacks ──────────────────────────────────────────────
        // Used when the full ICAO code is not in _table (e.g. a variant or custom livery code).

        private static readonly Dictionary<string, AircraftPerformance> _prefixFallback =
            new Dictionary<string, AircraftPerformance>(System.StringComparer.OrdinalIgnoreCase)
        {
            { "A32",  new AircraftPerformance { VmoKts = 350, Category = "Narrow-body jet (A320 family)" } },
            { "A33",  new AircraftPerformance { VmoKts = 330, Category = "Wide-body jet (A330 family)" } },
            { "A34",  new AircraftPerformance { VmoKts = 330, Category = "Wide-body jet (A340 family)" } },
            { "A35",  new AircraftPerformance { VmoKts = 330, Category = "Wide-body jet (A350 family)" } },
            { "A38",  new AircraftPerformance { VmoKts = 330, Category = "Wide-body jet (A380 family)" } },
            { "B73",  new AircraftPerformance { VmoKts = 340, Category = "Narrow-body jet (B737 family)" } },
            { "B3",   new AircraftPerformance { VmoKts = 340, Category = "Narrow-body jet (B737 MAX)" } },
            { "B74",  new AircraftPerformance { VmoKts = 365, Category = "Wide-body jet (B747 family)" } },
            { "B75",  new AircraftPerformance { VmoKts = 350, Category = "Narrow-to-wide (B757 family)" } },
            { "B76",  new AircraftPerformance { VmoKts = 350, Category = "Wide-body jet (B767 family)" } },
            { "B77",  new AircraftPerformance { VmoKts = 330, Category = "Wide-body jet (B777 family)" } },
            { "B78",  new AircraftPerformance { VmoKts = 330, Category = "Wide-body jet (B787 family)" } },
            { "AT4",  new AircraftPerformance { VmoKts = 250, Category = "Regional turboprop (ATR 42)" } },
            { "AT7",  new AircraftPerformance { VmoKts = 250, Category = "Regional turboprop (ATR 72)" } },
            { "DH8",  new AircraftPerformance { VmoKts = 260, Category = "Regional turboprop (Dash 8)" } },
            { "DHC",  new AircraftPerformance { VmoKts = 215, Category = "Regional turboprop (DHC)" } },
            { "CRJ",  new AircraftPerformance { VmoKts = 320, Category = "Regional jet (CRJ family)" } },
            { "E17",  new AircraftPerformance { VmoKts = 320, Category = "Regional jet (E-jet 170)" } },
            { "E19",  new AircraftPerformance { VmoKts = 320, Category = "Regional jet (E-jet 190)" } },
            { "E13",  new AircraftPerformance { VmoKts = 320, Category = "Regional jet (ERJ-135)" } },
            { "E14",  new AircraftPerformance { VmoKts = 320, Category = "Regional jet (ERJ-145)" } },
            { "BE2",  new AircraftPerformance { VmoKts = 260, Category = "Turboprop (King Air)" } },
            { "BE3",  new AircraftPerformance { VmoKts = 260, Category = "Turboprop (King Air 350)" } },
            { "C20",  new AircraftPerformance { VmoKts = 175, Category = "Turboprop single (Caravan)" } },
        };

        // ─── Generic defaults ─────────────────────────────────────────────────────
        private static readonly AircraftPerformance _defaultJet =
            new AircraftPerformance { VmoKts = 320, Category = "Generic (unknown type)" };

        // ─── Public API ───────────────────────────────────────────────────────────

        /// <summary>
        /// Returns the performance data for the given ICAO type designator.
        /// Resolution order: exact → 4-char prefix → 3-char prefix → generic default.
        /// </summary>
        /// <param name="icaoType">
        /// ICAO aircraft type designator from SimBrief/phpVMS.
        /// Example: "B738", "AT76", "BE58", "A20N"
        /// </param>
        public static AircraftPerformance Get(string icaoType)
        {
            if (string.IsNullOrWhiteSpace(icaoType))
                return _defaultJet;

            string key = icaoType.Trim().ToUpperInvariant();

            // 1. Exact match
            if (_table.TryGetValue(key, out var exact))
                return exact;

            // 2. Try progressively shorter prefixes (4, 3 chars)
            for (int len = System.Math.Min(key.Length - 1, 4); len >= 3; len--)
            {
                string prefix = key.Substring(0, len);
                if (_prefixFallback.TryGetValue(prefix, out var prefixMatch))
                    return prefixMatch;
            }

            // 3. Generic default
            return _defaultJet;
        }

        /// <summary>
        /// Returns the Vapp speed window [VappMin, VappMax] in knots for the 1000 ft approach gate.
        /// Derived from ICAO approach categories (PANS-OPS / TERPS):
        ///   Cat A (&lt; 91 kts Vref): Light piston/GA           → [65,  100]
        ///   Cat B (91–120 kts):     Regional turboprops        → [90,  135]
        ///   Cat C (121–140 kts):    Regional/narrow-body jets  → [120, 165]
        ///   Cat D (141–165 kts):    Wide-body jets (default)   → [140, 185]
        /// </summary>
        public static (int VappMin, int VappMax) GetApproachSpeedRange(string icaoType)
        {
            var perf = Get(icaoType);
            string cat = perf.Category?.ToLowerInvariant() ?? "";

            if (cat.Contains("light piston") || cat.Contains("light twin"))
                return (65, 100);   // ICAO Cat A
            if (cat.Contains("turboprop"))
                return (90, 135);   // ICAO Cat B
            if (cat.Contains("regional jet") || cat.Contains("narrow-body") || cat.Contains("narrow-to-wide"))
                return (120, 165);  // ICAO Cat C
            return (140, 185);      // ICAO Cat D (wide-body / generic)
        }
    }
}