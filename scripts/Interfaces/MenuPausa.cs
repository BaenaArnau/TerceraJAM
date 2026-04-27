using Godot;
using System;

public partial class MenuPausa : CanvasLayer
{
	[Export] private CanvasLayer _settings;
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
    {
		if (_settings == null)
			_settings = GetNodeOrNull<CanvasLayer>("Settings");

		if (_settings != null)
			_settings.Visible = false;
    }

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}

	public override void _Input(InputEvent @event)
	{
		if (!Visible)
			return;

		if (@event.IsActionPressed("pausa"))
		{
			onContinuePressed();
			GetViewport().SetInputAsHandled();
		}
	}

	private void onContinuePressed()
	{
		GetTree().Paused = false;
		Visible = false;
		if (_settings != null)
			_settings.Visible = false;
	}

	private void onRestartPressed()
	{
			GetTree().Paused = false;
		GetTree().ChangeSceneToFile("res://scenes/Map/map.tscn");
	}
	private void onSettingPressed()
	{
		if (_settings != null)
			_settings.Visible = true;
	}	
	private void onExitPressed()
	{
		GetTree().Paused = false;
		GetTree().ChangeSceneToFile("res://scenes/Interfaces/menu_principal.tscn");
	}

}
