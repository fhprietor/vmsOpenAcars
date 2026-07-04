using System;
using System.Drawing;

namespace vmsOpenAcars.Services
{
    public interface IAppLogger
    {
        void Info(string msg);
        void Warning(string msg);
        void Error(string msg, Exception ex = null);
        void Success(string msg);
    }
}
