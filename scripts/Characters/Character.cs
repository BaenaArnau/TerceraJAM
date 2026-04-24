using Godot;
using System.Collections.Generic;

namespace SpellsAndRooms.scripts.Characters
{
	/// <summary>
	/// Clase base abstracta que representa un personaje en el juego.
	/// Gestiona estadísticas, habilidades, daño, salud y maná.
	/// </summary>
	public abstract partial class Character : AnimatedSprite2D
	{
		/// <summary>
		/// Enumeración que define los tipos de daño disponibles en el juego.
		/// </summary>
		public enum DamageType
		{
			/// <summary>Daño de fuego.</summary>
			Fire,
			/// <summary>Daño de agua.</summary>
			Water,
			/// <summary>Daño de tierra.</summary>
			Earth,
			/// <summary>Daño físico.</summary>
			Physical,
			/// <summary>Sin tipo de daño específico.</summary>
			None
		}

		// ==================== Constantes ====================

		/// <summary>Número máximo de habilidades que puede tener un personaje.</summary>
		private const int MAX_SKILLS = 4;

		/// <summary>Multiplicador de daño cuando el enemigo es débil al tipo de daño (50% más).</summary>
		private const float WEAKNESS_MULTIPLIER = 1.5f;

		/// <summary>Multiplicador de daño cuando el enemigo es resistente al tipo de daño (40% menos).</summary>
		private const float RESISTANCE_MULTIPLIER = 0.6f;

		/// <summary>Daño mínimo que puede causar un ataque.</summary>
		private const int MIN_DAMAGE = 1;

		/// <summary>Salud/Maná mínimo permitido.</summary>
		private const int MIN_STAT = 0;

		/// <summary>Salud/Maná mínimo inicial para un personaje vivo.</summary>
		private const int MIN_INITIAL_STAT = 1;

		// ==================== Propiedades Exportadas ====================

		/// <summary>Nombre del personaje.</summary>
		[Export] public string CharacterName;

		/// <summary>Salud máxima del personaje.</summary>
		[Export] public int BaseHealth;

		/// <summary>Maná máximo del personaje.</summary>
		[Export] public int BaseMana;

		/// <summary>Daño base que inflige el personaje.</summary>
		[Export] public int Damage;

		/// <summary>Tipo de daño al que el personaje tiene resistencia (reduce daño recibido).</summary>
		[Export] public DamageType DamageResistance;

		/// <summary>Tipo de daño al que el personaje es débil (aumenta daño recibido).</summary>
		[Export] public DamageType DamageWeakness;

		// ==================== Propiedades Privadas ====================

		/// <summary>Salud actual del personaje.</summary>
		private int _health;

		/// <summary>Maná actual del personaje.</summary>
		private int _mana;

		/// <summary>Lista de habilidades del personaje.</summary>
		private readonly List<Skill> _skills = new List<Skill>();

		// ==================== Propiedades Públicas ====================

		/// <summary>
		/// Obtiene o establece la salud actual del personaje.
		/// </summary>
		public int Health
		{
			get => _health;
			set => _health = Mathf.Clamp(value, MIN_STAT, BaseHealth);
		}

		/// <summary>
		/// Obtiene o establece el maná actual del personaje.
		/// </summary>
		public int Mana
		{
			get => _mana;
			set => _mana = Mathf.Clamp(value, MIN_STAT, BaseMana);
		}

		/// <summary>
		/// Obtiene la lista de solo lectura de habilidades del personaje.
		/// </summary>
		public IReadOnlyList<Skill> Skills => _skills;

		/// <summary>
		/// Obtiene un valor que indica si el personaje está vivo.
		/// </summary>
		public bool IsAlive => Health > 0;

		// ==================== Métodos ====================

		/// <summary>
		/// Aplica daño al personaje.
		/// </summary>
		/// <param name="amount">Cantidad de daño a aplicar. Se asegura que sea mayor o igual a 0.</param>
		public void TakeDamage(int amount)
		{
			int safeDamage = Mathf.Max(MIN_STAT, amount);
			Health -= safeDamage;
		}

		/// <summary>
		/// Restaura salud del personaje hasta el máximo.
		/// </summary>
		/// <param name="amount">Cantidad de salud a restaurar. Se asegura que sea mayor o igual a 0.</param>
		public void Heal(int amount)
		{
			int safeAmount = Mathf.Max(MIN_STAT, amount);
			Health += safeAmount;
		}

