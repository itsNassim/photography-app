using System;
using System.IO;
using System.Linq;
using PhotoStudioApp.Data;
using PhotoStudioApp.Models;

namespace PhotoStudioApp.Services
{
    /// <summary>Thrown for expected, user-facing close-day failures (bad paths, existing
    /// archive folder, missing category folder, etc.) so the UI can show a clean dialog
    /// instead of a raw exception.</summary>
    public class CloseDayException : Exception
    {
        public CloseDayException(string message) : base(message) { }
        public CloseDayException(string message, Exception inner) : base(message, inner) { }
    }

    /// <summary>Thrown when a close would archive an empty day (no photos, no charges) so the
    /// caller can ask the user to confirm instead of silently proceeding or hard-failing.</summary>
    public class EmptyDayException : Exception
    {
        public EmptyDayException(string message) : base(message) { }
    }

    public class ClosingService
    {
        private readonly FolderCountService _folders = new FolderCountService();
        private readonly CategoryRepository _categoryRepo = new CategoryRepository();
        private readonly CustomChargeRepository _chargeRepo = new CustomChargeRepository();
        private readonly DailyCloseRepository _closeRepo = new DailyCloseRepository();
        private readonly SettingsRepository _settingsRepo = new SettingsRepository();

        /// <summary>Runs the full close-day workflow. The password must already have been
        /// verified by the caller before this is invoked. Pass confirmedEmptyDay=true to proceed
        /// even when no photos and no custom charges were found (after the caller has asked the
        /// user to confirm following an EmptyDayException). Returns the persisted DailyClose
        /// record (with line items and custom charges) for the breakdown/print screen.</summary>
        public DailyClose CloseDay(bool confirmedEmptyDay = false)
        {
            var settings = _settingsRepo.LoadAll();

            if (string.IsNullOrWhiteSpace(settings.StudioRootPath))
                throw new CloseDayException("The studio folder path is not set. Configure it in Settings first.");
            if (string.IsNullOrWhiteSpace(settings.ArchiveRootPath))
                throw new CloseDayException("The archive folder path is not set. Configure it in Settings first.");
            if (!_folders.IsPathReachable(settings.ArchiveRootPath))
                throw new CloseDayException($"The archive destination is unreachable:\n{settings.ArchiveRootPath}\n\nCheck the drive/network path and try again.");

            var categories = _categoryRepo.GetActiveCategories();
            if (categories.Count == 0)
                throw new CloseDayException("There are no active categories configured.");

            string todayFolderAbs = Path.Combine(settings.StudioRootPath, settings.TodayFolderName);
            _folders.EnsureFolderExists(todayFolderAbs);

            var close = new DailyClose
            {
                CloseDate = settings.CurrentWorkDate,
                ClosedAt = DateTime.Now
            };

            int totalPhotosFound = 0;

            // 1) Count each category folder BEFORE moving anything, and record a line item
            //    per category (even zero-count categories, so the report is complete).
            foreach (var cat in categories)
            {
                string catAbs = Path.Combine(settings.StudioRootPath, cat.FolderRelativePath);
                if (!Directory.Exists(catAbs))
                {
                    // Not a hard failure - staff may not have used that category today.
                    close.LineItems.Add(new DailyCloseLineItem
                    {
                        CategoryId = cat.Id,
                        CategoryName = cat.Name,
                        Count = 0,
                        UnitPrice = cat.CurrentPrice
                    });
                    continue;
                }

                int count = _folders.CountPhotos(catAbs);
                totalPhotosFound += count;
                close.LineItems.Add(new DailyCloseLineItem
                {
                    CategoryId = cat.Id,
                    CategoryName = cat.Name,
                    Count = count,
                    UnitPrice = cat.CurrentPrice
                });
            }

            var charges = _chargeRepo.GetForWorkDate(settings.CurrentWorkDate);
            foreach (var c in charges)
                close.CustomCharges.Add(new DailyCloseCustomChargeSnapshot { Description = c.Description, Amount = c.Amount });

            close.GrandTotal = close.LineItems.Sum(li => li.Subtotal) + close.CustomCharges.Sum(c => c.Amount);

            if (totalPhotosFound == 0 && close.CustomCharges.Count == 0 && !confirmedEmptyDay)
            {
                throw new EmptyDayException(
                    "No photos were found in any category folder, and there are no custom charges today. " +
                    "Close the day anyway?");
            }

            // 2) Move each category's photos into the "today" folder first.
            foreach (var cat in categories)
            {
                string catAbs = Path.Combine(settings.StudioRootPath, cat.FolderRelativePath);
                if (Directory.Exists(catAbs))
                    _folders.MovePhotos(catAbs, todayFolderAbs);
            }

            // 3) Move "today" into the archive, named with the actual close date.
            DateTime closeDateValue = DateTime.ParseExact(settings.CurrentWorkDate, "yyyy-MM-dd", null);
            string archiveFolderName = closeDateValue.ToString(settings.DateFolderFormat);
            string archiveDest = Path.Combine(settings.ArchiveRootPath, archiveFolderName);

            try
            {
                _folders.MoveDirectory(todayFolderAbs, archiveDest);
            }
            catch (IOException ex)
            {
                throw new CloseDayException(ex.Message, ex);
            }
            close.ArchiveFolderPath = archiveDest;

            // 4) Create tomorrow's working folder (same naming convention, skip-weekday rule).
            var closedWeekdays = WorkdayCalculator.ParseClosedWeekdays(settings.ClosedWeekdaysCsv);
            DateTime nextWorkDate = WorkdayCalculator.NextWorkDate(closeDateValue, closedWeekdays);
            _folders.EnsureFolderExists(Path.Combine(settings.StudioRootPath, settings.TodayFolderName));
            foreach (var cat in categories)
                _folders.EnsureFolderExists(Path.Combine(settings.StudioRootPath, cat.FolderRelativePath));

            // 5) Persist the close record and clear today's custom charges, atomically.
            using (var conn = Db.OpenConnection())
            using (var tx = conn.BeginTransaction())
            {
                try
                {
                    _closeRepo.Save(close, conn, tx);
                    _chargeRepo.DeleteAllForWorkDate(settings.CurrentWorkDate, conn, tx);
                    tx.Commit();
                }
                catch
                {
                    tx.Rollback();
                    throw;
                }
            }

            // 6) Advance the logical work date.
            settings.CurrentWorkDate = nextWorkDate.ToString("yyyy-MM-dd");
            _settingsRepo.SaveAll(settings);

            return close;
        }
    }
}
