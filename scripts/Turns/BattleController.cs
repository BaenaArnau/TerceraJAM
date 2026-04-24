using System.Collections.Generic;
using System.Linq;
using SpellsAndRooms.scripts.Characters;

namespace SpellsAndRooms.scripts.Turns
{
    public sealed class BattleController
    {
        public bool HasAliveEnemies(List<Enemy> enemies)
        {
            return enemies != null && enemies.Any(e => e.IsAlive);
        }

        public string PlayerBasicAttack(Player player, Enemy target)
        {
            if (player == null || target == null || !target.IsAlive)
                return "Objetivo invalido.";

            int outgoing = player.ModifyOutgoingDamage(player.Damage);
            int applied = target.CalculateAdjustedDamage(outgoing, Character.DamageType.Physical);
            target.TakeDamage(applied);
            return $"{player.CharacterName} ataca a {target.CharacterName} por {applied} (vida restante: {target.Health}).";
        }

        public string PlayerUseSkill(Player player, Skill skill, Enemy target = null, List<Enemy> enemies = null)
        {
            if (player == null || skill == null)
                return "Accion invalida.";

            if (!player.ConsumeMana(skill.ManaCost))
                return $"{player.CharacterName} no tiene mana para {skill.Name}.";

            if (skill.IsHealing)
            {
                player.Heal(skill.Damage);
                return $"{player.CharacterName} usa {skill.Name} y se cura {skill.Damage} (vida: {player.Health}).";
            }

            if (skill.MultiAttack && enemies != null && enemies.Any(e => e != null && e.IsAlive))
            {
                var logs = new List<string>();
                foreach (Enemy enemy in enemies.Where(e => e != null && e.IsAlive))
                {
                    int outgoing = player.ModifyOutgoingDamage(skill.Damage + player.Damage / 2);
                    int applied = enemy.CalculateAdjustedDamage(outgoing, skill.DamageType);
                    enemy.TakeDamage(applied);
                    logs.Add($"{player.CharacterName} usa {skill.Name} sobre {enemy.CharacterName}: {applied} dano. Vida objetivo: {enemy.Health}.");
                }

                return string.Join("\n", logs);
            }

            if (target == null || !target.IsAlive)
                return "No hay objetivo valido para la habilidad.";

            return ApplySkill(player, target, skill, sourceIsPlayer: true);
        }

        public List<string> ExecuteEnemyTurn(List<Enemy> enemies, Player player)
        {
            var logs = new List<string>();
            if (enemies == null || player == null || !player.IsAlive)
            {
                return logs;
            }

            foreach (Enemy enemy in enemies)
            {
                if (!enemy.IsAlive || !player.IsAlive)
                    continue;

                Skill skill = enemy.Skills.FirstOrDefault(s => enemy.Mana >= s.ManaCost && (!s.IsHealing || enemy.Health < enemy.BaseHealth / 2));
                if (skill != null && enemy.ConsumeMana(skill.ManaCost))
                {
                    logs.Add(skill.IsHealing
                        ? EnemySelfHeal(enemy, skill)
                        : ApplySkill(enemy, player, skill));
                    continue;
                }

                int applied = player.CalculateAdjustedDamage(enemy.Damage, Character.DamageType.Physical);
                applied = player.ModifyIncomingDamage(applied);
                player.TakeDamage(applied);
                logs.Add($"{enemy.CharacterName} golpea a {player.CharacterName} por {applied} (vida restante: {player.Health}).");
            }

            return logs;
        }

        public BattleResult BuildResult(Player player, List<Enemy> enemies)
        {
            int gold = enemies?.Where(e => !e.IsAlive).Sum(e => e.MoneyLoot) ?? 0;
            bool won = player != null && player.IsAlive && (enemies == null || enemies.All(e => !e.IsAlive));

            if (won)
                player.AddGold(gold);

            return new BattleResult
            {
                PlayerWon = won,
                EarnedGold = won ? gold : 0
            };
        }

