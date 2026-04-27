using Godot;
using SpellsAndRooms.scripts.Characters;

namespace SpellsAndRooms.scripts.Turns
{
    public partial class WinScene : Node
    {
        [Signal]
        public delegate void WinClosedEventHandler();

        private const string DefaultBackgroundPath = "res://assets/Turns/BattelBackground.png";
        private const string MainMenuScenePath = "res://scenes/Interfaces/menu_principal.tscn";

        private Player _player;
        private int _earnedGold;
        private bool _isFinalBossVictory;
        private bool _uiBuilt;

        private Label _titleLabel;
        private Label _messageLabel;
        private Label _goldLabel;
        private Label _totalGoldLabel;
        private Button _continueButton;

        public void StartWin(Player player, int earnedGold, bool isFinalBossVictory = false)
        {
            _player = player;
            _earnedGold = earnedGold;
            _isFinalBossVictory = isFinalBossVictory;

            if (IsInsideTree())
            {
                BuildUi();
                RefreshUi();
            }
        }

        public override void _Ready()
        {
            BuildUi();
            RefreshUi();
        }

        private void BuildUi()
        {
            if (_uiBuilt)
                return;

            _uiBuilt = true;

            var backgroundLayer = new CanvasLayer { Name = "BackgroundLayer", Layer = -1 };
            backgroundLayer.Layer = 10;
            AddChild(backgroundLayer);

            var background = new TextureRect
            {
                Name = "WinBackground",
                Texture = GD.Load<Texture2D>(DefaultBackgroundPath),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.Scale,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            background.AnchorLeft = 0;
            background.AnchorTop = 0;
            background.AnchorRight = 1;
            background.AnchorBottom = 1;
            backgroundLayer.AddChild(background);

            var uiLayer = new CanvasLayer { Name = "UiLayer", Layer = 11 };
            AddChild(uiLayer);

            var uiRoot = new Control
            {
                Name = "UiRoot",
                MouseFilter = Control.MouseFilterEnum.Stop
            };
            uiRoot.AnchorLeft = 0;
            uiRoot.AnchorTop = 0;
            uiRoot.AnchorRight = 1;
            uiRoot.AnchorBottom = 1;
            uiLayer.AddChild(uiRoot);

            var centerPanel = new PanelContainer
            {
                AnchorLeft = 0.15f,
                AnchorTop = 0.15f,
                AnchorRight = 0.85f,
                AnchorBottom = 0.85f
            };
            centerPanel.AddThemeStyleboxOverride("panel", CreatePanelStyle());
            uiRoot.AddChild(centerPanel);

            var layout = new VBoxContainer
            {
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill
            };
            centerPanel.AddChild(layout);

            _titleLabel = new Label
            {
                Text = "¡VICTORIA!",
                HorizontalAlignment = HorizontalAlignment.Center,
                CustomMinimumSize = new Vector2(0, 80),
                ThemeTypeVariation = "title"
            };
            layout.AddChild(_titleLabel);

            _messageLabel = new Label
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                CustomMinimumSize = new Vector2(0, 48)
            };
            layout.AddChild(_messageLabel);

            layout.AddChild(new HSeparator());

            var goldCardPanel = new PanelContainer
            {
                CustomMinimumSize = new Vector2(0, 140),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
            };
            goldCardPanel.AddThemeStyleboxOverride("panel", CreateCardStyle());
            layout.AddChild(goldCardPanel);

            var goldCardLayout = new VBoxContainer
            {
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill
            };
            goldCardPanel.AddChild(goldCardLayout);

            goldCardLayout.AddChild(new Label
            {
                Text = "Oro Ganado",
                HorizontalAlignment = HorizontalAlignment.Center
            });

            _goldLabel = new Label
            {
                Text = $"+{_earnedGold}",
                HorizontalAlignment = HorizontalAlignment.Center,
                CustomMinimumSize = new Vector2(0, 60)
            };
            goldCardLayout.AddChild(_goldLabel);

            _totalGoldLabel = new Label
            {
                Text = $"Oro Total: {_player?.Gold ?? 0}",
                HorizontalAlignment = HorizontalAlignment.Center
            };
            goldCardLayout.AddChild(_totalGoldLabel);

            layout.AddChild(new Control { CustomMinimumSize = new Vector2(0, 20) });

            _continueButton = new Button
            {
                Text = "Continuar",
                CustomMinimumSize = new Vector2(0, 50)
            };
            _continueButton.Pressed += OnContinuePressed;
            layout.AddChild(_continueButton);
        }

        private void RefreshUi()
        {
            if (_isFinalBossVictory)
                SeleccionPersonajes.UnlockMago(true);

            if (_player == null)
                return;

            if (_titleLabel != null)
                _titleLabel.Text = _isFinalBossVictory ? "¡BOSS FINAL DERROTADO!" : "¡VICTORIA!";

            if (_messageLabel != null)
                _messageLabel.Text = _isFinalBossVictory
                    ? "Has completado la aventura. Gracias por jugar."
                    : "Has ganado el combate.";

            _goldLabel.Text = $"+{_earnedGold}";
            if (_totalGoldLabel != null)
                _totalGoldLabel.Text = $"Oro Total: {_player.Gold}";
            if (_continueButton != null)
                _continueButton.Text = _isFinalBossVictory ? "Volver al Menu Principal" : "Continuar";
        }

        private void OnContinuePressed()
        {
            if (_isFinalBossVictory)
            {
                GetTree().ChangeSceneToFile(MainMenuScenePath);
                return;
            }

            EmitSignal(SignalName.WinClosed);
            QueueFree();
        }

        private static StyleBoxFlat CreatePanelStyle()
        {
            return new StyleBoxFlat
            {
                BgColor = new Color(0.05f, 0.15f, 0.08f, 0.92f),
                BorderColor = new Color(0.2f, 0.8f, 0.3f, 1.0f),
                BorderWidthLeft = 4,
                BorderWidthTop = 4,
                BorderWidthRight = 4,
                BorderWidthBottom = 4,
                CornerRadiusTopLeft = 12,
                CornerRadiusTopRight = 12,
                CornerRadiusBottomLeft = 12,
                CornerRadiusBottomRight = 12,
                ContentMarginLeft = 20,
                ContentMarginTop = 20,
                ContentMarginRight = 20,
                ContentMarginBottom = 20
            };
        }

        private static StyleBoxFlat CreateCardStyle()
        {
            return new StyleBoxFlat
            {
                BgColor = new Color(0.08f, 0.20f, 0.10f, 0.88f),
                BorderColor = new Color(0.3f, 0.9f, 0.4f, 1.0f),
                BorderWidthLeft = 2,
                BorderWidthTop = 2,
                BorderWidthRight = 2,
                BorderWidthBottom = 2,
                CornerRadiusTopLeft = 10,
                CornerRadiusTopRight = 10,
                CornerRadiusBottomLeft = 10,
                CornerRadiusBottomRight = 10,
                ContentMarginLeft = 12,
                ContentMarginTop = 12,
                ContentMarginRight = 12,
                ContentMarginBottom = 12
            };
        }
    }
}

