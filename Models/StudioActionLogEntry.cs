using System;

namespace PhotoStudioApp.Models
{
    /// <summary>One file-level event inside the studio's category folders - a photo entering a
    /// folder, being removed, renamed, purged, or archived at close time. Stored encrypted at
    /// rest - see Services.LogCryptoService / Data.StudioActionLogRepository.</summary>
    public class StudioActionLogEntry
    {
        public int Id { get; set; }
        public DateTime Timestamp { get; set; }
        public string FileName { get; set; }
        /// <summary>E.g. "Entree (nouveau fichier)", "Suppression", "Renommage",
        /// "Suppression (purge)", "Deplacement (archivage)", "Archivage du dossier".</summary>
        public string Action { get; set; }
        /// <summary>Category folder this file belongs to, when known (null for a whole-day
        /// folder archiving event, which spans every category).</summary>
        public string CategoryName { get; set; }
        public string Details { get; set; }
    }
}
