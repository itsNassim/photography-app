using System;
using System.Collections.Generic;
using System.Linq;

namespace PhotoStudioApp.Services
{
    /// <summary>Computes the next logical work date, skipping configured closed weekdays
    /// (e.g. Friday) - configurable rather than hardcoded per the spec.</summary>
    public static class WorkdayCalculator
    {
        public static HashSet<DayOfWeek> ParseClosedWeekdays(string csv)
        {
            var set = new HashSet<DayOfWeek>();
            if (string.IsNullOrWhiteSpace(csv)) return set;

            foreach (var part in csv.Split(','))
            {
                var name = part.Trim();
                if (Enum.TryParse<DayOfWeek>(name, true, out var dow))
                    set.Add(dow);
            }
            return set;
        }

        public static string ToCsv(IEnumerable<DayOfWeek> days) =>
            string.Join(",", days.Select(d => d.ToString()));

        /// <summary>Given the date being closed, returns the next work date, skipping forward
        /// over any consecutive closed weekdays (so if e.g. both Friday and Saturday were closed,
        /// it would land on Sunday).</summary>
        public static DateTime NextWorkDate(DateTime closedDate, HashSet<DayOfWeek> closedWeekdays)
        {
            DateTime next = closedDate.AddDays(1);
            int safety = 0;
            while (closedWeekdays.Contains(next.DayOfWeek) && safety < 14)
            {
                next = next.AddDays(1);
                safety++;
            }
            return next;
        }
    }
}
