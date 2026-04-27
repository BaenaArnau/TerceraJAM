using Godot;
using System;

public partial class SeleccionPersonajes : CanvasLayer
{

	[Export] private TextureButton _caballeroButton;
	[Export] private TextureButton _magoButton;
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
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
	}

	public void onMagoSelect()
	{
		GD.Print("Mago seleccionado");
		// Desactivar el botón de Caballero
		_caballeroButton.ButtonPressed = false;
	}
}