		/// <summary>
		/// Intenta consumir una cantidad específica de maná.
		/// </summary>
		/// <param name="amount">Cantidad de maná a consumir. Se asegura que sea mayor o igual a 0.</param>
		/// <returns>true si hay suficiente maná; false en caso contrario.</returns>
		public bool ConsumeMana(int amount)
		{
			int safeAmount = Mathf.Max(MIN_STAT, amount);
			if (Mana < safeAmount)
				return false;

			Mana -= safeAmount;
			return true;
		}

		/// <summary>
		/// Restaura maná del personaje hasta el máximo.
		/// </summary>
		/// <param name="amount">Cantidad de maná a restaurar. Se asegura que sea mayor o igual a 0.</param>
		public void RestoreMana(int amount)
		{
			int safeAmount = Mathf.Max(MIN_STAT, amount);
			Mana += safeAmount;
		}

		/// <summary>
		/// Calcula el daño ajustado según el tipo de daño y las resistencias/debilidades del personaje.
		/// </summary>
		/// <param name="amount">Cantidad de daño base.</param>
		/// <param name="incomingType">Tipo de daño entrante.</param>
		/// <returns>Cantidad de daño ajustada (mínimo 1).</returns>
		public int CalculateAdjustedDamage(int amount, DamageType incomingType)
		{
			float multiplier = GetDamageMultiplier(incomingType);
			int adjustedDamage = Mathf.RoundToInt(amount * multiplier);
			return Mathf.Max(MIN_DAMAGE, adjustedDamage);
		}

		/// <summary>
		/// Agrega una habilidad al personaje si no alcanzó el límite máximo y la habilidad no está duplicada.
		/// </summary>
		/// <param name="skill">Habilidad a agregar.</param>
		/// <returns>true si la habilidad fue agregada exitosamente; false en caso contrario.</returns>
		public bool AddSkill(Skill skill)
		{
			if (skill == null || _skills.Contains(skill))
				return false;

			if (_skills.Count >= MAX_SKILLS)
				return false;

			_skills.Add(skill);
			return true;
		}

		/// <summary>
		/// Remueve una habilidad del personaje.
		/// </summary>
		/// <param name="skill">Habilidad a remover.</param>
		/// <returns>true si la habilidad fue removida; false si no existía.</returns>
		public bool RemoveSkill(Skill skill)
		{
			if (skill == null)
				return false;

			return _skills.Remove(skill);
		}

		// ==================== Métodos Privados ====================

		/// <summary>
		/// Obtiene el multiplicador de daño basado en el tipo de daño y las resistencias/debilidades.
		/// </summary>
		/// <param name="incomingType">Tipo de daño entrante.</param>
		/// <returns>Multiplicador a aplicar al daño.</returns>
		private float GetDamageMultiplier(DamageType incomingType)
		{
			if (incomingType == DamageWeakness)
				return WEAKNESS_MULTIPLIER;
			
			if (incomingType == DamageResistance)
				return RESISTANCE_MULTIPLIER;
			
			return 1.0f;
		}

		// ==================== Constructor ====================

		/// <summary>
		/// Inicializa una nueva instancia de la clase Character.
		/// </summary>
		/// <param name="name">Nombre del personaje.</param>
		/// <param name="health">Salud inicial del personaje.</param>
		/// <param name="baseHealth">Salud máxima del personaje.</param>
		/// <param name="mana">Maná inicial del personaje.</param>
		/// <param name="baseMana">Maná máximo del personaje.</param>
		/// <param name="damage">Daño base del personaje.</param>
		/// <param name="damageResistance">Tipo de daño al que el personaje tiene resistencia.</param>
		/// <param name="damageWeakness">Tipo de daño al que el personaje es débil.</param>
		protected Character(string name, int health, int baseHealth, int mana, int baseMana, int damage, DamageType damageResistance, DamageType damageWeakness)
		{
			CharacterName = name;
			BaseHealth = Mathf.Max(MIN_INITIAL_STAT, baseHealth);
			Health = Mathf.Max(MIN_INITIAL_STAT, health);
			BaseMana = Mathf.Max(MIN_STAT, baseMana);
			Mana = Mathf.Max(MIN_STAT, mana);
			Damage = Mathf.Max(MIN_INITIAL_STAT, damage);
			DamageResistance = damageResistance;
			DamageWeakness = damageWeakness;
		}
	}
}

