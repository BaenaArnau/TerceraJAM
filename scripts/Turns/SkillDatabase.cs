using Godot;
using System;
using System.Collections.Generic;
using SpellsAndRooms.scripts.Characters;

namespace SpellsAndRooms.scripts.Turns
{
    /// @brief Base de datos de skills cargada desde CSV para combate y recompensas.
    public sealed class SkillDatabase
    {
        /// @brief Metadatos de skill usados en tienda/cofres.
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

        /// @brief Inicializa la base y carga `Skill.csv`.
        public SkillDatabase()
        {
            LoadFromCsv();
        }

        /// @brief Intenta recuperar una skill por nombre normalizado.
        /// @param skillName Nombre buscado.
        /// @param skill Skill encontrada.
        /// @return true si existe una entrada para ese nombre.
        public bool TryGetSkill(string skillName, out Skill skill)
        {
            return _skills.TryGetValue(Normalize(skillName), out skill);
        }

        /// @brief Devuelve una skill o fallback si no existe en la base.
        public Skill GetSkillOrDefault(string skillName, Skill fallback = null)
        {
            return TryGetSkill(skillName, out Skill skill) ? skill : fallback;
        }

        /// @brief Convierte una lista de nombres en instancias de `Skill` válidas.
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
                    pathIndex = TurnCsvUtils.FindColumnIndex(headerCols, "path");
                    isHeader = false;
                    continue;
                }

                List<string> cols = CsvUtils.SplitLine(line);
                if (cols.Count < 5)
                    continue;

                string name = cols[0].Trim();
                Character.DamageType damageType = ParseDamageType(cols[1].Trim());
                int damage = TurnCsvUtils.ParseInt(cols[2], 1);
                int manaCost = TurnCsvUtils.ParseInt(cols[3], 0);
                bool multiHit = TurnCsvUtils.ParseBool(cols[4]);
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
                    Price = cols.Count > 6 ? TurnCsvUtils.ParseInt(cols[6], 0) : 0,
                    ImagePath = pathIndex >= 0 && pathIndex < cols.Count
                        ? cols[pathIndex].Trim()
                        : (cols.Count > 7 ? cols[7].Trim() : string.Empty)
                });
            }
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


        private static string Normalize(string value)
        {
            return (value ?? string.Empty).Trim();
        }
    }
}

