using System;
using System.IO;
using System.Windows.Forms;
using PhotoStudioApp.Data;
using PhotoStudioApp.UI;

namespace PhotoStudioApp
{
    internal static class Program
    {
        /// <summary>
        /// Folder (next to the .exe, writable, works from Program Files on Win7 too because
        /// we use %ProgramData% instead) where the SQLite database file lives.
        /// </summary>
        public static string DataDirectory
        {
            get
            {
                string commonDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "PhotoStudioApp");
                if (TryEnsureWritable(commonDir))
                    return commonDir;

                // Fallback for machines where the current user can't write to %ProgramData%
                // (no installer ran to grant that ACL) - per-user AppData is always writable.
                string localDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "PhotoStudioApp");
                Directory.CreateDirectory(localDir);
                return localDir;
            }
        }

        private static bool TryEnsureWritable(string dir)
        {
            try
            {
                Directory.CreateDirectory(dir);
                string probe = Path.Combine(dir, ".writetest");
                File.WriteAllText(probe, "ok");
                File.Delete(probe);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static string DatabasePath => Path.Combine(DataDirectory, "studio.db");

        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            Application.ThreadException += (s, e) =>
                ShowFatal(e.Exception);
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
                ShowFatal(e.ExceptionObject as Exception);

            try
            {
                SchemaInitializer.EnsureDatabase(DatabasePath);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Impossible de créer ou d'ouvrir la base de données locale :\n" + ex.Message,
                    "Erreur au demarrage", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            Application.Run(new MainForm());
        }

        private static void ShowFatal(Exception ex)
        {
            if (ex == null) return;
            MessageBox.Show(
                "Une erreur inattendue est survenue :\n\n" + ex.Message,
                "Studio Photo App - Erreur",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
