using System;
using System.Collections.Generic;
using Godot;
using SpellsAndRooms.scripts.Items;

namespace SpellsAndRooms.scripts.Characters
{
	/// <summary>
	/// Clase que representa al jugador en el juego. Hereda de Character e incluye propiedades específicas como oro, consumibles, objetos pasivos y gestión de habilidades.
	/// </summary>
	public partial class Player : Character
	{
		/// <summary>
		/// Número máximo de habilidades que el jugador puede tener equipadas al mismo tiempo. Este límite se aplica tanto al aprender nuevas habilidades como al reemplazar habilidades existentes, asegurando que el jugador no pueda superar este número de habilidades activas.
		/// </summary>
		public const int MaxSkillSlots = 4;
		
		/// <summary>
		/// Cantidad de oro que el jugador posee. El oro se utiliza como moneda para comprar objetos, habilidades y otros recursos dentro del juego. El jugador puede ganar oro al derrotar enemigos, completar misiones o encontrar tesoros, y puede gastarlo en tiendas o con NPCs para mejorar su equipo y habilidades. La cantidad de oro no puede ser negativa, y el jugador debe tener suficiente oro para
		/// </summary>
		public int Gold { get; private set; }
		
		/// <summary>
		/// Lista de consumibles que el jugador tiene en su inventario. Los consumibles son objetos que el jugador puede usar durante el combate para obtener beneficios temporales, como curar salud, restaurar maná o aplicar efectos positivos. Cada consumible tiene un tipo y una potencia que determinan su efecto específico. El jugador puede tener múltiples consumibles en su inventario, pero solo puede usar uno por turno.
		/// </summary>
		public List<ConsumableItem> Consumables = new List<ConsumableItem>();
		
		/// <summary>
		/// Lista de objetos pasivos que el jugador ha adquirido. Los objetos pasivos proporcionan bonificaciones permanentes o efectos especiales al jugador, como aumento de daño, reducción de daño recibido, regener
		/// </summary>
		public List<PassiveItem> Passives = new List<PassiveItem>();
		
		/// <summary>
		/// Lista de habilidades que el jugador tiene equipadas. El jugador puede aprender nuevas habilidades a lo largo del juego, pero solo puede tener un número limitado de habilidades equipadas al mismo tiempo (definido por MaxSkillSlots). Las habilidades equipadas son las que el jugador puede usar durante el combate, y cada habilidad tiene un tipo de daño, un costo de maná y un efecto específico. El jugador puede reemplazar habilidades existentes con nuevas habilidades aprendidas, siempre respetando el límite de habilidades equipadas.
		/// </summary>
		public int SkillCount => Skills.Count;

		/// <summary>
		/// Constructor por defecto que inicializa al jugador con valores predeterminados. Estos valores pueden ser sobrescritos al crear una instancia específica del jugador, pero proporcionan una base inicial para el personaje principal del juego.
		/// </summary>
		public Player() : base("Heroe", 1, 1, 0, 0, 1, DamageType.Physical, DamageType.Fire)
		{
		}

