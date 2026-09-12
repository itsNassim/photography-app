using System.Globalization;

namespace PhotoStudioApp.Services
{
    /// <summary>The whole app displays and names folders in French, regardless of the
    /// machine's own Windows display language/locale - so date formatting always goes
    /// through this fixed French culture instead of CultureInfo.CurrentCulture.</summary>
    public static class AppCulture
    {
        public static readonly CultureInfo French = CultureInfo.GetCultureInfo("fr-FR");
    }
}
