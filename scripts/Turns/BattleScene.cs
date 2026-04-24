using Godot;
using System.Collections.Generic;
using System.Linq;
using SpellsAndRooms.scripts.Characters;
using SpellsAndRooms.scripts.Items;

namespace SpellsAndRooms.scripts.Turns
{
    public partial class BattleScene : Node2D
    {
        [Signal] public delegate void BattleFinishedEventHandler(bool playerWon, int earnedGold);

        [Export] public PackedScene WinScenePacked;
        [Export] public PackedScene LoseScenePacked;

        private const string BattleBackgroundPath = "res://assets/Turns/BattelBackground.png";
        private const string WinScenePath = "res://scenes/Turns/Win.tscn";
        private const string LoseScenePath = "res://scenes/Turns/Lose.tscn";

        private readonly BattleController _battleController = new BattleController();
        private Player _player;
        private List<Enemy> _enemies = new List<Enemy>();
        private Skill _pendingSkill;
        private bool _battleEnded;
        private bool _uiBuilt;

        private TextureRect _background;
        private CanvasLayer _backgroundLayer;
        private CanvasLayer _battleFieldLayer;
        private Node2D _battleField;
        private CanvasLayer _uiLayer;
        private Control _uiRoot;

        private Label _playerNameLabel;
        private Label _playerHpLabel;
        private ProgressBar _playerHpBar;
        private Label _playerMpLabel;
        private ProgressBar _playerMpBar;

        private VBoxContainer _enemyInfoContainer;
        private GridContainer _commandGrid;
        private GridContainer _skillGrid;
        private GridContainer _itemGrid;
        private GridContainer _targetGrid;
        private RichTextLabel _logLabel;

        private Button _attackButton;
        private Button _magicButton;
        private Button _itemButton;
        private Button _defendButton;
        private Button _backButton;

        private readonly Dictionary<Enemy, Node2D> _enemyNodes = new Dictionary<Enemy, Node2D>();

        public void StartBattle(Player player, List<Enemy> enemies)
        {
            _player = player;
            _enemies = enemies ?? new List<Enemy>();

            GD.Print($"[BattleScene] StartBattle called - Player: {_player?.CharacterName}, Enemies: {_enemies?.Count}");
            
            if (_player != null)
                GD.Print($"[BattleScene] Player Details - Health: {_player.Health}, IsAlive: {_player.IsAlive}, Type: {_player.GetType().Name}");

            if (IsInsideTree())
            {
                GD.Print("[BattleScene] StartBattle - Node is inside tree, calling BuildUi/AttachCombatants");
                BuildUi();
                AttachCombatants();
                RefreshUi();
                ShowMainCommands();
                StartPlayerTurn();
            }
            else
                GD.PrintErr("[BattleScene] StartBattle - Node is NOT inside tree yet!");
        }

        public override void _Ready()
        {
            GD.Print("[BattleScene] _Ready called");
            BuildUi();
            GD.Print($"[BattleScene] _Ready - Player: {_player?.CharacterName}, Enemies: {_enemies?.Count}");
            AttachCombatants();
            RefreshUi();

            if (_player != null)
            {
                ShowMainCommands();
                StartPlayerTurn();
            }
        }