		/// <summary>
		/// Constructor que permite inicializar al jugador con valores específicos para todas sus propiedades, incluyendo las heredadas de Character y las propias de Player. Este constructor es útil para crear una instancia del jugador con estadísticas personalizadas, como al cargar un juego guardado o al configurar el personaje al inicio del juego.
		/// </summary>
		/// <param name="name">
		/// El nombre del jugador. Este nombre se mostrará en la interfaz de usuario y puede ser utilizado para identificar al jugador en el juego. El nombre no debe ser nulo ni vacío, y se recomienda que sea descriptivo para mejorar la inmersión del jugador.
		/// </param>
		/// <param name="health">
		/// La salud actual del jugador. Este valor puede ser modificado durante el combate y no debe exceder el valor de baseHealth ni ser menor que 0. La salud representa la cantidad de daño que el jugador puede recibir antes de ser derrotado, y es una estadística crucial para la supervivencia del personaje.
		/// </param>
		/// <param name="baseHealth">
		/// La salud máxima del jugador. Este valor define el límite superior de la salud actual y se utiliza para calcular la salud restante durante el combate. No debe ser menor que 1, ya que un jugador con baseHealth de 0 o menos no podría sobrevivir en el juego.
		/// </param>
		/// <param name="mana">
		/// La cantidad actual de maná del jugador. Este valor puede ser modificado durante el combate y no debe exceder el valor de baseMana ni ser menor que 0. El maná se utiliza para activar habilidades y efectos especiales, y es una estadística importante para la gestión de recursos durante el combate.
		/// </param>
		/// <param name="baseMana">
		/// La cantidad máxima de maná del jugador. Este valor define el límite superior del maná actual y se utiliza
		/// </param>
		/// <param name="damage">
		/// El daño base que el jugador inflige con sus ataques. Este valor puede ser modificado por habilidades, resistencias y debilidades durante el combate, pero no debe ser menor que 1. El daño representa la cantidad de daño que el jugador puede infligir a los enemigos, y es una estadística clave para la ofensiva del personaje.
		/// </param>
		/// <param name="damageResistance">
		/// El tipo de daño al que el jugador tiene resistencia. Este valor se utiliza para calcular el daño recibido durante el combate, aplicando un multiplicador de resistencia si el ataque coincide con este tipo. Si el jugador no tiene resistencia a ningún tipo de daño, este valor debe ser DamageType.None. La resistencia puede ser una parte importante de la estrategia del jugador para sobrevivir a enemigos específicos.
		/// </param>
		/// <param name="damageWeakness">
		/// El tipo de daño al que el jugador es débil. Este valor se utiliza para calcular el daño recibido durante el combate, aplicando un multiplicador de debilidad si el ataque coincide con este tipo. Si el jugador no tiene debilidad a ningún tipo de daño, este valor debe ser DamageType.None. La debilidad puede representar una vulnerabilidad que el jugador debe tener en cuenta al enfrentarse a ciertos enemigos o al elegir habilidades.
		/// </param>
		public Player(string name, int health, int baseHealth, int mana, int baseMana, int damage, DamageType damageResistance, DamageType damageWeakness) : base(name, health, baseHealth, mana, baseMana, damage, damageResistance, damageWeakness)
		{
		}
		
		/// <summary>
		/// Agrega una cantidad específica de oro al jugador. El oro se utiliza como moneda para comprar objetos, habilidades y otros recursos dentro del juego. Este método asegura que la cantidad de oro no sea negativa, por lo que si se intenta agregar una cantidad negativa, simplemente no se realizará ningún cambio en el oro del jugador.
		/// </summary>
		/// <param name="amount">
		/// La cantidad de oro a agregar al jugador. Este valor debe ser un entero no negativo, donde valores más altos indican una mayor cantidad de oro. Si se proporciona un valor negativo, este método no realizará ningún cambio en el oro del jugador, asegurando que la cantidad de oro siempre sea cero o positiva.
		/// </param>
		public void AddGold(int amount)
		{
			Gold += Mathf.Max(0, amount);
		}
		
		/// <summary>
		/// Resta una cantidad específica de oro al jugador. Este método asegura que la cantidad de oro no sea negativa, por lo que si se intenta restar una cantidad mayor a la que el jugador tiene, el oro del jugador se establecerá en cero en lugar de volverse negativo. Además, si se proporciona una cantidad negativa para restar, este método no realizará ningún cambio en el oro del jugador.
		/// </summary>
		/// <param name="amount">
		/// La cantidad de oro a restar al jugador. Este valor debe ser un entero no negativo, donde valores más altos indican una mayor cantidad de oro a restar. Si se proporciona un valor negativo, este método no realizará ningún cambio en el oro del jugador. Si se intenta restar una cantidad mayor a la que el jugador tiene, el oro del jugador se establecerá en cero, asegurando que la cantidad de oro siempre sea cero o positiva.
		/// </param>
		public void RemoveGold(int amount)
		{
			Gold = Mathf.Max(0, Gold - Mathf.Max(0, amount));
		}

