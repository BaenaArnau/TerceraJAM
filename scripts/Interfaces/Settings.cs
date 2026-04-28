using Godot;
using System;

public partial class Settings : CanvasLayer
{

	[Export] private CheckBox _fullScreenCheckBox;
	[Export] private HSlider _sonidoSlider;
	[Export] private HSlider _musicaSlider;

	private bool isFullScreen;
	private float _sonidoValue;
	private float _musicaValue;
	private const string SETTINGS_FILE_PATH = "res://configFile/settings.cfg";
	private ConfigFile _configFile = new ConfigFile();
	public override void _Ready()
    {
        loadSettings();
    }
	public void onVolverPressed()
	{
		Visible = false;
	}

	public void onSonidoChanged(float value)
	{

		float db = Mathf.LinearToDb(value);
		_sonidoValue = value;
		AudioServer.SetBusVolumeDb(AudioServer.GetBusIndex("Master"), db);
		_sonidoSlider.Value = _sonidoValue;
		
	}

	public void onMusicaChanged(float value)
	{
		float db = Mathf.LinearToDb(value);
		_musicaValue = value;
		AudioServer.SetBusVolumeDb(AudioServer.GetBusIndex("Master"), db);
		 _musicaSlider.Value = _musicaValue;
	}
	public void saveSettings()
    {
        _configFile.SetValue("Display", "FullScreen", isFullScreen);
		_configFile.SetValue("Sonido", "Volume", _sonidoValue);
		_configFile.SetValue("Musica", "Volume", _musicaValue);
		Error err =_configFile.Save(SETTINGS_FILE_PATH);
		if (err != Error.Ok)
		{			
			GD.PrintErr("Error al guardar la configuración: " + err);
		}
    }

// Configurar el estado del checkbox de pantalla completa
	public void fullScreenActive(bool active)
	{
		if (active)
		{
			isFullScreen = true;
			DisplayServer.WindowSetMode(DisplayServer.WindowMode.ExclusiveFullscreen);
		}
		else
		{
			isFullScreen = false;
			DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
		}
	}

	public void loadSettings()
	{
		Error err = _configFile.Load(SETTINGS_FILE_PATH);
		if (err != Error.Ok)
		{
			GD.PrintErr("Error al cargar la configuración: " + err);
			return;
		}
		 
		 isFullScreen = (bool)_configFile.GetValue("Display", "FullScreen", false);
		 _fullScreenCheckBox.ButtonPressed = isFullScreen;
		DisplayServer.WindowSetMode(isFullScreen ? DisplayServer.WindowMode.ExclusiveFullscreen : DisplayServer.WindowMode.Windowed);

		 _sonidoValue = (float)_configFile.GetValue("Sonido", "Volume", 0.0f);
		 _sonidoSlider.Value = _sonidoValue;
		 AudioServer.SetBusVolumeDb(AudioServer.GetBusIndex("Master"), Mathf.LinearToDb(_sonidoValue));

		 _musicaValue = (float)_configFile.GetValue("Musica", "Volume", 0.0f);
		 _musicaSlider.Value = _musicaValue;
		 AudioServer.SetBusVolumeDb(AudioServer.GetBusIndex("Master"), Mathf.LinearToDb(_musicaValue));
	}

}
