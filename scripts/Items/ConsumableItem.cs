using Godot;

namespace SpellsAndRooms.scripts.Items
{
	public partial class ConsumableItem : Node
	{
		public string ItemName { get; set; } = "Consumable";
		public string Type { get; set; } = string.Empty;
		public string Subtype { get; set; } = string.Empty;
		public int Potency { get; set; }
		public string Description { get; set; } = string.Empty;
	}
}