using Godot;
using System.Collections.Generic;
using SpellsAndRooms.scripts.Characters;
using SpellsAndRooms.scripts.Items;

namespace SpellsAndRooms.scripts.Turns
{
    public partial class TreasureScene : Node
    {
        [Signal]
        public delegate void TreasureClosedEventHandler();

        [Export] public Texture2D BackgroundTexture;

        private enum RewardType
        {
            Gold,
            Consumable,
            Passive,
            Skill
        }

        private sealed class TreasureReward
        {
            public RewardType Type { get; init; }
            public string Id { get; init; } = string.Empty;
            public string Title { get; init; } = string.Empty;
            public string Subtitle { get; init; } = string.Empty;
            public string Description { get; init; } = string.Empty;
            public string ImagePath { get; init; } = string.Empty;
            public int GoldAmount { get; init; }
            public string ConsumableType { get; init; } = string.Empty;
            public string ConsumableSubtype { get; init; } = string.Empty;
            public int ConsumablePotency { get; init; }
            public string PassiveType { get; init; } = string.Empty;
            public int PassiveBonusValue { get; init; }
        }

        private const float GoldWeight = 0.55f;
        private const float ConsumableWeight = 1.0f;
        private const float PassiveWeight = 1.0f;
        private const float SkillWeight = 0.35f;

        private const string DefaultPromptText = "Un cofre te espera. Decide si te quedas con el botin.";
        private const string DefaultTreasureBackgroundPath = "res://assets/Turns/BattelBackground.png";

        private Player _player;
        private ItemDatabase _itemDatabase;
        private SkillDatabase _skillDatabase;
        private readonly RandomNumberGenerator _rng = new RandomNumberGenerator();
        private TreasureReward _reward;

        private CanvasLayer _backgroundLayer;
        private CanvasLayer _uiLayer;
        private Control _uiRoot;

        private Label _playerGoldLabel;
        private Label _titleLabel;
        private Label _promptLabel;
        private TextureRect _rewardImage;
        private Label _rewardNameLabel;
        private Label _rewardTypeLabel;
        private RichTextLabel _rewardDescriptionLabel;
        private Button _takeButton;
        private Button _leaveButton;
        private Button _continueButton;

        private PanelContainer _replacementPanel;
        private Label _replacementLabel;
        private GridContainer _replacementGrid;
        private Button _replacementCancelButton;

        private bool _uiBuilt;
        private bool _rewardResolved;
        private Skill _pendingSkillToLearn;
        private TreasureReward _pendingSkillReward;
        private bool _isChoosingReplacement;

        public void StartTreasure(Player player)
        {
            _player = player;
            _itemDatabase = new ItemDatabase();
            _skillDatabase = new SkillDatabase();
            _rng.Randomize();
            GenerateReward();

            if (_reward.Type == RewardType.Gold && _player != null)
            {
                _player.AddGold(_reward.GoldAmount);
                _rewardResolved = true;
            }

            if (IsInsideTree())
            {
                BuildUi();
                RefreshUi();
            }
        }

        public override void _Ready()
        {
            BuildUi();
            if (_player != null && _reward == null)
                GenerateReward();

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
                Name = "TreasureBackground",
                Texture = BackgroundTexture ?? GD.Load<Texture2D>(DefaultTreasureBackgroundPath),
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

            var outer = new MarginContainer
            {
                Name = "OuterMargin",
                AnchorLeft = 0,
                AnchorTop = 0,
                AnchorRight = 1,
                AnchorBottom = 1
            };
            _uiRoot.AddChild(outer);

            var inner = new VBoxContainer
            {
                Name = "InnerLayout",
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill
            };
            outer.AddChild(inner);

            _titleLabel = new Label
            {
                Text = "=== COFRE ===",
                HorizontalAlignment = HorizontalAlignment.Center,
                CustomMinimumSize = new Vector2(0, 48)
            };
            inner.AddChild(_titleLabel);

            _playerGoldLabel = new Label
            {
                Text = "Oro: 0",
                HorizontalAlignment = HorizontalAlignment.Center,
                CustomMinimumSize = new Vector2(0, 28)
            };
            inner.AddChild(_playerGoldLabel);

            _promptLabel = new Label
            {
                Text = DefaultPromptText,
                HorizontalAlignment = HorizontalAlignment.Center,
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            };
            inner.AddChild(_promptLabel);

            var rewardPanel = new PanelContainer
            {
                CustomMinimumSize = new Vector2(0, 320),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill
            };
            rewardPanel.AddThemeStyleboxOverride("panel", CreateCardStyle());
            inner.AddChild(rewardPanel);

            var rewardLayout = new VBoxContainer
            {
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill
            };
            rewardPanel.AddChild(rewardLayout);

            var imageFrame = new PanelContainer
            {
                CustomMinimumSize = new Vector2(0, 130),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
            };
            imageFrame.AddThemeStyleboxOverride("panel", CreateImageFrameStyle());
            rewardLayout.AddChild(imageFrame);

            _rewardImage = new TextureRect
            {
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill
            };
            imageFrame.AddChild(_rewardImage);

            _rewardNameLabel = new Label
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                CustomMinimumSize = new Vector2(0, 34)
            };
            rewardLayout.AddChild(_rewardNameLabel);

