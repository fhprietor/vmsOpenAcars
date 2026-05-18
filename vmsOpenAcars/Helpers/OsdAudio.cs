using System;
using System.Media;
using System.Reflection;
using vmsOpenAcars.UI.Forms;

namespace vmsOpenAcars.Helpers
{
    public static class OsdAudio
    {
        private static readonly SoundPlayer _info     = Load("chime_info.wav");
        private static readonly SoundPlayer _success  = Load("chime_success.wav");
        private static readonly SoundPlayer _warning  = Load("chime_warning.wav");
        private static readonly SoundPlayer _critical = Load("chime_critical.wav");

        public static void Play(OsdSeverity severity, bool forcePlay = false)
        {
            if (!forcePlay && !AppConfig.OsdSoundEnabled) return;
            SoundPlayer player;
            switch (severity)
            {
                case OsdSeverity.Success:  player = _success;  break;
                case OsdSeverity.Warning:  player = _warning;  break;
                case OsdSeverity.Critical: player = _critical; break;
                default:                   player = _info;     break;
            }
            try { player?.Play(); } catch { }
        }

        private static SoundPlayer Load(string filename)
        {
            try
            {
                var stream = Assembly.GetExecutingAssembly()
                    .GetManifestResourceStream("vmsOpenAcars.Resources.Audio." + filename);
                if (stream == null) return null;
                var p = new SoundPlayer(stream);
                p.Load();
                return p;
            }
            catch { return null; }
        }
    }
}