		/// <summary>
		/// Verifica si el jugador tiene suficiente oro para pagar una cantidad específica. Este método se utiliza para determinar si el jugador puede realizar compras o pagar costos asociados con habilidades, objetos o eventos dentro del juego. Si la cantidad proporcionada es negativa, este método considerará que el jugador siempre puede pagar, ya que no se requiere oro para una cantidad negativa.
		/// </summary>
		/// <param name="amount">
		/// La cantidad de oro que se desea verificar si el jugador puede pagar. Este valor debe ser un entero, donde valores positivos indican una cantidad de oro a pagar. Si se proporciona un valor negativo, este método considerará que el jugador siempre puede pagar, ya que no se requiere oro para una cantidad negativa. Si el jugador tiene suficiente oro para cubrir la cantidad especificada, este método devolverá true; de lo contrario, devolverá false.
		/// </param>
		/// <returns>
		/// true si el jugador tiene suficiente oro para pagar la cantidad especificada; false en caso contrario. Si se proporciona una cantidad negativa, este método devolverá true, ya que no se requiere oro para una cantidad negativa.
		/// </returns>
		public bool CanAfford(int amount)
		{
			return Gold >= Mathf.Max(0, amount);
		}

		/// <summary>
		/// Agrega un consumible al inventario del jugador. Este método verifica que el objeto consumible no sea nulo antes de agregarlo a la lista de consumibles del jugador. Los consumibles son objetos que el jugador puede usar durante el combate para obtener beneficios temporales, como cur
		/// </summary>
		/// <param name="item">
		/// El consumible a agregar al inventario del jugador. Este objeto debe ser una instancia válida de ConsumableItem, que contiene información sobre el tipo de consumible, su potencia y otros atributos relevantes. Si se proporciona un valor nulo, este método no realizará ningún cambio en el inventario del jugador, asegurando que solo se agreguen consumibles válidos a la lista.
		/// </param>
		public void AddConsumable(ConsumableItem item)
		{
			if (item == null)
				return;

			Consumables.Add(item);
		}

		/// <summary>
		/// Agrega un objeto pasivo al jugador. Este método verifica que el objeto pasivo no sea nulo antes de agregarlo a la lista de objetos pasivos del jugador. Los objetos pasivos proporcionan bonificaciones permanentes o efectos especiales al jugador, como aumento de daño, reducción de daño recibido, regener
		/// </summary>
		/// <param name="item">
		/// El objeto pasivo a agregar al jugador. Este objeto debe ser una instancia válida de PassiveItem, que contiene información sobre el tipo de bonificación que proporciona, su valor y otros atributos relevantes. Si se proporciona un valor nulo, este método no realizará ningún cambio en la lista de objetos pasivos del jugador, asegurando que solo se agreguen objetos pasivos válidos a la lista.
		/// </param>
		public void AddPassive(PassiveItem item)
		{
			if (item == null)
				return;

			Passives.Add(item);
		}

		/// <summary>
		/// Verifica si el jugador tiene una habilidad específica equipada. Este método compara el nombre de la habilidad proporcionada con los nombres de las habilidades equipadas por el jugador, ignorando mayúsculas y espacios en blanco. Si se encuentra una coincidencia, este método devuelve true; de lo contrario, devuelve false. Si se proporciona un nombre de habilidad nulo o vacío, este método devolverá false, ya que no se puede buscar una habilidad sin un nombre válido.
		/// </summary>
		/// <param name="skillName">
		/// El nombre de la habilidad que se desea verificar si el jugador tiene equipada. Este valor debe ser una cadena no nula ni vacía, que representa el nombre de la habilidad a buscar. La comparación se realiza ignorando mayúsculas y espacios en blanco, lo que permite cierta flexibilidad en la forma en que se ingresan los nombres de las habilidades. Si se proporciona un valor nulo o vacío, este método devolverá false, ya que no se puede buscar una habilidad sin un nombre válido.
		/// </param>
		/// <returns>
		/// true si el jugador tiene una habilidad equipada que coincide con el nombre proporcionado; false en caso contrario. La comparación se realiza de manera insensible a mayúsculas y espacios en blanco, lo que permite que el jugador ingrese el nombre de la habilidad de diferentes formas sin afectar el resultado de la búsqueda. Si se proporciona un nombre de habilidad nulo o vacío, este método devolverá false.
		/// </returns>
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
		
