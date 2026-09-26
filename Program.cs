using System;
using System.IO;
using System.Threading;
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

        // Fixed, unique name (a GUID, not the app name) for the single-instance Mutex - using
        // a GUID instead of a plain string like "PhotoStudioApp" avoids any real-world chance of
        // colliding with some unrelated app/service that happens to use the same simple name.
        private const string SingleInstanceMutexName = "Local\\PhotoStudioApp-9F2B6E1A-7C4D-4B8A-9E3F-1D5C8A6B2F70";

        [STAThread]
        private static void Main()
        {
            // Single-instance enforcement: a named Mutex is the standard, lightweight way to do
            // this in WinForms without extra dependencies. "createdNew" is true only for the
            // very first process to claim this Mutex name on the machine (per-session, since
            // it's prefixed "Local\\") - any second launch sees createdNew=false and exits
            // immediately, before touching the database or creating any window at all.
            bool createdNew;
            using (var singleInstanceMutex = new Mutex(true, SingleInstanceMutexName, out createdNew))
            {
                if (!createdNew)
                {
                    MessageBox.Show(
                        "Studio Photo App est déjà en cours d'exécution.\n\n" +
                        "Regardez la barre des tâches - la fenêtre déjà ouverte doit être utilisée à la place.",
                        "Déjà ouvert", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                RunApplication();

                // Keeping the Mutex referenced (via this using block) for the whole lifetime of
                // Application.Run ensures it isn't released/finalized early - GC.KeepAlive isn't
                // strictly needed here since the local variable's scope already covers the call,
                // but is added for extra safety against any future refactor that might otherwise
                // let the JIT consider the Mutex reference "dead" before Application.Run returns.
                GC.KeepAlive(singleInstanceMutex);
            }
        }

        private static void RunApplication()
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
