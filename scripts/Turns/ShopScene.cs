using Godot;
using SpellsAndRooms.scripts.Characters;

namespace SpellsAndRooms.scripts.Turns
{
    public partial class ShopScene : Node
    {
        [Signal]
        public delegate void ShopClosedEventHandler();

        private Player _player;
        private ItemDatabase _itemDatabase;
        private CanvasLayer _backgroundLayer;
        private CanvasLayer _uiLayer;
        private Control _uiRoot;

        private Label _playerGoldLabel;
        private VBoxContainer _itemsContainer;
        private RichTextLabel _itemDescriptionLabel;
        private Label _itemPriceLabel;
        private Button _buyButton;
        private Button _closeButton;
        private ItemDatabase.ConsumableDefinition _selectedItem;

        public void StartShop(Player player)
        {
            _player = player;
            _itemDatabase = new ItemDatabase();

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
                Text = "=== TIENDA ===",
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

            // Contenedor principal con dos columnas
            var mainRow = new HBoxContainer
            {
                Name = "MainRow",
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill
            };
            inner.AddChild(mainRow);

            // Panel izquierdo: lista de items
            var leftPanel = new PanelContainer
            {
                Name = "LeftPanel",
                CustomMinimumSize = new Vector2(300, 0),
                SizeFlagsVertical = Control.SizeFlags.ExpandFill
            };
            mainRow.AddChild(leftPanel);

            var leftLayout = new VBoxContainer
            {
                Name = "LeftLayout",
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill
            };
            leftPanel.AddChild(leftLayout);

            var itemsLabel = new Label { Text = "Artículos disponibles:" };
            leftLayout.AddChild(itemsLabel);

            var scrollContainer = new ScrollContainer
            {
                Name = "ScrollContainer",
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill
            };
            leftLayout.AddChild(scrollContainer);

            _itemsContainer = new VBoxContainer
            {
                Name = "ItemsContainer",
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
            };
            scrollContainer.AddChild(_itemsContainer);

            // Panel derecho: detalles del item
            var rightPanel = new PanelContainer
            {
                Name = "RightPanel",
                CustomMinimumSize = new Vector2(350, 0),
                SizeFlagsVertical = Control.SizeFlags.ExpandFill
            };
            mainRow.AddChild(rightPanel);

            var rightLayout = new VBoxContainer
            {
                Name = "RightLayout",
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill
            };
            rightPanel.AddChild(rightLayout);

            var detailsLabel = new Label { Text = "Detalles del artículo:" };
            rightLayout.AddChild(detailsLabel);

            _itemDescriptionLabel = new RichTextLabel
            {
                Name = "ItemDescription",
                BbcodeEnabled = false,
                FitContent = true,
                ScrollFollowing = true,
                CustomMinimumSize = new Vector2(0, 150),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill,
                Text = "Selecciona un artículo para ver sus detalles."
            };
            rightLayout.AddChild(_itemDescriptionLabel);

            _itemPriceLabel = new Label
            {
                Text = "Precio: --",
                HorizontalAlignment = HorizontalAlignment.Right
            };
            rightLayout.AddChild(_itemPriceLabel);

            _buyButton = new Button
            {
                Text = "Comprar",
                Disabled = true,
                CustomMinimumSize = new Vector2(0, 40)
            };
            _buyButton.Pressed += OnBuyPressed;
            rightLayout.AddChild(_buyButton);

            // Botón de salida
            _closeButton = new Button
            {
                Text = "Cerrar Tienda",
                CustomMinimumSize = new Vector2(0, 40)
            };
            _closeButton.Pressed += OnClosePressed;
            inner.AddChild(_closeButton);
        }

        private void RefreshUi()
        {
            if (_player == null || _itemDatabase == null)
                return;

            _playerGoldLabel.Text = $"Oro: {_player.Gold}";

            // Limpiar y repoblar lista de items
            foreach (Node child in _itemsContainer.GetChildren())
            {
                child.QueueFree();
            }

            foreach (var item in _itemDatabase.Consumables)
            {
                var itemButton = new Button
                {
                    Text = $"{item.Name} - {item.Power} oro",
                    CustomMinimumSize = new Vector2(0, 40),
                    SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
                };
                itemButton.Pressed += () => OnItemSelected(item);
                _itemsContainer.AddChild(itemButton);
            }
        }

        private void OnItemSelected(ItemDatabase.ConsumableDefinition item)
        {
            _selectedItem = item;
            
            _itemDescriptionLabel.Clear();
            _itemDescriptionLabel.AppendText($"Nombre: {item.Name}\n");
            _itemDescriptionLabel.AppendText($"Tipo: {item.Type}\n");
            _itemDescriptionLabel.AppendText($"Subtipo: {item.Subtype}\n");
            _itemDescriptionLabel.AppendText($"\nDescripción:\n{item.Description}");

            _itemPriceLabel.Text = $"Precio: {item.Power} oro";
            _buyButton.Disabled = _player == null || _player.Gold < item.Power;
        }

        private void OnBuyPressed()
        {
            if (_selectedItem == null || _player == null || _player.Gold < _selectedItem.Power)
                return;

            // Restar oro al jugador
            _player.RemoveGold(_selectedItem.Power);

            // Aquí se podría añadir el item al inventario del jugador
            // Por ahora solo mostramos que se compró
            GD.Print($"[ShopScene] Se compró: {_selectedItem.Name} por {_selectedItem.Power} oro");

            RefreshUi();
        }

        private void OnClosePressed()
        {
            EmitSignal(SignalName.ShopClosed);
            QueueFree();
        }
    }
}







