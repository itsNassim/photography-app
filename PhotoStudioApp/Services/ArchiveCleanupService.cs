using System;
using System.Collections.Generic;
using System.Linq;
using PhotoStudioApp.Data;
using PhotoStudioApp.Models;

namespace PhotoStudioApp.Services
{
    /// <summary>Everything to do with removing archived days from the History list/counts - a
    /// single day, everything older than a configured retention period, or the whole history at
    /// once. PERMANENT POLICY: archive folders on disk (the actual photos) are NEVER deleted by
    /// anything in this class, under any circumstance - every method here only ever removes the
    /// database/history record. Do not reintroduce folder deletion here.</summary>
    public class ArchiveCleanupService
    {
        private readonly DailyCloseRepository _closeRepo = new DailyCloseRepository();
        private readonly SettingsRepository _settingsRepo = new SettingsRepository();

        /// <summary>Closed days whose ClosedAt is older than the given number of days.
        /// Returns an empty list if retentionDays &lt;= 0 (feature considered "off").</summary>
        public List<DailyClose> FindClosesOlderThan(int retentionDays)
        {
            if (retentionDays <= 0) return new List<DailyClose>();

            DateTime cutoff = DateTime.Now.AddDays(-retentionDays);
            return _closeRepo.GetHistory().Where(c => c.ClosedAt < cutoff).ToList();
        }

        /// <summary>Deletes one closed day's database record (removing it - and its revenue -
        /// from the History count/list). The archive folder on disk is NEVER touched by this or
        /// any other method in this class - only the count/history entry is ever removed. This
        /// is an explicit, permanent policy: archive folders (the actual photos) must never be
        /// deleted by any bulk or single "cleanup"/"delete" action in the app, no matter which
        /// button triggered it.</summary>
        public void DeleteSingle(DailyClose close)
        {
            _closeRepo.DeleteClose(close.Id);
        }

        /// <summary>Runs DeleteSingle (history entry only - see the note on DeleteSingle) for
        /// every closed day older than the configured retention setting. Returns how many were
        /// deleted.</summary>
        public int CleanupOlderThanConfiguredRetention()
        {
            var settings = _settingsRepo.LoadAll();
            var toDelete = FindClosesOlderThan(settings.ArchiveRetentionDays);
            foreach (var close in toDelete)
                DeleteSingle(close);
            return toDelete.Count;
        }

        /// <summary>Closed days whose CloseDate falls outside the current calendar month
        /// (year+month different from today's). Used by "garder uniquement le mois en cours".</summary>
        public List<DailyClose> FindClosesOutsideCurrentMonth()
        {
            string currentYearMonth = DateTime.Now.ToString("yyyy-MM");
            return _closeRepo.GetHistory()
                .Where(c => string.IsNullOrEmpty(c.CloseDate) || c.CloseDate.Length < 7 || c.CloseDate.Substring(0, 7) != currentYearMonth)
                .ToList();
        }

        /// <summary>Removes the history entry (only - see the note on DeleteSingle) for every
        /// closed day whose CloseDate isn't in the current calendar month. Returns how many
        /// were removed.</summary>
        public int DeleteAllExceptCurrentMonth()
        {
            var toDelete = FindClosesOutsideCurrentMonth();
            foreach (var close in toDelete)
                DeleteSingle(close);
            return toDelete.Count;
        }

        /// <summary>Clears every history entry (ALL of them). Per the permanent no-folder-deletion
        /// policy, archive folders on disk are NEVER touched by this - "Tout supprimer" only
        /// wipes the History list/counts, exactly like every other delete action in this class.
        /// The photos remain on disk under the archive root, untouched.</summary>
        public void DeleteAll()
        {
            _closeRepo.DeleteAllHistory();
        }
    }
}
