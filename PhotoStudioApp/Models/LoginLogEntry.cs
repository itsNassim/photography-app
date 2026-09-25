using System;

namespace PhotoStudioApp.Models
{
    /// <summary>One admin-password prompt shown anywhere in the app - which protected action
    /// triggered it, which button the user clicked to get there, how many wrong attempts were
    /// made, and whether it ultimately succeeded. Stored encrypted at rest - see
    /// Services.LogCryptoService / Data.LoginLogRepository.</summary>
    public class LoginLogEntry
    {
        public int Id { get; set; }
        public DateTime Timestamp { get; set; }
        /// <summary>What the password was protecting, e.g. "Supprimer une journee de l'historique".</summary>
        public string ActionName { get; set; }
        /// <summary>The exact button/control label the user clicked to reach this prompt.</summary>
        public string ButtonLabel { get; set; }
        /// <summary>Number of WRONG attempts made before the prompt closed (0 = correct on the
        /// first try, or cancelled before typing anything).</summary>
        public int AttemptsMade { get; set; }
        public bool Success { get; set; }
        /// <summary>Extra context, e.g. "Verrouille - trop de tentatives incorrectes" or
        /// "Annule par l'utilisateur".</summary>
        public string Details { get; set; }
    }
}
