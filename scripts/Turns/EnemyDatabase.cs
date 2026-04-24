using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;
using SpellsAndRooms.scripts.Characters;

namespace SpellsAndRooms.scripts.Turns
{
    public sealed class EnemyDatabase
    {
        private readonly SkillDatabase _skillDatabase;
        private readonly List<EnemyTemplate> _templates = new List<EnemyTemplate>();
        private const string EnemyCsvPath = "res://Files/Enemy.csv";

        private static readonly Dictionary<string, string> ScenePathByName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["dark goblin"] = "res://scenes/Characters/Enemy/Goblin.tscn",
            ["goblin"] = "res://scenes/Characters/Enemy/Goblin.tscn",
            ["skeleton"] = "res://scenes/Characters/Enemy/Skeleton.tscn",
            ["slime"] = "res://scenes/Characters/Enemy/Slime.tscn",
            ["black thing"] = "res://scenes/Characters/Enemy/BlackThing.tscn",
            ["blackthing"] = "res://scenes/Characters/Enemy/BlackThing.tscn"
        };

        private static readonly HashSet<string> BossEnemiesByName = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "black thing",
            "blackthing"
        };

        public IReadOnlyList<EnemyTemplate> Templates => _templates;

        public EnemyDatabase(SkillDatabase skillDatabase)
        {
            _skillDatabase = skillDatabase ?? new SkillDatabase();
            LoadFromCsv();
        }

        public bool TryGetTemplate(string enemyName, out EnemyTemplate template)
        {
            template = _templates.Find(t => string.Equals(t.Name, enemyName, StringComparison.OrdinalIgnoreCase));
            return template != null;
        }

        private void LoadFromCsv()
        {
            if (!FileAccess.FileExists(EnemyCsvPath))
            {
                GD.PrintErr($"No se encontro {EnemyCsvPath}. Se usara una lista vacia de enemigos.");
                return;
            }

            using FileAccess file = FileAccess.Open(EnemyCsvPath, FileAccess.ModeFlags.Read);
            if (file == null)
            {
                GD.PrintErr($"No se pudo abrir {EnemyCsvPath}.");
                return;
            }

            bool isHeader = true;
            while (!file.EofReached())
            {
                string line = file.GetLine().Trim();
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                if (isHeader)
                {
                    isHeader = false;
                    continue;
                }

                List<string> cols = CsvUtils.SplitLine(line);
                if (cols.Count == 0)
                {
                    continue;
                }

                string name = cols[0].Trim();
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                string scenePath = ResolveScenePath(name);
                if (string.IsNullOrWhiteSpace(scenePath))
                {
                    GD.PrintErr($"No se encontro escena para el enemigo '{name}'.");
                    continue;
                }

                PackedScene packedScene = ResourceLoader.Load<PackedScene>(scenePath);
                if (packedScene == null)
                {
                    GD.PrintErr($"No se pudo cargar la escena de enemigo '{scenePath}'.");
                    continue;
                }

                Enemy probe = packedScene.Instantiate<Enemy>();
                if (probe == null)
                {
                    GD.PrintErr($"La escena '{scenePath}' no instancia un Enemy valido.");
                    continue;
                }

                var skillNames = new List<string>();
                for (int i = 1; i < cols.Count; i++)
                {
                    string skillName = cols[i].Trim();
                    if (string.IsNullOrWhiteSpace(skillName) || skillName.Equals("null", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    skillNames.Add(skillName);
                }

                List<Skill> skills = _skillDatabase.ResolveSkills(skillNames);
                if (skills.Count == 0)
                {
                    skills.Add(new Skill("Golpe", 0, Mathf.Max(1, probe.Damage), Character.DamageType.Physical));
                }

                _templates.Add(new EnemyTemplate
                {
                    Name = string.IsNullOrWhiteSpace(probe.CharacterName) ? name : probe.CharacterName,
                    ScenePath = scenePath,
                    Scene = packedScene,
                    IsBoss = IsBossEnemy(name, scenePath),
                    Difficulty = Mathf.Max(1, probe.Difficulty),
                    Loot = Mathf.Max(0, probe.MoneyLoot),
                    Skills = skills.ToArray()
                });

                probe.Free();
            }
        }

        private static string ResolveScenePath(string enemyName)
        {
            string key = Normalize(enemyName);
            return ScenePathByName.TryGetValue(key, out string scenePath) ? scenePath : string.Empty;
        }

        private static string Normalize(string value)
        {
            return (value ?? string.Empty).Trim();
        }

        private static bool IsBossEnemy(string enemyName, string scenePath)
        {
            string normalizedName = Normalize(enemyName);
            if (BossEnemiesByName.Contains(normalizedName))
                return true;

            return !string.IsNullOrWhiteSpace(scenePath)
                && scenePath.IndexOf("BlackThing", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
