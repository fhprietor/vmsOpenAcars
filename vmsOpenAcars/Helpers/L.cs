using vmsOpenAcars.Services;

namespace vmsOpenAcars.Helpers
{
    public static class L
    {
        public static string _(string key, params object[] args)
        {
            return LocalizationService.Instance.GetString(key, args);
        }
    }
}
