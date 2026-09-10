using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PhotoStudioApp.Data;
using PhotoStudioApp.Models;

namespace PhotoStudioApp.Services
{
    /// <summary>Everything to do with deleting archived days - a single day, everything older
    /// than a configured retention period, or the whole archive at once. Always deletes the DB
    /// record and the on-disk folder together, so History and the actual archive stay in sync.</summary>
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

        /// <summary>Deletes one closed day: its archive folder on disk (if it still exists) and
        /// its database record.</summary>
        public void DeleteSingle(DailyClose close)
        {
            TryDeleteFolder(close.ArchiveFolderPath);
            _closeRepo.DeleteClose(close.Id);
        }

        /// <summary>Runs DeleteSingle for every closed day older than the configured retention
        /// setting. Returns how many were deleted.</summary>
        public int CleanupOlderThanConfiguredRetention()
        {
            var settings = _settingsRepo.LoadAll();
            var toDelete = FindClosesOlderThan(settings.ArchiveRetentionDays);
            foreach (var close in toDelete)
                DeleteSingle(close);
            return toDelete.Count;
        }

        /// <summary>Deletes EVERYTHING: every archived day's folder under the archive root, and
        /// every history record. The archive root folder itself is left in place (emptied), so
        /// the configured path keeps working without needing to be re-selected.</summary>
        public void DeleteAll()
        {
            var settings = _settingsRepo.LoadAll();

            if (!string.IsNullOrWhiteSpace(settings.ArchiveRootPath) && Directory.Exists(settings.ArchiveRootPath))
            {
                foreach (var entry in Directory.EnumerateFileSystemEntries(settings.ArchiveRootPath))
                {
                    try
                    {
                        if (Directory.Exists(entry))
                            Directory.Delete(entry, recursive: true);
                        else
                            File.Delete(entry);
                    }
                    catch
                    {
                        // Best-effort: skip anything locked/in-use rather than aborting the
                        // whole cleanup over one stubborn file.
                    }
                }
            }

            _closeRepo.DeleteAllHistory();
        }

        private void TryDeleteFolder(string path)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
                    Directory.Delete(path, recursive: true);
            }
            catch
            {
                // Best-effort - if the folder is locked/in-use/already gone, still remove the
                // DB record so History doesn't get stuck pointing at something unusable.
            }
        }
    }
}
