using Godot;
using System;
using System.Collections.Generic;
using SpellsAndRooms.scripts.Items;
using SpellsAndRooms.scripts.Characters;

namespace SpellsAndRooms.scripts.Turns
{
    /// <summary>
    /// Representa la escena de la tienda que aparece entre encuentros, donde el jugador puede comprar consumibles, pasivos o habilidades usando el oro acumulado.
    /// </summary>
    public partial class ShopScene : Node
    {
        /// <summary>
        /// Señal emitida cuando el jugador cierra la tienda, para que el EncounterDirector pueda continuar con el flujo del juego.
        /// </summary>
        [Signal] public delegate void ShopClosedEventHandler();

        /// <summary>
        /// Enum para diferenciar los tipos de ofertas que pueden aparecer en la tienda, lo que facilita su manejo y aplicación al jugador.
        /// </summary>
        private enum OfferType
        {
            Consumable,
            Passive,
            Skill
        }

        /// <summary>
        /// Clase interna que representa una oferta individual en la tienda, con toda la información necesaria para mostrarla al jugador y aplicarla si se compra.
        /// </summary>
        private sealed class ShopOffer
        {
            public OfferType Type { get; init; }
            public string Id { get; init; } = string.Empty;
            public string Title { get; init; } = string.Empty;
            public string Subtitle { get; init; } = string.Empty;
            public string Description { get; init; } = string.Empty;
            public Texture2D ImageTexture { get; init; }
            public int Price { get; init; }
            public bool Purchased { get; set; }
        }

        private const float ConsumableWeight = 1.0f;
        private const float PassiveWeight = 1.0f;
        private const float SkillWeight = 0.45f;
        private const int OffersPerVisit = 3;
        private const string DefaultStatusText = "Pasa el raton por una carta para ver la descripcion.";

        [ExportGroup("Art")]
        [Export] public Texture2D ShopBackgroundTexture;

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
        
        /// <summary>
        /// Inicializa la tienda con el jugador que la visita, generando las ofertas disponibles y preparando la interfaz. Este método debe ser llamado por el EncounterDirector antes de mostrar la tienda, para asegurarse de que se muestre con la información correcta del jugador y las ofertas adecuadas a su progreso.
        /// </summary>
        /// <param name="player">
        /// El jugador que está visitando la tienda. Se utiliza para generar ofertas relevantes a su estado actual (como habilidades que no tiene) y para aplicar las compras que realice. Es importante que este objeto esté completamente inicializado y refleje el estado actual del jugador antes de llamar a este método.
        /// </param>
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
        
        /// <summary>
        /// Método de Godot que se llama cuando la escena entra en el árbol. Se encarga de construir la interfaz de usuario si aún no se ha hecho, generar las ofertas si el jugador ya está asignado pero no hay ofertas (caso raro) y refrescar la interfaz para mostrar la información actualizada del jugador y las ofertas disponibles. Este método garantiza que la tienda esté lista para ser interactuada tan pronto como se muestre, siempre y cuando el jugador haya sido asignado previamente a través de StartShop.
        /// </summary>
        public override void _Ready()
        {
            BuildUi();
            if (_player != null && _activeOffers.Count == 0)
                GenerateOffers();

            if (_player != null)
                RefreshUi(resetStatus: true);
        }
        
        /// <summary>
        /// Construye la interfaz de usuario de la tienda, creando los nodos necesarios para mostrar el fondo, las ofertas, el oro del jugador y los mensajes de estado. Este método se asegura de que la estructura de la interfaz esté correctamente organizada en capas para permitir una fácil gestión de los elementos visuales y de interacción. Se llama automáticamente en _Ready, pero también se puede llamar manualmente si es necesario reconstruir la interfaz por alguna razón (aunque en este diseño no se espera que eso sea común).
        /// </summary>
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
                Texture = ShopBackgroundTexture ?? GD.Load<Texture2D>("res://assets/Turns/Shop.png"),
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
        
        /// <summary>
        /// Refresca la interfaz de usuario para mostrar la información actualizada del jugador (como el oro disponible) y las ofertas en la tienda. Si se indica resetStatus, también restablece el mensaje de estado al texto predeterminado. Este método se llama después de cualquier acción que pueda cambiar el estado del jugador o las ofertas (como comprar una carta) para asegurarse de que la interfaz refleje correctamente la situación actual. También se llama en _Ready para mostrar la información inicial cuando se abre la tienda.
        /// </summary>
        /// <param name="resetStatus">
        /// Indica si el mensaje de estado debe restablecerse al texto predeterminado. Esto es útil para limpiar mensajes temporales después de que el jugador interactúa con las ofertas, asegurando que la interfaz no quede con mensajes obsoletos o confusos después de una acción. En general, se recomienda pasar true después de acciones del jugador y false solo cuando se actualiza la interfaz por razones internas sin necesidad de cambiar el mensaje de estado.
        /// </param>
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
        
        /// <summary>
        /// Crea un Control que representa visualmente una oferta en la tienda, mostrando su imagen, título, subtítulo, descripción y precio. Este Control también maneja la interacción del mouse para mostrar el mensaje de estado al pasar el cursor sobre la carta y para permitir la compra al hacer clic en el botón correspondiente. La apariencia de la carta se personaliza con estilos para que sea atractiva y coherente con el diseño general de la tienda. Este método se llama desde RefreshUi para generar las cartas de las ofertas activas cada vez que se actualiza la interfaz.
        /// </summary>
        /// <param name="offer">
        /// La oferta que se va a representar en la carta. Se espera que este objeto contenga toda la información necesaria para mostrar la oferta correctamente (como el título, descripción, imagen y precio) y para manejar su compra (como el tipo de oferta y si ya fue comprada). Es importante que este objeto esté completamente inicializado antes de llamar a este método, ya que se asume que sus propiedades son válidas y se utilizan directamente para configurar la apariencia y funcionalidad de la carta.
        /// </param>
        /// <param name="index">
        /// El índice de la oferta dentro de la lista de ofertas activas. Este valor se utiliza para identificar la oferta cuando el jugador interactúa con su carta (por ejemplo, al hacer clic en el botón de compra) y para pasar esta información a los métodos que manejan la compra o la selección de reemplazo. Es importante que este índice corresponda correctamente a la posición de la oferta en la lista de ofertas activas para evitar errores al aplicar las compras o al mostrar información adicional.
        /// </param>
        /// <returns>
        /// Un Control que representa visualmente la oferta en la tienda, con toda la información y funcionalidad necesaria para que el jugador pueda interactuar con ella. Este Control se agrega a la interfaz de usuario en RefreshUi para mostrar las ofertas disponibles al jugador. La carta incluye la imagen (si está disponible), el título, subtítulo, descripción truncada y un botón para comprar, todo estilizado de manera coherente con el diseño de la tienda. Además, maneja eventos de mouse para mostrar información adicional en el mensaje de estado cuando el jugador pasa el cursor sobre la carta.
        /// </returns>
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
            Texture2D texture = offer.ImageTexture;
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

