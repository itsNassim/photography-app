using System.Collections.Generic;

namespace PhotoStudioApp.Models
{
    /// <summary>
    /// Simple key/value settings backed by the Settings table.
    /// Kept as a typed wrapper so the rest of the app doesn't sprinkle magic strings around.
    /// </summary>
    public class AppSettings
    {
        public string StudioRootPath { get; set; }
        public string ArchiveRootPath { get; set; }
        public string TodayFolderName { get; set; } = "today";
        /// <summary>Format string (DateTime.ToString) used both for the "today" folder display name
        /// and for the archived folder name, e.g. "dd-MMMM" or "yyyy-MM-dd".</summary>
        public string DateFolderFormat { get; set; } = "dd-MMMM";
        public string PasswordHash { get; set; }
        public string PasswordSalt { get; set; }
        /// <summary>Comma-separated System.DayOfWeek values that are skipped when computing "tomorrow".</summary>
        public string ClosedWeekdaysCsv { get; set; } = "Friday";
        /// <summary>Current logical work date ("today"), yyyy-MM-dd. Advances only on Close Day.</summary>
        public string CurrentWorkDate { get; set; }
        public int MaxPasswordAttempts { get; set; } = 5;

        public static readonly List<string> AllWeekdayNames = new List<string>
        {
            "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday"
        };
    }
}
