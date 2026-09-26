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
        /// <summary>Format string (DateTime.ToString, French culture) used for the daily working
        /// folder's name and the day-level archive folder name, e.g. "dd MMMM" -> "07 septembre".
        /// The working folder is always named this way now - there is no more static "today" name.</summary>
        public string DateFolderFormat { get; set; } = "dd MMMM";
        public string PasswordHash { get; set; }
        public string PasswordSalt { get; set; }
        /// <summary>Comma-separated System.DayOfWeek values that are skipped when computing "tomorrow".</summary>
        public string ClosedWeekdaysCsv { get; set; } = "Friday";
        /// <summary>Current logical work date ("today"), yyyy-MM-dd. Advances only on Close Day.</summary>
        public string CurrentWorkDate { get; set; }
        public int MaxPasswordAttempts { get; set; } = 5;
        /// <summary>Minutes the admin-password prompt refuses to even open once
        /// MaxPasswordAttempts is reached, app-wide (shared across every AuthGate call site) -
        /// see PasswordLockoutUntilUtc for the actual active-lockout timestamp.</summary>
        public int PasswordLockoutMinutes { get; set; } = 5;
        /// <summary>UTC timestamp ("o" round-trip format) until which the admin password prompt
        /// is blocked, set by PasswordPromptDialog the moment MaxPasswordAttempts is reached and
        /// checked by AuthGate before it will even show the dialog. Empty/blank means no active
        /// lockout. Persisted (not just in-memory) so restarting the app can't be used to reset
        /// the countdown.</summary>
        public string PasswordLockoutUntilUtc { get; set; } = "";
        /// <summary>Comma-separated filename prefixes purged from each category folder before
        /// counting/moving anything at close time (case-insensitive), e.g. "re,GSW".</summary>
        public string PurgedFilePrefixesCsv { get; set; } = "re,GSW";
        /// <summary>Archived days older than this are eligible for the "Nettoyer les anciennes
        /// archives" action in History. 0 means the feature is off (nothing is ever auto-flagged).</summary>
        public int ArchiveRetentionDays { get; set; } = 0;

        /// <summary>Master on/off for the "Archiver la journee (sans revenu)" feature - a way
        /// for a worker to close/archive the day without ever seeing the revenue figures.
        /// Controlled from the History/Archive tab. When false, the whole feature is hidden in
        /// Parametres regardless of WorkerArchiveArmed below.</summary>
        public bool WorkerArchiveFeatureEnabled { get; set; } = false;

        /// <summary>One-shot arming switch for the worker-archive button in Parametres - an
        /// admin turns this on to make the button clickable for exactly one use; the button
        /// (and this switch) automatically turn back off the moment it's used, so it has to be
        /// re-armed every time.</summary>
        public bool WorkerArchiveArmed { get; set; } = false;

        /// <summary>Login-log entries (admin-password prompts) older than this are eligible for
        /// automatic cleanup, run whenever the log view loads. 0 means the feature is off
        /// (nothing is ever auto-deleted) - same convention as ArchiveRetentionDays.</summary>
        public int LoginLogRetentionDays { get; set; } = 90;

        /// <summary>Studio-action-log entries (file events in the category folders) older than
        /// this are eligible for automatic cleanup, run whenever the log view loads. 0 means the
        /// feature is off - same convention as ArchiveRetentionDays.</summary>
        public int StudioLogRetentionDays { get; set; } = 90;

        public static readonly List<string> AllWeekdayNames = new List<string>
        {
            "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday"
        };
    }
}
