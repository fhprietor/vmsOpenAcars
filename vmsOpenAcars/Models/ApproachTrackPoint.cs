namespace vmsOpenAcars.Models
{
    public class ApproachTrackPoint
    {
        public int    FlightId    { get; set; }
        public int    SeqNo       { get; set; }
        public double Lat         { get; set; }
        public double Lon         { get; set; }
        public double AltFt       { get; set; }
        public double AglFt       { get; set; }
        public double IasKt       { get; set; }
        public double VsFpm       { get; set; }
        public double HeadingDeg  { get; set; }
        public double DistNm      { get; set; }   // distance before threshold (positive = approaching)
        public double LateralFt   { get; set; }   // signed: positive = right of centreline

        /// <summary>
        /// Dirección del viento **en ese punto de la traza**, en grados (de donde viene), tal y como
        /// la publicaba el simulador. Null = no hubo dato (0/0 o FSUIPC desconectado), **nunca 0**
        /// para decir «no lo sé»: un 0/0 es indistinguible de la calma y fingiría un viento en calma
        /// en todo el descenso.
        /// </summary>
        public double? WindDirDeg   { get; set; }

        /// <summary>
        /// Intensidad del viento en ese punto, en nudos. Null = sin dato, por la misma razón que
        /// <see cref="WindDirDeg"/>.
        /// </summary>
        public double? WindSpeedKt  { get; set; }
    }
}