        private void BuildUi()
        {
            if (_uiBuilt)
                return;

            GD.Print("[BattleScene] Starting BuildUi...");

            // Background en su propia CanvasLayer (Layer -1) para estar al frente del mapa
            _backgroundLayer = new CanvasLayer { Name = "BackgroundLayer", Layer = -1 };
            AddChild(_backgroundLayer);

            _background = new TextureRect
            {
                Name = "BattleBackground",
                Texture = GD.Load<Texture2D>(BattleBackgroundPath),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.Scale,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            _background.AnchorLeft = 0;
            _background.AnchorTop = 0;
            _background.AnchorRight = 1;
            _background.AnchorBottom = 1;
            _backgroundLayer.AddChild(_background);

            // UI en CanvasLayer Layer 2 (para estar sobre battlefield pero bajo win/lose)
            _uiLayer = new CanvasLayer { Name = "UiLayer", Layer = 2 };
            AddChild(_uiLayer);

            // Sprites de combatientes en CanvasLayer Layer 0
            _battleFieldLayer = new CanvasLayer { Name = "BattleFieldLayer", Layer = 0 };
            AddChild(_battleFieldLayer);

            _battleField = new Node2D { Name = "BattleField" };
            _battleFieldLayer.AddChild(_battleField);

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

            var outer = new MarginContainer
            {
                Name = "OuterMargin",
                AnchorLeft = 0,
                AnchorTop = 0,
                AnchorRight = 1,
                AnchorBottom = 1,
                ThemeTypeVariation = "MarginContainer"
            };
            _uiRoot.AddChild(outer);

            var inner = new VBoxContainer
            {
                Name = "InnerLayout",
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill
            };
            outer.AddChild(inner);

            var topRow = new HBoxContainer
            {
                Name = "TopRow",
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill
            };
            inner.AddChild(topRow);

            var playerPanel = new PanelContainer
            {
                Name = "PlayerPanel",
                CustomMinimumSize = new Vector2(260, 0),
                SizeFlagsVertical = Control.SizeFlags.ExpandFill
            };
            playerPanel.AddThemeStyleboxOverride("panel", CreatePlayerPanelStyle());
            topRow.AddChild(playerPanel);

            var playerLayout = new VBoxContainer
            {
                Name = "PlayerLayout",
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill
            };
            playerPanel.AddChild(playerLayout);

            _playerNameLabel = new Label { Name = "PlayerName", Text = "Heroe" };
            playerLayout.AddChild(_playerNameLabel);

            _playerHpLabel = new Label { Name = "PlayerHpLabel", Text = "HP: 0/0" };
            playerLayout.AddChild(_playerHpLabel);

            _playerHpBar = new ProgressBar
            {
                Name = "PlayerHpBar",
                CustomMinimumSize = new Vector2(0, 20),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
            };
            playerLayout.AddChild(_playerHpBar);

            _playerMpLabel = new Label { Name = "PlayerMpLabel", Text = "MP: 0/0" };
            playerLayout.AddChild(_playerMpLabel);

            _playerMpBar = new ProgressBar
            {
                Name = "PlayerMpBar",
                CustomMinimumSize = new Vector2(0, 20),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
            };
            playerLayout.AddChild(_playerMpBar);

            var middlePanel = new PanelContainer
            {
                Name = "MiddlePanel",
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill
            };
            middlePanel.AddThemeStyleboxOverride("panel", CreateMiddlePanelStyle());
            topRow.AddChild(middlePanel);

            var middleLayout = new VBoxContainer
            {
                Name = "MiddleLayout",
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill
            };
            middlePanel.AddChild(middleLayout);

            var enemyLabel = new Label { Text = "Enemigos" };
            middleLayout.AddChild(enemyLabel);

            _enemyInfoContainer = new VBoxContainer
            {
                Name = "EnemyInfoContainer",
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill
            };
            middleLayout.AddChild(_enemyInfoContainer);

            _targetGrid = new GridContainer
            {
                Name = "TargetGrid",
                Columns = 2,
                Visible = false,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
            };
            middleLayout.AddChild(_targetGrid);

            var rightPanel = new PanelContainer
            {
                Name = "RightPanel",
                CustomMinimumSize = new Vector2(330, 0),
                SizeFlagsVertical = Control.SizeFlags.ExpandFill
            };
            rightPanel.AddThemeStyleboxOverride("panel", CreateRightPanelStyle());
            topRow.AddChild(rightPanel);

            var rightLayout = new VBoxContainer
            {
                Name = "RightLayout",
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill
            };
            rightPanel.AddChild(rightLayout);

            var commandLabel = new Label { Text = "Acciones" };
            rightLayout.AddChild(commandLabel);

            _commandGrid = new GridContainer
            {
                Name = "CommandGrid",
                Columns = 2,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
            };
            rightLayout.AddChild(_commandGrid);

            var skillLabel = new Label { Text = "Magias" };
            rightLayout.AddChild(skillLabel);

            _skillGrid = new GridContainer
            {
                Name = "SkillGrid",
                Columns = 2,
                Visible = false,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
            };
            rightLayout.AddChild(_skillGrid);

            var itemLabel = new Label { Text = "Objetos" };
            rightLayout.AddChild(itemLabel);

            _itemGrid = new GridContainer
            {
                Name = "ItemGrid",
                Columns = 2,
                Visible = false,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
            };
            rightLayout.AddChild(_itemGrid);

            _backButton = CreateMenuButton("Volver");
            _backButton.Pressed += ShowMainCommands;
            rightLayout.AddChild(_backButton);

            _logLabel = new RichTextLabel
            {
                Name = "Log",
                BbcodeEnabled = false,
                FitContent = true,
                ScrollFollowing = true,
                CustomMinimumSize = new Vector2(0, 150),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill
            };
            inner.AddChild(_logLabel);

            _uiBuilt = true;
        }

        private void AttachCombatants()
        {
            if (_battleField == null)
                return;

            foreach (Node child in _battleField.GetChildren())
            {
                child.QueueFree();
            }

            _enemyNodes.Clear();

            if (_player != null)
            {
                if (_player.GetParent() != null)
                    _player.GetParent().RemoveChild(_player);

                _battleField.AddChild(_player);
                _battleField.MoveChild(_player, 0);
                
                if (_player is AnimatedSprite2D playerAnimated)
                {
                    playerAnimated.Animation = "Idle";
                    playerAnimated.Play();
                    GD.Print($"[BattleScene] Player AnimatedSprite2D loaded: {_player.CharacterName}");
                }
                
                CallDeferred(nameof(PositionPlayer));
            }

            for (int i = 0; i < _enemies.Count; i++)
            {
                Enemy enemy = _enemies[i];
                if (enemy == null)
                    continue;

                if (enemy.GetParent() != null)
                    enemy.GetParent().RemoveChild(enemy);

                _battleField.AddChild(enemy);
                
                if (enemy is AnimatedSprite2D enemyAnimated)
                {
                    enemyAnimated.Animation = "Idle";
                    enemyAnimated.Play();
                    GD.Print($"[BattleScene] Enemy AnimatedSprite2D loaded: {enemy.CharacterName}");
                }

                _enemyNodes[enemy] = enemy;
                
                int currentIndex = i;
                CallDeferred(nameof(PositionEnemyDeferred), enemy, currentIndex);
            }
            
            GD.Print($"[BattleScene] AttachCombatants complete - Player: {(_player != null ? _player.CharacterName : "null")}, Enemies: {_enemies.Count}");
        }

        private void PositionPlayer()
        {
            if (_player == null)
                return;

            Vector2 viewport = GetViewportRect().Size;
            GD.Print($"[BattleScene] PositionPlayer - Viewport: {viewport}");
            
            if (viewport.X == 0 || viewport.Y == 0)
            {
                GD.PrintErr("[BattleScene] Viewport is zero! Using fallback values (1280, 720)");
                viewport = new Vector2(1280, 720);
            }
            
            _player.Position = new Vector2(viewport.X * 0.22f, viewport.Y * 0.70f);
            _player.Scale = new Vector2(2.0f, 2.0f);
            _player.FlipH = false;
            _player.Visible = true;
            _player.ZIndex = 10;
            
            GD.Print($"[BattleScene] Player positioned - Pos: {_player.Position}, Scale: {_player.Scale}, Visible: {_player.Visible}, ZIndex: {_player.ZIndex}, Parent: {_player.GetParent()?.Name}");
        }

        private void PositionEnemyDeferred(Enemy enemy, int index)
        {
            PositionEnemy(enemy, index);
        }

        private void PositionEnemy(Enemy enemy, int index)
        {
            if (enemy == null)
                return;
            
            Vector2 viewport = GetViewportRect().Size;
            
            if (viewport.X == 0 || viewport.Y == 0)
            {
                GD.PrintErr("[BattleScene] Viewport is zero! Using fallback values (1280, 720)");
                viewport = new Vector2(1280, 720);
            }
            
            float baseX = viewport.X * 0.72f;
            float baseY = viewport.Y * 0.35f;
            float xOffset = (index % 2) * 110.0f;
            float yOffset = (index / 2) * 110.0f;
            
            enemy.Position = new Vector2(baseX + xOffset, baseY + yOffset);
            enemy.Scale = Vector2.One * 1.8f;
            enemy.FlipH = true;
            enemy.Visible = true;
            enemy.ZIndex = 5;
            
            GD.Print($"[BattleScene] Enemy {enemy.CharacterName} positioned - Pos: {enemy.Position}, Scale: {enemy.Scale}, Visible: {enemy.Visible}, ZIndex: {enemy.ZIndex}, Parent: {enemy.GetParent()?.Name}");
        }

        private void PlayAttackAnimation(Character character)
        {
            if (character is AnimatedSprite2D animatedSprite)
            {
                if (animatedSprite.SpriteFrames.GetAnimationNames().Contains("Attack"))
                {
                    animatedSprite.Animation = "Attack";
                    animatedSprite.Play();
                }
            }
        }

        private void PlayIdleAnimation(Character character)
        {
            if (character is AnimatedSprite2D animatedSprite)
            {
                if (animatedSprite.SpriteFrames.GetAnimationNames().Contains("Idle"))
                {
                    animatedSprite.Animation = "Idle";
                    animatedSprite.Play();
                }
            }
        }

        private void ShowMainCommands()
        {
            if (_commandGrid == null)
                return;

            ClearChildren(_commandGrid);
            _commandGrid.Visible = true;
            if (_skillGrid != null)
                _skillGrid.Visible = false;
            if (_itemGrid != null)
                _itemGrid.Visible = false;
            if (_targetGrid != null)
                _targetGrid.Visible = false;

            _attackButton = CreateMenuButton("Ataque");
            _attackButton.Pressed += OnAttackPressed;
            _commandGrid.AddChild(_attackButton);

            _magicButton = CreateMenuButton("Magia");
            _magicButton.Pressed += ShowSkillMenu;
            _commandGrid.AddChild(_magicButton);

            _itemButton = CreateMenuButton("Objeto");
            _itemButton.Disabled = _player == null || _player.Consumables.Count == 0;
            _itemButton.Pressed += ShowItemMenu;
            _commandGrid.AddChild(_itemButton);

            _defendButton = CreateMenuButton("Defensa");
            _defendButton.Disabled = true;
            _commandGrid.AddChild(_defendButton);
        }

        private void ShowSkillMenu()
        {
            if (_player == null || _skillGrid == null)
                return;

            ClearChildren(_skillGrid);
            _commandGrid.Visible = true;
            _skillGrid.Visible = true;
            _targetGrid.Visible = false;
            _skillGrid.Columns = 2;

            foreach (Skill skill in _player.Skills)
            {
                Button skillButton = CreateMenuButton(skill.IsHealing
                    ? $"{skill.Name}\n{skill.ManaCost} MP"
                    : skill.MultiAttack
                        ? $"{skill.Name}\n{skill.ManaCost} MP / Todos"
                        : $"{skill.Name}\n{skill.ManaCost} MP");
                skillButton.Disabled = _player.Mana < skill.ManaCost;
                skillButton.Pressed += () => OnSkillPressed(skill);
                _skillGrid.AddChild(skillButton);
            }
        }

        private void ShowItemMenu()
        {
            if (_player == null || _itemGrid == null)
                return;

            ClearChildren(_itemGrid);
            _commandGrid.Visible = true;
            _skillGrid.Visible = false;
            _targetGrid.Visible = false;
            _itemGrid.Visible = true;
            _itemGrid.Columns = 2;

            if (_player.Consumables.Count == 0)
            {
                AddLog("No tienes consumibles.");
                return;
            }

            for (int i = 0; i < _player.Consumables.Count; i++)
            {
                int itemIndex = i;
                ConsumableItem item = _player.Consumables[itemIndex];
                if (item == null)
                    continue;

                Button itemButton = CreateMenuButton($"{item.ItemName}\n{item.Subtype} ({item.Potency})");
                itemButton.Pressed += () => OnItemPressed(itemIndex);
                _itemGrid.AddChild(itemButton);
            }
        }

        private void ShowTargetMenuForAttack(Skill skill = null)
        {
            _pendingSkill = skill;
            ClearChildren(_targetGrid);
            _targetGrid.Visible = true;
            _skillGrid.Visible = false;
            _commandGrid.Visible = true;

            foreach (Enemy enemy in _enemies.Where(e => e != null && e.IsAlive))
            {
                Button targetButton = CreateMenuButton($"{enemy.CharacterName}\nHP:{enemy.Health}");
                targetButton.Pressed += () => OnTargetSelected(enemy);
                _targetGrid.AddChild(targetButton);
            }
        }

        private void OnAttackPressed()
        {
            _pendingSkill = null;
            ShowTargetMenuForAttack();
            AddLog("Elige un objetivo para el ataque.");
        }

        private void OnSkillPressed(Skill skill)
        {
            if (_battleEnded || _player == null || skill == null)
                return;

            _pendingSkill = skill;
            if (skill.IsHealing)
            {
                AddLog(_battleController.PlayerUseSkill(_player, skill));
                FinishPlayerAction();
                return;
            }

            if (skill.MultiAttack)
            {
                AddLog(_battleController.PlayerUseSkill(_player, skill, null, _enemies));
                FinishPlayerAction();
                return;
            }

            ShowTargetMenuForAttack(skill);
            AddLog($"Elige un objetivo para {skill.Name}.");
        }

        private void OnTargetSelected(Enemy enemy)
        {
            if (_battleEnded || _player == null || enemy == null || !enemy.IsAlive)
                return;

            PlayAttackAnimation(_player);

            string log = _pendingSkill == null
                ? _battleController.PlayerBasicAttack(_player, enemy)
                : _battleController.PlayerUseSkill(_player, _pendingSkill, enemy);

            _pendingSkill = null;
            AddLog(log);
            FinishPlayerAction();
        }

        private void OnItemPressed(int itemIndex)
        {
            if (_battleEnded || _player == null)
                return;

            if (_player.TryUseConsumable(itemIndex, out string log))
            {
                AddLog(log);
                FinishPlayerAction();
                return;
            }

            AddLog(log);
            RefreshUi();
            ShowItemMenu();
        }

        private void FinishPlayerAction()
        {
            if (_player != null)
                CallDeferred(nameof(PlayIdleAnimation), _player);

            RefreshUi();
            if (TryFinishBattle())
                return;

            ToggleBattleControls(false);
            CallDeferred(nameof(RunEnemyTurn));
        }

        private async void RunEnemyTurn()
        {
            await ToSignal(GetTree().CreateTimer(0.35), SceneTreeTimer.SignalName.Timeout);

            foreach (Enemy enemy in _enemies.Where(e => e != null && e.IsAlive))
            {
                PlayAttackAnimation(enemy);
            }

            await ToSignal(GetTree().CreateTimer(0.5), SceneTreeTimer.SignalName.Timeout);

            foreach (string enemyLog in _battleController.ExecuteEnemyTurn(_enemies, _player))
            {
                AddLog(enemyLog);
            }

            foreach (Enemy enemy in _enemies.Where(e => e != null && e.IsAlive))
            {
                PlayIdleAnimation(enemy);
            }

            RefreshUi();
            if (TryFinishBattle())
                return;

            ToggleBattleControls(true);
            ShowMainCommands();
            StartPlayerTurn();
        }

        private void StartPlayerTurn()
        {
            if (_battleEnded || _player == null)
                return;

            RefreshUi();
            ToggleBattleControls(true);
            AddLog("Tu turno: elige una accion.");
        }

        private bool TryFinishBattle()
        {
            if (_battleEnded || _player == null)
                return true;

            bool enemiesAlive = _battleController.HasAliveEnemies(_enemies);
            if (_player.IsAlive && enemiesAlive)
                return false;

            _battleEnded = true;
            BattleResult result = _battleController.BuildResult(_player, _enemies);
            AddLog(result.PlayerWon
                ? $"Victoria. Oro ganado: {result.EarnedGold}. Oro total: {_player.Gold}."
                : "Derrota. El equipo ha caido.");

            DetachCombatants();

            if (result.PlayerWon)
                ShowWinScene(result.EarnedGold);
            else
                ShowLoseScene();

            return true;
        }

        private void ShowWinScene(int earnedGold)
        {
            if (WinScenePacked == null)
            {
                WinScenePacked = ResourceLoader.Load<PackedScene>(WinScenePath);
                if (WinScenePacked == null)
                {
                    GD.PrintErr($"No se pudo cargar la escena de victoria en '{WinScenePath}'.");
                    EmitSignal(SignalName.BattleFinished, true, earnedGold);
                    QueueFree();
                    return;
                }
            }

            Node winInstance = WinScenePacked.Instantiate();
            if (winInstance is not WinScene winScene)
            {
                GD.PrintErr("La escena de victoria no usa WinScene.cs.");
                winInstance.QueueFree();
                EmitSignal(SignalName.BattleFinished, true, earnedGold);
                QueueFree();
                return;
            }

            AddChild(winScene);
            winScene.StartWin(_player, earnedGold);
            winScene.WinClosed += () => OnWinClosed(earnedGold);
        }

        private void ShowLoseScene()
        {
            if (LoseScenePacked == null)
            {
                LoseScenePacked = ResourceLoader.Load<PackedScene>(LoseScenePath);
                if (LoseScenePacked == null)
                {
                    GD.PrintErr($"No se pudo cargar la escena de derrota en '{LoseScenePath}'.");
                    EmitSignal(SignalName.BattleFinished, false, 0);
                    QueueFree();
                    return;
                }
            }

            Node loseInstance = LoseScenePacked.Instantiate();
            if (loseInstance is not LoseScene loseScene)
            {
                GD.PrintErr("La escena de derrota no usa LoseScene.cs.");
                loseInstance.QueueFree();
                EmitSignal(SignalName.BattleFinished, false, 0);
                QueueFree();
                return;
            }

            AddChild(loseScene);
            loseScene.StartLose(_player);
            loseScene.LoseClosed += OnLoseClosed;
        }

        private void OnWinClosed(int earnedGold)
        {
            EmitSignal(SignalName.BattleFinished, true, earnedGold);
            QueueFree();
        }

        private void OnLoseClosed()
        {
            EmitSignal(SignalName.BattleFinished, false, 0);
            QueueFree();
        }

        private void DetachCombatants()
        {
            if (_player != null && _player.GetParent() == _battleField)
                _battleField.RemoveChild(_player);

            foreach (Enemy enemy in _enemies)
            {
                if (enemy != null && enemy.GetParent() == _battleField)
                {
                    _battleField.RemoveChild(enemy);
                    enemy.QueueFree();
                }
            }
        }

        private void RefreshUi()
        {
            RefreshPlayerUi();
            RefreshEnemyUi();
        }

        private void RefreshPlayerUi()
        {
            if (_player == null)
                return;

            _playerNameLabel.Text = _player.CharacterName;
            _playerHpLabel.Text = $"HP: {_player.Health}/{_player.BaseHealth}";
            _playerHpBar.MaxValue = _player.BaseHealth;
            _playerHpBar.Value = _player.Health;
            _playerMpLabel.Text = $"MP: {_player.Mana}/{_player.BaseMana}";
            _playerMpBar.MaxValue = _player.BaseMana;
            _playerMpBar.Value = _player.Mana;
            _player.Modulate = _player.IsAlive ? Colors.White : new Color(1, 0.4f, 0.4f, 0.7f);
        }

        private void RefreshEnemyUi()
        {
            if (_enemyInfoContainer == null)
                return;

            ClearChildren(_enemyInfoContainer);

            foreach (Enemy enemy in _enemies)
            {
                var enemyPanel = new VBoxContainer();

                var nameLabel = new Label
                {
                    Text = enemy.CharacterName
                };
                enemyPanel.AddChild(nameLabel);

                var hpBar = new ProgressBar
                {
                    CustomMinimumSize = new Vector2(0, 18),
                    SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                    MaxValue = enemy.BaseHealth,
                    Value = enemy.Health
                };
                enemyPanel.AddChild(hpBar);

                var hpLabel = new Label
                {
                    Text = "HP: " + enemy.Health + "/" + enemy.BaseHealth +
                           " | MP: " + enemy.Mana + "/" + enemy.BaseMana +
                           " | Oro: " + enemy.MoneyLoot
                };
                enemyPanel.AddChild(hpLabel);

                _enemyInfoContainer.AddChild(enemyPanel);

                if (_enemyNodes.TryGetValue(enemy, out Node2D enemyNode))
                {
                    enemyNode.Visible = enemy.IsAlive;
                    enemyNode.Modulate = !enemy.IsAlive
                        ? new Color(1, 1, 1, 0.25f)
                        : enemy.Health < enemy.BaseHealth * 0.3f
                            ? new Color(1, 0.5f, 0.5f, 1)
                            : enemy.Health < enemy.BaseHealth * 0.6f
                                ? new Color(1, 0.85f, 0.6f, 1)
                                : Colors.White;
                }
            }

            if (_targetGrid.Visible)
                ShowTargetMenuForAttack(_pendingSkill);
        }

        private void ToggleBattleControls(bool enabled)
        {
            SetButtonsEnabled(_commandGrid, enabled);
            SetButtonsEnabled(_skillGrid, enabled);
            SetButtonsEnabled(_itemGrid, enabled);
            SetButtonsEnabled(_targetGrid, enabled);
            if (_backButton != null)
                _backButton.Disabled = !enabled;
        }

        private static void SetButtonsEnabled(Container container, bool enabled)
        {
            if (container == null)
                return;

            foreach (Node child in container.GetChildren())
            {
                if (child is Button button)
                    button.Disabled = !enabled;
            }
        }

        private static void ClearChildren(Container container)
        {
            if (container == null)
                return;

            foreach (Node child in container.GetChildren())
            {
                child.QueueFree();
            }
        }

        private void AddLog(string message)
        {
            if (_logLabel == null || string.IsNullOrWhiteSpace(message))
                return;

            foreach (string line in message.Split('\n'))
            {
                string trimmed = line.Trim();
                if (!string.IsNullOrWhiteSpace(trimmed))
                    _logLabel.AppendText(trimmed + "\n");
            }
        }

        private static Button CreateMenuButton(string text)
        {
            return new Button
            {
                Text = text,
                CustomMinimumSize = new Vector2(110, 58),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.Fill
            };
        }

        private static StyleBoxFlat CreatePlayerPanelStyle()
        {
            return new StyleBoxFlat
            {
                BgColor = new Color(0.08f, 0.08f, 0.10f, 0.85f),
                BorderColor = new Color(0.6f, 0.7f, 0.8f, 1.0f),
                BorderWidthLeft = 2,
                BorderWidthTop = 2,
                BorderWidthRight = 2,
                BorderWidthBottom = 2,
                CornerRadiusTopLeft = 8,
                CornerRadiusTopRight = 8,
                CornerRadiusBottomLeft = 8,
                CornerRadiusBottomRight = 8,
                ContentMarginLeft = 10,
                ContentMarginTop = 10,
                ContentMarginRight = 10,
                ContentMarginBottom = 10
            };
        }

        private static StyleBoxFlat CreateMiddlePanelStyle()
        {
            return new StyleBoxFlat
            {
                BgColor = new Color(0.10f, 0.08f, 0.08f, 0.85f),
                BorderColor = new Color(0.8f, 0.6f, 0.6f, 1.0f),
                BorderWidthLeft = 2,
                BorderWidthTop = 2,
                BorderWidthRight = 2,
                BorderWidthBottom = 2,
                CornerRadiusTopLeft = 8,
                CornerRadiusTopRight = 8,
                CornerRadiusBottomLeft = 8,
                CornerRadiusBottomRight = 8,
                ContentMarginLeft = 10,
                ContentMarginTop = 10,
                ContentMarginRight = 10,
                ContentMarginBottom = 10
            };
        }

        private static StyleBoxFlat CreateRightPanelStyle()
        {
            return new StyleBoxFlat
            {
                BgColor = new Color(0.08f, 0.10f, 0.08f, 0.85f),
                BorderColor = new Color(0.6f, 0.8f, 0.6f, 1.0f),
                BorderWidthLeft = 2,
                BorderWidthTop = 2,
                BorderWidthRight = 2,
                BorderWidthBottom = 2,
                CornerRadiusTopLeft = 8,
                CornerRadiusTopRight = 8,
                CornerRadiusBottomLeft = 8,
                CornerRadiusBottomRight = 8,
                ContentMarginLeft = 10,
                ContentMarginTop = 10,
                ContentMarginRight = 10,
                ContentMarginBottom = 10
            };
        }
    }
}