		/// <summary>
		/// Intenta aprender una nueva habilidad. Este método verifica que la habilidad no sea nula, que el jugador no tenga ya una habilidad con el mismo nombre equipada y que el número de habilidades equipadas no haya alcanzado el límite máximo definido por MaxSkillSlots. Si la habilidad se puede aprender, se agrega a la lista de habilidades del jugador y se devuelve true; de lo contrario, se devuelve false sin realizar ningún cambio en las habilidades del jugador.
		/// </summary>
		/// <param name="skill">
		/// La habilidad que se desea aprender. Este objeto debe ser una instancia válida de Skill, que contiene información sobre el nombre de la habilidad, su tipo de daño, costo de maná y otros atributos relevantes. Si se proporciona un valor nulo, este método no realizará ningún cambio en las habilidades del jugador y devolverá false. Además, si el jugador ya tiene una habilidad con el mismo nombre equipada o si el número de habilidades equipadas ha alcanzado el límite máximo, este método también devolverá false sin realizar ningún cambio.
		/// </param>
		/// <returns>
		/// true si la habilidad se aprendió exitosamente y ahora está equipada por el jugador; false en caso contrario. Para que una habilidad se aprenda exitosamente, debe ser una instancia válida de Skill, el jugador no debe tener ya una habilidad con el mismo nombre equipada y el número de habilidades equipadas no debe haber alcanzado el límite máximo definido por MaxSkillSlots. Si alguna de estas condiciones no se cumple, este método devolverá false sin realizar ningún cambio en las habilidades del jugador.
		/// </returns>
		public bool TryLearnSkill(Skill skill)
		{
			if (skill == null)
				return false;

			if (HasSkill(skill.Name) || SkillCount >= MaxSkillSlots)
				return false;

			AddSkill(skill);
			return HasSkill(skill.Name);
		}
		/// <summary>
		/// Intenta reemplazar una habilidad equipada en un índice específico con una nueva habilidad. Este método verifica que la nueva habilidad no sea nula, que el índice sea válido dentro de la lista de habilidades del jugador, que el jugador no tenga ya una habilidad con el mismo nombre equipada (excluyendo la habilidad que se desea reemplazar) y que el número de habilidades equipadas no haya alcanzado el límite máximo definido por MaxSkillSlots. Si la habilidad se puede reemplazar, se elimina la habilidad existente en el índice especificado (si existe) y se agrega la nueva habilidad a la lista de habilidades del jugador, devolviendo true; de lo contrario, se devuelve false sin realizar ningún cambio en las habilidades del jugador.
		/// </summary>
		/// <param name="index">
		/// El índice de la habilidad equipada que se desea reemplazar. Este valor debe ser un entero que representa la posición de la habilidad en la lista de habilidades del jugador. El índice debe ser válido dentro de la lista de habilidades, es decir, debe ser mayor o igual a 0 y menor que el número de habilidades equipadas. Si se proporciona un índice fuera de este rango, este método devolverá false sin realizar ningún cambio en las habilidades del jugador.
		/// </param>
		/// <param name="newSkill">
		/// La nueva habilidad que se desea equipar en lugar de la habilidad existente en el índice especificado. Este objeto debe ser una instancia válida de Skill, que contiene información sobre el nombre de la habilidad, su tipo de daño, costo de maná y otros atributos relevantes. Si se proporciona un valor nulo, este método no realizará ningún cambio en las habilidades del jugador y devolverá false. Además, si el jugador ya tiene una habilidad con el mismo nombre equipada (excluyendo la habilidad que se desea reemplazar) o si el número de habilidades equipadas ha alcanzado el límite máximo definido por MaxSkillSlots, este método también devolverá false sin realizar ningún cambio.
		/// </param>
		/// <returns>
		/// true si la habilidad fue reemplazada exitosamente y la nueva habilidad ahora está equipada por el jugador; false en caso contrario. Para que una habilidad sea reemplazada exitosamente, la nueva habilidad debe ser una instancia válida de Skill, el índice debe ser válido dentro de la lista de habilidades del jugador, el jugador no debe tener ya una habilidad con el mismo nombre equipada (excluyendo la habilidad que se desea reemplazar) y el número de habilidades equipadas no debe haber alcanzo el límite máximo definido por MaxSkillSlots. Si alguna de estas condiciones no se cumple, este método devolverá false sin realizar ningún cambio en las habilidades del jugador. Si la habilidad se reemplaza exitosamente, la habilidad existente en el índice especificado (si existe) se eliminará y la nueva habilidad se agregará a la lista de habilidades del jugador, asegurando que el jugador tenga la nueva habilidad equipada en lugar de la anterior.
		/// </returns>
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

