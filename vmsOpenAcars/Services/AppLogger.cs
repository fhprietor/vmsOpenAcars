using System;
using System.Drawing;
using vmsOpenAcars.UI;

namespace vmsOpenAcars.Services
{
    public class AppLogger : IAppLogger
    {
        public event Action<string, Color> OnMessage;

        public void Info(string msg)    => OnMessage?.Invoke(msg, Theme.MainText);
        public void Warning(string msg) => OnMessage?.Invoke(msg, Theme.Warning);
        public void Error(string msg, Exception ex = null)
        {
            string full = ex != null ? $"{msg}: {ex.Message}" : msg;
            OnMessage?.Invoke(full, Theme.Danger);
        }
        public void Success(string msg) => OnMessage?.Invoke(msg, Theme.Success);
    }
}
