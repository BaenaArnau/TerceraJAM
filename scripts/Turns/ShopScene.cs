using Godot;
using System;
using System.Collections.Generic;
using SpellsAndRooms.scripts.Items;
using SpellsAndRooms.scripts.Characters;

namespace SpellsAndRooms.scripts.Turns
{
    public partial class ShopScene : Node
    {
        [Signal]
        public delegate void ShopClosedEventHandler();

        private enum OfferType
        {
            Consumable,
            Passive,
            Skill
        }

        private sealed class ShopOffer
        {
            public OfferType Type { get; init; }
            public string Id { get; init; } = string.Empty;
            public string Title { get; init; } = string.Empty;
            public string Subtitle { get; init; } = string.Empty;
            public string Description { get; init; } = string.Empty;
            public string ImagePath { get; init; } = string.Empty;
            public int Price { get; init; }
            public bool Purchased { get; set; }
        }

        private const float ConsumableWeight = 1.0f;
        private const float PassiveWeight = 1.0f;
        private const float SkillWeight = 0.45f;
        private const int OffersPerVisit = 3;
        private const string DefaultStatusText = "Pasa el raton por una carta para ver la descripcion.";

        private Player _player;
        private ItemDatabase _itemDatabase;
        private SkillDatabase _skillDatabase;
        private readonly RandomNumberGenerator _rng = new RandomNumberGenerator();
        private CanvasLayer _backgroundLayer;
        private CanvasLayer _uiLayer;
        private Control _uiRoot;

        private Label _playerGoldLabel;
        private Label _statusLabel;
        private HBoxContainer _cardsContainer;
        private Button _closeButton;
        private PanelContainer _replacementPanel;
        private Label _replacementLabel;
        private GridContainer _replacementGrid;
        private Button _replacementCancelButton;
        private bool _uiBuilt;
        private bool _isChoosingReplacement;
        private int _pendingSkillOfferIndex = -1;
        private Skill _pendingSkillToLearn;
        private readonly List<ShopOffer> _activeOffers = new List<ShopOffer>();

        public void StartShop(Player player)
        {
            _player = player;
            _itemDatabase = new ItemDatabase();
            _skillDatabase = new SkillDatabase();
            _rng.Randomize();
            GenerateOffers();

            if (IsInsideTree())
            {
                BuildUi();
                RefreshUi(resetStatus: true);
            }
        }

        public override void _Ready()
        {
            BuildUi();
            if (_player != null && _activeOffers.Count == 0)
                GenerateOffers();

            if (_player != null)
                RefreshUi(resetStatus: true);
        }

