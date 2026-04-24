using Godot;

namespace SpellsAndRooms.scripts.Characters
{
	/// <summary>
	/// Clase que representa una habilidad o hechizo que un personaje puede usar en combate. Incluye propiedades
	/// </summary>
	public class Skill
	{
		/// <summary>
		/// El nombre de la habilidad. Este nombre se mostrará en la interfaz de usuario y puede ser utilizado para identificar la habilidad en el juego. Si no se proporciona un nombre válido, se usará "Attack" como valor predeterminado.
		/// </summary>
		public string Name { get; }
		
		/// <summary>
		/// El costo de maná requerido para usar esta habilidad. Este valor debe ser un entero no negativo, donde 0 indica que la habilidad no consume maná. El sistema de combate verificará que el personaje
		/// </summary>
		public int ManaCost { get; }
		
		/// <summary>
		/// El daño base que esta habilidad inflige al objetivo. Este valor debe ser un entero positivo, donde valores más altos indican habilidades más poderosas. El daño real infligido durante el combate puede ser modificado por resistencias, debilidades y otros factores, pero este valor representa la cantidad base de daño antes de aplicar cualquier modificación.
		/// </summary>
		public int Damage { get; }
		
		/// <summary>
		/// El tipo de daño que esta habilidad inflige. Este valor se utiliza para calcular el daño recibido durante el combate, aplicando multiplicadores de resistencia o debilidad según corresponda. Si la habilidad no tiene un tipo de daño específico, este valor debe ser DamageType.None.
		/// </summary>
		public Character.DamageType DamageType { get; }
		
		/// <summary>
		/// Indica si esta habilidad es un ataque múltiple, lo que significa que puede afectar a varios objetivos en lugar de solo uno. Si este valor es true, el sistema de combate permitirá que la habilidad se aplique a múltiples enemigos o aliados según corresponda. Si es false, la habilidad solo afectará a un objetivo específico.
		/// </summary>
		public bool MultiAttack { get; }
		
		/// <summary>
		/// Indica si esta habilidad es una habilidad de curación en lugar de un ataque. Si este valor es true, la habilidad restaurará salud a los objetivos en lugar de infligir daño. Si es false, la habilidad se considerará un ataque que inflige daño. Este valor afecta cómo se calcula el efecto de la habilidad durante el combate, aplicando las reglas de curación en lugar de las reglas de daño cuando corresponda.
		/// </summary>
		public bool IsHealing { get; }

		/// <summary>
		/// Constructor que permite inicializar una habilidad con valores específicos para todas sus propiedades. Este constructor garantiza que los valores proporcionados sean válidos, aplicando restricciones como no permitir costos de maná negativos o daños menores que 1. Si el nombre proporcionado es nulo o solo contiene espacios en blanco, se usará "Attack" como valor predeterminado.
		/// </summary>
		/// <param name="name">
		/// El nombre de la habilidad. Este nombre se mostrará en la interfaz de usuario y puede ser utilizado para identificar la habilidad en el juego. Si no se proporciona un nombre válido, se usará "Attack" como valor predeterminado.
		/// </param>
		/// <param name="manaCost">
		/// El costo de maná requerido para usar esta habilidad. Este valor debe ser un entero no negativo, donde 0 indica que la habilidad no consume maná. El sistema de combate verificará que el personaje
		/// </param>
		/// <param name="damage">
		/// El daño base que esta habilidad inflige al objetivo. Este valor debe ser un entero positivo, donde valores más altos indican habilidades más poderosas. El daño real infligido durante el combate puede ser modificado por resistencias, debilidades y otros factores, pero este valor representa la cantidad base de daño antes de aplicar cualquier modificación.
		/// </param>
		/// <param name="damageType">
		/// El tipo de daño que esta habilidad inflige. Este valor se utiliza para calcular el daño recibido durante el combate, aplicando multiplicadores de resistencia o debilidad según corresponda. Si la habilidad no tiene un tipo de daño específico, este valor debe ser DamageType.None.
		/// </param>
		/// <param name="multiAttack">
		/// Indica si esta habilidad es un ataque múltiple, lo que significa que puede afectar a varios objetivos en lugar de solo uno. Si este valor es true, el sistema de combate permitirá que la habilidad se aplique a múltiples enemigos o aliados según corresponda. Si es false, la habilidad solo afectará a un objetivo específico.
		/// </param>
		/// <param name="isHealing">
		/// Indica si esta habilidad es una habilidad de curación en lugar de un ataque. Si este valor es true, la habilidad restaurará salud a los objetivos en lugar de infligir daño. Si es false, la habilidad se considerará un ataque que inflige daño. Este valor afecta cómo se calcula el efecto de la habilidad durante el combate, aplicando las reglas de curación en lugar de las reglas de daño cuando corresponda.
		/// </param>
		public Skill(string name, int manaCost, int damage, Character.DamageType damageType, bool multiAttack = false, bool isHealing = false)
		{
			Name = string.IsNullOrWhiteSpace(name) ? "Attack" : name;
			ManaCost = Mathf.Max(0, manaCost);
			Damage = Mathf.Max(1, damage);
			DamageType = damageType;
			MultiAttack = multiAttack;
			IsHealing = isHealing;
		}
	}
}

