using System;
using System.Collections.Generic;
using PhotoStudioApp.Data;
using PhotoStudioApp.Models;

namespace PhotoStudioApp.Services
{
    /// <summary>Thin wrapper around LoginLogRepository - the single place every admin-password
    /// prompt in the app reports through (see PasswordPromptDialog). Every method swallows its
    /// own exceptions where it matters (Record) so a logging hiccup can never block or crash a
    /// password prompt - worst case, that one attempt goes unrecorded.</summary>
    public static class LoginLogService
    {
        private static readonly LoginLogRepository Repo = new LoginLogRepository();

        public static void Record(string actionName, string buttonLabel, int attemptsMade, bool success, string details = null)
        {
            try
            {
                Repo.Insert(new LoginLogEntry
                {
                    Timestamp = DateTime.Now,
                    ActionName = string.IsNullOrWhiteSpace(actionName) ? "Action protegee" : actionName,
                    ButtonLabel = buttonLabel ?? "",
                    AttemptsMade = attemptsMade,
                    Success = success,
                    Details = details ?? ""
                });
            }
            catch
            {
                // Logging must never take down a password prompt.
            }
        }

        public static List<LoginLogEntry> GetAll() => Repo.GetAll();

        /// <summary>Deletes entries older than the configured LoginLogRetentionDays. Safe to
        /// call every time the log view loads - a no-op when the setting is 0 (off) or there's
        /// nothing old enough to remove.</summary>
        public static int CleanupOlderThanConfigured()
        {
            var settings = new SettingsRepository().LoadAll();
            return Repo.DeleteOlderThan(settings.LoginLogRetentionDays);
        }

        public static void DeleteAll() => Repo.DeleteAll();
    }
}
