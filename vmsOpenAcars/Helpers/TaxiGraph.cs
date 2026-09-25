using System;
using System.Collections.Generic;
using System.Linq;

namespace vmsOpenAcars.Helpers
{
    /// <summary>
    /// Grafo de calles de rodaje y ruta más razonable entre dos puntos.
    ///
    /// Los segmentos de NavData ya traen nombre y extremos, así que el aeropuerto es un grafo:
    /// los extremos que coinciden (o casi) son un nodo y cada segmento es una arista. Sobre eso,
    /// un Dijkstra da la secuencia de calles más corta para ir de donde está el avión al umbral
    /// de la pista elegida. Es lo que alimenta la ruta que el piloto ve en el popup y puede
    /// editar, y el «gira a la derecha en M en 120 m» de los avisos.
    ///
    /// Puro a propósito: no toca red ni WinForms, para poder probarlo con los segmentos reales
    /// de un aeropuerto (ver <c>RaasTests</c>, que usa 106 segmentos de SKBO).
    /// </summary>
    internal static class TaxiGraph
    {
        /// <summary>Un tramo de calle con nombre y extremos (espejo de `NavTaxiway`).</summary>
        internal sealed class Segment
        {
            public string Name { get; set; }
            public double Lat1   { get; set; }
            public double Lon1   { get; set; }
            public double Lat2   { get; set; }
            public double Lon2   { get; set; }
        }

        internal sealed class RouteSuggestion
        {
            public bool          Found;
            public double        DistanceM;
            public List<string>  Names = new List<string>();

            /// <summary>Ruta tal como se escribe y se edita: nombres separados por espacios.</summary>
            public string Text => string.Join(" ", Names);
        }

        // Los extremos que distan menos que esto se consideran el mismo nodo. 45 m es del orden
        // del ancho de una calle de rodaje: más ajustado y un aeropuerto real queda desconectado
        // (los datasets no comparten exactamente el punto de unión), más holgado y se fusionan
        // calles paralelas distintas.
        internal const double SnapM = 45.0;

