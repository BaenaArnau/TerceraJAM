using Godot;
using System.Collections.Generic;

namespace SpellsAndRooms.scripts.Turns
{
    /// @brief Carga y expone datos de consumibles y pasivos desde CSV.
    public sealed class ItemDatabase
    {
        /// @brief Datos normalizados de un consumible disponible en tienda/cofres.
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

        /// @brief Datos normalizados de un pasivo disponible en tienda/cofres.
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

        /// @brief Inicializa la base y carga ambos CSV de items.
        public ItemDatabase()
        {
            LoadConsumables();
            LoadPassives();
        }

        /// @brief Carga consumibles desde `ConsumableItems.csv`.
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
                    pathIndex = TurnCsvUtils.FindColumnIndex(headerCols, "path");
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
                    Potency = TurnCsvUtils.ParseInt(cols[3], 0),
                    Description = cols[4].Trim(),
                    Price = cols.Count > 5 ? TurnCsvUtils.ParseInt(cols[5], 0) : TurnCsvUtils.ParseInt(cols[3], 0),
                    ImagePath = pathIndex >= 0 && pathIndex < cols.Count
                        ? cols[pathIndex].Trim()
                        : (cols.Count > 6 ? cols[6].Trim() : string.Empty)
                });
            }
        }

        /// @brief Carga pasivos desde `PassiveItem.csv`.
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
                    pathIndex = TurnCsvUtils.FindColumnIndex(headerCols, "path");
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
                    BonusValue = TurnCsvUtils.ParseInt(cols[2], 0),
                    Description = cols[3].Trim(),
                    Price = TurnCsvUtils.ParseInt(cols[4], 0),
                    ImagePath = pathIndex >= 0 && pathIndex < cols.Count
                        ? cols[pathIndex].Trim()
                        : (cols.Count > 5 ? cols[5].Trim() : string.Empty)
                });
            }
        }
    }
}


