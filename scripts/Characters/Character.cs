using Godot;
using System.Collections.Generic;


namespace SpellsAndRooms.scripts.Characters
{
	public abstract partial class Character : AnimatedSprite2D
	{
		public enum DamageType
		{
			Fire,
			Water,
			Earth,
			Physical,
			None
		}

		[Export] public string CharacterName;
		[Export] public int BaseHealth;
		public int Health;
		[Export] public int BaseMana;
		public int Mana;
		[Export] public int Damage;
		[Export] public DamageType DamageResistance;
		[Export] public DamageType DamageWeakness;
		public bool IsAlive => Health > 0;

		private readonly List<Skill> _skills = new List<Skill>();
		public IReadOnlyList<Skill> Skills => _skills;

		public void TakeDamage(int amount)
		{
			Health = Mathf.Max(0, Health - Mathf.Max(0, amount));
		}

		public void Heal(int amount)
		{
			Health = Mathf.Clamp(Health + Mathf.Max(0, amount), 0, BaseHealth);
		}

		public bool ConsumeMana(int amount)
		{
			int safeAmount = Mathf.Max(0, amount);
			if (Mana < safeAmount)
				return false;

			Mana -= safeAmount;
			return true;
		}

		public void RestoreMana(int amount)
		{
			Mana = Mathf.Clamp(Mana + Mathf.Max(0, amount), 0, BaseMana);
		}

		public int CalculateAdjustedDamage(int amount, DamageType incomingType)
		{
			float multiplier = 1.0f;
			if (incomingType == DamageWeakness)
				multiplier = 1.5f;
			else if (incomingType == DamageResistance)
				multiplier = 0.6f;

			return Mathf.Max(1, Mathf.RoundToInt(amount * multiplier));
		}

		public void AddSkill(Skill skill)
		{
			if (skill == null || _skills.Contains(skill))
				return;

			if (_skills.Count >= 4)
				return;

			_skills.Add(skill);
		}

		public void RemoveSkill(Skill skill)
		{
			if (skill == null)
				return;

			_skills.Remove(skill);
		}

		protected Character(string name, int health, int baseHealth, int mana, int baseMana, int damage, DamageType damageResistance, DamageType damageWeakness)
		{
			CharacterName = name;
			Health = Mathf.Max(1, health);
			BaseHealth = Mathf.Max(1, baseHealth);
			Mana = Mathf.Max(0, mana);
			BaseMana = Mathf.Max(0, baseMana);
			Damage = Mathf.Max(1, damage);
			DamageResistance = damageResistance;
			DamageWeakness = damageWeakness;
		}
	}
}