		/// <summary>
		/// Calcula el bono total proporcionado por los objetos pasivos del jugador para un tipo específico de bonificación. Este método recorre la lista de objetos pasivos del jugador y suma los valores de bonificación de aquellos objetos que coincidan con el tipo de bonificación especificado, ignorando mayúsculas y espacios en blanco. Si se proporciona un tipo de bonificación nulo o vacío, este método devolverá 0, ya que no se puede calcular un bono sin un tipo válido.
		/// </summary>
		/// <param name="passiveType">
		/// El tipo de bonificación para el cual se desea calcular el bono total proporcionado por los objetos pasivos del jugador. Este valor debe ser una cadena no nula ni vacía, que representa el tipo de bonificación a buscar en los objetos pasivos. La comparación se realiza ignorando mayúsculas y espacios en blanco, lo que permite cierta flexibilidad en la forma en que se ingresan los tipos de bonificación. Si se proporciona un valor nulo o vacío, este método devolverá 0, ya que no se puede calcular un bono sin un tipo válido.
		/// </param>
		/// <returns>
		/// El bono total proporcionado por los objetos pasivos del jugador para el tipo de bonificación especificado. Este valor se calcula sumando los valores de bonificación de todos los objetos pasivos que coincidan con el tipo de bonificación proporcionado, ignorando mayúsculas y espacios en blanco. Si no hay objetos pasivos que coincidan con el tipo de bonificación o si se proporciona un tipo de bonificación nulo o vacío, este método devolverá 0, indicando que no hay ningún bono aplicado para ese tipo.
		/// </returns>
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
		
		/// <summary>
		/// Modifica el daño de salida del jugador aplicando los bonos proporcionados por los objetos pasivos relacionados con el tipo "weapon". Este método calcula el bono total para el tipo "weapon" utilizando el método GetTotalPassiveBonus y luego aplica ese bono como un porcentaje al daño base. El resultado se redondea al entero más cercano y se asegura que sea al menos 1, garantizando que el jugador siempre inflija al menos 1 punto de daño, incluso si el bono es negativo o si el daño base es bajo.
		/// </summary>
		/// <param name="baseDamage">
		/// La cantidad de daño base que se desea modificar. Este valor debe ser un entero, donde valores más altos indican un mayor daño base. El daño base representa la cantidad de daño que el jugador infligiría sin considerar los bonos proporcionados por los objetos pasivos. Si se proporciona un valor negativo, este método lo tratará como 0, asegurando que el daño modificado no sea negativo.
		/// </param>
		/// <returns>
		/// La cantidad de daño modificada después de aplicar los bonos proporcionados por los objetos pasivos relacionados con el tipo "weapon". Este valor se calcula aplicando el bono total como un porcentaje al daño base, redondeando el resultado al entero más cercano y asegurando que sea al menos 1. Si el daño base es negativo, se tratará como 0, lo que significa que el jugador infligirá al menos 1 punto de daño incluso si el bono es negativo o si el daño base es bajo.
		/// </returns>
		public int ModifyOutgoingDamage(int baseDamage)
		{
			float bonusPercent = GetTotalPassiveBonus("weapon") / 100.0f;
			int modified = Mathf.RoundToInt(Mathf.Max(0, baseDamage) * (1.0f + bonusPercent));
			return Mathf.Max(1, modified);
		}
		
