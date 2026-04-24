using Godot;

namespace SpellsAndRooms.scripts.Items
{
	public partial class PassiveItem : Node
    {
      public string ItemName { get; set; } = "Passive";
      public string Type { get; set; } = string.Empty;
      public int BonusValue { get; set; }
      public string Description { get; set; } = string.Empty;
    }
}

