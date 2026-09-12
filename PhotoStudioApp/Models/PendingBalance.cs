namespace PhotoStudioApp.Models
{
    /// <summary>An outstanding amount still owed by a client who only paid part of a charge
    /// (e.g. paid 50 of an 80 total). Tracked here instead of a text file so it can be shown in
    /// the Home tab and picked again from the "Ajouter une charge" dialog until it's fully paid
    /// off, at which point it's deleted automatically.</summary>
    public class PendingBalance
    {
        public int Id { get; set; }
        public string Description { get; set; }
        public decimal RemainingAmount { get; set; }
        /// <summary>yyyy-MM-dd - the work date the balance was first created on. Informational
        /// only: the eventual payment always counts toward whatever day it's actually paid on,
        /// not this date.</summary>
        public string CreatedDate { get; set; }
    }
}