		/// <summary>
		/// Modifica el daño entrante al jugador aplicando los bonos de reducción proporcionados por los objetos pasivos relacionados con el tipo "armor". Este método calcula el porcentaje de reducción total para el tipo "armor" utilizando el método GetTotalPassiveBonus y luego aplica esa reducción al daño base. El resultado se redondea al entero más cercano y se asegura que sea al menos 1 si el daño base es positivo, garantizando que el jugador siempre reciba al menos 1 punto de daño si el ataque es efectivo, incluso si la reducción es alta o si el daño base es bajo. Si el daño base es negativo o cero, este método devolverá 0, ya que no tendría sentido aplicar una reducción a un ataque que no inflige daño.
		/// </summary>
		/// <param name="baseDamage">
		/// La cantidad de daño base que se desea modificar. Este valor debe ser un entero, donde valores más altos indican un mayor daño base. El daño base representa la cantidad de daño que el jugador recibiría sin considerar los bonos de reducción proporcionados por los objetos pasivos. Si se proporciona un valor negativo o cero, este método devolverá 0, ya que no tendría sentido aplicar una reducción a un ataque que no inflige daño.
		/// </param>
		/// <returns>
		/// La cantidad de daño modificada después de aplicar los bonos de reducción proporcionados por los objetos pasivos relacionados con el tipo "armor". Este valor se calcula aplicando el porcentaje de reducción al daño base, redondeando el resultado al entero más cercano y asegurando que sea al menos 1 si el daño base es positivo. Si el daño base es negativo o cero, este método devolverá 0, indicando que no se aplica ninguna reducción a un ataque que no inflige daño.
		/// </returns>
		public int ModifyIncomingDamage(int baseDamage)
		{
			float reductionPercent = Mathf.Clamp(GetTotalPassiveBonus("armor") / 100.0f, 0.0f, 0.8f);
			int modified = Mathf.RoundToInt(Mathf.Max(0, baseDamage) * (1.0f - reductionPercent));
			return baseDamage > 0 ? Mathf.Max(1, modified) : 0;
		}
		
		/// <summary>
		/// Intenta usar un consumible del inventario del jugador en un índice específico. Este método verifica que el índice sea válido dentro de la lista de consumibles del jugador y que el consumible en ese índice no sea nulo. Luego, determina el tipo de efecto del consumible basándose en su subtipo (por ejemplo, si contiene "heal" o "mana") y aplica el efecto correspondiente al jugador, como curar salud o restaurar maná. Si el consumible se usa exitosamente, se elimina de la lista de consumibles del jugador y se devuelve true junto con un mensaje de log que describe el efecto aplicado; de lo contrario, se devuelve false junto con un mensaje de log que indica que el consumible es inválido o no tiene efecto en combate.
		/// </summary>
		/// <param name="index">
		/// El índice del consumible en el inventario del jugador que se desea usar. Este valor debe ser un entero que representa la posición del consumible en la lista de consumibles del jugador. El índice debe ser válido dentro de la lista, es decir, debe ser mayor o igual a 0 y menor que el número de consumibles en el inventario. Si se proporciona un índice fuera de este rango, este método devolverá false junto con un mensaje de log que indica que el consumible es inválido.
		/// </param>
		/// <param name="log">
		/// Un mensaje de log que describe el resultado de intentar usar el consumible. Si el consumible se usa exitosamente, este mensaje indicará el efecto aplicado al jugador, como la cantidad de salud recuperada o maná restaurado. Si el consumible es inválido o no tiene efecto en combate, este mensaje indicará esa situación. El mensaje de log se devuelve como un parámetro de salida para que pueda ser utilizado por otros sistemas del juego, como la interfaz de usuario o el sistema de eventos.
		/// </param>
		/// <returns>
		/// true si el consumible se usó exitosamente y se aplicó su efecto al jugador; false en caso contrario. Para que un consumible se use exitosamente, el índice debe ser válido dentro de la lista de consumibles del jugador, el consumible en ese índice no debe ser nulo y su subtipo debe indicar un efecto aplicable (como "heal" para curar salud o "mana
		/// </returns>
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
		
		/// <summary>
		/// Método llamado cuando el nodo del jugador se agrega a la escena. Este método inicializa la salud y el maná del jugador a sus valores base definidos en las propiedades heredadas de Character. Esto asegura que el jugador comience con su salud y maná completos al iniciar el juego o al aparecer en la escena, proporcionando una base sólida para la gestión de recursos durante el combate.
		/// </summary>
		public override void _Ready()
		{
			base._Ready();
			Health = BaseHealth;
			Mana = BaseMana;
		}
	}
}