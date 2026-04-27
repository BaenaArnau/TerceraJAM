using Godot;

namespace SpellsAndRooms.scripts.Turns
{
    [GlobalClass]
    public partial class ShopImageEntry : Resource
    {
        [Export] public string Id = string.Empty;
        [Export] public Texture2D Texture;
    }
}