        /// <summary>
        /// Secuencia de calles más corta de (<paramref name="fromLat"/>,<paramref name="fromLon"/>)
        /// al punto de destino. Devuelve <c>Found = false</c> si no hay segmentos, si no hay ruta
        /// o si el destino no está a una distancia razonable de la red.
        /// </summary>
        internal static RouteSuggestion Suggest(
            IEnumerable<Segment> segments,
            double fromLat, double fromLon, double toLat, double toLon)
        {
            var result = new RouteSuggestion();
            if (segments == null) return result;

            var segs = segments.Where(s => s != null
                                        && !string.IsNullOrWhiteSpace(s.Name)).ToList();
            if (segs.Count == 0) return result;

            // ── Nodos: fusión de extremos cercanos ────────────────────────────────
            var nodeLat = new List<double>();
            var nodeLon = new List<double>();
            int NodeOf(double lat, double lon)
            {
                for (int i = 0; i < nodeLat.Count; i++)
                {
                    if (GeoMath.DistanceNm(lat, lon, nodeLat[i], nodeLon[i]) * GeoMath.MetersPerNm
                        <= SnapM)
                        return i;
                }
                nodeLat.Add(lat);
                nodeLon.Add(lon);
                return nodeLat.Count - 1;
            }

            var edgeA    = new int[segs.Count];
            var edgeB    = new int[segs.Count];
            var edgeCost = new double[segs.Count];
            for (int i = 0; i < segs.Count; i++)
            {
                edgeA[i]    = NodeOf(segs[i].Lat1, segs[i].Lon1);
                edgeB[i]    = NodeOf(segs[i].Lat2, segs[i].Lon2);
                edgeCost[i] = GeoMath.DistanceNm(segs[i].Lat1, segs[i].Lon1,
                                                 segs[i].Lat2, segs[i].Lon2) * GeoMath.MetersPerNm;
            }

            // ── Adyacencia ────────────────────────────────────────────────────────
            var adjacency = new List<int>[nodeLat.Count];
            for (int i = 0; i < adjacency.Length; i++) adjacency[i] = new List<int>();
            for (int i = 0; i < segs.Count; i++)
            {
                if (edgeA[i] == edgeB[i]) continue;      // segmento degenerado
                adjacency[edgeA[i]].Add(i);
                adjacency[edgeB[i]].Add(i);
            }

            int NearestNode(double lat, double lon)
            {
                int best = -1; double bestM = double.MaxValue;
                for (int i = 0; i < nodeLat.Count; i++)
                {
                    double d = GeoMath.DistanceNm(lat, lon, nodeLat[i], nodeLon[i])
                               * GeoMath.MetersPerNm;
                    if (d < bestM) { bestM = d; best = i; }
                }
                return best;
            }

            int start = NearestNode(fromLat, fromLon);
            int goal  = NearestNode(toLat, toLon);
            if (start < 0 || goal < 0 || start == goal) return result;
            if (GeoMath.DistanceNm(fromLat, fromLon, nodeLat[start], nodeLon[start])
                * GeoMath.MetersPerNm > 300.0) return result;   // el avión no está en la red

            // ── Dijkstra ─────────────────────────────────────────────────────────
            var dist     = new double[nodeLat.Count];
            var previous = new int[nodeLat.Count];      // arista por la que se llegó
            var visited  = new bool[nodeLat.Count];
            for (int i = 0; i < dist.Length; i++) { dist[i] = double.MaxValue; previous[i] = -1; }
            dist[start] = 0.0;

            for (int step = 0; step < nodeLat.Count; step++)
            {
                int u = -1; double best = double.MaxValue;
                for (int i = 0; i < dist.Length; i++)
                    if (!visited[i] && dist[i] < best) { best = dist[i]; u = i; }
                if (u < 0) break;
                visited[u] = true;
                if (u == goal) break;

                foreach (int e in adjacency[u])
                {
                    int v = edgeA[e] == u ? edgeB[e] : edgeA[e];
                    if (visited[v]) continue;
                    double nd = dist[u] + edgeCost[e];
                    if (nd < dist[v]) { dist[v] = nd; previous[v] = e; }
                }
            }

            if (dist[goal] == double.MaxValue) return result;

            // ── Reconstrucción, colapsando tramos consecutivos de la misma calle ──
            var names = new List<string>();
            int cursor = goal;
            while (cursor != start && previous[cursor] >= 0)
            {
                int e = previous[cursor];
                string name = segs[e].Name.Trim();
                if (names.Count == 0
                    || !string.Equals(names[0], name, StringComparison.OrdinalIgnoreCase))
                    names.Insert(0, name);
                cursor = edgeA[e] == cursor ? edgeB[e] : edgeA[e];
            }

            result.Found     = names.Count > 0;
            result.Names     = names;
            result.DistanceM = dist[goal];
            return result;
        }