        private void BuildUi()
        {
            if (_uiBuilt)
                return;

            _uiBuilt = true;

            // Fondo en CanvasLayer -1
            _backgroundLayer = new CanvasLayer { Name = "BackgroundLayer", Layer = -1 };
            AddChild(_backgroundLayer);

            var background = new TextureRect
            {
                Name = "ShopBackground",
                Texture = GD.Load<Texture2D>("res://assets/Turns/Shop.png"),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.Scale,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            background.AnchorLeft = 0;
            background.AnchorTop = 0;
            background.AnchorRight = 1;
            background.AnchorBottom = 1;
            _backgroundLayer.AddChild(background);

            // UI en CanvasLayer 1
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

            // Título
            var titleLabel = new Label
            {
                Text = "=== TIENDA (3 CARTAS) ===",
                HorizontalAlignment = HorizontalAlignment.Center,
                CustomMinimumSize = new Vector2(0, 50)
            };
            inner.AddChild(titleLabel);

            // Oro del jugador
            _playerGoldLabel = new Label
            {
                Text = $"Oro: 0",
                HorizontalAlignment = HorizontalAlignment.Center,
                CustomMinimumSize = new Vector2(0, 30)
            };
            inner.AddChild(_playerGoldLabel);

            _statusLabel = new Label
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                Text = DefaultStatusText,
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            };
            inner.AddChild(_statusLabel);

            // Fila de 3 cartas
            _cardsContainer = new HBoxContainer
            {
                Name = "CardsRow",
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill
            };
            inner.AddChild(_cardsContainer);

            // Botón de salida
            _closeButton = new Button
            {
                Text = "Cerrar Tienda",
                CustomMinimumSize = new Vector2(0, 40)
            };
            _closeButton.Pressed += OnClosePressed;
            inner.AddChild(_closeButton);

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

        private void RefreshUi(bool resetStatus = false)
        {
            if (_player == null || _itemDatabase == null)
                return;

            _playerGoldLabel.Text = $"Oro: {_player.Gold}";
            if (resetStatus)
                _statusLabel.Text = DefaultStatusText;

            foreach (Node child in _cardsContainer.GetChildren())
                child.QueueFree();

            for (int i = 0; i < _activeOffers.Count; i++)
            {
                ShopOffer offer = _activeOffers[i];
                _cardsContainer.AddChild(CreateOfferCard(offer, i));
            }
        }

        private Control CreateOfferCard(ShopOffer offer, int index)
        {
            var panel = new PanelContainer
            {
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill,
                CustomMinimumSize = new Vector2(250, 0)
            };

            panel.AddThemeStyleboxOverride("panel", CreateCardStyle());
            panel.TooltipText = $"{offer.Title}\n{offer.Description}\nPrecio: {offer.Price} oro";
            panel.MouseEntered += () => OnOfferHover(offer);
            panel.MouseExited += OnOfferHoverExit;

            var layout = new VBoxContainer
            {
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill
            };
            panel.AddChild(layout);

            var imageFrame = new PanelContainer
            {
                CustomMinimumSize = new Vector2(0, 130),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
            };
            imageFrame.AddThemeStyleboxOverride("panel", CreateImageFrameStyle());
            layout.AddChild(imageFrame);

            var imageTexture = new TextureRect
            {
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill
            };
            Texture2D texture = TryLoadTexture(offer.ImagePath);
            if (texture != null)
                imageTexture.Texture = texture;
            imageFrame.AddChild(imageTexture);

            if (texture == null)
            {
                // Fallback simple para cartas sin arte asociado.
                imageFrame.AddChild(new Label
                {
                    Text = "?",
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                    SizeFlagsVertical = Control.SizeFlags.ExpandFill
                });
            }

            layout.AddChild(new Label
            {
                Text = offer.Title,
                HorizontalAlignment = HorizontalAlignment.Center,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                CustomMinimumSize = new Vector2(0, 36)
            });

            layout.AddChild(new Label
            {
                Text = offer.Subtitle,
                HorizontalAlignment = HorizontalAlignment.Center
            });

            layout.AddChild(new Label
            {
                Text = TruncateDescription(offer.Description),
                SizeFlagsVertical = Control.SizeFlags.ExpandFill,
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            });

            var buyButton = new Button
            {
                Text = offer.Purchased ? "Comprado" : $"Comprar ({offer.Price} oro)",
                Disabled = offer.Purchased || !_player.CanAfford(offer.Price),
                CustomMinimumSize = new Vector2(0, 42)
            };

            buyButton.Pressed += () => OnBuyPressed(index);
            layout.AddChild(buyButton);

            return panel;
        }

        private static string TruncateDescription(string text)
        {
            string safe = (text ?? string.Empty).Trim();
            if (safe.Length <= 90)
                return safe;

            return safe.Substring(0, 87) + "...";
        }

        private static StyleBoxFlat CreateCardStyle()
        {
            var style = new StyleBoxFlat
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

            return style;
        }

        private static StyleBoxFlat CreateImageFrameStyle()
        {
            var style = new StyleBoxFlat
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

            return style;
        }

        private void OnOfferHover(ShopOffer offer)
        {
            if (offer == null)
                return;

            _statusLabel.Text = $"{offer.Title}: {offer.Description}";
        }

        private void OnOfferHoverExit()
        {
            _statusLabel.Text = DefaultStatusText;
        }

        private Texture2D TryLoadTexture(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !ResourceLoader.Exists(path))
                return null;

            return GD.Load<Texture2D>(path);
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

        private static string Normalize(string value)
        {
            return (value ?? string.Empty).Trim().ToLowerInvariant();
        }

        private void GenerateOffers()
        {
            _activeOffers.Clear();
            var consumedIds = new HashSet<string>();

            List<ShopOffer> consumablePool = BuildConsumablePool();
            List<ShopOffer> passivePool = BuildPassivePool();
            List<ShopOffer> skillPool = BuildSkillPool();

            while (_activeOffers.Count < OffersPerVisit)
            {
                OfferType? selectedType = PickWeightedType(consumablePool, passivePool, skillPool, consumedIds);
                if (!selectedType.HasValue)
                    break;

                ShopOffer selectedOffer = PickOffer(selectedType.Value, consumablePool, passivePool, skillPool, consumedIds);
                if (selectedOffer == null)
                    continue;

                _activeOffers.Add(selectedOffer);
                consumedIds.Add(selectedOffer.Id);
            }

            // Fallback: si faltan cartas por poca data, completamos con cualquier oferta disponible.
            foreach (ShopOffer offer in consumablePool)
            {
                if (_activeOffers.Count >= OffersPerVisit)
                    break;
                if (consumedIds.Add(offer.Id))
                    _activeOffers.Add(offer);
            }

            foreach (ShopOffer offer in passivePool)
            {
                if (_activeOffers.Count >= OffersPerVisit)
                    break;
                if (consumedIds.Add(offer.Id))
                    _activeOffers.Add(offer);
            }

            foreach (ShopOffer offer in skillPool)
            {
                if (_activeOffers.Count >= OffersPerVisit)
                    break;
                if (consumedIds.Add(offer.Id))
                    _activeOffers.Add(offer);
            }
        }

        private List<ShopOffer> BuildConsumablePool()
        {
            var pool = new List<ShopOffer>();
            foreach (ItemDatabase.ConsumableDefinition def in _itemDatabase.Consumables)
            {
                pool.Add(new ShopOffer
                {
                    Type = OfferType.Consumable,
                    Id = $"consumable:{def.Name}",
                    Title = def.Name,
                    Subtitle = $"Consumible - {def.Subtype}",
                    Description = def.Description,
                    ImagePath = ResolveConsumableImagePath(def),
                    Price = def.Price
                });
            }

            return pool;
        }

        private List<ShopOffer> BuildPassivePool()
        {
            var pool = new List<ShopOffer>();
            foreach (ItemDatabase.PassiveDefinition def in _itemDatabase.Passives)
            {
                pool.Add(new ShopOffer
                {
                    Type = OfferType.Passive,
                    Id = $"passive:{def.Name}",
                    Title = def.Name,
                    Subtitle = $"Pasivo - {def.Type}",
                    Description = def.Description,
                    ImagePath = ResolvePassiveImagePath(def),
                    Price = def.Price
                });
            }

            return pool;
        }

        private List<ShopOffer> BuildSkillPool()
        {
            var pool = new List<ShopOffer>();
            foreach (SkillDatabase.SkillDefinition def in _skillDatabase.SkillDefinitions)
            {
                if (_player != null && _player.HasSkill(def.Name))
                    continue;

                pool.Add(new ShopOffer
                {
                    Type = OfferType.Skill,
                    Id = $"skill:{def.Name}",
                    Title = def.Name,
                    Subtitle = "Habilidad",
                    Description = def.Description,
                    ImagePath = ResolveSkillImagePath(def),
                    Price = def.Price
                });
            }

            return pool;
        }

        private OfferType? PickWeightedType(
            List<ShopOffer> consumables,
            List<ShopOffer> passives,
            List<ShopOffer> skills,
            HashSet<string> consumedIds)
        {
            bool hasConsumables = HasAvailable(consumables, consumedIds);
            bool hasPassives = HasAvailable(passives, consumedIds);
            bool hasSkills = HasAvailable(skills, consumedIds);

            float totalWeight = 0.0f;
            totalWeight += hasConsumables ? ConsumableWeight : 0.0f;
            totalWeight += hasPassives ? PassiveWeight : 0.0f;
            totalWeight += hasSkills ? SkillWeight : 0.0f;

            if (totalWeight <= 0.0f)
                return null;

            float roll = _rng.RandfRange(0.0f, totalWeight);
            if (hasConsumables)
            {
                if (roll <= ConsumableWeight)
                    return OfferType.Consumable;
                roll -= ConsumableWeight;
            }

            if (hasPassives)
            {
                if (roll <= PassiveWeight)
                    return OfferType.Passive;
            }

            if (hasSkills)
                return OfferType.Skill;

            return null;
        }

        private static bool HasAvailable(List<ShopOffer> offers, HashSet<string> consumedIds)
        {
            foreach (ShopOffer offer in offers)
            {
                if (!consumedIds.Contains(offer.Id))
                    return true;
            }

            return false;
        }

        private ShopOffer PickOffer(
            OfferType offerType,
            List<ShopOffer> consumables,
            List<ShopOffer> passives,
            List<ShopOffer> skills,
            HashSet<string> consumedIds)
        {
            List<ShopOffer> source = offerType switch
            {
                OfferType.Consumable => consumables,
                OfferType.Passive => passives,
                OfferType.Skill => skills,
                _ => null
            };

            if (source == null)
                return null;

            var candidates = new List<ShopOffer>();
            foreach (ShopOffer offer in source)
            {
                if (!consumedIds.Contains(offer.Id))
                    candidates.Add(offer);
            }

            if (candidates.Count == 0)
                return null;

            return candidates[_rng.RandiRange(0, candidates.Count - 1)];
        }

        private void OnBuyPressed(int offerIndex)
        {
            if (_isChoosingReplacement)
                return;

            if (_player == null || offerIndex < 0 || offerIndex >= _activeOffers.Count)
                return;

            ShopOffer offer = _activeOffers[offerIndex];
            if (offer.Purchased)
                return;

            if (!_player.CanAfford(offer.Price))
            {
                _statusLabel.Text = "No tienes oro suficiente.";
                RefreshUi();
                return;
            }

            if (offer.Type == OfferType.Skill)
            {
                string skillName = offer.Id.Replace("skill:", string.Empty);
                if (_player.HasSkill(skillName))
                {
                    _statusLabel.Text = "Ya tienes esa habilidad.";
                    RefreshUi();
                    return;
                }

                if (!_skillDatabase.TryGetSkill(skillName, out Skill skillToLearn) || skillToLearn == null)
                {
                    _statusLabel.Text = "No se pudo preparar la habilidad.";
                    return;
                }

                if (_player.SkillCount >= Player.MaxSkillSlots)
                {
                    ShowSkillReplacementPrompt(offerIndex, skillToLearn);
                    return;
                }
            }

            bool bought = ApplyOffer(offer);
            if (!bought)
            {
                _statusLabel.Text = "No se pudo completar la compra.";
                return;
            }

            _player.RemoveGold(offer.Price);
            offer.Purchased = true;
            _statusLabel.Text = $"Compraste {offer.Title}.";
            RefreshUi();
        }

        private bool ApplyOffer(ShopOffer offer)
        {
            switch (offer.Type)
            {
                case OfferType.Consumable:
                    return ApplyConsumable(offer);

                case OfferType.Passive:
                    return ApplyPassive(offer);

                case OfferType.Skill:
                    return ApplySkill(offer);

                default:
                    return false;
            }
        }

        private bool ApplyConsumable(ShopOffer offer)
        {
            ItemDatabase.ConsumableDefinition selected = null;
            foreach (ItemDatabase.ConsumableDefinition def in _itemDatabase.Consumables)
            {
                if (offer.Id == $"consumable:{def.Name}")
                {
                    selected = def;
                    break;
                }
            }

            if (selected == null)
                return false;

            _player.AddConsumable(new ConsumableItem
            {
                ItemName = selected.Name,
                Type = selected.Type,
                Subtype = selected.Subtype,
                Potency = selected.Potency,
                Description = selected.Description
            });

            return true;
        }

        private bool ApplyPassive(ShopOffer offer)
        {
            ItemDatabase.PassiveDefinition selected = null;
            foreach (ItemDatabase.PassiveDefinition def in _itemDatabase.Passives)
            {
                if (offer.Id == $"passive:{def.Name}")
                {
                    selected = def;
                    break;
                }
            }

            if (selected == null)
                return false;

            _player.AddPassive(new PassiveItem
            {
                ItemName = selected.Name,
                Type = selected.Type,
                BonusValue = selected.BonusValue,
                Description = selected.Description
            });

            return true;
        }

        private bool ApplySkill(ShopOffer offer)
        {
            string skillName = offer.Id.Replace("skill:", string.Empty);
            if (_player.HasSkill(skillName))
                return false;

            if (!_skillDatabase.TryGetSkill(skillName, out Skill skill) || skill == null)
                return false;

            return _player.TryLearnSkill(skill);
        }

        private void ShowSkillReplacementPrompt(int offerIndex, Skill skill)
        {
            if (_player == null || skill == null || offerIndex < 0 || offerIndex >= _activeOffers.Count)
                return;

            _isChoosingReplacement = true;
            _pendingSkillOfferIndex = offerIndex;
            _pendingSkillToLearn = skill;

            ClearContainer(_replacementGrid);
            _replacementLabel.Text = $"Slots llenos. Elige una habilidad para reemplazar por {skill.Name}.";
            _replacementPanel.Visible = true;
            _statusLabel.Text = "Selecciona una habilidad para reemplazar.";

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
            if (!_isChoosingReplacement || _player == null || _pendingSkillToLearn == null)
                return;

            if (_pendingSkillOfferIndex < 0 || _pendingSkillOfferIndex >= _activeOffers.Count)
            {
                CancelSkillReplacement();
                return;
            }

            ShopOffer offer = _activeOffers[_pendingSkillOfferIndex];
            if (offer.Purchased)
            {
                CancelSkillReplacement();
                return;
            }

            if (!_player.CanAfford(offer.Price))
            {
                _statusLabel.Text = "No tienes oro suficiente.";
                CancelSkillReplacement();
                RefreshUi();
                return;
            }

            if (!_player.TryReplaceSkill(skillIndex, _pendingSkillToLearn))
            {
                _statusLabel.Text = "No se pudo reemplazar esa habilidad.";
                return;
            }

            _player.RemoveGold(offer.Price);
            offer.Purchased = true;
            _statusLabel.Text = $"Compraste {offer.Title} y reemplazaste una habilidad.";
            HideSkillReplacementPrompt();
            RefreshUi();
        }

        private void CancelSkillReplacement()
        {
            if (!_isChoosingReplacement)
                return;

            HideSkillReplacementPrompt();
            _statusLabel.Text = DefaultStatusText;
        }

        private void HideSkillReplacementPrompt()
        {
            _isChoosingReplacement = false;
            _pendingSkillOfferIndex = -1;
            _pendingSkillToLearn = null;

            if (_replacementPanel != null)
                _replacementPanel.Visible = false;
        }

        private static void ClearContainer(Container container)
        {
            if (container == null)
                return;

            foreach (Node child in container.GetChildren())
                child.QueueFree();
        }

        private void OnClosePressed()
        {
            EmitSignal(SignalName.ShopClosed);
            QueueFree();
        }
    }
}







