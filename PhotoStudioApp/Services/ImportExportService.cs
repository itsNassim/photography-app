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
        private readonly CategoryAddOnRepository _addOnRepo = new CategoryAddOnRepository();

        public class ImportResult
        {
            public int Added;
            public int SkippedDuplicates;
            public List<string> Errors = new List<string>();
        }

        /// <summary>A category name plus its raw (still-encoded) Extras field, held between the
        /// two passes ImportCategories makes over a file - not a C# tuple, since this project
        /// targets .NET Framework 4.6 and tuple syntax needs the separate System.ValueTuple
        /// package this project intentionally doesn't add (see PasswordService's own note on a
        /// similar 4.6 gap).</summary>
        private class PendingCategoryAddOns
        {
            public string Name;
            public string ExtrasField;
        }

        // ---------- Categories ----------

        public void ExportCategories(string filePath)
        {
            var categories = _categoryRepo.GetAllCategories(includeInactive: true);
            var categoriesById = categories.ToDictionary(c => c.Id);
            var stockById = _stockRepo.GetAll(activeOnly: false).ToDictionary(s => s.Id);

            var sb = new StringBuilder();
            sb.AppendLine("Name,HasFolder,FolderRelativePath,CurrentPrice,IsCustom,IsActive,Extras");
            foreach (var c in categories)
            {
                sb.AppendLine(string.Join(",",
                    CsvEncode(c.Name),
                    c.HasFolder ? "1" : "0",
                    CsvEncode(c.FolderRelativePath ?? ""),
                    c.CurrentPrice.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    c.IsCustom ? "1" : "0",
                    c.IsActive ? "1" : "0",
                    CsvEncode(BuildExtrasField(c.Id, categoriesById, stockById))));
            }
            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
        }

        /// <summary>Encodes a category's optional extras (see CategoryAddOn) as one CSV field:
        /// semicolon-separated "CAT:Name" / "STOCK:Name" tokens, referencing the linked category
        /// or stock item BY NAME (matching how this whole file already identifies everything, and
        /// how ImportCategories re-links extras on the way back in). A referenced category/stock
        /// item whose row has since disappeared is silently skipped - nothing to encode for it.</summary>
        private string BuildExtrasField(int categoryId, Dictionary<int, Category> categoriesById, Dictionary<int, StockItem> stockById)
        {
            var tokens = new List<string>();
            foreach (var a in _addOnRepo.GetForParent(categoryId))
            {
                Category addOnCat;
                StockItem addOnStock;
                if (a.AddOnCategoryId.HasValue && categoriesById.TryGetValue(a.AddOnCategoryId.Value, out addOnCat))
                    tokens.Add("CAT:" + addOnCat.Name);
                else if (a.AddOnStockItemId.HasValue && stockById.TryGetValue(a.AddOnStockItemId.Value, out addOnStock))
                    tokens.Add("STOCK:" + addOnStock.Name);
            }
            return string.Join(";", tokens);
        }

        /// <summary>Adds every category from the file that doesn't already exist (matched by
        /// Name, case-insensitive) - existing categories are left completely untouched.
        /// Imported categories are always added as custom (IsCustom=true), active.</summary>
        /// <summary>Adds every category from the file that doesn't already exist, exactly as
        /// before, in a first pass - THEN, in a second pass over just the newly-added ones,
        /// re-creates their optional extras (see BuildExtrasField/CategoryAddOn) by looking up
        /// each referenced name among ALL categories/stock items that now exist locally
        /// (including ones this same import just added, and any pre-existing ones - extras can
        /// legitimately point at a category the user already had). A referenced name that still
        /// can't be found (e.g. a stock item that hasn't been imported yet - categories and stock
        /// are separate imports) is skipped and reported in result.Errors, without failing the
        /// rest of the import. Existing categories are never modified, so their extras are never
        /// touched by an import either.</summary>
        public ImportResult ImportCategories(string filePath)
        {
            var result = new ImportResult();
            var existingNames = new HashSet<string>(
                _categoryRepo.GetAllCategories(includeInactive: true).Select(c => c.Name.Trim()),
                StringComparer.OrdinalIgnoreCase);

            var newlyAdded = new List<PendingCategoryAddOns>();

            foreach (var row in ReadCsvRows(filePath, minColumns: 6, maxColumns: 7, result))
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
                newlyAdded.Add(new PendingCategoryAddOns { Name = name.Trim(), ExtrasField = row[6] });
                result.Added++;
            }

            if (newlyAdded.Any(p => !string.IsNullOrWhiteSpace(p.ExtrasField)))
                LinkImportedExtras(newlyAdded, result);

            return result;
        }

        private void LinkImportedExtras(List<PendingCategoryAddOns> newlyAdded, ImportResult result)
        {
            var categoriesByName = new Dictionary<string, Category>(StringComparer.OrdinalIgnoreCase);
            foreach (var cat in _categoryRepo.GetAllCategories(includeInactive: true))
                if (!categoriesByName.ContainsKey(cat.Name.Trim()))
                    categoriesByName[cat.Name.Trim()] = cat;

            var stockByName = new Dictionary<string, StockItem>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in _stockRepo.GetAll(activeOnly: false))
                if (!stockByName.ContainsKey(item.Name.Trim()))
                    stockByName[item.Name.Trim()] = item;

            foreach (var pending in newlyAdded)
            {
                if (string.IsNullOrWhiteSpace(pending.ExtrasField)) continue;

                Category parent;
                if (!categoriesByName.TryGetValue(pending.Name, out parent)) continue;

                foreach (var token in pending.ExtrasField.Split(';'))
                {
                    if (string.IsNullOrWhiteSpace(token)) continue;
                    int colonIdx = token.IndexOf(':');
                    if (colonIdx < 0) continue;

                    string kind = token.Substring(0, colonIdx);
                    string refName = token.Substring(colonIdx + 1).Trim();

                    Category addOnCat;
                    StockItem addOnStock;
                    if (kind == "CAT" && categoriesByName.TryGetValue(refName, out addOnCat))
                        _addOnRepo.Add(parent.Id, addOnCat.Id, null);
                    else if (kind == "STOCK" && stockByName.TryGetValue(refName, out addOnStock))
                        _addOnRepo.Add(parent.Id, null, addOnStock.Id);
                    else
                        result.Errors.Add($"Extra \"{refName}\" introuvable pour la catégorie \"{pending.Name}\" - ignoré.");
                }
            }
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
        /// <summary>Exact-column-count convenience overload, unchanged behavior for every
        /// existing caller (Stock's import).</summary>
        private static IEnumerable<string[]> ReadCsvRows(string filePath, int expectedColumns, ImportResult result)
        {
            return ReadCsvRows(filePath, expectedColumns, expectedColumns, result);
        }

        /// <summary>Reads a CSV file (with a header row, which is skipped) into raw string[]
        /// rows, handling quoted fields (including embedded commas/quotes). A row is accepted
        /// when its column count falls anywhere in [minColumns, maxColumns] - this is what lets
        /// ImportCategories accept both an older 6-column export (from before the Extras column
        /// existed) and the current 7-column one: a short row is padded with empty trailing
        /// columns up to maxColumns. Rows outside that range are recorded as errors and skipped
        /// rather than throwing, so one bad line doesn't abort the whole import.</summary>
        private static IEnumerable<string[]> ReadCsvRows(string filePath, int minColumns, int maxColumns, ImportResult result)
        {
            string[] lines = File.ReadAllLines(filePath, Encoding.UTF8);
            for (int i = 1; i < lines.Length; i++) // skip header row
            {
                string line = lines[i];
                if (string.IsNullOrWhiteSpace(line)) continue;

                var fields = ParseCsvLine(line);
                if (fields.Count < minColumns || fields.Count > maxColumns)
                {
                    result.Errors.Add($"Ligne {i + 1} ignorée (nombre de colonnes inattendu).");
                    continue;
                }
                while (fields.Count < maxColumns)
                    fields.Add("");
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