            _rewardTypeLabel = new Label
            {
                HorizontalAlignment = HorizontalAlignment.Center
            };
            rewardLayout.AddChild(_rewardTypeLabel);

            _rewardDescriptionLabel = new RichTextLabel
            {
                BbcodeEnabled = false,
                FitContent = true,
                ScrollFollowing = true,
                CustomMinimumSize = new Vector2(0, 100),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill
            };
            rewardLayout.AddChild(_rewardDescriptionLabel);

            var buttonRow = new HBoxContainer
            {
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
            };
            rewardLayout.AddChild(buttonRow);

            _takeButton = new Button
            {
                Text = "Coger",
                CustomMinimumSize = new Vector2(0, 42),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
            };
            _takeButton.Pressed += OnTakePressed;
            buttonRow.AddChild(_takeButton);

            _leaveButton = new Button
            {
                Text = "Dejar",
                CustomMinimumSize = new Vector2(0, 42),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
            };
            _leaveButton.Pressed += OnLeavePressed;
            buttonRow.AddChild(_leaveButton);

            _continueButton = new Button
            {
                Text = "Continuar",
                Visible = false,
                CustomMinimumSize = new Vector2(0, 42),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
            };
            _continueButton.Pressed += OnContinuePressed;
            rewardLayout.AddChild(_continueButton);

            _replacementPanel = new PanelContainer
            {
                Name = "ReplacementPanel",
                Visible = false,
                AnchorLeft = 0.14f,
                AnchorTop = 0.18f,
                AnchorRight = 0.86f,
                AnchorBottom = 0.82f,
                MouseFilter = Control.MouseFilterEnum.Stop
            };
            _replacementPanel.AddThemeStyleboxOverride("panel", CreateCardStyle());
            _uiRoot.AddChild(_replacementPanel);

            var replacementLayout = new VBoxContainer
            {
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill
            };
            _replacementPanel.AddChild(replacementLayout);

            _replacementLabel = new Label
            {
                Text = "Elige una habilidad a reemplazar",
                HorizontalAlignment = HorizontalAlignment.Center,
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            };
            replacementLayout.AddChild(_replacementLabel);

            _replacementGrid = new GridContainer
            {
                Columns = 2,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill
            };
            replacementLayout.AddChild(_replacementGrid);

            _replacementCancelButton = new Button
            {
                Text = "Cancelar",
                CustomMinimumSize = new Vector2(0, 40)
            };
            _replacementCancelButton.Pressed += CancelSkillReplacement;
            replacementLayout.AddChild(_replacementCancelButton);
        }

