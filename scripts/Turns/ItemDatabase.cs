using Godot;
using System.Collections.Generic;
using System.Globalization;

namespace SpellsAndRooms.scripts.Turns
{
    public sealed class ItemDatabase
    {
        public sealed class ConsumableDefinition
        {
            public string Name { get; init; } = "Item";
            public string Type { get; init; } = string.Empty;
            public string Subtype { get; init; } = string.Empty;
            public int Potency { get; init; } = 0;
            public string Description { get; init; } = string.Empty;
            public int Price { get; init; } = 0;
            public string ImagePath { get; init; } = string.Empty;
        }

        public sealed class PassiveDefinition
        {
            public string Name { get; init; } = "Passive";
            public string Type { get; init; } = string.Empty;
            public int BonusValue { get; init; } = 0;
            public string Description { get; init; } = string.Empty;
            public int Price { get; init; } = 0;
            public string ImagePath { get; init; } = string.Empty;
        }

        private readonly List<ConsumableDefinition> _consumables = new List<ConsumableDefinition>();
        private readonly List<PassiveDefinition> _passives = new List<PassiveDefinition>();
        private const string ConsumableCsvPath = "res://Files/ConsumableItems.csv";
        private const string PassiveCsvPath = "res://Files/PassiveItem.csv";

        public List<ConsumableDefinition> Consumables => _consumables;
        public List<PassiveDefinition> Passives => _passives;

        public ItemDatabase()
        {
            LoadConsumables();
            LoadPassives();
        }

        private void LoadConsumables()
        {
            if (!FileAccess.FileExists(ConsumableCsvPath))
            {
                GD.PrintErr($"No se encontro {ConsumableCsvPath}. No se cargaran consumibles.");
                return;
            }

            using FileAccess file = FileAccess.Open(ConsumableCsvPath, FileAccess.ModeFlags.Read);
            if (file == null)
            {
                GD.PrintErr($"No se pudo abrir {ConsumableCsvPath}.");
                return;
            }

            bool isHeader = true;
            int pathIndex = -1;
            while (!file.EofReached())
            {
                string line = file.GetLine().Trim();
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                if (isHeader)
                {
                    List<string> headerCols = CsvUtils.SplitLine(line);
                    pathIndex = FindColumnIndex(headerCols, "path");
                    isHeader = false;
                    continue;
                }

                List<string> cols = CsvUtils.SplitLine(line);
                if (cols.Count < 5)
                    continue;

                string name = cols[0].Trim();
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                _consumables.Add(new ConsumableDefinition
                {
                    Name = name,
                    Type = cols[1].Trim(),
                    Subtype = cols[2].Trim(),
                    Potency = ParseInt(cols[3], 0),
                    Description = cols[4].Trim(),
                    Price = cols.Count > 5 ? ParseInt(cols[5], 0) : ParseInt(cols[3], 0),
                    ImagePath = pathIndex >= 0 && pathIndex < cols.Count
                        ? cols[pathIndex].Trim()
                        : (cols.Count > 6 ? cols[6].Trim() : string.Empty)
                });
            }
        }

        private void LoadPassives()
        {
            if (!FileAccess.FileExists(PassiveCsvPath))
            {
                GD.PrintErr($"No se encontro {PassiveCsvPath}. No se cargaran pasivos.");
                return;
            }

            using FileAccess file = FileAccess.Open(PassiveCsvPath, FileAccess.ModeFlags.Read);
            if (file == null)
            {
                GD.PrintErr($"No se pudo abrir {PassiveCsvPath}.");
                return;
            }

            bool isHeader = true;
            int pathIndex = -1;
            while (!file.EofReached())
            {
                string line = file.GetLine().Trim();
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                if (isHeader)
                {
                    List<string> headerCols = CsvUtils.SplitLine(line);
                    pathIndex = FindColumnIndex(headerCols, "path");
                    isHeader = false;
                    continue;
                }

                List<string> cols = CsvUtils.SplitLine(line);
                if (cols.Count < 5)
                    continue;

                string name = cols[0].Trim();
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                _passives.Add(new PassiveDefinition
                {
                    Name = name,
                    Type = cols[1].Trim(),
                    BonusValue = ParseInt(cols[2], 0),
                    Description = cols[3].Trim(),
                    Price = ParseInt(cols[4], 0),
                    ImagePath = pathIndex >= 0 && pathIndex < cols.Count
                        ? cols[pathIndex].Trim()
                        : (cols.Count > 5 ? cols[5].Trim() : string.Empty)
                });
            }
        }

        private static int FindColumnIndex(List<string> headers, string columnName)
        {
            if (headers == null || headers.Count == 0 || string.IsNullOrWhiteSpace(columnName))
                return -1;

            for (int i = 0; i < headers.Count; i++)
            {
                if (headers[i].Trim().Equals(columnName, System.StringComparison.OrdinalIgnoreCase))
                    return i;
            }

            return -1;
        }

        private static int ParseInt(string value, int fallback)
        {
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) ? parsed : fallback;
        }
    }
}


