using System.Globalization;
using System.Text;

namespace PhotoStudioApp.Services
{
    /// <summary>Case- and accent-insensitive "contains" matching for the search boxes - staff
    /// typing "categorie" or "cadre" should find "Catégorie" / "Cadre" without having to type the
    /// accents (or the capital letters) exactly.</summary>
    public static class TextSearch
    {
        /// <summary>Lowercases and strips accents/diacritics ("Catégorie" becomes "categorie").</summary>
        public static string Normalize(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            string decomposed = text.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(decomposed.Length);
            foreach (char ch in decomposed)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                    sb.Append(ch);
            }
            return sb.ToString().ToLowerInvariant();
        }

        /// <summary>True if any of the given fields contains the (already trimmed) search text.
        /// An empty search matches everything.</summary>
        public static bool MatchesAny(string search, params string[] fields)
        {
            string needle = Normalize(search?.Trim());
            if (needle.Length == 0) return true;
            foreach (var field in fields)
            {
                if (Normalize(field).Contains(needle))
                    return true;
            }
            return false;
        }
    }
}