        public BattleResult Resolve(Player player, List<Enemy> enemies)
        {
            var result = new BattleResult();
            if (player == null || enemies == null || enemies.Count == 0)
            {
                result.Log.Add("No hay enemigos en esta sala.");
                return result;
            }

            int round = 1;
            while (player.IsAlive && enemies.Any(e => e.IsAlive))
            {
                result.Log.Add($"-- Ronda {round} --");
                ExecutePlayerTurn(player, enemies, result.Log);

                foreach (Enemy enemy in enemies)
                {
                    if (!player.IsAlive)
                        break;

                    if (!enemy.IsAlive)
                        continue;

                    result.Log.AddRange(ExecuteEnemyTurn(enemies, player));
                    break;
                }

                round++;
                if (round > 100)
                {
                    result.Log.Add("Combate cortado por seguridad (100 rondas).");
                    break;
                }
            }

            BattleResult final = BuildResult(player, enemies);
            result.PlayerWon = final.PlayerWon;
            result.EarnedGold = final.EarnedGold;
            result.Log.Add(player.IsAlive
                ? $"Victoria. Oro ganado: {result.EarnedGold}. Oro total: {player.Gold}."
                : "Derrota. El jugador ha caido.");

            return result;
        }

        private static void ExecutePlayerTurn(Player player, List<Enemy> enemies, List<string> log)
        {
            Enemy target = enemies.FirstOrDefault(e => e.IsAlive);
            if (target == null)
                return;

            Skill chosenSkill = ChooseBestSkill(player, target);
            if (chosenSkill != null && player.ConsumeMana(chosenSkill.ManaCost))
            {
                if (chosenSkill.MultiAttack)
                {
                    log.Add($"{player.CharacterName} usa {chosenSkill.Name} contra todos los enemigos.");
                    foreach (Enemy enemy in enemies.Where(e => e.IsAlive))
                    {
                        int rawDamage = player.ModifyOutgoingDamage(chosenSkill.Damage + player.Damage / 2);
                        int areaDamage = enemy.CalculateAdjustedDamage(rawDamage, chosenSkill.DamageType);
                        enemy.TakeDamage(areaDamage);
                        log.Add($"  -> {enemy.CharacterName} recibe {areaDamage} dano (vida restante: {enemy.Health}).");
                    }
                }
                else
                    log.Add(ApplySkill(player, target, chosenSkill, sourceIsPlayer: true));

                return;
            }

            int outgoing = player.ModifyOutgoingDamage(player.Damage);
            int applied = target.CalculateAdjustedDamage(outgoing, Character.DamageType.Physical);
            target.TakeDamage(applied);
            log.Add($"{player.CharacterName} ataca a {target.CharacterName} por {applied} (vida restante: {target.Health}).");
        }

        private static Skill ChooseBestSkill(Player player, Enemy target)
        {
            return player.Skills
                .Where(s => !s.IsHealing && player.Mana >= s.ManaCost)
                .OrderByDescending(s => s.DamageType == target.DamageWeakness)
                .ThenByDescending(s => s.MultiAttack)
                .ThenByDescending(s => s.Damage)
                .FirstOrDefault();
        }

        private static string EnemySelfHeal(Enemy enemy, Skill skill)
        {
            enemy.Heal(skill.Damage);
            return $"{enemy.CharacterName} usa {skill.Name} y se cura {skill.Damage} (vida: {enemy.Health}).";
        }

        private static string ApplySkill(Character source, Character target, Skill skill, bool sourceIsPlayer = false)
        {
            if (skill.IsHealing)
            {
                source.Heal(skill.Damage);
                return $"{source.CharacterName} usa {skill.Name} y se cura {skill.Damage} (vida: {source.Health}).";
            }

            int rawDamage = skill.Damage + source.Damage / 2;
            if (sourceIsPlayer && source is Player playerSource)
                rawDamage = playerSource.ModifyOutgoingDamage(rawDamage);

            int applied = target.CalculateAdjustedDamage(rawDamage, skill.DamageType);
            if (target is Player playerTarget)
                applied = playerTarget.ModifyIncomingDamage(applied);

            target.TakeDamage(applied);

            string affinityText = "normal";
            if (skill.DamageType == target.DamageWeakness)
            {
                affinityText = "debilidad";
            }
            else if (skill.DamageType == target.DamageResistance)
            {
                affinityText = "resistencia";
            }

            return $"{source.CharacterName} usa {skill.Name} sobre {target.CharacterName}: {applied} dano ({affinityText}). Vida objetivo: {target.Health}.";
        }
    }
}

