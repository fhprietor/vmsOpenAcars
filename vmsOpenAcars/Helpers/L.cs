using vmsOpenAcars.Services;

namespace vmsOpenAcars.Helpers
{
    /// <summary>
    /// Provides a shorthand static helper for accessing localized strings.
    /// This class simplifies the syntax for retrieving translated text throughout the application.
    /// </summary>
    /// <remarks>
    /// Usage examples:
    /// <code>
    /// // Simple string
    /// string welcome = _("WelcomeMessage");
    /// 
    /// // String with format parameters
    /// string greeting = _("HelloUser", userName);
    /// 
    /// // In UI elements
    /// lblTitle.Text = _("MainTitle");
    /// </code>
    /// </remarks>
    public static class L
    {
        /// <summary>
        /// Retrieves a localized string for the specified key, optionally formatting it with provided arguments.
        /// This is a shorthand wrapper around <see cref="LocalizationService.Instance.GetString(string, object[])"/>.
        /// </summary>
        /// <param name="key">The localization key to look up in the current language resource file.</param>
        /// <param name="args">Optional arguments for string formatting (replaces {0}, {1}, etc. in the localized string).</param>
        /// <returns>
        /// The localized string corresponding to the key, with formatting applied if arguments are provided.
        /// If the key is not found, returns a fallback value (usually the key itself in debug format [[key]]).
        /// </returns>
        /// <example>
        /// <code>
        /// // Simple usage
        /// string message = _("FlightStarted");
        /// 
        /// // With formatting
        /// string alert = _("FlightDelayed", delayMinutes);
        /// </code>
        /// </example>
        public static string _(string key, params object[] args)
        {
            return LocalizationService.Instance.GetString(key, args);
        }
    }
}