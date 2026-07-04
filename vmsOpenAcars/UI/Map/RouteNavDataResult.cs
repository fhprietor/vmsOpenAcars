using System.Collections.Generic;
using vmsOpenAcars.Models.NavData;

namespace vmsOpenAcars.UI.Forms
{
    internal class RouteNavDataResult
    {
        public List<NavRunway>    OriginRunways;
        public List<NavRunway>    DestRunways;
        public List<NavProcedure> Sids;
        public List<NavProcedure> Stars;
        public List<NavApproach>  Approaches;
        public List<NavIls>       Ils;
        public NavAirportInfo     OriginInfo;
        public NavAirportInfo     DestInfo;
    }
}
