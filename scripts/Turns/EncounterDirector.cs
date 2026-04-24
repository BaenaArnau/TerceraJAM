using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using SpellsAndRooms.scripts.Characters;
using SpellsAndRooms.scripts.map;

namespace SpellsAndRooms.scripts.Turns
{
    public sealed class EncounterDirector
    {
        public const int MaxEnemiesPerRoom = 4;

        private readonly EnemyDatabase _enemyDatabase;
        private readonly RandomNumberGenerator _rng = new RandomNumberGenerator();

        public EncounterDirector(EnemyDatabase enemyDatabase)
        {
            _enemyDatabase = enemyDatabase;
            _rng.Randomize();
        }

        public List<Enemy> BuildEncounter(Room room)
        {
            IReadOnlyList<EnemyTemplate> templates = _enemyDatabase.Templates;
            if (templates.Count == 0)
            {
                return new List<Enemy>();
            }

            bool isBossRoom = room.Type == Room.RoomType.Boss;
            List<EnemyTemplate> typePool = templates
                .Where(t => isBossRoom ? t.IsBoss : !t.IsBoss)
                .ToList();

            if (typePool.Count == 0)
            {
                GD.PrintErr(isBossRoom
                    ? "No hay enemigos boss configurados. Se usa fallback general."
                    : "No hay enemigos normales configurados. Se usa fallback general.");
                typePool = templates.ToList();
            }

            int row = Mathf.Max(0, room.Row);
            int expectedDifficulty = 1 + row / 3;
            if (isBossRoom)
            {
                expectedDifficulty += 3;
            }

            List<EnemyTemplate> pool = typePool
                .Where(t => Math.Abs(t.Difficulty - expectedDifficulty) <= 2)
                .ToList();

            if (pool.Count == 0)
            {
                pool = typePool
                    .OrderBy(t => Math.Abs(t.Difficulty - expectedDifficulty))
                    .ThenByDescending(t => t.Difficulty)
                    .ToList();
            }

            int count = isBossRoom
                ? 1
                : Mathf.Clamp(1 + row / 4, 1, MaxEnemiesPerRoom);

            var enemies = new List<Enemy>(count);
            for (int i = 0; i < count; i++)
            {
                EnemyTemplate template = PickByDifficultyWeight(pool, expectedDifficulty);
                if (template == null)
                    continue;

                Enemy enemy = CreateEnemyFromTemplate(template);
                if (enemy != null)
                {
                    enemies.Add(enemy);
                }
            }

            return enemies;
        }

        private EnemyTemplate PickByDifficultyWeight(IReadOnlyList<EnemyTemplate> pool, int expectedDifficulty)
        {
            if (pool == null || pool.Count == 0)
                return null;

            float totalWeight = 0.0f;
            var weights = new float[pool.Count];

            for (int i = 0; i < pool.Count; i++)
            {
                EnemyTemplate template = pool[i];
                int delta = Math.Abs(template.Difficulty - expectedDifficulty);
                float weight = 1.0f / (1.0f + (delta * delta));
                weights[i] = weight;
                totalWeight += weight;
            }

            if (totalWeight <= 0.0f)
                return pool[_rng.RandiRange(0, pool.Count - 1)];

            float roll = _rng.RandfRange(0.0f, totalWeight);
            for (int i = 0; i < pool.Count; i++)
            {
                roll -= weights[i];
                if (roll <= 0.0f)
                    return pool[i];
            }

            return pool[pool.Count - 1];
        }

        private static Enemy CreateEnemyFromTemplate(EnemyTemplate template)
        {
            if (template?.Scene == null)
            {
                return null;
            }

            Enemy enemy = template.Scene.Instantiate<Enemy>();
            if (enemy == null)
            {
                return null;
            }

            enemy.Health = enemy.BaseHealth;
            enemy.Mana = enemy.BaseMana;

            foreach (Skill skill in template.Skills)
            {
                enemy.AddSkill(skill);
            }

            return enemy;
        }
    }
}