        private void RefreshUi()
        {
            if (_player == null || _reward == null)
                return;

            _playerGoldLabel.Text = $"Oro: {_player.Gold}";
            _promptLabel.Text = _rewardResolved ? "El cofre ya fue resuelto." : DefaultPromptText;

            _rewardNameLabel.Text = _reward.Title;
            _rewardTypeLabel.Text = _reward.Subtitle;
            _rewardDescriptionLabel.Clear();
            _rewardDescriptionLabel.AppendText(_reward.Description);

            Texture2D texture = TryLoadTexture(_reward.ImagePath);
            _rewardImage.Texture = texture;
            _rewardImage.Visible = texture != null;

            bool isGold = _reward.Type == RewardType.Gold;
            _takeButton.Visible = !isGold;
            _leaveButton.Visible = !isGold;
            _continueButton.Visible = isGold || _rewardResolved;

            if (isGold)
            {
                _continueButton.Text = "Continuar";
                _rewardDescriptionLabel.Clear();
                _rewardDescriptionLabel.AppendText($"Has encontrado {_reward.GoldAmount} de oro.");
            }
            else if (_rewardResolved)
            {
                _continueButton.Text = "Continuar";
            }
            else
            {
                _continueButton.Visible = false;
            }
        }

        private void GenerateReward()
        {
            _reward = PickReward();
            if (_reward == null)
            {
                _reward = new TreasureReward
                {
                    Type = RewardType.Gold,
                    Title = "Monedas",
                    Subtitle = "Dinero",
                    Description = "No habia botin util. Al menos has encontrado dinero.",
                    GoldAmount = 20
                };
            }
        }

        private TreasureReward PickReward()
        {
            List<TreasureReward> consumables = BuildConsumablePool();
            List<TreasureReward> passives = BuildPassivePool();
            List<TreasureReward> skills = BuildSkillPool();

            bool hasConsumables = consumables.Count > 0;
            bool hasPassives = passives.Count > 0;
            bool hasSkills = skills.Count > 0;

            float totalWeight = GoldWeight;
            totalWeight += hasConsumables ? ConsumableWeight : 0.0f;
            totalWeight += hasPassives ? PassiveWeight : 0.0f;
            totalWeight += hasSkills ? SkillWeight : 0.0f;

            float roll = _rng.RandfRange(0.0f, totalWeight);
            if (roll <= GoldWeight)
            {
                return new TreasureReward
                {
                    Type = RewardType.Gold,
                    Title = "Oro",
                    Subtitle = "Dinero",
                    Description = "Has encontrado una bolsa de oro.",
                    GoldAmount = _rng.RandiRange(15, 40)
                };
            }

            roll -= GoldWeight;
            if (hasConsumables)
            {
                if (roll <= ConsumableWeight)
                    return consumables[_rng.RandiRange(0, consumables.Count - 1)];
                roll -= ConsumableWeight;
            }

            if (hasPassives)
            {
                if (roll <= PassiveWeight)
                    return passives[_rng.RandiRange(0, passives.Count - 1)];
                roll -= PassiveWeight;
            }

            if (hasSkills)
                return skills[_rng.RandiRange(0, skills.Count - 1)];

            return null;
        }

        private List<TreasureReward> BuildConsumablePool()
        {
            var pool = new List<TreasureReward>();
            foreach (ItemDatabase.ConsumableDefinition def in _itemDatabase.Consumables)
            {
                pool.Add(new TreasureReward
                {
                    Type = RewardType.Consumable,
                    Id = $"consumable:{def.Name}",
                    Title = def.Name,
                    Subtitle = $"Consumible - {def.Subtype}",
                    Description = def.Description,
                    ImagePath = ResolveConsumableImagePath(def),
                    ConsumableType = def.Type,
                    ConsumableSubtype = def.Subtype,
                    ConsumablePotency = def.Potency
                });
            }

            return pool;
        }

        private List<TreasureReward> BuildPassivePool()
        {
            var pool = new List<TreasureReward>();
            foreach (ItemDatabase.PassiveDefinition def in _itemDatabase.Passives)
            {
                pool.Add(new TreasureReward
                {
                    Type = RewardType.Passive,
                    Id = $"passive:{def.Name}",
                    Title = def.Name,
                    Subtitle = $"Pasivo - {def.Type}",
                    Description = def.Description,
                    ImagePath = ResolvePassiveImagePath(def),
                    PassiveType = def.Type,
                    PassiveBonusValue = def.BonusValue
                });
            }

            return pool;
        }

