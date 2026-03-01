using System;

namespace vmsOpenAcars.Services
{
    /// <summary>
    /// Advanced IFR mock simulator for route:
    /// SKRG → MQT → AMVES → SKBO (ILS RWY 14R).
    /// 
    /// Includes:
    /// - Runway heading departure (007°)
    /// - Waypoint-based navigation
    /// - Smooth heading transitions
    /// - Cruise at FL230
    /// - Automatic Top of Descent calculation
    /// - Final approach alignment (135°)
    /// - Transition altitude logic (18,000 ft)
    /// </summary>
    public class MockSimulator
    {
        #region Coordinates

        private const double SKRG_Lat = 6.1718;
        private const double SKRG_Lon = -75.4221;

        private const double MQT_Lat = 5.2100;
        private const double MQT_Lon = -74.8836;

        private const double AMVES_Lat = 4.9000;
        private const double AMVES_Lon = -74.3000;

        private const double SKBO_Lat = 4.7011;
        private const double SKBO_Lon = -74.1469;

        #endregion

        #region Vertical Profile

        private const int CruiseAltitudeFt = 23000;
        private const int TransitionAltitudeFt = 18000;
        private const int DestinationElevationFt = 8360;

        private bool isDescending = false;

        #endregion

        #region Aircraft State

        public double CurrentLat { get; private set; }
        public double CurrentLon { get; private set; }
        public int CurrentAlt { get; private set; }
        public int CurrentGS { get; private set; }
        public int CurrentHeading { get; private set; }
        public bool IsOnGround { get; private set; }

        private int navStage = 0;
        // 0 = runway heading
        // 1 = MQT
        // 2 = AMVES
        // 3 = SKBO
        // 4 = Final RWY 14R

        #endregion

        public MockSimulator()
        {
            CurrentLat = SKRG_Lat;
            CurrentLon = SKRG_Lon;
            CurrentAlt = 7025;
            CurrentGS = 0;
            CurrentHeading = 7; // RWY 007
            IsOnGround = true;
        }

        /// <summary>
        /// Updates aircraft position and systems.
        /// Call periodically (e.g., every second).
        /// </summary>
        public void UpdatePosition()
        {
            UpdateSpeed();
            UpdateVerticalProfile();
            UpdateNavigationStage();
            UpdateHeading();
            MoveForward();
        }

        #region Speed

        private void UpdateSpeed()
        {
            if (CurrentGS < 420)
                CurrentGS += 10;
        }

        #endregion

        #region Vertical Logic

        private void UpdateVerticalProfile()
        {
            double distanceToBOG = DistanceTo(SKBO_Lat, SKBO_Lon);

            double altitudeToLose = CurrentAlt - DestinationElevationFt;
            double todDistance = (altitudeToLose / 1000.0) * 3.0;

            if (!isDescending &&
                distanceToBOG <= todDistance &&
                CurrentAlt >= CruiseAltitudeFt - 500)
            {
                isDescending = true;
            }

            if (!isDescending)
            {
                if (CurrentAlt < CruiseAltitudeFt)
                    CurrentAlt += 2000;
            }
            else
            {
                if (CurrentAlt > DestinationElevationFt + 500)
                    CurrentAlt -= 1800;
            }

            IsOnGround = CurrentAlt <= DestinationElevationFt + 50;
        }

        #endregion

        #region Navigation

        private void UpdateNavigationStage()
        {
            switch (navStage)
            {
                case 0:
                    if (CurrentAlt > 12000)
                        navStage = 1;
                    break;

                case 1:
                    if (DistanceTo(MQT_Lat, MQT_Lon) < 5)
                        navStage = 2;
                    break;

                case 2:
                    if (DistanceTo(AMVES_Lat, AMVES_Lon) < 5)
                        navStage = 3;
                    break;

                case 3:
                    if (DistanceTo(SKBO_Lat, SKBO_Lon) < 25)
                        navStage = 4;
                    break;
            }
        }

        private void UpdateHeading()
        {
            int targetHeading;

            switch (navStage)
            {
                case 0:
                    targetHeading = 7;
                    break;

                case 1:
                    targetHeading = CalculateBearing(CurrentLat, CurrentLon, MQT_Lat, MQT_Lon);
                    break;

                case 2:
                    targetHeading = CalculateBearing(CurrentLat, CurrentLon, AMVES_Lat, AMVES_Lon);
                    break;

                case 3:
                    targetHeading = CalculateBearing(CurrentLat, CurrentLon, SKBO_Lat, SKBO_Lon);
                    break;

                case 4:
                    targetHeading = 135; // ILS RWY 14R
                    break;

                default:
                    targetHeading = CurrentHeading;
                    break;
            }

            CurrentHeading = SmoothTurn(CurrentHeading, targetHeading, 3);
        }

        #endregion

        #region Movement

        private void MoveForward()
        {
            double distanceStep = 0.02;

            double rad = CurrentHeading * Math.PI / 180.0;

            CurrentLat += distanceStep * Math.Cos(rad);
            CurrentLon += distanceStep * Math.Sin(rad);
        }

        #endregion

        #region Math Helpers

        private int CalculateBearing(double lat1, double lon1, double lat2, double lon2)
        {
            double ToRad(double a) => Math.PI * a / 180.0;
            double ToDeg(double a) => a * 180.0 / Math.PI;

            double φ1 = ToRad(lat1);
            double φ2 = ToRad(lat2);
            double Δλ = ToRad(lon2 - lon1);

            double y = Math.Sin(Δλ) * Math.Cos(φ2);
            double x = Math.Cos(φ1) * Math.Sin(φ2) -
                       Math.Sin(φ1) * Math.Cos(φ2) * Math.Cos(Δλ);

            double θ = Math.Atan2(y, x);
            return (int)((ToDeg(θ) + 360) % 360);
        }

        private int SmoothTurn(int current, int target, int rate)
        {
            int diff = (target - current + 540) % 360 - 180;

            if (Math.Abs(diff) < rate)
                return target;

            return (current + Math.Sign(diff) * rate + 360) % 360;
        }

        private double DistanceTo(double lat, double lon)
        {
            double R = 6371;

            double dLat = (lat - CurrentLat) * Math.PI / 180;
            double dLon = (lon - CurrentLon) * Math.PI / 180;

            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                       Math.Cos(CurrentLat * Math.PI / 180) *
                       Math.Cos(lat * Math.PI / 180) *
                       Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

            return R * c;
        }

        #endregion

        #region Telemetry

        public object GetTelemetry()
        {
            return new
            {
                lat = CurrentLat,
                lon = CurrentLon,
                alt = CurrentAlt,
                gs = CurrentGS,
                heading = CurrentHeading,
                vs = isDescending ? -1800 : 2000,
                stdPressure = CurrentAlt >= TransitionAltitudeFt,
                navStage
            };
        }

        #endregion
    }
}