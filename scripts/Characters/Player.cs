using System;
using System.Collections.Generic;
using Godot;
using SpellsAndRooms.scripts.Items;


namespace SpellsAndRooms.scripts.Characters
{
	public partial class Player : Character
	{
		public const int MaxSkillSlots = 4;
		public int Gold { get; private set; }
		public List<ConsumableItem> Consumables = new List<ConsumableItem>();
		public List<PassiveItem> Passives = new List<PassiveItem>();
		public int SkillCount => Skills.Count;

		public Player() : base("Heroe", 1, 1, 0, 0, 1, DamageType.Physical, DamageType.Fire)
		{
		}

		public Player(string name, int health, int baseHealth, int mana, int baseMana, int damage, DamageType damageResistance, DamageType damageWeakness) : base(name, health, baseHealth, mana, baseMana, damage, damageResistance, damageWeakness)
		{
		}

		public void AddGold(int amount)
		{
			Gold += Mathf.Max(0, amount);
		}
		
		public void RemoveGold(int amount)
		{
			Gold = Mathf.Max(0, Gold - Mathf.Max(0, amount));
		}

		public bool CanAfford(int amount)
		{
			return Gold >= Mathf.Max(0, amount);
		}

		public void AddConsumable(ConsumableItem item)
		{
			if (item == null)
				return;

			Consumables.Add(item);
		}

		public void AddPassive(PassiveItem item)
		{
			if (item == null)
				return;

			Passives.Add(item);
		}

		public bool HasSkill(string skillName)
		{
			if (string.IsNullOrWhiteSpace(skillName))
				return false;

			foreach (Skill skill in Skills)
			{
				if (skill != null && skill.Name.Equals(skillName, StringComparison.OrdinalIgnoreCase))
					return true;
			}

			return false;
		}

		public bool TryLearnSkill(Skill skill)
		{
			if (skill == null)
				return false;

			if (HasSkill(skill.Name) || SkillCount >= MaxSkillSlots)
				return false;

			AddSkill(skill);
			return HasSkill(skill.Name);
		}

		public bool TryReplaceSkill(int index, Skill newSkill)
		{
			if (newSkill == null || index < 0 || index >= Skills.Count)
				return false;

			for (int i = 0; i < Skills.Count; i++)
			{
				Skill existing = Skills[i];
				if (i != index && existing != null && existing.Name.Equals(newSkill.Name, StringComparison.OrdinalIgnoreCase))
					return false;
			}

			Skill toReplace = Skills[index];
			if (toReplace != null)
				RemoveSkill(toReplace);

			AddSkill(newSkill);
			return HasSkill(newSkill.Name);
		}

		public int GetTotalPassiveBonus(string passiveType)
		{
			if (string.IsNullOrWhiteSpace(passiveType))
				return 0;

			int total = 0;
			foreach (PassiveItem passive in Passives)
			{
				if (passive == null)
					continue;

				if ((passive.Type ?? string.Empty).Trim().Equals(passiveType, StringComparison.OrdinalIgnoreCase))
					total += Mathf.Max(0, passive.BonusValue);
			}

			return total;
		}

		public int ModifyOutgoingDamage(int baseDamage)
		{
			float bonusPercent = GetTotalPassiveBonus("weapon") / 100.0f;
			int modified = Mathf.RoundToInt(Mathf.Max(0, baseDamage) * (1.0f + bonusPercent));
			return Mathf.Max(1, modified);
		}

		public int ModifyIncomingDamage(int baseDamage)
		{
			float reductionPercent = Mathf.Clamp(GetTotalPassiveBonus("armor") / 100.0f, 0.0f, 0.8f);
			int modified = Mathf.RoundToInt(Mathf.Max(0, baseDamage) * (1.0f - reductionPercent));
			return baseDamage > 0 ? Mathf.Max(1, modified) : 0;
		}

		public bool TryUseConsumable(int index, out string log)
		{
			log = "";
			if (index < 0 || index >= Consumables.Count)
			{
				log = "Consumible invalido.";
				return false;
			}

			ConsumableItem consumable = Consumables[index];
			if (consumable == null)
			{
				Consumables.RemoveAt(index);
				log = "Consumible invalido.";
				return false;
			}

			int amount = Mathf.Max(1, consumable.Potency);
			string subtype = (consumable.Subtype ?? string.Empty).Trim().ToLowerInvariant();

			if (subtype.Contains("heal") || subtype.Contains("vida") || subtype.Contains("health"))
			{
				Heal(amount);
				Consumables.RemoveAt(index);
				log = $"{CharacterName} usa {consumable.ItemName} y recupera {amount} de vida.";
				return true;
			}

			if (subtype.Contains("mana"))
			{
				RestoreMana(amount);
				Consumables.RemoveAt(index);
				log = $"{CharacterName} usa {consumable.ItemName} y recupera {amount} de mana.";
				return true;
			}

			log = $"{consumable.ItemName} no tiene efecto en combate.";
			return false;
		}

		public override void _Ready()
		{
			base._Ready();
			Health = BaseHealth;
			Mana = BaseMana;
		}
	}
}