        private List<TreasureReward> BuildSkillPool()
        {
            var pool = new List<TreasureReward>();
            foreach (SkillDatabase.SkillDefinition def in _skillDatabase.SkillDefinitions)
            {
                if (_player != null && _player.HasSkill(def.Name))
                    continue;

                pool.Add(new TreasureReward
                {
                    Type = RewardType.Skill,
                    Id = $"skill:{def.Name}",
                    Title = def.Name,
                    Subtitle = "Habilidad",
                    Description = def.Description,
                    ImagePath = ResolveSkillImagePath(def)
                });
            }

            return pool;
        }

        private void OnTakePressed()
        {
            if (_player == null || _reward == null || _rewardResolved)
                return;

            switch (_reward.Type)
            {
                case RewardType.Consumable:
                    _player.AddConsumable(new ConsumableItem
                    {
                        ItemName = _reward.Title,
                        Type = _reward.ConsumableType,
                        Subtype = _reward.ConsumableSubtype,
                        Potency = _reward.ConsumablePotency,
                        Description = _reward.Description
                    });
                    CloseTreasure($"Has cogido {_reward.Title}.");
                    return;

                case RewardType.Passive:
                    _player.AddPassive(new PassiveItem
                    {
                        ItemName = _reward.Title,
                        Type = _reward.PassiveType,
                        BonusValue = _reward.PassiveBonusValue,
                        Description = _reward.Description
                    });
                    CloseTreasure($"Has cogido {_reward.Title}.");
                    return;

                case RewardType.Skill:
                    if (!_skillDatabase.TryGetSkill(_reward.Title, out Skill skill) || skill == null)
                    {
                        CloseTreasure("No se pudo reclamar la habilidad.");
                        return;
                    }

                    if (_player.SkillCount >= Player.MaxSkillSlots)
                    {
                        ShowSkillReplacementPrompt(_reward, skill);
                        return;
                    }

                    if (!_player.TryLearnSkill(skill))
                    {
                        CloseTreasure("No se pudo reclamar la habilidad.");
                        return;
                    }

                    CloseTreasure($"Has aprendido {_reward.Title}.");
                    return;
            }
        }

        private void OnLeavePressed()
        {
            if (_reward == null || _rewardResolved)
                return;

            CloseTreasure("Has dejado el botin del cofre.");
        }

        private void OnContinuePressed()
        {
            if (_rewardResolved)
                CloseTreasure("Continuas tu ruta.");
        }

        private void ShowSkillReplacementPrompt(TreasureReward reward, Skill skill)
        {
            _pendingSkillReward = reward;
            _pendingSkillToLearn = skill;
            _isChoosingReplacement = true;

            ClearContainer(_replacementGrid);
            _replacementLabel.Text = $"Tu lista esta llena. Elige una habilidad para reemplazar por {skill.Name}.";
            _replacementPanel.Visible = true;

            for (int i = 0; i < _player.Skills.Count; i++)
            {
                int skillIndex = i;
                Skill currentSkill = _player.Skills[skillIndex];
                if (currentSkill == null)
                    continue;

                Button replaceButton = new Button
                {
                    Text = currentSkill.Name,
                    CustomMinimumSize = new Vector2(0, 40),
                    SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
                };
                replaceButton.Pressed += () => ConfirmSkillReplacement(skillIndex);
                _replacementGrid.AddChild(replaceButton);
            }
        }

        private void ConfirmSkillReplacement(int skillIndex)
        {
            if (!_isChoosingReplacement || _pendingSkillReward == null || _pendingSkillToLearn == null)
                return;

            if (!_player.TryReplaceSkill(skillIndex, _pendingSkillToLearn))
            {
                _promptLabel.Text = "No se pudo reemplazar esa habilidad.";
                return;
            }

            CloseTreasure($"Has reemplazado una habilidad por {_pendingSkillToLearn.Name}.");
        }

        private void CancelSkillReplacement()
        {
            if (!_isChoosingReplacement)
                return;

            _promptLabel.Text = DefaultPromptText;
            HideSkillReplacementPrompt();
        }

        private void HideSkillReplacementPrompt()
        {
            _isChoosingReplacement = false;
            _pendingSkillReward = null;
            _pendingSkillToLearn = null;

            if (_replacementPanel != null)
                _replacementPanel.Visible = false;
        }