        /// <summary>
        /// Trunca la descripción de una oferta para que no ocupe demasiado espacio en la carta, limitándola a un número razonable de caracteres y agregando puntos suspensivos si es necesario. Esto ayuda a mantener la interfaz limpia y legible, evitando que descripciones largas desborden el diseño de la carta o hagan que sea difícil para el jugador entender rápidamente de qué se trata la oferta. Se recomienda que las descripciones originales sean lo más claras y concisas posible para minimizar la necesidad de truncamiento, pero este método proporciona una capa adicional de protección para casos donde las descripciones puedan ser excesivamente largas.
        /// </summary>
        /// <param name="text">
        /// La descripción original de la oferta que se va a mostrar en la carta. Se espera que esta descripción pueda ser de cualquier longitud, y este método se encargará de truncarla si excede el límite establecido (en este caso, 90 caracteres). Es importante que esta descripción sea informativa y clara, ya que el jugador dependerá de ella para entender qué beneficios o características ofrece la carta, pero también es crucial que no sea tan larga como para afectar negativamente la presentación visual de la tienda.
        /// </param>
        /// <returns>
        /// Una versión truncada de la descripción original, limitada a un máximo de 90 caracteres. Si la descripción original es más corta o igual a este límite, se devuelve sin cambios (aunque se le aplicará un trim para eliminar espacios innecesarios). Si la descripción excede el límite, se corta en el carácter 87 y se agregan puntos suspensivos al final para indicar que hay más texto que no se muestra. Este formato ayuda a mantener la interfaz de la tienda ordenada y fácil de leer, permitiendo al jugador obtener una idea rápida de lo que ofrece la carta sin sentirse abrumado por una descripción demasiado larga.
        /// </returns>
        private static string TruncateDescription(string text)
        {
            string safe = (text ?? string.Empty).Trim();
            if (safe.Length <= 90)
                return safe;

            return safe.Substring(0, 87) + "...";
        }
        
        /// <summary>
        /// Crea un StyleBoxFlat personalizado para las cartas de la tienda, definiendo su apariencia con colores de fondo, bordes, esquinas redondeadas y márgenes de contenido. Este estilo se aplica a cada carta para darle una apariencia distintiva y coherente con el diseño general de la tienda, ayudando a que las ofertas se destaquen visualmente y sean atractivas para el jugador. Se recomienda ajustar los colores y tamaños según el estilo artístico del juego para lograr la mejor integración visual posible.
        /// </summary>
        /// <returns>
        /// Un StyleBoxFlat configurado con los parámetros deseados para las cartas de la tienda, incluyendo un color de fondo oscuro con algo de transparencia, bordes dorados para resaltar las cartas, esquinas redondeadas para un aspecto más amigable y márgenes internos para asegurar que el contenido de la carta no quede pegado a los bordes. Este estilo se utiliza en el método CreateOfferCard para aplicar la apariencia a cada carta generada en la interfaz de la tienda. Ajustar este estilo puede ayudar a mejorar la estética general de la tienda y hacer que las ofertas sean más atractivas visualmente para el jugador.
        /// </returns>
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
        
        /// <summary>
        /// Crea un StyleBoxFlat personalizado para el marco de la imagen en las cartas de la tienda, definiendo su apariencia con un color de fondo más oscuro y transparente, bordes grises para contrastar con la imagen, esquinas redondeadas para mantener la coherencia visual con el estilo de las cartas y márgenes internos para asegurar que la imagen no quede pegada a los bordes del marco. Este estilo se aplica al contenedor que rodea la imagen en cada carta, ayudando a que las imágenes se destaquen visualmente y mantengan una presentación ordenada dentro de la carta. Ajustar este estilo puede mejorar la integración visual de las imágenes en las cartas y hacer que se vean más atractivas para el jugador.
        /// </summary>
        /// <returns>
        /// Un StyleBoxFlat configurado con los parámetros deseados para el marco de la imagen en las cartas de la tienda, incluyendo un color de fondo oscuro y transparente para que la imagen resalte, bordes grises para crear un contraste visual, esquinas redondeadas para mantener la coherencia con el estilo general de las cartas y márgenes internos para asegurar que la imagen tenga espacio suficiente dentro del marco. Este estilo se utiliza en el método CreateOfferCard para aplicar la apariencia al contenedor de la imagen en cada carta generada en la interfaz de la tienda. Ajustar este estilo puede ayudar a mejorar la presentación visual de las imágenes en las cartas y hacer que se vean más integradas y atractivas para el jugador.
        /// </returns>
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
        
        /// <summary>
        /// Maneja el evento de compra de una oferta, verificando si el jugador puede permitirse la compra, aplicando los efectos correspondientes según el tipo de oferta (consumible, pasivo o habilidad) y actualizando la interfaz para reflejar los cambios. Este método se llama cuando el jugador hace clic en el botón de compra de una carta, y se encarga de validar la compra, aplicar sus efectos al jugador (como agregar un consumible al inventario, otorgar un pasivo o aprender una nueva habilidad) y marcar la oferta como comprada para evitar compras repetidas. Además, si la oferta es una habilidad que requiere reemplazo, inicia el proceso para que el jugador elija qué habilidad desea reemplazar.
        /// </summary>
        /// <param name="offer">
        /// La oferta que el jugador ha decidido comprar. Se espera que esta oferta esté completamente inicializada y que su propiedad Purchased sea false, ya que este método se encarga de validar la compra y marcarla como comprada si es exitosa. Es importante que esta oferta contenga toda la información necesaria para aplicar sus efectos correctamente al jugador, como el tipo de oferta, el precio, la descripción y cualquier dato adicional relevante para su aplicación (por ejemplo, qué consumible agregar o qué habilidad aprender). Este método también maneja casos especiales, como habilidades que requieren reemplazo, para asegurar una experiencia de compra fluida y coherente para el jugador.
        /// </param>
        private void OnOfferHover(ShopOffer offer)
        {
            if (offer == null)
                return;

            _statusLabel.Text = $"{offer.Title}: {offer.Description}";
        }
        
        /// <summary>
        /// Maneja el evento de salida del cursor de una oferta, restableciendo el mensaje de estado al texto predeterminado. Este método se llama cuando el jugador mueve el cursor fuera de la carta de una oferta después de haberla inspeccionado, y se encarga de limpiar el mensaje de estado para evitar que quede información obsoleta o confusa en la interfaz. Restablecer el mensaje de estado a un texto genérico también ayuda a mantener la interfaz limpia y enfocada, permitiendo al jugador obtener información relevante solo cuando está interactuando activamente con las ofertas.
        /// </summary>
        private void OnOfferHoverExit()
        {
            _statusLabel.Text = DefaultStatusText;
        }

