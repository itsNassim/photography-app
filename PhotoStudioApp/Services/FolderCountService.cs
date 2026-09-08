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

        /// <summary>Counts image files directly inside a category folder. Returns 0 (not an error)
        /// if the folder does not exist yet - staff may not have created it/dropped anything yet.</summary>
        public int CountPhotos(string absoluteFolderPath)
        {
            if (!Directory.Exists(absoluteFolderPath))
                return 0;

            return Directory.EnumerateFiles(absoluteFolderPath)
                .Count(f => PhotoExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()));
        }

        public void EnsureFolderExists(string absoluteFolderPath)
        {
            Directory.CreateDirectory(absoluteFolderPath);
        }

        /// <summary>Moves every photo file from source into destination (creating destination if
        /// needed). Non-photo files are left behind. Returns the number of files moved.</summary>
        public int MovePhotos(string sourceFolder, string destFolder)
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

        /// <summary>Moves a whole directory (e.g. the day's "today" folder) into an archive
        /// location. Throws IOException if the destination already exists, so the caller can
        /// show a clear "already exists" error instead of silently merging/overwriting.</summary>
        public void MoveDirectory(string sourceDir, string destDir)
        {
            if (Directory.Exists(destDir))
                throw new IOException($"A folder already exists at the destination:\n{destDir}");

            string parent = Path.GetDirectoryName(destDir);
            if (!string.IsNullOrEmpty(parent))
                Directory.CreateDirectory(parent);

            Directory.Move(sourceDir, destDir);
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
