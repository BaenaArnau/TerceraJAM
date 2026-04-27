using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;
using SpellsAndRooms.scripts.Characters;

namespace SpellsAndRooms.scripts.Turns
{
    public sealed class SkillDatabase
    {
        public sealed class SkillDefinition
        {
            public string Name { get; init; } = "Skill";
            public string Description { get; init; } = string.Empty;
            public int Price { get; init; } = 0;
            public string ImagePath { get; init; } = string.Empty;
        }

        private readonly Dictionary<string, Skill> _skills = new Dictionary<string, Skill>(StringComparer.OrdinalIgnoreCase);
        private readonly List<SkillDefinition> _skillDefinitions = new List<SkillDefinition>();
        private const string SkillCsvPath = "res://Files/Skill.csv";

        public IReadOnlyDictionary<string, Skill> Skills => _skills;
        public IReadOnlyList<SkillDefinition> SkillDefinitions => _skillDefinitions;

        public SkillDatabase()
        {
            LoadFromCsv();
        }

        public bool TryGetSkill(string skillName, out Skill skill)
        {
            return _skills.TryGetValue(Normalize(skillName), out skill);
        }

        public Skill GetSkillOrDefault(string skillName, Skill fallback = null)
        {
            return TryGetSkill(skillName, out Skill skill) ? skill : fallback;
        }

        public List<Skill> ResolveSkills(IEnumerable<string> skillNames)
        {
            var result = new List<Skill>();
            if (skillNames == null)
                return result;

            foreach (string name in skillNames)
            {
                if (TryGetSkill(name, out Skill skill) && skill != null)
                    result.Add(skill);
            }

            return result;
        }

        private void LoadFromCsv()
        {
            if (!FileAccess.FileExists(SkillCsvPath))
            {
                GD.PrintErr($"No se encontro {SkillCsvPath}. No se cargaran skills desde CSV.");
                return;
            }

            using FileAccess file = FileAccess.Open(SkillCsvPath, FileAccess.ModeFlags.Read);
            if (file == null)
            {
                GD.PrintErr($"No se pudo abrir {SkillCsvPath}.");
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
                Character.DamageType damageType = ParseDamageType(cols[1].Trim());
                int damage = ParseInt(cols[2], 1);
                int manaCost = ParseInt(cols[3], 0);
                bool multiHit = ParseBool(cols[4]);
                bool isHealing = damageType == Character.DamageType.None || cols[1].Trim().Equals("Healing", StringComparison.OrdinalIgnoreCase) || name.Equals("Cure", StringComparison.OrdinalIgnoreCase);

                if (string.IsNullOrWhiteSpace(name))
                    continue;

                _skills[Normalize(name)] = new Skill(
                    name,
                    manaCost,
                    damage,
                    damageType,
                    multiHit,
                    isHealing);

                _skillDefinitions.Add(new SkillDefinition
                {
                    Name = name,
                    Description = cols.Count > 5 ? cols[5].Trim() : string.Empty,
                    Price = cols.Count > 6 ? ParseInt(cols[6], 0) : 0,
                    ImagePath = pathIndex >= 0 && pathIndex < cols.Count
                        ? cols[pathIndex].Trim()
                        : (cols.Count > 7 ? cols[7].Trim() : string.Empty)
                });
            }
        }

        private static int FindColumnIndex(List<string> headers, string columnName)
        {
            if (headers == null || headers.Count == 0 || string.IsNullOrWhiteSpace(columnName))
                return -1;

            for (int i = 0; i < headers.Count; i++)
            {
                if (headers[i].Trim().Equals(columnName, StringComparison.OrdinalIgnoreCase))
                    return i;
            }

            return -1;
        }

        private static Character.DamageType ParseDamageType(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return Character.DamageType.Physical;

            if (Enum.TryParse(value, true, out Character.DamageType parsed))
                return parsed;

            if (value.Equals("Healing", StringComparison.OrdinalIgnoreCase))
                return Character.DamageType.None;

            return Character.DamageType.Physical;
        }

        private static bool ParseBool(string value)
        {
            return bool.TryParse(value, out bool parsed) && parsed;
        }

        private static int ParseInt(string value, int fallback)
        {
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) ? parsed : fallback;
        }

        private static string Normalize(string value)
        {
            return (value ?? string.Empty).Trim();
        }
    }
}