        /// <summary>
        /// Dado un conjunto de rutas candidatas, devuelve la primera textura que corresponde a un recurso existente.
        /// </summary>
        private static Texture2D LoadFirstExistingTexture(params string[] candidates)
        {
            foreach (string candidate in candidates)
            {
                if (!string.IsNullOrWhiteSpace(candidate) && ResourceLoader.Exists(candidate))
                    return GD.Load<Texture2D>(candidate);
            }

            return null;
        }

        private static Texture2D TryLoadTexture(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !ResourceLoader.Exists(path))
                return null;

            return GD.Load<Texture2D>(path);
        }
        
        /// <summary>
        /// Resuelve la textura para un consumible dado, utilizando su nombre y subtipo para determinar qué imagen asociar.
        /// </summary>
        private Texture2D ResolveConsumableTexture(ItemDatabase.ConsumableDefinition def)
        {
            Texture2D csvTexture = TryLoadTexture(def.ImagePath);
            if (csvTexture != null)
                return csvTexture;

            string name = Normalize(def.Name);
            string subtype = Normalize(def.Subtype);

            if (name.Contains("health") || subtype.Contains("healing"))
                return LoadFirstExistingTexture("res://assets/Items/Consumable/Poti.png");

            if (name.Contains("mana") || subtype.Contains("mana"))
                return LoadFirstExistingTexture("res://assets/Items/Consumable/PotiManai.png");

            return LoadFirstExistingTexture(
                $"res://assets/Items/Consumable/{def.Name}.png",
                "res://assets/Items/Consumable/Poti.png",
                "res://assets/Items/Consumable/PotiManai.png");
        }
        
        /// <summary>
        /// Resuelve la textura para un pasivo dado, utilizando su nombre y tipo para determinar qué imagen asociar.
        /// </summary>
        private Texture2D ResolvePassiveTexture(ItemDatabase.PassiveDefinition def)
        {
            Texture2D csvTexture = TryLoadTexture(def.ImagePath);
            if (csvTexture != null)
                return csvTexture;

            return LoadFirstExistingTexture(
                $"res://assets/Items/Passive/{def.Name}.png",
                "res://assets/Items/Passive/pecheGris.png");
        }
        
        /// <summary>
        /// Resuelve la textura para una habilidad dada, utilizando su nombre para determinar qué imagen asociar.
        /// </summary>
        private Texture2D ResolveSkillTexture(SkillDatabase.SkillDefinition def)
        {
            Texture2D csvTexture = TryLoadTexture(def.ImagePath);
            if (csvTexture != null)
                return csvTexture;

            string name = Normalize(def.Name);

            if (name.Contains("pyro"))
                return LoadFirstExistingTexture("res://assets/Characters/Enemy/BlackGoblin.png");
            if (name.Contains("aqua"))
                return LoadFirstExistingTexture("res://assets/Characters/Player/MagoAzul.png");
            if (name.Contains("earth"))
                return LoadFirstExistingTexture("res://assets/Characters/Enemy/Esqueleto.png");

            return LoadFirstExistingTexture(
                "res://assets/Characters/Enemy/Slime.png",
                "res://assets/Characters/Player/CaballeroNegro.png");
        }
        
        /// <summary>
        /// Normaliza una cadena para facilitar las comparaciones, convirtiéndola a minúsculas y eliminando espacios innecesarios. Este método se utiliza principalmente para comparar nombres y tipos de consumibles, pasivos y habilidades al resolver las rutas de las imágenes asociadas a las ofertas en la tienda. Al normalizar las cadenas, se asegura que las comparaciones sean más robustas y no se vean afectadas por diferencias en mayúsculas, espacios adicionales o formatos inconsistentes en los nombres y tipos definidos en la base de datos. Esto ayuda a mejorar la precisión de la asignación de imágenes a las ofertas, asegurando que se utilicen las imágenes correctas según el contenido de cada oferta.
        /// </summary>
        /// <param name="value">
        /// La cadena que se desea normalizar. Se espera que esta cadena pueda contener cualquier combinación de mayúsculas, minúsculas y espacios, y este método se encargará de convertirla a un formato estándar (todo en minúsculas y sin espacios innecesarios) para facilitar las comparaciones. Es importante que esta cadena no sea nula, aunque el método maneja ese caso devolviendo una cadena vacía, lo que permite que el proceso de normalización sea seguro incluso si se reciben entradas inesperadas o mal formateadas.
        /// </param>
        /// <returns>
        /// La cadena normalizada, convertida a minúsculas y con espacios innecesarios eliminados. Si la cadena de entrada es nula, se devuelve una cadena vacía. Este resultado se utiliza para realizar comparaciones más robustas al resolver las rutas de las imágenes para las ofertas en la tienda, asegurando que las comparaciones no se vean afectadas por diferencias en el formato de los nombres y tipos definidos en la base de datos. Al normalizar las cadenas, se mejora la precisión de la asignación de imágenes a las ofertas, lo que contribuye a una mejor presentación visual en la tienda.
        /// </returns>
        private static string Normalize(string value)
        {
            return (value ?? string.Empty).Trim().ToLowerInvariant();
        }

        /// <summary>
        /// Genera la lista de ofertas activas para la tienda, seleccionando aleatoriamente entre consumibles, pasivos y habilidades según las probabilidades definidas. Este método construye pools de ofertas disponibles para cada tipo (consumibles, pasivos y habilidades) basados en la base de datos y el estado actual del jugador (por ejemplo, evitando ofrecer habilidades que el jugador ya tiene). Luego, selecciona ofertas de estos pools utilizando un sistema de pesos para determinar qué tipo de oferta es más probable que aparezca, asegurándose de no repetir ofertas ya seleccionadas en la misma generación. Si no hay suficientes ofertas disponibles para llenar todas las ranuras, se completa con cualquier oferta restante para garantizar que siempre haya un número completo de ofertas activas en la tienda. Este método se llama cada vez que se necesita actualizar las ofertas en la tienda, como al entrar por primera vez o después de comprar una oferta.
        /// </summary>
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
        
