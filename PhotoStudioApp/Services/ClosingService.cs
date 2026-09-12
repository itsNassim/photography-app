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
        private readonly ExpenseRepository _expenseRepo = new ExpenseRepository();
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
                throw new CloseDayException("Le dossier du studio n'est pas configure. Configurez-le dans Paramètres.");
            if (string.IsNullOrWhiteSpace(settings.ArchiveRootPath))
                throw new CloseDayException("Le dossier d'archive n'est pas configure. Configurez-le dans Paramètres.");
            if (!_folders.IsPathReachable(settings.ArchiveRootPath))
                throw new CloseDayException($"Le dossier d'archive est inaccessible :\n{settings.ArchiveRootPath}\n\nVerifiez le chemin (disque/reseau) puis reessayez.");

            // Only folder-backed categories are counted/moved from disk. No-folder (fixed-price
            // "service") categories never appear here - their revenue is captured entirely
            // through the custom charges created against them during the day.
            var categories = _categoryRepo.GetActiveFolderCategories();

            string[] purgedPrefixes = (settings.PurgedFilePrefixesCsv ?? "")
                .Split(',')
                .Select(p => p.Trim())
                .Where(p => p.Length > 0)
                .ToArray();

            DateTime closeDateValue = DateTime.ParseExact(settings.CurrentWorkDate, "yyyy-MM-dd", null);
            string workingFolderName = closeDateValue.ToString(settings.DateFolderFormat, AppCulture.French);
            string workingFolderAbs = Path.Combine(settings.StudioRootPath, workingFolderName);
            _folders.EnsureFolderExists(workingFolderAbs);

            var close = new DailyClose
            {
                CloseDate = settings.CurrentWorkDate,
                ClosedAt = DateTime.Now
            };

            int totalPhotosFound = 0;

            // 1) For each category folder: purge rejected files FIRST (before any copy/count),
            //    then count what's left, then remember it for the move step below.
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
                        UnitPrice = cat.CurrentPrice,
                        PurgedCount = 0
                    });
                    continue;
                }

                // Rejects/duplicates (e.g. "re...", "GSW...") are calculated (counted) first,
                // then deleted - the tally is kept below for the day's audit trail so nothing
                // is ever silently discarded without being accounted for.
                int purged = _folders.DeleteFilesWithPrefixes(catAbs, purgedPrefixes);

                int count = _folders.CountPhotos(catAbs);
                totalPhotosFound += count;
                close.LineItems.Add(new DailyCloseLineItem
                {
                    CategoryId = cat.Id,
                    CategoryName = cat.Name,
                    Count = count,
                    UnitPrice = cat.CurrentPrice,
                    PurgedCount = purged
                });
            }

            var charges = _chargeRepo.GetForWorkDate(settings.CurrentWorkDate);
            foreach (var c in charges)
                close.CustomCharges.Add(new DailyCloseCustomChargeSnapshot { Description = c.Description, Amount = c.Amount });

            var expenses = _expenseRepo.GetForWorkDate(settings.CurrentWorkDate);
            foreach (var ex in expenses)
                close.Expenses.Add(new DailyCloseExpenseSnapshot { Description = ex.Description, Amount = ex.Amount });

            close.GrandTotal = close.LineItems.Sum(li => li.Subtotal) + close.CustomCharges.Sum(c => c.Amount);
            close.TotalExpenses = close.Expenses.Sum(ex => ex.Amount);
            close.NetTotal = close.GrandTotal - close.TotalExpenses;

            if (totalPhotosFound == 0 && close.CustomCharges.Count == 0 && close.Expenses.Count == 0 && !confirmedEmptyDay)
            {
                throw new EmptyDayException(
                    "Aucune photo trouvee dans les dossiers de categories, aucune charge additionnelle, " +
                    "et aucune dépense aujourd'hui. Clôturer quand meme la journée ?");
            }

            // 2) NOW move each category's (already-purged) photos into the working folder.
            foreach (var cat in categories)
            {
                string catAbs = Path.Combine(settings.StudioRootPath, cat.FolderRelativePath);
                if (Directory.Exists(catAbs))
                    _folders.MovePhotos(catAbs, workingFolderAbs);
            }

            // 3) Move the working folder into archiveRoot\YYYY\<mois en francais>\<dossier du jour>.
            //    Directory.CreateDirectory only creates whatever's missing and does nothing for
            //    levels that already exist, so "check year, then check month" happens for free.
            string yearFolderName = closeDateValue.Year.ToString();
            string monthFolderName = closeDateValue.ToString("MMMM", AppCulture.French);
            string archiveYearMonthDir = Path.Combine(settings.ArchiveRootPath, yearFolderName, monthFolderName);
            Directory.CreateDirectory(archiveYearMonthDir);

            string archiveDest = Path.Combine(archiveYearMonthDir, workingFolderName);

            try
            {
                _folders.MoveDirectory(workingFolderAbs, archiveDest);
            }
            catch (IOException ex)
            {
                throw new CloseDayException(ex.Message, ex);
            }
            close.ArchiveFolderPath = archiveDest;

            // 4) Re-create the (now empty) category folders ready for the next working day.
            //    The next working folder itself is created on demand the next time CloseDay()
            //    runs (named from whatever CurrentWorkDate is then) - nothing to pre-create here.
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
                    _expenseRepo.DeleteAllForWorkDate(settings.CurrentWorkDate, conn, tx);
                    tx.Commit();
                }
                catch
                {
                    tx.Rollback();
                    throw;
                }
            }

            // 6) Advance the logical work date (skipping configured closed weekdays).
            var closedWeekdays = WorkdayCalculator.ParseClosedWeekdays(settings.ClosedWeekdaysCsv);
            DateTime nextWorkDate = WorkdayCalculator.NextWorkDate(closeDateValue, closedWeekdays);
            settings.CurrentWorkDate = nextWorkDate.ToString("yyyy-MM-dd");
            _settingsRepo.SaveAll(settings);

            // 7) Pre-create tomorrow's working folder in the studio root right now, using the
            //    exact same date-format setting as today's - so it's ready and waiting in the
            //    studio folder immediately after closing, instead of only appearing the next
            //    time CloseDay() runs.
            string nextWorkingFolderName = nextWorkDate.ToString(settings.DateFolderFormat, AppCulture.French);
            string nextWorkingFolderAbs = Path.Combine(settings.StudioRootPath, nextWorkingFolderName);
            _folders.EnsureFolderExists(nextWorkingFolderAbs);

            return close;
        }
    }
}
