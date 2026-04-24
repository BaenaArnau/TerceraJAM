using Godot;


namespace SpellsAndRooms.scripts.Characters
{
    /// <summary>
    /// Clase que representa a un enemigo en el juego. Hereda de Character e incluye propiedades específicas como dificultad y botín de dinero.
    /// </summary>
    public partial class Enemy : Character
    {
        /// <summary>
        /// Dificultad del enemigo, que puede influir en su salud, daño y el botín que deja al ser derrotado.
        /// </summary>
        [Export] public int Difficulty;
        
        /// <summary>
        /// Cantidad de dinero que el enemigo dejará al ser derrotado. Esta cantidad puede depender de la dificultad del enemigo y otros factores.
        /// </summary>
        [Export] public int MoneyLoot;
        
        /// <summary>
        /// Constructor por defecto que inicializa un enemigo con valores predeterminados. Estos valores pueden ser sobrescritos al crear instancias específicas de enemigos.
        /// </summary>
        public Enemy() : base("Enemy", 1, 1, 0, 0, 1, DamageType.Physical, DamageType.Fire)
        {
            Difficulty = 1;
            MoneyLoot = 0;
        }

        /// <summary>
        /// Constructor que permite inicializar un enemigo con valores específicos para todas sus propiedades, incluyendo las heredadas de Character y las propias de Enemy.
        /// </summary>
        /// <param name="name">
        /// El nombre del enemigo. Este nombre se mostrará en la interfaz de usuario y puede ser utilizado para identificar al enemigo en el juego.
        /// </param>
        /// <param name="health">
        /// La salud actual del enemigo. Este valor puede ser modificado durante el combate y no debe exceder el valor de baseHealth ni ser menor que 0.
        /// </param>
        /// <param name="baseHealth">
        /// La salud máxima del enemigo. Este valor define el límite superior de la salud actual y se utiliza para calcular la salud restante durante el combate. No debe ser menor que 1.
        /// </param>
        /// <param name="mana">
        /// La cantidad actual de maná del enemigo. Este valor puede ser modificado durante el combate y no debe exceder el valor de baseMana ni ser menor que 0.
        /// </param>
        /// <param name="baseMana">
        /// La cantidad máxima de maná del enemigo. Este valor define el límite superior del maná actual y se utiliza
        /// </param>
        /// <param name="damage">
        /// El daño base que el enemigo inflige con sus ataques. Este valor puede ser modificado por habilidades, resistencias y debilidades durante el combate, pero no debe ser menor que 1.
        /// </param>
        /// <param name="damageResistance">
        /// El tipo de daño al que el enemigo es resistente. Este valor se utiliza para calcular el daño recibido durante el combate, aplicando un multiplicador de resistencia si el ataque coincide con este tipo. Si el enemigo no tiene resistencia a ningún tipo de daño, este valor debe ser DamageType.None.
        /// </param>
        /// <param name="damageWeakness">
        /// El tipo de daño al que el enemigo es débil. Este valor se utiliza para calcular el daño recibido durante el combate, aplicando un multiplicador de debilidad si el ataque coincide con este tipo. Si el enemigo no tiene debilidad a ningún tipo de daño, este valor debe ser DamageType.None.
        /// </param>
        /// <param name="difficulty">
        /// La dificultad del enemigo, que puede influir en su salud, daño y el botín que deja al ser derrotado. Este valor debe ser un entero positivo, donde valores más altos indican enemigos más difíciles. La dificultad puede ser utilizada por el sistema de generación de encuentros para seleccionar enemigos apropiados para la situación actual del jugador.
        /// </param>
        /// <param name="moneyLoot">
        /// La cantidad de dinero que el enemigo dejará al ser derrotado. Este valor debe ser un entero no negativo, donde valores más altos indican un botín más valioso. La cantidad de dinero puede depender de la dificultad del enemigo y otros factores, y se utiliza para recompensar al jugador por vencer al enemigo en combate.
        /// </param>
        public Enemy(string name, int health, int baseHealth, int mana, int baseMana, int damage, DamageType damageResistance, DamageType damageWeakness, int difficulty, int moneyLoot)
            : base(name, health, baseHealth, mana, baseMana, damage, damageResistance, damageWeakness)
        {
            Difficulty = Mathf.Max(1, difficulty);
            MoneyLoot = Mathf.Max(0, moneyLoot);
        }
    }
}
