using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using SpellsAndRooms.scripts.Characters;
using SpellsAndRooms.scripts.map;

namespace SpellsAndRooms.scripts.Turns
{
    /// <summary>
    /// Genera encuentros de enemigos para las habitaciones, basándose en la dificultad esperada y el tipo de habitación.
    /// </summary>
    public sealed class EncounterDirector
    {
        public const int MaxEnemiesPerRoom = 4;

        private readonly EnemyDatabase _enemyDatabase;
        private readonly RandomNumberGenerator _rng = new RandomNumberGenerator();
        
        /// <summary>
        /// Crea un nuevo EncounterDirector con la base de datos de enemigos proporcionada.
        /// </summary>
        /// <param name="enemyDatabase">
        /// La base de datos que contiene las plantillas de enemigos disponibles para generar encuentros.
        /// </param>
        public EncounterDirector(EnemyDatabase enemyDatabase)
        {
            _enemyDatabase = enemyDatabase;
            _rng.Randomize();
        }
        
        /// <summary>
        /// Genera una lista de enemigos para un encuentro en la habitación dada, considerando su tipo y fila para ajustar la dificultad.
        /// </summary>
        /// <param name="room">
        /// La habitación para la cual se generará el encuentro. Se utiliza su tipo (normal o jefe) y su fila para determinar la dificultad y el tipo de enemigos a incluir.
        /// </param>
        /// <returns>
        /// Una lista de enemigos generados para el encuentro. La cantidad y tipo de enemigos dependerá del tipo de habitación y su posición en el mapa, buscando siempre una dificultad adecuada.
        /// </returns>
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
            int slimeDifficultyCap = int.MaxValue;
            for (int i = 0; i < count; i++)
            {
                IReadOnlyList<EnemyTemplate> selectionPool = pool;
                if (!isBossRoom && slimeDifficultyCap != int.MaxValue)
                {
                    var weakerCandidates = pool
                        .Where(t => t.Difficulty < slimeDifficultyCap)
                        .ToList();

                    if (weakerCandidates.Count == 0)
                        break;

                    selectionPool = weakerCandidates;
                }

                EnemyTemplate template = PickByDifficultyWeight(selectionPool, expectedDifficulty);
                if (template == null)
                    continue;

                if (!isBossRoom && template.Name.IndexOf("slime", StringComparison.OrdinalIgnoreCase) >= 0)
                    slimeDifficultyCap = template.Difficulty;

                Enemy enemy = CreateEnemyFromTemplate(template);
                if (enemy != null)
                {
                    enemies.Add(enemy);
                }
            }

            return enemies;
        }
        
        /// <summary>
        /// Selecciona una plantilla de enemigo del pool dado, utilizando un sistema de pesos basado en la cercanía de su dificultad a la dificultad esperada. Las plantillas con dificultad más cercana al valor esperado tendrán mayor probabilidad de ser seleccionadas, pero también existe la posibilidad de elegir plantillas con dificultades más alejadas, aunque con menor probabilidad.
        /// </summary>
        /// <param name="pool">
        /// La lista de plantillas de enemigos entre las cuales se realizará la selección. Se asume que esta lista ya ha sido filtrada para incluir solo plantillas relevantes según el tipo de habitación y la dificultad esperada.
        /// </param>
        /// <param name="expectedDifficulty">
        /// El valor de dificultad que se espera para el encuentro, basado en la fila de la habitación y si es un jefe o no. Este valor se utiliza para calcular los pesos de selección de cada plantilla en el pool, favoreciendo aquellas cuya dificultad esté más cerca de este valor.
        /// </param>
        /// <returns>
        /// La plantilla de enemigo seleccionada del pool, elegida de manera aleatoria pero ponderada por la cercanía de su dificultad al valor esperado. Si el pool está vacío, se devuelve null. En caso de que todas las plantillas tengan un peso de selección igual a cero (lo cual es improbable pero posible), se seleccionará una plantilla al azar sin considerar los pesos.
        /// </returns>
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
        
        /// <summary>
        /// Crea una instancia de enemigo a partir de la plantilla dada, configurando su salud, maná y habilidades según lo definido en la plantilla. Si la plantilla no tiene una escena válida o si la instancia no se puede crear correctamente, se devuelve null. Este método se encarga de traducir los datos estáticos de la plantilla en un objeto de juego real que pueda ser utilizado en el encuentro.
        /// </summary>
        /// <param name="template">
        /// La plantilla de enemigo a partir de la cual se creará la instancia. Se espera que esta plantilla contenga una referencia a una escena válida que pueda ser instanciada, así como información sobre las habilidades que el enemigo debe tener. Si la plantilla es null o no tiene una escena válida, este método devolverá null.
        /// </param>
        /// <returns>
        /// Una instancia de enemigo creada a partir de la plantilla proporcionada, con su salud, maná y habilidades configurados según lo definido en la plantilla. Si la plantilla no es válida o si ocurre un error durante la instanciación, se devuelve null. Este método garantiza que el enemigo creado esté listo para ser utilizado en el encuentro, siempre y cuando la plantilla sea correcta y la escena pueda ser instanciada sin problemas.
        /// </returns>
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
