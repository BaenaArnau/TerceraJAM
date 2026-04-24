using Godot;
using SpellsAndRooms.scripts.Characters;

namespace SpellsAndRooms.scripts.Turns
{
    public sealed class EnemyTemplate
    {
        public string Name { get; init; } = "Enemy";
        public string ScenePath { get; init; } = string.Empty;
        public PackedScene Scene { get; init; }
        public bool IsBoss { get; init; }
        public int Difficulty { get; init; } = 1;
        public int Loot { get; init; } = 0;
        public Skill[] Skills { get; init; } = [];
    }
}
