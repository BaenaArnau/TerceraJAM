using Godot;
using SpellsAndRooms.scripts.Characters;

namespace SpellsAndRooms.scripts.Turns
{
    public partial class LoseScene : Node
    {
        [Signal]
        public delegate void LoseClosedEventHandler();

        private const string DefaultBackgroundPath = "res://assets/Turns/BattelBackground.png";

        private Player _player;
        private bool _uiBuilt;

        private Button _continueButton;

        public void StartLose(Player player)
        {
            _player = player;

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

            var backgroundLayer = new CanvasLayer { Name = "BackgroundLayer", Layer = 10 };
            AddChild(backgroundLayer);

            var background = new TextureRect
            {
                Name = "LoseBackground",
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

            var titleLabel = new Label
            {
                Text = "¡DERROTA!",
                HorizontalAlignment = HorizontalAlignment.Center,
                CustomMinimumSize = new Vector2(0, 80),
                ThemeTypeVariation = "title"
            };
            layout.AddChild(titleLabel);

            layout.AddChild(new HSeparator());

            var messageLabel = new Label
            {
                Text = "Tu equipo ha sido derrotado.",
                HorizontalAlignment = HorizontalAlignment.Center,
                CustomMinimumSize = new Vector2(0, 60),
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            };
            layout.AddChild(messageLabel);

            var statsPanel = new PanelContainer
            {
                CustomMinimumSize = new Vector2(0, 100),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
            };
            statsPanel.AddThemeStyleboxOverride("panel", CreateCardStyle());
            layout.AddChild(statsPanel);

            var statsLayout = new VBoxContainer
            {
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill
            };
            statsPanel.AddChild(statsLayout);

            if (_player != null)
            {
                statsLayout.AddChild(new Label
                {
                    Text = $"Oro Restante: {_player.Gold}",
                    HorizontalAlignment = HorizontalAlignment.Center
                });

                statsLayout.AddChild(new Label
                {
                    Text = $"Vida: 0/{_player.BaseHealth}",
                    HorizontalAlignment = HorizontalAlignment.Center
                });

                statsLayout.AddChild(new Label
                {
                    Text = $"Habilidades: {_player.Skills.Count}/4",
                    HorizontalAlignment = HorizontalAlignment.Center
                });
            }

            layout.AddChild(new Control { CustomMinimumSize = new Vector2(0, 20) });

            _continueButton = new Button
            {
                Text = "Volver al Menú",
                CustomMinimumSize = new Vector2(0, 50)
            };
            _continueButton.Pressed += OnContinuePressed;
            layout.AddChild(_continueButton);
        }

        private void RefreshUi()
        {
            // UI ya está actualizada en BuildUi
        }

        private void OnContinuePressed()
        {
            EmitSignal(SignalName.LoseClosed);
            QueueFree();
        }

        private static StyleBoxFlat CreatePanelStyle()
        {
            return new StyleBoxFlat
            {
                BgColor = new Color(0.15f, 0.05f, 0.05f, 0.92f),
                BorderColor = new Color(0.9f, 0.2f, 0.2f, 1.0f),
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
                BgColor = new Color(0.20f, 0.08f, 0.08f, 0.88f),
                BorderColor = new Color(0.9f, 0.3f, 0.3f, 1.0f),
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