        /// <summary>
        /// Punto de unión entre dos calles: el par de extremos (uno de cada nombre) más cercano
        /// entre sí. Devuelve false si las dos calles no se tocan.
        /// </summary>
        internal static bool TryFindJunction(
            IEnumerable<Segment> segments, string nameA, string nameB,
            out double junctionLat, out double junctionLon)
        {
            junctionLat = 0.0; junctionLon = 0.0;
            if (segments == null || string.IsNullOrWhiteSpace(nameA)
                || string.IsNullOrWhiteSpace(nameB)) return false;
            if (string.Equals(nameA, nameB, StringComparison.OrdinalIgnoreCase)) return false;

            var list = segments.Where(s => s != null && !string.IsNullOrWhiteSpace(s.Name)).ToList();
            double bestM = double.MaxValue;
            bool found = false;

            foreach (var a in list)
            {
                if (!string.Equals(a.Name, nameA, StringComparison.OrdinalIgnoreCase)) continue;
                foreach (var b in list)
                {
                    if (!string.Equals(b.Name, nameB, StringComparison.OrdinalIgnoreCase)) continue;
                    foreach (var pa in new[] { (a.Lat1, a.Lon1), (a.Lat2, a.Lon2) })
                    foreach (var pb in new[] { (b.Lat1, b.Lon1), (b.Lat2, b.Lon2) })
                    {
                        double d = GeoMath.DistanceNm(pa.Item1, pa.Item2, pb.Item1, pb.Item2)
                                   * GeoMath.MetersPerNm;
                        if (d < bestM)
                        {
                            bestM      = d;
                            junctionLat = (pa.Item1 + pb.Item1) / 2.0;
                            junctionLon = (pa.Item2 + pb.Item2) / 2.0;
                            found       = true;
                        }
                    }
                }
            }

            // Si las calles no llegan a tocarse (hueco mayor que un nodo), el "cruce" no es real:
            // devolver un punto a 200 m de distancia solo produciría un aviso de giro falso.
            return found && bestM <= SnapM * 2.0;
        }

        /// <summary>Rumbo del tramo de esa calle más cercano al punto dado.</summary>
        internal static double SegmentBearing(
            IEnumerable<Segment> segments, string name, double lat, double lon)
        {
            if (segments == null || string.IsNullOrWhiteSpace(name)) return double.NaN;
            double bestM = double.MaxValue, bearing = double.NaN;
            foreach (var s in segments)
            {
                if (s == null || !string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase))
                    continue;
                double d = GeoMath.DistanceToSegmentNm(lat, lon, s.Lat1, s.Lon1, s.Lat2, s.Lon2)
                           * GeoMath.MetersPerNm;
                if (d < bestM)
                {
                    bestM   = d;
                    bearing = GeoMath.BearingDeg(s.Lat1, s.Lon1, s.Lat2, s.Lon2);
                }
            }
            return bearing;
        }

        /// <summary>
        /// Radio dentro del cual se considera que el avión está sobre una calle (m).
        /// </summary>
        internal const double TaxiwayRadiusM = 300.0;

        /// <summary>
        /// Calle más cercana, con el criterio que ya usaba `NavDataService.NearestTaxiway`: si se
        /// pasa un rumbo, un segmento que va claramente en contra (más de 50° del rumbo o de su
        /// recíproco) se penaliza ×2.5, para no elegir la calle paralela equivocada en una
        /// intersección. Vive aquí —y no en el servicio— para que la repetición de una traza real
        /// en los tests use **esta misma** regla y no una copia.
        /// </summary>
        internal static string NearestName(
            IEnumerable<Segment> segments, double lat, double lon, double heading = double.NaN)
        {
            if (segments == null) return null;
            string bestName  = null;
            double bestScore = double.MaxValue;
            bool   useHdg    = !double.IsNaN(heading);

            foreach (var s in segments)
            {
                if (s == null || string.IsNullOrWhiteSpace(s.Name)) continue;

                double d = GeoMath.DistanceToSegmentNm(lat, lon, s.Lat1, s.Lon1, s.Lat2, s.Lon2)
                           * GeoMath.MetersPerNm;
                if (d >= TaxiwayRadiusM) continue;

                double score = d;
                if (useHdg && d > 1.0)
                {
                    double brg   = GeoMath.BearingDeg(s.Lat1, s.Lon1, s.Lat2, s.Lon2);
                    double delta = Math.Min(GeoMath.BearingDiffDeg(heading, brg),
                                            GeoMath.BearingDiffDeg(heading, (brg + 180.0) % 360.0));
                    if (delta > 50.0) score *= 2.5;
                }

                if (score < bestScore) { bestScore = score; bestName = s.Name; }
            }
            return bestName;
        }
    }
}