        private void CloseTreasure(string message)
        {
            _rewardResolved = true;
            _promptLabel.Text = message;
            HideSkillReplacementPrompt();
            RefreshUi();
            EmitSignal(SignalName.TreasureClosed);
            QueueFree();
        }

        private Texture2D TryLoadTexture(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !ResourceLoader.Exists(path))
                return null;

            return GD.Load<Texture2D>(path);
        }

        private static string Normalize(string value)
        {
            return (value ?? string.Empty).Trim().ToLowerInvariant();
        }

        private static string FirstExistingPath(params string[] candidates)
        {
            foreach (string candidate in candidates)
            {
                if (!string.IsNullOrWhiteSpace(candidate) && ResourceLoader.Exists(candidate))
                    return candidate;
            }

            return string.Empty;
        }

        private string ResolveConsumableImagePath(ItemDatabase.ConsumableDefinition def)
        {
            string name = Normalize(def.Name);
            string subtype = Normalize(def.Subtype);

            if (name.Contains("health") || subtype.Contains("healing"))
                return FirstExistingPath("res://assets/Items/Consumable/Poti.png");

            if (name.Contains("mana") || subtype.Contains("mana"))
                return FirstExistingPath("res://assets/Items/Consumable/PotiManai.png");

            return FirstExistingPath(
                $"res://assets/Items/Consumable/{def.Name}.png",
                "res://assets/Items/Consumable/Poti.png",
                "res://assets/Items/Consumable/PotiManai.png");
        }

        private string ResolvePassiveImagePath(ItemDatabase.PassiveDefinition def)
        {
            return FirstExistingPath(
                $"res://assets/Items/Passive/{def.Name}.png",
                "res://assets/Items/Passive/pecheGris.png");
        }

        private string ResolveSkillImagePath(SkillDatabase.SkillDefinition def)
        {
            string name = Normalize(def.Name);

            if (name.Contains("pyro"))
                return FirstExistingPath("res://assets/Characters/Enemy/BlackGoblin.png");
            if (name.Contains("aqua"))
                return FirstExistingPath("res://assets/Characters/Player/MagoAzul.png");
            if (name.Contains("earth"))
                return FirstExistingPath("res://assets/Characters/Enemy/Esqueleto.png");

            return FirstExistingPath(
                "res://assets/Characters/Enemy/Slime.png",
                "res://assets/Characters/Player/CaballeroNegro.png");
        }

        private static StyleBoxFlat CreateCardStyle()
        {
            return new StyleBoxFlat
            {
                BgColor = new Color(0.10f, 0.10f, 0.14f, 0.92f),
                BorderColor = new Color(0.83f, 0.71f, 0.41f, 1.0f),
                BorderWidthLeft = 3,
                BorderWidthTop = 3,
                BorderWidthRight = 3,
                BorderWidthBottom = 3,
                CornerRadiusTopLeft = 12,
                CornerRadiusTopRight = 12,
                CornerRadiusBottomLeft = 12,
                CornerRadiusBottomRight = 12,
                ContentMarginLeft = 12,
                ContentMarginTop = 12,
                ContentMarginRight = 12,
                ContentMarginBottom = 12
            };
        }

        private static StyleBoxFlat CreateImageFrameStyle()
        {
            return new StyleBoxFlat
            {
                BgColor = new Color(0.06f, 0.06f, 0.09f, 0.90f),
                BorderColor = new Color(0.44f, 0.50f, 0.62f, 1.0f),
                BorderWidthLeft = 2,
                BorderWidthTop = 2,
                BorderWidthRight = 2,
                BorderWidthBottom = 2,
                CornerRadiusTopLeft = 8,
                CornerRadiusTopRight = 8,
                CornerRadiusBottomLeft = 8,
                CornerRadiusBottomRight = 8,
                ContentMarginLeft = 6,
                ContentMarginTop = 6,
                ContentMarginRight = 6,
                ContentMarginBottom = 6
            };
        }

        private static void ClearContainer(Container container)
        {
            if (container == null)
                return;

            foreach (Node child in container.GetChildren())
                child.QueueFree();
        }
    }
}
