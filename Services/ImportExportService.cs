using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using PhotoStudioApp.Data;
using PhotoStudioApp.Models;

namespace PhotoStudioApp.Services
{
    /// <summary>Exports/imports Categories and Stock items as simple CSV files. Deliberately
    /// hand-rolled CSV (no external library needed) since the data is flat and simple. Import
    /// NEVER creates a duplicate: an existing row with the same Name (case-insensitive) is
    /// always skipped, never overwritten and never re-added - the caller gets back how many
    /// were actually added vs. skipped so it can tell the user.</summary>
    public class ImportExportService
    {
        private readonly CategoryRepository _categoryRepo = new CategoryRepository();
        private readonly StockRepository _stockRepo = new StockRepository();

        public class ImportResult
        {
            public int Added;
            public int SkippedDuplicates;
            public List<string> Errors = new List<string>();
        }

        // ---------- Categories ----------

        public void ExportCategories(string filePath)
        {
            var categories = _categoryRepo.GetAllCategories(includeInactive: true);
            var sb = new StringBuilder();
            sb.AppendLine("Name,HasFolder,FolderRelativePath,CurrentPrice,IsCustom,IsActive");
            foreach (var c in categories)
            {
                sb.AppendLine(string.Join(",",
                    CsvEncode(c.Name),
                    c.HasFolder ? "1" : "0",
                    CsvEncode(c.FolderRelativePath ?? ""),
                    c.CurrentPrice.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    c.IsCustom ? "1" : "0",
                    c.IsActive ? "1" : "0"));
            }
            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
        }

        /// <summary>Adds every category from the file that doesn't already exist (matched by
        /// Name, case-insensitive) - existing categories are left completely untouched.
        /// Imported categories are always added as custom (IsCustom=true), active.</summary>
        public ImportResult ImportCategories(string filePath)
        {
            var result = new ImportResult();
            var existingNames = new HashSet<string>(
                _categoryRepo.GetAllCategories(includeInactive: true).Select(c => c.Name.Trim()),
                StringComparer.OrdinalIgnoreCase);

            foreach (var row in ReadCsvRows(filePath, expectedColumns: 6, result))
            {
                string name = row[0];
                if (string.IsNullOrWhiteSpace(name)) continue;

                if (existingNames.Contains(name.Trim()))
                {
                    result.SkippedDuplicates++;
                    continue;
                }

                bool hasFolder = row[1] == "1";
                string folderPath = row[2];
                decimal price;
                decimal.TryParse(row[3], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out price);

                _categoryRepo.AddCategory(name.Trim(), hasFolder, hasFolder ? folderPath : null, isCustom: true, initialPrice: price);
                existingNames.Add(name.Trim()); // guard against duplicate rows within the same file
                result.Added++;
            }
            return result;
        }

        // ---------- Stock items ----------

        public void ExportStock(string filePath)
        {
            var items = _stockRepo.GetAll(activeOnly: false);
            var sb = new StringBuilder();
            sb.AppendLine("Name,Category,Quantity,UnitPrice,LowStockThreshold,IsActive");
            foreach (var s in items)
            {
                sb.AppendLine(string.Join(",",
                    CsvEncode(s.Name),
                    CsvEncode(s.Category ?? ""),
                    s.Quantity.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    s.UnitPrice.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    s.LowStockThreshold.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    s.IsActive ? "1" : "0"));
            }
            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
        }

        /// <summary>Adds every stock item from the file that doesn't already exist (matched by
        /// Name, case-insensitive) - existing items (and their current quantity) are left
        /// completely untouched.</summary>
        public ImportResult ImportStock(string filePath)
        {
            var result = new ImportResult();
            var existingNames = new HashSet<string>(
                _stockRepo.GetAll(activeOnly: false).Select(s => s.Name.Trim()),
                StringComparer.OrdinalIgnoreCase);

            foreach (var row in ReadCsvRows(filePath, expectedColumns: 6, result))
            {
                string name = row[0];
                if (string.IsNullOrWhiteSpace(name)) continue;

                if (existingNames.Contains(name.Trim()))
                {
                    result.SkippedDuplicates++;
                    continue;
                }

                int qty, lowStock;
                decimal price;
                int.TryParse(row[2], out qty);
                decimal.TryParse(row[3], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out price);
                int.TryParse(row[4], out lowStock);

                _stockRepo.Add(new StockItem
                {
                    Name = name.Trim(),
                    Category = row[1],
                    Quantity = qty,
                    UnitPrice = price,
                    LowStockThreshold = lowStock
                });
                existingNames.Add(name.Trim());
                result.Added++;
            }
            return result;
        }

        // ---------- Minimal CSV helpers (no external library) ----------

        private static string CsvEncode(string value)
        {
            if (value == null) value = "";
            bool needsQuotes = value.Contains(",") || value.Contains("\"") || value.Contains("\n") || value.Contains("\r");
            if (!needsQuotes) return value;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        /// <summary>Reads a CSV file (with a header row, which is skipped) into raw string[]
        /// rows, handling quoted fields (including embedded commas/quotes). Rows with the wrong
        /// column count are recorded as errors and skipped rather than throwing, so one bad line
        /// doesn't abort the whole import.</summary>
        private static IEnumerable<string[]> ReadCsvRows(string filePath, int expectedColumns, ImportResult result)
        {
            string[] lines = File.ReadAllLines(filePath, Encoding.UTF8);
            for (int i = 1; i < lines.Length; i++) // skip header row
            {
                string line = lines[i];
                if (string.IsNullOrWhiteSpace(line)) continue;

                var fields = ParseCsvLine(line);
                if (fields.Count != expectedColumns)
                {
                    result.Errors.Add($"Ligne {i + 1} ignorée (nombre de colonnes inattendu).");
                    continue;
                }
                yield return fields.ToArray();
            }
        }

        private static List<string> ParseCsvLine(string line)
        {
            var fields = new List<string>();
            var current = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < line.Length && line[i + 1] == '"') { current.Append('"'); i++; }
                        else inQuotes = false;
                    }
                    else current.Append(c);
                }
                else
                {
                    if (c == '"') inQuotes = true;
                    else if (c == ',') { fields.Add(current.ToString()); current.Clear(); }
                    else current.Append(c);
                }
            }
            fields.Add(current.ToString());
            return fields;
        }
    }
}
