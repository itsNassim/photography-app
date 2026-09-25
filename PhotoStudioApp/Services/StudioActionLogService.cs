using System;
using System.Collections.Generic;
using PhotoStudioApp.Data;
using PhotoStudioApp.Models;

namespace PhotoStudioApp.Services
{
    /// <summary>Thin wrapper around StudioActionLogRepository - the place FolderCountService
    /// (bulk/explicit operations) and HomeTab's FileSystemWatcher (live folder activity) both
    /// report through. Record() swallows its own exceptions so a logging hiccup can never break
    /// the actual file operation it's describing.</summary>
    public static class StudioActionLogService
    {
        private static readonly StudioActionLogRepository Repo = new StudioActionLogRepository();

        public static void Record(string fileName, string action, string categoryName = null, string details = null)
        {
            try
            {
                Repo.Insert(new StudioActionLogEntry
                {
                    Timestamp = DateTime.Now,
                    FileName = fileName ?? "",
                    Action = string.IsNullOrWhiteSpace(action) ? "Action" : action,
                    CategoryName = categoryName ?? "",
                    Details = details ?? ""
                });
            }
            catch
            {
                // Logging must never break a real file operation.
            }
        }

        public static List<StudioActionLogEntry> GetAll() => Repo.GetAll();

        /// <summary>Deletes entries older than the configured StudioLogRetentionDays. Safe to
        /// call every time the log view loads - a no-op when the setting is 0 (off) or there's
        /// nothing old enough to remove.</summary>
        public static int CleanupOlderThanConfigured()
        {
            var settings = new SettingsRepository().LoadAll();
            return Repo.DeleteOlderThan(settings.StudioLogRetentionDays);
        }

        public static void DeleteAll() => Repo.DeleteAll();
    }
}
