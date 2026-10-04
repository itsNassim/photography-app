using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PhotoStudioApp.Services
{
    /// <summary>Everything that touches the studio's photo folders on disk.</summary>
    public class FolderCountService
    {
        private static readonly string[] PhotoExtensions =
        {
            ".jpg", ".jpeg", ".png", ".bmp", ".tif", ".tiff", ".gif", ".raw", ".cr2", ".nef", ".webp"
        };

        /// <summary>Counts image files directly inside a category folder. This is the BILLED
        /// count - it deliberately counts every photo, including ones that will later be purged
        /// by prefix (e.g. "re...", "GSW..."): those are still real photos taken and billed for,
        /// the prefix purge afterwards is disk cleanup only and must never reduce what's billed.
        /// See ClosingService.CloseDay, which always counts a category BEFORE purging it, for
        /// exactly that reason. Returns 0 (not an error) if the folder does not exist yet - staff
        /// may not have created it/dropped anything yet.</summary>
        public int CountPhotos(string absoluteFolderPath)
        {
            if (!Directory.Exists(absoluteFolderPath))
                return 0;

            return Directory.EnumerateFiles(absoluteFolderPath)
                .Count(f => PhotoExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()));
        }

        /// <summary>Parses the comma-separated PurgedFilePrefixesCsv setting into a clean list of
        /// non-empty, trimmed prefixes, for DeleteFilesWithPrefixes.</summary>
        public static string[] ParsePrefixCsv(string csv)
        {
            return (csv ?? "")
                .Split(',')
                .Select(p => p.Trim())
                .Where(p => p.Length > 0)
                .ToArray();
        }

        public void EnsureFolderExists(string absoluteFolderPath)
        {
            Directory.CreateDirectory(absoluteFolderPath);
        }

        /// <summary>Deletes any file directly inside the folder whose name starts with one of
        /// the given prefixes (case-insensitive) - used to clean up rejects/duplicates (e.g.
        /// "re...", "GSW...") from a category folder AFTER that folder's photos have already been
        /// counted/billed for the day (see ClosingService.CloseDay) - this is disk cleanup only
        /// and must never run before the count, or those files would be billed for nothing.
        /// Every matching file is first CALCULATED (tallied into a fixed list) and only THEN
        /// deleted - never deleted opportunistically while still being enumerated - so the
        /// returned count is always an accurate tally of what was purged, never a guess made
        /// after the fact. Returns the number of files deleted (0 if the folder doesn't exist or
        /// nothing matches). Each deletion is recorded in the studio action log (categoryName is
        /// only used for that log entry - pass null if the caller doesn't have one).</summary>
        public int DeleteFilesWithPrefixes(string absoluteFolderPath, string categoryName, params string[] prefixes)
        {
            // 1) Calculate first: find every matching file up front, as a stable snapshot,
            //    before deleting anything.
            var matchingFiles = FindFilesWithPrefixes(absoluteFolderPath, prefixes);

            // 2) Only now delete what was calculated above - sent to the Recycle Bin rather
            //    than deleted permanently, so a wrongly-configured prefix doesn't cause
            //    unrecoverable data loss.
            foreach (var file in matchingFiles)
            {
                RecycleFile(file);
                LogStudioAction(Path.GetFileName(file), "Suppression (purge)", categoryName,
                    "Déjà compté/facturé - nettoyage du dossier après comptage - envoyé a la corbeille");
            }

            return matchingFiles.Count;
        }

        /// <summary>Same matching rules as DeleteFilesWithPrefixes, but read-only - counts how
        /// many files WOULD be purged without touching anything. Used by
        /// ClosingService.PreviewCurrentState so the "Voir l'état actuel" preview can show an
        /// accurate PurgedCount without actually deleting/recycling anything.</summary>
        public int CountFilesWithPrefixes(string absoluteFolderPath, params string[] prefixes)
        {
            return FindFilesWithPrefixes(absoluteFolderPath, prefixes).Count;
        }

        /// <summary>Shared matching logic for DeleteFilesWithPrefixes/CountFilesWithPrefixes:
        /// every file directly inside the folder whose name starts with one of prefixes
        /// (case-insensitive). Empty list (not an error) if the folder doesn't exist or no
        /// prefixes were given.</summary>
        private static List<string> FindFilesWithPrefixes(string absoluteFolderPath, string[] prefixes)
        {
            if (!Directory.Exists(absoluteFolderPath) || prefixes == null || prefixes.Length == 0)
                return new List<string>();

            return Directory.EnumerateFiles(absoluteFolderPath)
                .Where(file =>
                {
                    string name = Path.GetFileName(file);
                    return prefixes.Any(p => !string.IsNullOrEmpty(p) &&
                        name.StartsWith(p, StringComparison.OrdinalIgnoreCase));
                })
                .ToList();
        }

        /// <summary>Moves every photo file from source into destination (creating destination if
        /// needed). Non-photo files are left behind. Returns the number of files moved. Each
        /// move is recorded in the studio action log (categoryName is only used for that log
        /// entry - pass null if the caller doesn't have one).</summary>
        public int MovePhotos(string sourceFolder, string destFolder, string categoryName = null)
        {
            if (!Directory.Exists(sourceFolder))
                return 0;

            Directory.CreateDirectory(destFolder);

            var files = Directory.EnumerateFiles(sourceFolder)
                .Where(f => PhotoExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                .ToList();

            int moved = 0;
            foreach (var file in files)
            {
                string fileName = Path.GetFileName(file);
                string destPath = Path.Combine(destFolder, fileName);
                destPath = ResolveNameCollision(destPath);
                File.Move(file, destPath);
                moved++;
                LogStudioAction(fileName, "Déplacement (archivage)", categoryName,
                    $"Vers le dossier du jour ({Path.GetFileName(destFolder)})");
            }
            return moved;
        }

        /// <summary>If destPath already exists, appends " (2)", " (3)", ... before the extension
        /// so a same-named file from a different category never overwrites another silently.</summary>
        private string ResolveNameCollision(string destPath)
        {
            if (!File.Exists(destPath)) return destPath;

            string dir = Path.GetDirectoryName(destPath);
            string name = Path.GetFileNameWithoutExtension(destPath);
            string ext = Path.GetExtension(destPath);
            int counter = 2;
            string candidate;
            do
            {
                candidate = Path.Combine(dir, $"{name} ({counter}){ext}");
                counter++;
            } while (File.Exists(candidate));
            return candidate;
        }

        /// <summary>Archives a whole directory (e.g. the day's "today" folder) into an archive
        /// location: the contents are COPIED to destDir first, and only once that copy has fully
        /// succeeded is the original sent to the Recycle Bin (never deleted permanently) - so a
        /// failure or interruption partway through never leaves the day's photos existing only in
        /// one, partially-written place. Throws IOException if the destination already exists, so
        /// the caller can show a clear "already exists" error instead of silently merging/
        /// overwriting. Records one summary entry in the studio action log for the whole folder
        /// (not per-file - the individual photos inside were already logged by MovePhotos on
        /// their way into this folder).</summary>
        public void ArchiveDirectory(string sourceDir, string destDir)
        {
            if (Directory.Exists(destDir))
                throw new IOException($"Un dossier existe déjà a cet emplacement :\n{destDir}");

            string parent = Path.GetDirectoryName(destDir);
            if (!string.IsNullOrEmpty(parent))
                Directory.CreateDirectory(parent);

            CopyDirectoryRecursive(sourceDir, destDir);
            RecycleDirectory(sourceDir);

            LogStudioAction(Path.GetFileName(sourceDir), "Archivage du dossier", null,
                $"Copié vers {destDir}, puis original envoyé a la corbeille");
        }

        /// <summary>Recursively copies every file and sub-folder from sourceDir into destDir
        /// (which is created if needed). .NET Framework has no built-in recursive directory copy,
        /// so this is the manual equivalent of "xcopy /E".</summary>
        private static void CopyDirectoryRecursive(string sourceDir, string destDir)
        {
            Directory.CreateDirectory(destDir);

            foreach (var file in Directory.EnumerateFiles(sourceDir))
                File.Copy(file, Path.Combine(destDir, Path.GetFileName(file)), overwrite: false);

            foreach (var subDir in Directory.EnumerateDirectories(sourceDir))
                CopyDirectoryRecursive(subDir, Path.Combine(destDir, Path.GetFileName(subDir)));
        }

        /// <summary>Sends a single file to the Recycle Bin instead of deleting it permanently -
        /// the standard .NET Framework way to do this (Microsoft.VisualBasic.FileIO.FileSystem),
        /// despite the assembly name it's ordinary framework code, not VB-specific.</summary>
        private static void RecycleFile(string path)
        {
            Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(
                path,
                Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
        }

        /// <summary>Sends a whole directory (and everything inside it) to the Recycle Bin instead
        /// of deleting it permanently. Only ever called AFTER its contents have already been
        /// copied somewhere safe (see ArchiveDirectory) - this is cleanup, not the only copy.</summary>
        private static void RecycleDirectory(string path)
        {
            Microsoft.VisualBasic.FileIO.FileSystem.DeleteDirectory(
                path,
                Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
        }

        /// <summary>Records one studio action log entry. Never throws - a logging failure must
        /// never interrupt a real file operation (purge/move/archive) already in progress.</summary>
        private static void LogStudioAction(string fileName, string action, string categoryName, string details)
        {
            try
            {
                StudioActionLogService.Record(fileName, action, categoryName, details);
            }
            catch
            {
                // Best-effort only - see summary above.
            }
        }

        public bool IsPathReachable(string path)
        {
            try
            {
                string root = Path.GetPathRoot(path);
                return !string.IsNullOrEmpty(root) && (Directory.Exists(root) || Directory.Exists(path) || CanCreate(path));
            }
            catch
            {
                return false;
            }
        }

        private bool CanCreate(string path)
        {
            try
            {
                Directory.CreateDirectory(path);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
