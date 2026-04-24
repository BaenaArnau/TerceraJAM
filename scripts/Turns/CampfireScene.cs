using Godot;
using SpellsAndRooms.scripts.Characters;

namespace SpellsAndRooms.scripts.Turns
{
    public partial class CampfireScene : Node
    {
        [Signal]
        public delegate void CampfireClosedEventHandler();

        [Export] public Texture2D BackgroundTexture;

        private const string DefaultPromptText = "La fogata te da dos opciones: descansar o seguir adelante.";
        private const int SkipRestGold = 20;
        private const string DefaultBackgroundPath = "res://assets/Turns/BattelBackground.png";

        private Player _player;
        private CanvasLayer _backgroundLayer;
        private CanvasLayer _uiLayer;
        private Control _uiRoot;

        private Label _goldLabel;
        private Label _statusLabel;
        private Button _restButton;
        private Button _skipRestButton;
        private Button _continueButton;

        private bool _uiBuilt;
        private bool _resolved;

        public void StartCampfire(Player player)
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
            if (_player != null)
                RefreshUi();
        }

        private void BuildUi()
        {
            if (_uiBuilt)
                return;

            _uiBuilt = true;

            _backgroundLayer = new CanvasLayer { Name = "BackgroundLayer", Layer = -1 };
            AddChild(_backgroundLayer);

            var background = new TextureRect
            {
                Name = "CampfireBackground",
                Texture = BackgroundTexture ?? GD.Load<Texture2D>(DefaultBackgroundPath),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.Scale,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            background.AnchorLeft = 0;
            background.AnchorTop = 0;
            background.AnchorRight = 1;
            background.AnchorBottom = 1;
            _backgroundLayer.AddChild(background);

            _uiLayer = new CanvasLayer { Name = "UiLayer", Layer = 1 };
            AddChild(_uiLayer);

            _uiRoot = new Control
            {
                Name = "UiRoot",
                MouseFilter = Control.MouseFilterEnum.Stop
            };
            _uiRoot.AnchorLeft = 0;
            _uiRoot.AnchorTop = 0;
            _uiRoot.AnchorRight = 1;
            _uiRoot.AnchorBottom = 1;
            _uiLayer.AddChild(_uiRoot);

            var centerPanel = new PanelContainer
            {
                AnchorLeft = 0.20f,
                AnchorTop = 0.20f,
                AnchorRight = 0.80f,
                AnchorBottom = 0.80f
            };
            centerPanel.AddThemeStyleboxOverride("panel", CreatePanelStyle());
            _uiRoot.AddChild(centerPanel);

            var layout = new VBoxContainer
            {
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill
            };
            centerPanel.AddChild(layout);

            layout.AddChild(new Label
            {
                Text = "=== FOGATA ===",
                HorizontalAlignment = HorizontalAlignment.Center,
                CustomMinimumSize = new Vector2(0, 46)
            });

            _goldLabel = new Label
            {
                Text = "Oro: 0",
                HorizontalAlignment = HorizontalAlignment.Center,
                CustomMinimumSize = new Vector2(0, 30)
            };
            layout.AddChild(_goldLabel);

            _statusLabel = new Label
            {
                Text = DefaultPromptText,
                HorizontalAlignment = HorizontalAlignment.Center,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill
            };
            layout.AddChild(_statusLabel);

            _restButton = new Button
            {
                Text = "Descansar (recupera 50% de vida y mana faltantes)",
                CustomMinimumSize = new Vector2(0, 44)
            };
            _restButton.Pressed += OnRestPressed;
            layout.AddChild(_restButton);

            _skipRestButton = new Button
            {
                Text = $"No descansar (+{SkipRestGold} oro)",
                CustomMinimumSize = new Vector2(0, 44)
            };
            _skipRestButton.Pressed += OnSkipRestPressed;
            layout.AddChild(_skipRestButton);

            _continueButton = new Button
            {
                Text = "Continuar",
                Visible = false,
                CustomMinimumSize = new Vector2(0, 42)
            };
            _continueButton.Pressed += OnContinuePressed;
            layout.AddChild(_continueButton);
        }

        private void RefreshUi()
        {
            if (_player == null)
                return;

            _goldLabel.Text = $"Oro: {_player.Gold}";
            _restButton.Disabled = _resolved;
            _skipRestButton.Disabled = _resolved;
            _continueButton.Visible = _resolved;

            if (!_resolved)
                _statusLabel.Text = DefaultPromptText;
        }

        private void OnRestPressed()
        {
            if (_player == null || _resolved)
                return;

            int missingHealth = Mathf.Max(0, _player.BaseHealth - _player.Health);
            int missingMana = Mathf.Max(0, _player.BaseMana - _player.Mana);
            int recoveredHealth = Mathf.CeilToInt(missingHealth * 0.5f);
            int recoveredMana = Mathf.CeilToInt(missingMana * 0.5f);

            _player.Heal(recoveredHealth);
            _player.RestoreMana(recoveredMana);

            _resolved = true;
            _statusLabel.Text = $"Descansas en la fogata. +{recoveredHealth} vida y +{recoveredMana} mana.";
            RefreshUi();
        }

        private void OnSkipRestPressed()
        {
            if (_player == null || _resolved)
                return;

            _player.AddGold(SkipRestGold);
            _resolved = true;
            _statusLabel.Text = $"Sigues adelante sin descansar y consigues {SkipRestGold} de oro.";
            RefreshUi();
        }

        private void OnContinuePressed()
        {
            EmitSignal(SignalName.CampfireClosed);
            QueueFree();
        }

        private static StyleBoxFlat CreatePanelStyle()
        {
            return new StyleBoxFlat
            {
                BgColor = new Color(0.08f, 0.08f, 0.10f, 0.90f),
                BorderColor = new Color(0.85f, 0.56f, 0.25f, 1.0f),
                BorderWidthLeft = 3,
                BorderWidthTop = 3,
                BorderWidthRight = 3,
                BorderWidthBottom = 3,
                CornerRadiusTopLeft = 12,
                CornerRadiusTopRight = 12,
                CornerRadiusBottomLeft = 12,
                CornerRadiusBottomRight = 12,
                ContentMarginLeft = 16,
                ContentMarginTop = 16,
                ContentMarginRight = 16,
                ContentMarginBottom = 16
            };
        }
    }
}

