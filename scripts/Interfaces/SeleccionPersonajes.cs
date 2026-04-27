using Godot;
using System;
using SpellsAndRooms.scripts.map;

public partial class SeleccionPersonajes : CanvasLayer
{

	[Export] private TextureButton _caballeroButton;
	[Export] private TextureButton _magoButton;
	private Map _map;
	private string _selectedCharacter = "";
	private static bool _magoIsUnlocked = false;
	
	public override void _Ready()
	{
		_magoButton.Disabled = !_magoIsUnlocked;
	}
	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}
	public void onCaballeroSelect()
	{
		GD.Print("Caballero seleccionado");
		// Desactivar el botón de Mago
		_magoButton.ButtonPressed = false;
		_selectedCharacter = "res://scenes/Characters/Player/Oathbreakers.tscn";
	}

	public void onMagoSelect()
	{
		GD.Print("Mago seleccionado");
		// Desactivar el botón de Caballero
		_caballeroButton.ButtonPressed = false;
		_selectedCharacter = "res://scenes/Characters/Player/Wizard.tscn";
	}

	public void onVolverPressed()
	{
		GetTree().ChangeSceneToFile("res://scenes/Interfaces/menu_principal.tscn");
	}

	public void onSelecionarPressed()
	{
		Map.SetSelectedPlayerScenePath(_selectedCharacter);
		GetTree().ChangeSceneToFile("res://scenes/Map/map.tscn");
	}
	
	public static void UnlockMago(bool unlock)
	{
		_magoIsUnlocked = unlock;
	}
}