        /// <summary>
        /// Construye la lista de ofertas de consumibles disponibles para la tienda, basándose en las definiciones de consumibles en la base de datos. Este método itera sobre todas las definiciones de consumibles, creando una oferta para cada una que incluye información relevante como el tipo, ID, título, subtítulo, descripción, ruta de la imagen y precio. La ruta de la imagen se resuelve utilizando el método ResolveConsumableImagePath para asignar imágenes específicas según el nombre y subtipo del consumible. Esta lista se utiliza posteriormente para seleccionar aleatoriamente qué consumibles ofrecer en la tienda, asegurando que solo se incluyan aquellos que están definidos en la base de datos y que tengan toda la información necesaria para ser presentados correctamente al jugador.
        /// </summary>
        /// <returns>
        /// Una lista de ofertas de consumibles disponibles para la tienda, construida a partir de las definiciones de consumibles en la base de datos. Cada oferta incluye información como el tipo (consumible), ID único, título, subtítulo, descripción, ruta de la imagen y precio. Esta lista se utiliza para seleccionar aleatoriamente qué consumibles ofrecer al jugador en la tienda, asegurando que solo se incluyan aquellos que están definidos en la base de datos y que tengan toda la información necesaria para ser presentados correctamente. Si no hay definiciones de consumibles en la base de datos, esta lista estará vacía, lo que significa que no se ofrecerán consumibles en la tienda.
        /// </returns>
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
                    ImageTexture = ResolveConsumableTexture(def),
                    Price = def.Price
                });
            }

            return pool;
        }
        
        /// <summary>
        /// Construye la lista de ofertas de pasivos disponibles para la tienda, basándose en las definiciones de pasivos en la base de datos. Este método itera sobre todas las definiciones de pasivos, creando una oferta para cada una que incluye información relevante como el tipo, ID, título, subtítulo, descripción, ruta de la imagen y precio. La ruta de la imagen se resuelve utilizando el método ResolvePassiveImagePath para asignar imágenes específicas según el nombre y tipo del pasivo. Esta lista se utiliza posteriormente para seleccionar aleatoriamente qué pasivos ofrecer en la tienda, asegurando que solo se incluyan aquellos que están definidos en la base de datos y que tengan toda la información necesaria para ser presentados correctamente al jugador.
        /// </summary>
        /// <returns>
        /// Una lista de ofertas de pasivos disponibles para la tienda, construida a partir de las definiciones de pasivos en la base de datos. Cada oferta incluye información como el tipo (pasivo), ID único, título, subtítulo, descripción, ruta de la imagen y precio. Esta lista se utiliza para seleccionar aleatoriamente qué pasivos ofrecer al jugador en la tienda, asegurando que solo se incluyan aquellos que están definidos en la base de datos y que tengan toda la información necesaria para ser presentados correctamente. Si no hay definiciones de pasivos en la base de datos, esta lista estará vacía, lo que significa que no se ofrecerán pasivos en la tienda.
        /// </returns>
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
                    ImageTexture = ResolvePassiveTexture(def),
                    Price = def.Price
                });
            }

            return pool;
        }
        
        /// <summary>
        /// Construye la lista de ofertas de habilidades disponibles para la tienda, basándose en las definiciones de habilidades en la base de datos y el estado actual del jugador. Este método itera sobre todas las definiciones de habilidades, creando una oferta para cada una que el jugador aún no posee, incluyendo información relevante como el tipo, ID, título, subtítulo, descripción, ruta de la imagen y precio. La ruta de la imagen se resuelve utilizando el método ResolveSkillImagePath para asignar imágenes específicas según el nombre de la habilidad. Esta lista se utiliza posteriormente para seleccionar aleatoriamente qué habilidades ofrecer en la tienda, asegurando que solo se incluyan aquellas que están definidas en la base de datos y que el jugador aún no ha aprendido, lo que proporciona una experiencia de compra relevante y personalizada.
        /// </summary>
        /// <returns>
        /// Una lista de ofertas de habilidades disponibles para la tienda, construida a partir de las definiciones de habilidades en la base de datos y el estado actual del jugador. Cada oferta incluye información como el tipo (habilidad), ID único, título, subtítulo, descripción, ruta de la imagen y precio. Solo se incluyen habilidades que el jugador aún no posee, lo que garantiza que las ofertas sean relevantes y personalizadas para el jugador. Si no hay definiciones de habilidades en la base de datos o si el jugador ya posee todas las habilidades disponibles, esta lista estará vacía, lo que significa que no se ofrecerán habilidades en la tienda.
        /// </returns>
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
                    ImageTexture = ResolveSkillTexture(def),
                    Price = def.Price
                });
            }

            return pool;
        }
        
        /// <summary>
        /// Selecciona aleatoriamente un tipo de oferta (consumible, pasivo o habilidad) basado en las probabilidades definidas y la disponibilidad de ofertas para cada tipo. Este método verifica qué tipos de ofertas tienen opciones disponibles que aún no han sido consumidas en la generación actual, calcula el peso total basado en los tipos disponibles y luego realiza una tirada aleatoria para seleccionar un tipo de oferta según esos pesos. Si no hay tipos de ofertas disponibles, devuelve null para indicar que no se puede seleccionar ningún tipo. Este método es fundamental para garantizar una distribución equilibrada y variada de ofertas en la tienda, adaptándose dinámicamente a la disponibilidad de ofertas en cada categoría.
        /// </summary>
        /// <param name="consumables">
        /// La lista de ofertas de consumibles disponibles para la tienda. Se espera que esta lista contenga todas las ofertas de consumibles que se pueden ofrecer al jugador, basándose en las definiciones de consumibles en la base de datos. Este método verificará si hay ofertas de consumibles disponibles que aún no han sido consumidas en la generación actual para determinar si el tipo de oferta de consumible puede ser seleccionado. Si esta lista está vacía o si todas las ofertas han sido consumidas, el peso para el tipo de oferta de consumible se considerará como cero, lo que afectará la probabilidad de selección en el proceso de selección ponderada.
        /// </param>
        /// <param name="passives">
        /// La lista de ofertas de pasivos disponibles para la tienda. Se espera que esta lista contenga todas las ofertas de pasivos que se pueden ofrecer al jugador, basándose en las definiciones de pasivos en la base de datos. Este método verificará si hay ofertas de pasivos disponibles que aún no han sido consumidas en la generación actual para determinar si el tipo de oferta de pasivo puede ser seleccionado. Si esta lista está vacía o si todas las ofertas han sido consumidas, el peso para el tipo de oferta de pasivo se considerará como cero, lo que afectará la probabilidad de selección en el proceso de selección ponderada.
        /// </param>
        /// <param name="skills">
        /// La lista de ofertas de habilidades disponibles para la tienda. Se espera que esta lista contenga todas las ofertas de habilidades que se pueden ofrecer al jugador, basándose en las definiciones de habilidades en la base de datos y el estado actual del jugador (es decir, solo incluir habilidades que el jugador aún no posee). Este método verificará si hay ofertas de habilidades disponibles que aún no han sido consumidas en la generación actual para determinar si el tipo de oferta de habilidad puede ser seleccionado. Si esta lista está vacía o si todas las ofertas han sido consumidas, el peso para el tipo de oferta de habilidad se considerará como cero, lo que afectará la probabilidad de selección en el proceso de selección ponderada.
        /// </param>
        /// <param name="consumedIds">
        /// Un conjunto de IDs de ofertas que ya han sido seleccionadas en la generación actual. Este conjunto se utiliza para verificar si una oferta específica ya ha sido consumida (es decir, ya ha sido seleccionada para ser una oferta activa) durante el proceso de selección ponderada. Si una oferta tiene un ID que está presente en este conjunto, se considera consumida y no se tendrá en cuenta para la selección de tipos de ofertas disponibles. Este mecanismo asegura que no se seleccionen ofertas repetidas durante la generación de las ofertas activas para la tienda, lo que contribuye a una experiencia de compra más variada y equilibrada para el jugador.
        /// </param>
        /// <returns>
        /// El tipo de oferta seleccionado aleatoriamente basado en las probabilidades definidas y la disponibilidad de ofertas para cada tipo. Si no hay tipos de ofertas disponibles (es decir, si todas las listas están vacías o si todas las ofertas han sido consumidas), se devuelve null para indicar que no se puede seleccionar ningún tipo. Este resultado se utiliza en el proceso de generación de ofertas para determinar qué tipo de oferta seleccionar a continuación, asegurando una distribución equilibrada y variada de ofertas en la tienda según la disponibilidad actual.
        /// </returns>
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
        
        /// <summary>
        /// Verifica si hay ofertas disponibles en la lista dada que aún no han sido consumidas, es decir, que no están presentes en el conjunto de IDs consumidos. Este método se utiliza para determinar si un tipo de oferta (consumible, pasivo o habilidad) tiene opciones disponibles para ser seleccionadas durante el proceso de generación de ofertas activas para la tienda. Si al menos una oferta en la lista no ha sido consumida, se considera que hay ofertas disponibles para ese tipo, lo que afecta la probabilidad de selección en el proceso de selección ponderada.
        /// </summary>
        /// <param name="offers">
        /// La lista de ofertas que se desea verificar para disponibilidad. Se espera que esta lista contenga todas las ofertas de un tipo específico (consumibles, pasivos o habilidades) que se pueden ofrecer al jugador en la tienda. Este método iterará sobre esta lista para verificar si hay al menos una oferta cuyo ID no esté presente en el conjunto de IDs consumidos, lo que indicaría que esa oferta aún está disponible para ser seleccionada como una oferta activa en la tienda. Si la lista está vacía o si todas las ofertas han sido consumidas, este método devolverá false, indicando que no hay ofertas disponibles para ese tipo.
        /// </param>
        /// <param name="consumedIds">
        /// Un conjunto de IDs de ofertas que ya han sido seleccionadas en la generación actual. Este conjunto se utiliza para verificar si una oferta específica ya ha sido consumida (es decir, ya ha sido seleccionada para ser una oferta activa) durante el proceso de generación de ofertas. Si una oferta tiene un ID que está presente en este conjunto, se considera consumida y no se tendrá en cuenta como disponible para la selección de tipos de ofertas. Este mecanismo asegura que no se seleccionen ofertas repetidas durante la generación de las ofertas activas para la tienda, lo que contribuye a una experiencia de compra más variada y equilibrada para el jugador.
        /// </param>
        /// <returns>
        /// true si hay al menos una oferta en la lista que no ha sido consumida (es decir, cuyo ID no está presente en el conjunto de IDs consumidos), o false si todas las ofertas han sido consumidas o si la lista está vacía. Este resultado se utiliza para determinar si un tipo de oferta tiene opciones disponibles para ser seleccionadas durante el proceso de generación de ofertas activas para la tienda, lo que afecta la probabilidad de selección en el proceso de selección ponderada. Si se devuelve false, el peso para ese tipo de oferta se considerará como cero, lo que reducirá la probabilidad de que ese tipo sea seleccionado.
        /// </returns>
        private static bool HasAvailable(List<ShopOffer> offers, HashSet<string> consumedIds)
        {
            foreach (ShopOffer offer in offers)
            {
                if (!consumedIds.Contains(offer.Id))
                    return true;
            }

            return false;
        }
        
        /// <summary>
        /// Selecciona aleatoriamente una oferta de la lista dada que no haya sido consumida (es decir, cuyo ID no esté presente en el conjunto de IDs consumidos). Este método se utiliza para seleccionar una oferta específica de un tipo determinado (consumible, pasivo o habilidad) durante el proceso de generación de ofertas activas para la tienda. Si hay ofertas disponibles que no han sido consumidas, se selecciona una de ellas al azar y se devuelve como resultado. Si no hay ofertas disponibles, se devuelve null para indicar que no se pudo seleccionar ninguna oferta de ese tipo.
        /// </summary>
        /// <param name="offerType">
        /// El tipo de oferta que se desea seleccionar (consumible, pasivo o habilidad). Este parámetro se utiliza para determinar de qué lista de ofertas se debe seleccionar la oferta específica. Dependiendo del valor de este parámetro, el método accederá a la lista correspondiente (consumables, passives o skills) para buscar ofertas que no hayan sido consumidas y seleccionar una de ellas al azar. Es importante que este parámetro tenga un valor válido que corresponda a uno de los tipos de oferta definidos, ya que de lo contrario el método no podrá determinar correctamente de qué lista seleccionar la oferta y podría devolver null.
        /// </param>
        /// <param name="consumables">
        /// La lista de ofertas de consumibles disponibles para la tienda. Se espera que esta lista contenga todas las ofertas de consumibles que se pueden ofrecer al jugador, basándose en las definiciones de consumibles en la base de datos. Este método verificará esta lista para encontrar ofertas de consumibles que no hayan sido consumidas (es decir, cuyos IDs no estén presentes en el conjunto de IDs consumidos) y seleccionará una de ellas al azar si el tipo de oferta solicitado es consumible. Si esta lista está vacía o si todas las ofertas han sido consumidas, este método no seleccionará ninguna oferta de consumible y devolverá null.
        /// </param>
        /// <param name="passives">
        /// La lista de ofertas de pasivos disponibles para la tienda. Se espera que esta lista contenga todas las ofertas de pasivos que se pueden ofrecer al jugador, basándose en las definiciones de pasivos en la base de datos. Este método verificará esta lista para encontrar ofertas de pasivos que no hayan sido consumidas (es decir, cuyos IDs no estén presentes en el conjunto de IDs consumidos) y seleccionará una de ellas al azar si el tipo de oferta solicitado es pasivo. Si esta lista está vacía o si todas las ofertas han sido consumidas, este método no seleccionará ninguna oferta de pasivo y devolverá null.
        /// </param>
        /// <param name="skills">
        /// La lista de ofertas de habilidades disponibles para la tienda. Se espera que esta lista contenga todas las ofertas de habilidades que se pueden ofrecer al jugador, basándose en las definiciones de habilidades en la base de datos y el estado actual del jugador (es decir, solo incluir habilidades que el jugador aún no posee). Este método verificará esta lista para encontrar ofertas de habilidades que no hayan sido consumidas (es decir, cuyos IDs no estén presentes en el conjunto de IDs consumidos) y seleccionará una de ellas al azar si el tipo de oferta solicitado es habilidad. Si esta lista está vacía o si todas las ofertas han sido consumidas, este método no seleccionará ninguna oferta de habilidad y devolverá null.
        /// </param>
        /// <param name="consumedIds">
        /// Un conjunto de IDs de ofertas que ya han sido seleccionadas en la generación actual. Este conjunto se utiliza para verificar si una oferta específica ya ha sido consumida (es decir, ya ha sido seleccionada para ser una oferta activa) durante el proceso de generación de ofertas. Si una oferta tiene un ID que está presente en este conjunto, se considera consumida y no se tendrá en cuenta para la selección de ofertas disponibles. Este mecanismo asegura que no se seleccionen ofertas repetidas durante la generación de las ofertas activas para la tienda, lo que contribuye a una experiencia de compra más variada y equilibrada para el jugador. Al pasar este conjunto al método, se garantiza que solo se seleccionen ofertas que aún no han sido consumidas en la generación actual.
        /// </param>
        /// <returns>
        /// Una oferta seleccionada aleatoriamente de la lista correspondiente al tipo de oferta dado, que no haya sido consumida (es decir, cuyo ID no esté presente en el conjunto de IDs consumidos). Si el tipo de oferta dado es consumible, se seleccionará una oferta de la lista de consumibles; si es pasivo, se seleccionará una oferta de la lista de pasivos; y si es habilidad, se seleccionará una oferta de la lista de habilidades. Si no hay ofertas disponibles para el tipo dado (es decir, si todas las ofertas han sido consumidas o si la lista correspondiente está vacía), se devolverá null para indicar que no se pudo seleccionar ninguna oferta de ese tipo. Este resultado se utiliza en el proceso de generación de ofertas para agregar una oferta específica a la lista de ofertas activas en la tienda.
        /// </returns>
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
        
        /// <summary>
        /// Maneja la lógica cuando el jugador presiona el botón de compra para una oferta específica. Este método verifica si el jugador puede comprar la oferta (es decir, si tiene suficiente oro y si la oferta no ha sido comprada previamente), y luego aplica los efectos de la oferta al jugador. Si la oferta es una habilidad y el jugador ya tiene el máximo de habilidades, se muestra un mensaje para elegir una habilidad a reemplazar en lugar de comprar directamente. Si la compra se realiza con éxito, se actualiza el estado del jugador (por ejemplo, restando el oro) y se marca la oferta como comprada, lo que afecta su presentación en la tienda. Este método es fundamental para manejar la interacción del jugador con las ofertas en la tienda y garantizar que las compras se realicen de manera coherente con las reglas del juego.
        /// </summary>
        /// <param name="offerIndex">
        /// El índice de la oferta que el jugador ha seleccionado para comprar. Este índice se utiliza para identificar qué oferta específica se está intentando comprar dentro de la lista de ofertas activas en la tienda. Es importante que este índice sea válido (es decir, que esté dentro del rango de la lista de ofertas activas) para evitar errores al acceder a la oferta correspondiente. Este parámetro se pasa desde el botón de compra asociado a cada oferta, lo que permite que el método sepa exactamente qué oferta se está intentando comprar y pueda aplicar los efectos correctos al jugador.
        /// </param>
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
        
        /// <summary>
        /// Aplica los efectos de una oferta comprada al jugador, actualizando su estado según el tipo de oferta (consumible, pasivo o habilidad). Este método maneja la lógica específica para cada tipo de oferta, como agregar un consumible al inventario del jugador, otorgar un pasivo o enseñar una nueva habilidad. Si la aplicación de la oferta se realiza con éxito, se devuelve true; de lo contrario, se devuelve false para indicar que no se pudieron aplicar los efectos de la oferta. Este método es crucial para garantizar que las compras en la tienda tengan un impacto real y coherente en el estado del jugador, reflejando correctamente los beneficios asociados a cada oferta.
        /// </summary>
        /// <param name="offer">
        /// La oferta que se ha comprado y cuyos efectos se deben aplicar al jugador. Este objeto contiene toda la información relevante sobre la oferta, como su tipo (consumible, pasivo o habilidad), ID, título, descripción, precio y cualquier otra información necesaria para determinar cómo aplicar sus efectos al jugador. Es importante que este objeto sea válido y que su tipo esté correctamente definido para que el método pueda manejar la aplicación de los efectos de manera adecuada según las reglas del juego. Este parámetro se pasa desde el método de compra después de verificar que la compra es válida y se utiliza para actualizar el estado del jugador en consecuencia.
        /// </param>
        /// <returns>
        /// true si los efectos de la oferta se aplicaron con éxito al jugador, o false si no se pudieron aplicar por alguna razón (por ejemplo, si la oferta es inválida o si hubo un error al actualizar el estado del jugador). Este resultado se utiliza para determinar si la compra se completó correctamente y para actualizar el mensaje de estado en la tienda en consecuencia. Si se devuelve false, el método de compra puede mostrar un mensaje de error al jugador para indicar que no se pudo completar la compra, mientras que si se devuelve true, se puede mostrar un mensaje de éxito y actualizar la interfaz de usuario para reflejar los cambios en el estado del jugador.
        /// </returns>
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
        
        /// <summary>
        /// Aplica los efectos de una oferta de consumible al jugador, agregando el consumible correspondiente a su inventario. Este método busca la definición del consumible en la base de datos utilizando el ID de la oferta, y si encuentra una coincidencia, crea un nuevo objeto de consumible con las propiedades definidas en la base de datos y lo agrega al inventario del jugador. Si la aplicación del consumible se realiza con éxito, se devuelve true; de lo contrario, se devuelve false para indicar que no se pudieron aplicar los efectos del consumible (por ejemplo, si no se encontró la definición en la base de datos). Este método es esencial para garantizar que las compras de consumibles en la tienda tengan un impacto real y coherente en el estado del jugador, reflejando correctamente los beneficios asociados a cada consumible.
        /// </summary>
        /// <param name="offer">
        /// La oferta de consumible que se ha comprado y cuyos efectos se deben aplicar al jugador. Este objeto contiene toda la información relevante sobre la oferta, como su tipo (consumible), ID, título, descripción, precio y cualquier otra información necesaria para determinar cómo aplicar sus efectos al jugador. Es importante que este objeto sea válido y que su ID corresponda a una definición de consumible en la base de datos para que el método pueda manejar la aplicación de los efectos de manera adecuada según las reglas del juego. Este parámetro se pasa desde el método de compra después de verificar que la compra es válida y se utiliza para actualizar el estado del jugador en consecuencia.
        /// </param>
        /// <returns>
        /// true si los efectos del consumible se aplicaron con éxito al jugador (es decir, si se encontró la definición del consumible en la base de datos y se agregó correctamente al inventario del jugador), o false si no se pudieron aplicar por alguna razón (por ejemplo, si no se encontró la definición en la base de datos). Este resultado se utiliza para determinar si la compra del consumible se completó correctamente y para actualizar el mensaje de estado en la tienda en consecuencia. Si se devuelve false, el método de compra puede mostrar un mensaje de error al jugador para indicar que no se pudo completar la compra, mientras que si se devuelve true, se puede mostrar un mensaje de éxito y actualizar la interfaz de usuario para reflejar los cambios en el estado del jugador.
        /// </returns>
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
        
        /// <summary>
        /// Aplica los efectos de una oferta de pasivo al jugador, otorgándole el pasivo correspondiente. Este método busca la definición del pasivo en la base de datos utilizando el ID de la oferta, y si encuentra una coincidencia, crea un nuevo objeto de pasivo con las propiedades definidas en la base de datos y lo agrega a la lista de pasivos del jugador. Si la aplicación del pasivo se realiza con éxito, se devuelve true; de lo contrario, se devuelve false para indicar que no se pudieron aplicar los efectos del pasivo (por ejemplo, si no se encontró la definición en la base de datos). Este método es esencial para garantizar que las compras de pasivos en la tienda tengan un impacto real y coherente en el estado del jugador, reflejando correctamente los beneficios asociados a cada pasivo.
        /// </summary>
        /// <param name="offer">
        /// La oferta de pasivo que se ha comprado y cuyos efectos se deben aplicar al jugador. Este objeto contiene toda la información relevante sobre la oferta, como su tipo (pasivo), ID, título, descripción, precio y cualquier otra información necesaria para determinar cómo aplicar sus efectos al jugador. Es importante que este objeto sea válido y que su ID corresponda a una definición de pasivo en la base de datos para que el método pueda manejar la aplicación de los efectos de manera adecuada según las reglas del juego. Este parámetro se pasa desde el método de compra después de verificar que la compra es válida y se utiliza para actualizar el estado del jugador en consecuencia.
        /// </param>
        /// <returns>
        /// true si los efectos del pasivo se aplicaron con éxito al jugador (es decir, si se encontró la definición del pasivo en la base de datos y se agregó correctamente a la lista de pasivos del jugador), o false si no se pudieron aplicar por alguna razón (por ejemplo, si no se encontró la definición en la base de datos). Este resultado se utiliza para determinar si la compra del pasivo se completó correctamente y para actualizar el mensaje de estado en la tienda en consecuencia. Si se devuelve false, el método de compra puede mostrar un mensaje de error al jugador para indicar que no se pudo completar la compra, mientras que si se devuelve true, se puede mostrar un mensaje de éxito y actualizar la interfaz de usuario para reflejar los cambios en el estado del jugador.
        /// </returns>
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
        
        /// <summary>
        /// Aplica los efectos de una oferta de habilidad al jugador, enseñándole la habilidad correspondiente. Este método busca la definición de la habilidad en la base de datos utilizando el ID de la oferta, y si encuentra una coincidencia, crea un nuevo objeto de habilidad con las propiedades definidas en la base de datos y lo agrega a la lista de habilidades del jugador. Si el jugador ya tiene el máximo de habilidades permitidas, este método no aplicará la nueva habilidad y devolverá false para indicar que no se pudieron aplicar los efectos de la oferta. Si la aplicación de la habilidad se realiza con éxito, se devuelve true; de lo contrario, se devuelve false para indicar que no se pudieron aplicar los efectos de la habilidad (por ejemplo, si no se encontró la definición en la base de datos o si el jugador ya tiene el máximo de habilidades). Este método es esencial para garantizar que las compras de habilidades en la tienda tengan un impacto real y coherente en el estado del jugador, reflejando correctamente los beneficios asociados a cada habilidad.
        /// </summary>
        /// <param name="offer">
        /// La oferta de habilidad que se ha comprado y cuyos efectos se deben aplicar al jugador. Este objeto contiene toda la información relevante sobre la oferta, como su tipo (habilidad), ID, título, descripción, precio y cualquier otra información necesaria para determinar cómo aplicar sus efectos al jugador. Es importante que este objeto sea válido y que su ID corresponda a una definición de habilidad en la base de datos para que el método pueda manejar la aplicación de los efectos de manera adecuada según las reglas del juego. Este parámetro se pasa desde el método de compra después de verificar que la compra es válida y se utiliza para actualizar el estado del jugador en consecuencia. Si el jugador ya tiene el máximo de habilidades permitidas, este método no aplicará la nueva habilidad y devolverá false para indicar que no se pudieron aplicar los efectos de la oferta.
        /// </param>
        /// <returns>
        /// true si los efectos de la habilidad se aplicaron con éxito al jugador (es decir, si se encontró la definición de la habilidad en la base de datos y se agregó correctamente a la lista de habilidades del jugador), o false si no se pudieron aplicar por alguna razón (por ejemplo, si no se encontró la definición en la base de datos o si el jugador ya tiene el máximo de habilidades permitidas). Este resultado se utiliza para determinar si la compra de la habilidad se completó correctamente y para actualizar el mensaje de estado en la tienda en consecuencia. Si se devuelve false, el método de compra puede mostrar un mensaje de error al jugador para indicar que no se pudo completar la compra, mientras que si se devuelve true, se puede mostrar un mensaje de éxito y actualizar la interfaz de usuario para reflejar los cambios en el estado del jugador.
        /// </returns>
        private bool ApplySkill(ShopOffer offer)
        {
            string skillName = offer.Id.Replace("skill:", string.Empty);
            if (_player.HasSkill(skillName))
                return false;

            if (!_skillDatabase.TryGetSkill(skillName, out Skill skill) || skill == null)
                return false;

            return _player.TryLearnSkill(skill);
        }
        
        /// <summary>
        /// Muestra un mensaje para que el jugador elija una habilidad a reemplazar cuando intenta comprar una nueva habilidad pero ya tiene el máximo de habilidades permitidas. Este método configura la interfaz de usuario para mostrar una lista de las habilidades actuales del jugador, permitiéndole seleccionar una de ellas para reemplazar por la nueva habilidad que está intentando comprar. Si el jugador selecciona una habilidad para reemplazar, se confirmará la compra y se aplicarán los cambios correspondientes al estado del jugador. Si el jugador cancela la selección o si ocurre algún error durante el proceso, se ocultará el mensaje y se mantendrá el estado actual sin cambios. Este método es crucial para manejar situaciones en las que el jugador desea aprender una nueva habilidad pero ya ha alcanzado su límite de habilidades, proporcionando una forma de gestionar esta limitación de manera interactiva y coherente con las reglas del juego.
        /// </summary>
        /// <param name="offerIndex">
        /// El índice de la oferta de habilidad que el jugador está intentando comprar y para la cual se muestra el mensaje de reemplazo. Este índice se utiliza para identificar qué oferta específica se está intentando comprar dentro de la lista de ofertas activas en la tienda, lo que permite que el método sepa exactamente qué habilidad se está intentando aprender y pueda mostrar el mensaje de reemplazo con la información correcta. Es importante que este índice sea válido (es decir, que esté dentro del rango de la lista de ofertas activas) para evitar errores al acceder a la oferta correspondiente y para garantizar que el mensaje de reemplazo se muestre con la información correcta sobre la habilidad que se está intentando comprar.
        /// </param>
        /// <param name="skill">
        /// La habilidad que el jugador está intentando comprar y para la cual se muestra el mensaje de reemplazo. Este objeto contiene toda la información relevante sobre la habilidad, como su nombre, descripción, efectos y cualquier otra información necesaria para mostrar en el mensaje de reemplazo y para que el jugador pueda tomar una decisión informada sobre qué habilidad reemplazar. Es importante que este objeto sea válido y que contenga la información correcta sobre la habilidad que se está intentando comprar para que el mensaje de reemplazo sea claro y útil para el jugador al momento de seleccionar una habilidad para reemplazar.
        /// </param>
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

        /// <summary>
        /// Confirma la selección de una habilidad para reemplazar cuando el jugador está en el proceso de elegir una habilidad a reemplazar para aprender una nueva habilidad. Este método verifica que el jugador esté actualmente eligiendo una habilidad para reemplazar y que los índices y objetos involucrados sean válidos. Si la selección es válida, se realiza el reemplazo de la habilidad seleccionada por la nueva habilidad que se está intentando comprar, se actualiza el estado del jugador (por ejemplo, restando el oro y marcando la oferta como comprada) y se oculta el mensaje de reemplazo. Si la selección no es válida o si ocurre algún error durante el proceso, se cancela la selección y se oculta el mensaje sin realizar cambios en el estado del jugador. Este método es esencial para manejar la lógica de reemplazo de habilidades de manera coherente con las reglas del juego y para garantizar que las decisiones del jugador tengan un impacto real en su estado.
        /// </summary>
        /// <param name="skillIndex">
        /// El índice de la habilidad actual del jugador que ha seleccionado para reemplazar por la nueva habilidad que está intentando comprar. Este índice se utiliza para identificar qué habilidad específica del jugador se va a reemplazar en caso de que la selección sea confirmada. Es importante que este índice sea válido (es decir, que esté dentro del rango de la lista de habilidades del jugador) para evitar errores al acceder a la habilidad correspondiente y para garantizar que el reemplazo se realice correctamente en el estado del jugador. Este parámetro se pasa desde los botones de selección en el mensaje de reemplazo, lo que permite que el método sepa exactamente qué habilidad se está intentando reemplazar.
        /// </param>
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
        
        /// <summary>
        /// Cancela el proceso de selección de una habilidad para reemplazar, ocultando el mensaje de reemplazo y restableciendo el estado relacionado con la selección. Este método se utiliza para manejar situaciones en las que el jugador decide cancelar la selección de una habilidad para reemplazar (por ejemplo, si se da cuenta de que no quiere comprar la nueva habilidad o si comete un error al seleccionar una habilidad para reemplazar). Al cancelar la selección, se oculta el mensaje de reemplazo, se restablecen las variables relacionadas con la selección pendiente y se actualiza el mensaje de estado en la tienda para reflejar que no se está eligiendo una habilidad para reemplazar. Este método es importante para proporcionar una forma de salir del proceso de selección de habilidades sin realizar cambios en el estado del jugador, lo que mejora la experiencia del usuario al permitirle corregir errores o reconsiderar sus decisiones.
        /// </summary>
        private void CancelSkillReplacement()
        {
            if (!_isChoosingReplacement)
                return;

            HideSkillReplacementPrompt();
            _statusLabel.Text = DefaultStatusText;
        }
        
        /// <summary>
        /// Oculta el mensaje de selección de habilidad para reemplazar y restablece el estado relacionado con la selección. Este método se utiliza para finalizar el proceso de selección de habilidades para reemplazar, ya sea después de que el jugador haya confirmado una selección válida o después de que haya cancelado la selección. Al ocultar el mensaje, se restablecen las variables relacionadas con la selección pendiente (como el índice de la oferta y la habilidad que se intentaba aprender) y se oculta el panel de reemplazo en la interfaz de usuario. Este método es esencial para garantizar que el estado del juego se mantenga coherente después de que el proceso de selección de habilidades para reemplazar haya concluido, evitando que queden variables pendientes o mensajes visibles que puedan causar confusión al jugador.
        /// </summary>
        private void HideSkillReplacementPrompt()
        {
            _isChoosingReplacement = false;
            _pendingSkillOfferIndex = -1;
            _pendingSkillToLearn = null;

            if (_replacementPanel != null)
                _replacementPanel.Visible = false;
        }
        
        /// <summary>
        /// Limpia todos los nodos hijos de un contenedor dado, eliminándolos de la escena. Este método se utiliza para limpiar dinámicamente el contenido de un contenedor en la interfaz de usuario, como el grid de selección de habilidades para reemplazar, antes de agregar nuevos elementos. Al llamar a QueueFree() en cada nodo hijo, se asegura que los nodos se eliminen correctamente de la escena y se liberen los recursos asociados. Es importante verificar que el contenedor no sea null antes de intentar acceder a sus hijos para evitar errores. Este método es fundamental para mantener la interfaz de usuario actualizada y libre de elementos obsoletos o duplicados durante el proceso de generación de ofertas y selección de habilidades en la tienda.
        /// </summary>
        /// <param name="container">
        /// El contenedor cuyos nodos hijos se deben eliminar. Este parámetro se espera que sea un nodo de tipo Container (o una de sus subclases) que contenga los nodos que se desean limpiar. Es importante que este parámetro no sea null para evitar errores al intentar acceder a sus hijos. Al pasar el contenedor correcto, este método podrá eliminar todos los nodos hijos de manera efectiva, lo que es esencial para mantener la interfaz de usuario actualizada y libre de elementos obsoletos o duplicados durante el proceso de generación de ofertas y selección de habilidades en la tienda.
        /// </param>
        private static void ClearContainer(Container container)
        {
            if (container == null)
                return;

            foreach (Node child in container.GetChildren())
                child.QueueFree();
        }
        
        /// <summary>
        /// Maneja la lógica cuando el jugador presiona el botón de cerrar la tienda, emitiendo una señal para indicar que la tienda se ha cerrado y luego eliminando la escena de la tienda de la memoria. Este método es fundamental para permitir que el jugador salga de la tienda y regrese al flujo normal del juego después de haber interactuado con las ofertas disponibles. Al emitir la señal "ShopClosed", otros nodos o sistemas en el juego pueden escuchar esta señal para realizar cualquier acción necesaria (como reactivar el control del jugador o actualizar el estado del juego) después de que la tienda se haya cerrado. Finalmente, al llamar a QueueFree(), se asegura que la escena de la tienda se elimine correctamente de la memoria, liberando los recursos asociados y evitando posibles fugas de memoria.
        /// </summary>
        private void OnClosePressed()
        {
            EmitSignal(SignalName.ShopClosed);
            QueueFree();
        }
    }
}







