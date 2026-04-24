using Godot;
using System.Collections.Generic;
using System.Linq;
using SpellsAndRooms.scripts.Characters;
using SpellsAndRooms.scripts.Items;

namespace SpellsAndRooms.scripts.Turns
{
    /// <summary>
    /// Controla la escena de batalla, gestionando la interfaz, las animaciones y la lógica de turnos entre el jugador y los enemigos.
    /// </summary>
    public partial class BattleScene : Node2D
    {
        /// <summary>
        /// Evento que se emite al finalizar la batalla, indicando si el jugador ganó o perdió y cuánto oro ganó en caso de victoria.
        /// </summary>
        [Signal] public delegate void BattleFinishedEventHandler(bool playerWon, int earnedGold);

        [Export] public PackedScene WinScenePacked;
        [Export] public PackedScene LoseScenePacked;

        private const string BattleBackgroundPath = "res://assets/Turns/BattelBackground.png";
        private const string WinScenePath = "res://scenes/Turns/Win.tscn";
        private const string LoseScenePath = "res://scenes/Turns/Lose.tscn";
        private static readonly Vector2 BaseViewport = new Vector2(1152, 648);

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
        private RichTextLabel _infoLabel;
        private Label _commandSectionLabel;
        private Label _skillSectionLabel;
        private Label _itemSectionLabel;
        private RichTextLabel _logLabel;
        private PanelContainer _logPanel;
        private PanelContainer _playerPanel;
        private PanelContainer _enemyPanel;
        private PanelContainer _commandPanel;
        private Button _toggleLogButton;
        private bool _isLogVisible = true;

        private Button _attackButton;
        private Button _magicButton;
        private Button _itemButton;
        private Button _defendButton;
        private Button _backButton;

        private readonly Dictionary<Enemy, Node2D> _enemyNodes = new Dictionary<Enemy, Node2D>();
        
        /// <summary>
        /// Inicia la batalla con el jugador y la lista de enemigos proporcionados. Configura la interfaz, posiciona a los combatientes y comienza el turno del jugador.
        /// </summary>
        /// <param name="player">
        /// El jugador que participará en la batalla. Se espera que ya tenga sus estadísticas, habilidades y objetos configurados antes de llamar a este método.
        /// </param>
        /// <param name="enemies">
        /// La lista de enemigos que el jugador enfrentará en esta batalla. Cada enemigo debe tener sus estadísticas y comportamientos definidos para que la batalla funcione correctamente. Si se pasa null, se inicializará como una lista vacía, lo que resultará en una batalla sin enemigos (posiblemente para pruebas o escenarios especiales).
        /// </param>
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
                ApplyResponsiveLayout();
            }
            else
                GD.PrintErr("[BattleScene] StartBattle - Node is NOT inside tree yet!");
        }
        
        /// <summary>
        /// Método llamado por Godot cuando la escena se carga y se agrega al árbol de nodos. Se encarga de construir la interfaz de usuario, posicionar a los combatientes en el campo de batalla y mostrar los comandos principales para el jugador. También aplica un diseño responsivo para adaptarse a diferentes tamaños de pantalla. Este método es crucial para inicializar correctamente la escena de batalla y garantizar que todo esté listo para que el jugador comience a interactuar.
        /// </summary>
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

            ApplyResponsiveLayout();
        }
        
        /// <summary>
        /// Construye la interfaz de usuario de la escena de batalla, creando los nodos necesarios para mostrar el fondo, los paneles de información del jugador y enemigos, los comandos disponibles y el registro de acciones. Este método se asegura de que cada elemento esté correctamente anclado y organizado dentro de la jerarquía de nodos para garantizar una experiencia visual coherente y funcional durante la batalla. Además, se encarga de configurar estilos personalizados para los paneles y botones, así como de establecer las conexiones necesarias para manejar eventos como el cambio de tamaño de la ventana o la interacción con los botones.
        /// </summary>
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

            _playerPanel = new PanelContainer
            {
                Name = "PlayerPanel",
                CustomMinimumSize = new Vector2(220, 130)
            };
            _playerPanel.AnchorLeft = 0.02f;
            _playerPanel.AnchorTop = 0.75f;
            _playerPanel.AnchorRight = 0.22f;
            _playerPanel.AnchorBottom = 0.93f;
            _playerPanel.AddThemeStyleboxOverride("panel", CreatePlayerPanelStyle());
            _uiRoot.AddChild(_playerPanel);

            var playerLayout = new VBoxContainer
            {
                Name = "PlayerLayout",
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill
            };
            _playerPanel.AddChild(playerLayout);

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

            _enemyPanel = new PanelContainer
            {
                Name = "EnemyPanel",
                CustomMinimumSize = new Vector2(250, 150)
            };
            _enemyPanel.AnchorLeft = 0.74f;
            _enemyPanel.AnchorTop = 0.02f;
            _enemyPanel.AnchorRight = 0.98f;
            _enemyPanel.AnchorBottom = 0.25f;
            _enemyPanel.AddThemeStyleboxOverride("panel", CreateMiddlePanelStyle());
            _uiRoot.AddChild(_enemyPanel);

            var enemyLayout = new VBoxContainer
            {
                Name = "EnemyLayout",
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill
            };
            _enemyPanel.AddChild(enemyLayout);

            var enemyLabel = new Label { Text = "Enemigos" };
            enemyLayout.AddChild(enemyLabel);

            var enemyScroll = new ScrollContainer
            {
                Name = "EnemyScroll",
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill
            };
            enemyLayout.AddChild(enemyScroll);

            _enemyInfoContainer = new VBoxContainer
            {
                Name = "EnemyInfoContainer",
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill
            };
            enemyScroll.AddChild(_enemyInfoContainer);

            _commandPanel = new PanelContainer
            {
                Name = "CommandPanel",
                CustomMinimumSize = new Vector2(360, 160)
            };
            _commandPanel.AnchorLeft = 0.30f;
            _commandPanel.AnchorTop = 0.72f;
            _commandPanel.AnchorRight = 0.70f;
            _commandPanel.AnchorBottom = 0.93f;
            _commandPanel.AddThemeStyleboxOverride("panel", CreateRightPanelStyle());
            _uiRoot.AddChild(_commandPanel);

            var commandLayout = new VBoxContainer
            {
                Name = "CommandLayout",
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill
            };
            _commandPanel.AddChild(commandLayout);

            _commandSectionLabel = new Label
            {
                Text = "Acciones",
                HorizontalAlignment = HorizontalAlignment.Center
            };
            commandLayout.AddChild(_commandSectionLabel);

            _commandGrid = new GridContainer
            {
                Name = "CommandGrid",
                Columns = 2,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
            };
            commandLayout.AddChild(_commandGrid);

            _skillSectionLabel = new Label
            {
                Text = "Magias",
                HorizontalAlignment = HorizontalAlignment.Center
            };
            commandLayout.AddChild(_skillSectionLabel);

            _skillGrid = new GridContainer
            {
                Name = "SkillGrid",
                Columns = 2,
                Visible = false,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill
            };
            commandLayout.AddChild(_skillGrid);

            _itemSectionLabel = new Label
            {
                Text = "Objetos",
                HorizontalAlignment = HorizontalAlignment.Center
            };
            commandLayout.AddChild(_itemSectionLabel);

            _itemGrid = new GridContainer
            {
                Name = "ItemGrid",
                Columns = 2,
                Visible = false,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill
            };
            commandLayout.AddChild(_itemGrid);

            _targetGrid = new GridContainer
            {
                Name = "TargetGrid",
                Columns = 2,
                Visible = false,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill
            };
            commandLayout.AddChild(_targetGrid);

            _infoLabel = new RichTextLabel
            {
                Name = "InfoLabel",
                Visible = false,
                BbcodeEnabled = false,
                FitContent = false,
                ScrollFollowing = false,
                CustomMinimumSize = new Vector2(0, 80),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill
            };
            commandLayout.AddChild(_infoLabel);

            _backButton = CreateMenuButton("Volver");
            _backButton.Pressed += ShowMainCommands;
            _backButton.Visible = false;
            commandLayout.AddChild(_backButton);

            _logPanel = new PanelContainer
            {
                Name = "LogPanel",
                CustomMinimumSize = new Vector2(280, 140)
            };
            _logPanel.AnchorLeft = 0.02f;
            _logPanel.AnchorTop = 0.02f;
            _logPanel.AnchorRight = 0.34f;
            _logPanel.AnchorBottom = 0.24f;
            _logPanel.AddThemeStyleboxOverride("panel", CreateMiddlePanelStyle());
            _uiRoot.AddChild(_logPanel);

            var logLayout = new VBoxContainer
            {
                Name = "LogLayout",
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill
            };
            _logPanel.AddChild(logLayout);

            var logHeader = new HBoxContainer
            {
                Name = "LogHeader",
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
            };
            logLayout.AddChild(logHeader);

            logHeader.AddChild(new Label
            {
                Text = "Registro",
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
            });

            _toggleLogButton = new Button
            {
                Text = "Ocultar",
                CustomMinimumSize = new Vector2(110, 34)
            };
            _toggleLogButton.Pressed += ToggleLogVisibility;
            logHeader.AddChild(_toggleLogButton);

            _logLabel = new RichTextLabel
            {
                Name = "Log",
                BbcodeEnabled = false,
                FitContent = false,
                ScrollFollowing = true,
                CustomMinimumSize = new Vector2(0, 100),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill
            };
            logLayout.AddChild(_logLabel);

            ApplyLogVisibility();
            _uiRoot.Resized += ApplyResponsiveLayout;

            _uiBuilt = true;
        }
        
        /// <summary>
        /// Agrega los nodos de los combatientes (jugador y enemigos) al campo de batalla, asegurándose de que estén correctamente posicionados y visibles. Este método también se encarga de limpiar cualquier nodo anterior del campo de batalla para evitar superposiciones o inconsistencias visuales. Para cada combatiente, se verifica si es un AnimatedSprite2D para configurar su animación inicial en "Idle". Además, se utiliza CallDeferred para asegurar que la posición de los nodos se establezca después de que hayan sido agregados a la escena, lo que garantiza que las dimensiones del viewport estén disponibles para calcular las posiciones adecuadas.
        /// </summary>
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
        
        /// <summary>
        /// Posiciona al jugador en el campo de batalla, ubicándolo en la zona central-izquierda para evitar superposiciones con los paneles de información. Este método calcula la posición del jugador en función del tamaño del viewport, asegurándose de que se mantenga una proporción adecuada independientemente de la resolución de la pantalla. Además, establece la escala, orientación y visibilidad del jugador para garantizar que se muestre correctamente durante la batalla. Se utiliza CallDeferred para asegurar que esta configuración se realice después de que el nodo haya sido agregado a la escena y las dimensiones del viewport estén disponibles.
        /// </summary>
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
            
            // El jugador queda en la zona central-izquierda para no taparse con paneles inferiores.
            _player.Position = new Vector2(viewport.X * 0.30f, viewport.Y * 0.56f);
            _player.Scale = new Vector2(2.0f, 2.0f);
            _player.FlipH = false;
            _player.Visible = true;
            _player.ZIndex = 10;
            
            GD.Print($"[BattleScene] Player positioned - Pos: {_player.Position}, Scale: {_player.Scale}, Visible: {_player.Visible}, ZIndex: {_player.ZIndex}, Parent: {_player.GetParent()?.Name}");
        }
        
        /// <summary>
        /// Posiciona a un enemigo en el campo de batalla, ubicándolo en la zona central-derecha para evitar superposiciones con los paneles de información. Este método calcula la posición del enemigo en función del tamaño del viewport y su índice en la lista de enemigos, asegurándose de que se mantenga una proporción adecuada independientemente de la resolución de la pantalla. Además, establece la escala, orientación y visibilidad del enemigo para garantizar que se muestre correctamente durante la batalla. Se utiliza CallDeferred para asegurar que esta configuración se realice después de que el nodo haya sido agregado a la escena y las dimensiones del viewport estén disponibles.
        /// </summary>
        /// <param name="enemy">
        /// El enemigo que se va a posicionar en el campo de batalla. Se espera que este nodo ya haya sido agregado a la escena antes de llamar a este método, para asegurar que las dimensiones del viewport estén disponibles para calcular la posición adecuada. El método se encargará de configurar la posición, escala, orientación y visibilidad del enemigo para garantizar que se muestre correctamente durante la batalla.
        /// </param>
        /// <param name="index">
        /// El índice del enemigo en la lista de enemigos, utilizado para calcular su posición en el campo de batalla. Este índice determina la fila y columna en la que se ubicará el enemigo, permitiendo distribuir múltiples enemigos de manera ordenada en la zona central-derecha del campo de batalla. Se asume que los enemigos se organizan en una cuadrícula con un número fijo de columnas, y el índice se utiliza para calcular los desplazamientos horizontales y verticales necesarios para posicionar cada enemigo correctamente.
        /// </param>
        private void PositionEnemyDeferred(Enemy enemy, int index)
        {
            PositionEnemy(enemy, index);
        }
        
        /// <summary>
        /// Posiciona a un enemigo en el campo de batalla, ubicándolo en la zona central-derecha para evitar superposiciones con los paneles de información. Este método calcula la posición del enemigo en función del tamaño del viewport y su índice en la lista de enemigos, asegurándose de que se mantenga una proporción adecuada independientemente de la resolución de la pantalla. Además, establece la escala, orientación y visibilidad del enemigo para garantizar que se muestre correctamente durante la batalla. Se utiliza CallDeferred para asegurar que esta configuración se realice después de que el nodo haya sido agregado a la escena y las dimensiones del viewport estén disponibles.
        /// </summary>
        /// <param name="enemy">
        /// El enemigo que se va a posicionar en el campo de batalla. Se espera que este nodo ya haya sido agregado a la escena antes de llamar a este método, para asegurar que las dimensiones del viewport estén disponibles para calcular la posición adecuada. El método se encargará de configurar la posición, escala, orientación y visibilidad del enemigo para garantizar que se muestre correctamente durante la batalla.
        /// </param>
        /// <param name="index">
        /// El índice del enemigo en la lista de enemigos, utilizado para calcular su posición en el campo de batalla. Este índice determina la fila y columna en la que se ubicará el enemigo, permitiendo distribuir múltiples enemigos de manera ordenada en la zona central-derecha del campo de batalla. Se asume que los enemigos se organizan en una cuadrícula con un número fijo de columnas, y el índice se utiliza para calcular los desplazamientos horizontales y verticales necesarios para posicionar cada enemigo correctamente.
        /// </param>
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
            
            int columns = 2;
            int row = index / columns;
            int col = index % columns;
            float baseX = _enemies.Count == 1 ? viewport.X * 0.72f : viewport.X * 0.60f;
            float baseY = viewport.Y * 0.42f;
            float xOffset = col * 150.0f;
            float yOffset = row * 120.0f;

            float x = Mathf.Clamp(baseX + xOffset, viewport.X * 0.52f, viewport.X * 0.92f);
            float y = Mathf.Clamp(baseY + yOffset, viewport.Y * 0.30f, viewport.Y * 0.70f);
            enemy.Position = new Vector2(x, y);
            enemy.Scale = Vector2.One * 1.8f;
            enemy.FlipH = false;
            enemy.Visible = true;
            enemy.ZIndex = 5;
            
            GD.Print($"[BattleScene] Enemy {enemy.CharacterName} positioned - Pos: {enemy.Position}, Scale: {enemy.Scale}, Visible: {enemy.Visible}, ZIndex: {enemy.ZIndex}, Parent: {enemy.GetParent()?.Name}");
        }
        
        /// <summary>
        /// Reproduce la animación de ataque del personaje dado, si es un AnimatedSprite2D y tiene una animación llamada "Attack". Este método verifica el tipo del personaje y la existencia de la animación antes de intentar reproducirla, para evitar errores en tiempo de ejecución. Si el personaje no es un AnimatedSprite2D o no tiene la animación "Attack", el método simplemente no hará nada, permitiendo que la batalla continúe sin interrupciones visuales. Esta función se utiliza para mejorar la experiencia visual durante los ataques, proporcionando retroalimentación visual al jugador sobre las acciones que se están realizando en el campo de batalla.
        /// </summary>
        /// <param name="character">
        /// El personaje para el cual se desea reproducir la animación de ataque. Se espera que este personaje sea un nodo que herede de Character y, preferiblemente, un AnimatedSprite2D para que la animación pueda ser reproducida correctamente. Si el personaje no cumple con estas condiciones, el método no realizará ninguna acción, lo que permite que la lógica de la batalla continúe sin problemas incluso si el personaje no tiene una representación visual animada.
        /// </param>
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
        
        /// <summary>
        /// Reproduce la animación de espera (idle) del personaje dado, si es un AnimatedSprite2D y tiene una animación llamada "Idle". Este método verifica el tipo del personaje y la existencia de la animación antes de intentar reproducirla, para evitar errores en tiempo de ejecución. Si el personaje no es un AnimatedSprite2D o no tiene la animación "Idle", el método simplemente no hará nada, permitiendo que la batalla continúe sin interrupciones visuales. Esta función se utiliza para mantener una apariencia animada y viva de los personajes durante los momentos en los que no están realizando acciones específicas, proporcionando una experiencia visual más atractiva y dinámica durante la batalla.
        /// </summary>
        /// <param name="character">
        /// El personaje para el cual se desea reproducir la animación de espera (idle). Se espera que este personaje sea un nodo que herede de Character y, preferiblemente, un AnimatedSprite2D para que la animación pueda ser reproducida correctamente. Si el personaje no cumple con estas condiciones, el método no realizará ninguna acción, lo que permite que la lógica de la batalla continúe sin problemas incluso si el personaje no tiene una representación visual animada o no tiene una animación de espera definida.
        /// </param>
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
        
        /// <summary>
        /// Muestra los comandos principales disponibles para el jugador en su turno, como atacar, usar magia, usar objetos o defenderse. Este método se encarga de limpiar cualquier comando anterior de la interfaz y configurar los botones correspondientes a cada acción, habilitando o deshabilitando según las condiciones del jugador (por ejemplo, si tiene consumibles disponibles). Además, se asegura de que solo se muestren los comandos principales, ocultando cualquier menú de habilidades, objetos o selección de objetivos que pueda haber estado visible anteriormente. Esta función es fundamental para guiar al jugador a través de sus opciones durante su turno y garantizar una experiencia de usuario clara y fluida durante la batalla.
        /// </summary>
        private void ShowMainCommands()
        {
            if (_commandGrid == null)
                return;

            ClearChildren(_commandGrid);
            _commandGrid.Visible = true;
            _commandSectionLabel.Visible = true;
            if (_skillGrid != null)
                _skillGrid.Visible = false;
            if (_skillSectionLabel != null)
                _skillSectionLabel.Visible = false;
            if (_itemGrid != null)
                _itemGrid.Visible = false;
            if (_itemSectionLabel != null)
                _itemSectionLabel.Visible = false;
            if (_targetGrid != null)
                _targetGrid.Visible = false;
            if (_infoLabel != null)
                _infoLabel.Visible = false;
            if (_backButton != null)
                _backButton.Visible = false;

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

            _defendButton = CreateMenuButton("Informacion");
            _defendButton.Pressed += ShowInfoMenu;
            _commandGrid.AddChild(_defendButton);
        }
        
        /// <summary>
        /// Muestra el menú de habilidades disponibles para el jugador, permitiéndole seleccionar una habilidad para usar durante su turno. Este método se encarga de limpiar cualquier comando anterior de la interfaz y configurar los botones correspondientes a cada habilidad que el jugador tenga, habilitando o deshabilitando según las condiciones del jugador (por ejemplo, si tiene suficiente mana para usar la habilidad). Además, se asegura de que solo se muestre el menú de habilidades, ocultando cualquier menú de comandos principales, objetos o selección de objetivos que pueda haber estado visible anteriormente. Esta función es esencial para permitir al jugador acceder a sus habilidades y tomar decisiones estratégicas durante la batalla.
        /// </summary>
        private void ShowSkillMenu()
        {
            if (_player == null || _skillGrid == null)
                return;

            ClearChildren(_skillGrid);
            _commandGrid.Visible = false;
            _commandSectionLabel.Visible = false;
            _skillGrid.Visible = true;
            _skillSectionLabel.Visible = true;
            _itemGrid.Visible = false;
            _itemSectionLabel.Visible = false;
            _targetGrid.Visible = false;
            if (_infoLabel != null)
                _infoLabel.Visible = false;
            if (_backButton != null)
                _backButton.Visible = true;
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
        
        /// <summary>
        /// Muestra el menú de objetos consumibles disponibles para el jugador, permitiéndole seleccionar un objeto para usar durante su turno. Este método se encarga de limpiar cualquier comando anterior de la interfaz y configurar los botones correspondientes a cada objeto consumible que el jugador tenga, habilitando o deshabilitando según las condiciones del jugador (por ejemplo, si tiene consumibles disponibles). Además, se asegura de que solo se muestre el menú de objetos, ocultando cualquier menú de comandos principales, habilidades o selección de objetivos que pueda haber estado visible anteriormente. Esta función es crucial para permitir al jugador acceder a sus objetos consumibles y utilizarlos estratégicamente durante la batalla.
        /// </summary>
        private void ShowItemMenu()
        {
            if (_player == null || _itemGrid == null)
                return;

            ClearChildren(_itemGrid);
            _commandGrid.Visible = false;
            _commandSectionLabel.Visible = false;
            _skillGrid.Visible = false;
            _skillSectionLabel.Visible = false;
            _targetGrid.Visible = false;
            _itemGrid.Visible = true;
            _itemSectionLabel.Visible = true;
            if (_infoLabel != null)
                _infoLabel.Visible = false;
            if (_backButton != null)
                _backButton.Visible = true;
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
        
        /// <summary>
        /// Muestra el menú de selección de objetivos para un ataque o habilidad, permitiendo al jugador elegir a qué enemigo desea atacar o usar una habilidad. Este método se encarga de limpiar cualquier comando anterior de la interfaz y configurar los botones correspondientes a cada enemigo que esté vivo en el campo de batalla, mostrando su nombre y puntos de vida actuales. Además, se asegura de que solo se muestre el menú de selección de objetivos, ocultando cualquier menú de comandos principales, habilidades, objetos o información que pueda haber estado visible anteriormente. Esta función es fundamental para guiar al jugador a través del proceso de selección de objetivos durante su turno y garantizar una experiencia de usuario clara y fluida durante la batalla.
        /// </summary>
        /// <param name="skill">
        /// La habilidad que se va a usar contra el objetivo seleccionado. Este parámetro es opcional y se utiliza para mostrar información adicional en el menú de selección de objetivos, como el costo de mana o si la habilidad afecta a múltiples objetivos. Si se proporciona una habilidad, el menú de selección de objetivos se adaptará para reflejar las características de esa habilidad, ayudando al jugador a tomar una decisión informada sobre a qué enemigo atacar o usar la habilidad. Si no se proporciona una habilidad, el menú simplemente mostrará los enemigos disponibles para un ataque básico.
        /// </param>
        private void ShowTargetMenuForAttack(Skill skill = null)
        {
            _pendingSkill = skill;
            ClearChildren(_targetGrid);
            _targetGrid.Visible = true;
            _commandSectionLabel.Visible = false;
            _skillGrid.Visible = false;
            _skillSectionLabel.Visible = false;
            _itemGrid.Visible = false;
            _itemSectionLabel.Visible = false;
            _commandGrid.Visible = false;
            if (_infoLabel != null)
                _infoLabel.Visible = false;
            if (_backButton != null)
                _backButton.Visible = true;

            foreach (Enemy enemy in _enemies.Where(e => e != null && e.IsAlive))
            {
                Button targetButton = CreateMenuButton($"{enemy.CharacterName}\nHP:{enemy.Health}");
                targetButton.Pressed += () => OnTargetSelected(enemy);
                _targetGrid.AddChild(targetButton);
            }
        }
        
        /// <summary>
        /// Muestra el menú de información del héroe, proporcionando detalles sobre sus resistencias, debilidades y objetos pasivos. Este método se encarga de limpiar cualquier comando anterior de la interfaz y configurar el área de información para mostrar los atributos relevantes del jugador, como su resistencia al daño, debilidad y una lista de objetos pasivos que tiene equipados. Además, se asegura de que solo se muestre el menú de información, ocultando cualquier menú de comandos principales, habilidades, objetos o selección de objetivos que pueda haber estado visible anteriormente. Esta función es esencial para permitir al jugador revisar las estadísticas y características de su héroe durante la batalla, ayudándolo a tomar decisiones estratégicas informadas.
        /// </summary>
        private void ShowInfoMenu()
        {
            if (_player == null || _infoLabel == null)
                return;

            _commandGrid.Visible = false;
            _commandSectionLabel.Visible = false;
            _skillGrid.Visible = false;
            _skillSectionLabel.Visible = false;
            _itemGrid.Visible = false;
            _itemSectionLabel.Visible = false;
            _targetGrid.Visible = false;
            _infoLabel.Visible = true;
            if (_backButton != null)
                _backButton.Visible = true;

            var lines = new List<string>
            {
                "Informacion del heroe",
                $"Resistencia: {_player.DamageResistance}",
                $"Debilidad: {_player.DamageWeakness}",
                "",
                "Pasivos:" 
            };

            if (_player.Passives.Count == 0)
                lines.Add("- No tienes objetos pasivos.");
            else
            {
                foreach (PassiveItem passive in _player.Passives)
                {
                    if (passive == null)
                        continue;

                    string passiveLine = $"- {passive.ItemName} [{passive.Type}] +{passive.BonusValue}";
                    if (!string.IsNullOrWhiteSpace(passive.Description))
                        passiveLine += $": {passive.Description}";
                    lines.Add(passiveLine);
                }
            }

            _infoLabel.Clear();
            _infoLabel.AppendText(string.Join("\n", lines));
        }
        
        /// <summary>
        /// Aplica un diseño responsivo a los paneles de la interfaz de usuario, ajustando sus tamaños mínimos en función del tamaño actual del viewport. Este método se llama cada vez que la ventana se redimensiona para garantizar que los elementos de la interfaz se mantengan proporcionales y legibles en diferentes resoluciones. Calcula una escala basada en la relación entre el tamaño actual del viewport y un tamaño base predefinido, y luego aplica esta escala a los tamaños mínimos de los paneles de jugador, enemigos, comandos y registro. Además, ajusta el tamaño del panel de registro según si está visible o no, para optimizar el espacio disponible en la pantalla. Esta función es crucial para mantener una experiencia de usuario consistente y agradable en una variedad de dispositivos y configuraciones de pantalla.
        /// </summary>
        private void ApplyResponsiveLayout()
        {
            if (_uiRoot == null)
                return;

            Vector2 viewport = GetViewportRect().Size;
            if (viewport.X <= 0 || viewport.Y <= 0)
                viewport = BaseViewport;

            float widthScale = viewport.X / BaseViewport.X;
            float heightScale = viewport.Y / BaseViewport.Y;
            float scale = Mathf.Clamp(Mathf.Min(widthScale, heightScale), 1.0f, 1.9f);

            if (_playerPanel != null)
                _playerPanel.CustomMinimumSize = new Vector2(220, 130) * scale;
            if (_enemyPanel != null)
                _enemyPanel.CustomMinimumSize = new Vector2(250, 150) * scale;
            if (_commandPanel != null)
                _commandPanel.CustomMinimumSize = new Vector2(360, 160) * scale;

            if (_logPanel != null)
            {
                Vector2 visibleSize = new Vector2(280, 140) * scale;
                Vector2 collapsedSize = new Vector2(280, 54) * scale;
                _logPanel.CustomMinimumSize = _isLogVisible ? visibleSize : collapsedSize;
            }

            if (_toggleLogButton != null)
                _toggleLogButton.CustomMinimumSize = new Vector2(110, 34) * scale;
        }
        
        /// <summary>
        /// Maneja la acción de presionar el botón de ataque, preparando la interfaz para que el jugador seleccione un objetivo para su ataque básico. Este método se encarga de limpiar cualquier habilidad pendiente que el jugador pueda haber seleccionado previamente y luego muestra el menú de selección de objetivos para ataques, permitiendo al jugador elegir a qué enemigo desea atacar. Además, agrega un mensaje al registro para guiar al jugador en el proceso de selección del objetivo. Esta función es esencial para iniciar la secuencia de ataque del jugador y garantizar una experiencia de usuario clara y fluida durante la batalla.
        /// </summary>
        private void OnAttackPressed()
        {
            _pendingSkill = null;
            ShowTargetMenuForAttack();
            AddLog("Elige un objetivo para el ataque.");
        }
        
        /// <summary>
        /// Maneja la acción de presionar un botón de habilidad, preparando la interfaz para que el jugador seleccione un objetivo para la habilidad seleccionada. Este método verifica si la habilidad es de curación o de ataque múltiple y, en caso afirmativo, ejecuta la habilidad inmediatamente sin necesidad de seleccionar un objetivo. Para habilidades de ataque estándar, muestra el menú de selección de objetivos para ataques, permitiendo al jugador elegir a qué enemigo desea usar la habilidad. Además, agrega un mensaje al registro para guiar al jugador en el proceso de selección del objetivo. Esta función es crucial para manejar las diferentes mecánicas de las habilidades y garantizar una experiencia de usuario clara y fluida durante la batalla.
        /// </summary>
        /// <param name="skill">
        /// La habilidad que el jugador ha seleccionado para usar. Este parámetro se utiliza para determinar el tipo de habilidad (curación, ataque múltiple o ataque estándar) y para mostrar información relevante en el menú de selección de objetivos. Si la habilidad es de curación, se ejecutará inmediatamente sin necesidad de seleccionar un objetivo. Si la habilidad es de ataque múltiple, también se ejecutará inmediatamente, afectando a todos los enemigos. Para habilidades de ataque estándar, se mostrará el menú de selección de objetivos para que el jugador pueda elegir a qué enemigo desea usar la habilidad. Este parámetro es esencial para manejar las diferentes mecánicas de las habilidades y garantizar una experiencia de usuario clara y fluida durante la batalla.
        /// </param>
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
        
        /// <summary>
        /// Maneja la acción de seleccionar un objetivo para un ataque o habilidad, ejecutando la acción correspondiente y actualizando el estado de la batalla. Este método verifica si la batalla ha terminado, si el jugador o el enemigo seleccionado son nulos o si el enemigo no está vivo, y en caso afirmativo, simplemente retorna sin hacer nada. Si la selección es válida, reproduce la animación de ataque del jugador, ejecuta el ataque básico o la habilidad seleccionada contra el enemigo objetivo utilizando el controlador de batalla, y luego agrega el resultado al registro. Finalmente, llama a FinishPlayerAction para finalizar el turno del jugador y pasar al turno de los enemigos. Esta función es esencial para manejar la lógica de selección de objetivos y garantizar una experiencia de usuario clara y fluida durante la batalla.
        /// </summary>
        /// <param name="enemy">
        /// El enemigo que el jugador ha seleccionado como objetivo para su ataque o habilidad. Este parámetro se utiliza para determinar a quién se dirigirá la acción del jugador, ya sea un ataque básico o una habilidad específica. El método verificará que el enemigo seleccionado sea válido (no nulo, vivo y dentro de la batalla) antes de ejecutar la acción, asegurando que la lógica de la batalla se mantenga coherente y evitando errores en tiempo de ejecución. Este parámetro es fundamental para manejar la interacción del jugador con los enemigos durante su turno y garantizar una experiencia de usuario clara y fluida durante la batalla.
        /// </param>
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
        
        /// <summary>
        /// Maneja la acción de presionar un botón de objeto consumible, intentando usar el objeto seleccionado por el jugador y actualizando el estado de la batalla en consecuencia. Este método verifica si la batalla ha terminado o si el jugador es nulo, y en caso afirmativo, simplemente retorna sin hacer nada. Si la selección es válida, intenta usar el objeto consumible correspondiente al índice proporcionado, obteniendo un mensaje de resultado que se agrega al registro. Si el uso del objeto fue exitoso, se finaliza la acción del jugador y se pasa al turno de los enemigos. Si el uso del objeto no fue exitoso (por ejemplo, si el objeto no se puede usar en ese momento), se refresca la interfaz y se vuelve a mostrar el menú de objetos para que el jugador pueda intentar seleccionar otro objeto o revisar su inventario. Esta función es crucial para manejar la lógica de uso de objetos consumibles y garantizar una experiencia de usuario clara y fluida durante la batalla.
        /// </summary>
        /// <param name="itemIndex">
        /// El índice del objeto consumible que el jugador ha seleccionado para usar. Este parámetro se utiliza para identificar qué objeto del inventario del jugador se intentará usar, permitiendo que el método acceda a la información del objeto y ejecute su efecto correspondiente. El método verificará que el índice sea válido y que el objeto pueda ser usado en ese momento antes de ejecutar la acción, asegurando que la lógica de la batalla se mantenga coherente y evitando errores en tiempo de ejecución. Este parámetro es fundamental para manejar la interacción del jugador con sus objetos consumibles durante su turno y garantizar una experiencia de usuario clara y fluida durante la batalla.
        /// </param>
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
        
        /// <summary>
        /// Finaliza la acción del jugador después de realizar un ataque, usar una habilidad o consumir un objeto, actualizando el estado de la batalla y pasando al turno de los enemigos. Este método se encarga de reproducir la animación de espera (idle) del jugador para indicar que ha terminado su acción, refrescar la interfaz de usuario para mostrar los cambios en la salud, mana y otros atributos, y luego verificar si la batalla ha terminado. Si la batalla no ha terminado, desactiva los controles del jugador para evitar acciones adicionales durante el turno de los enemigos, y luego llama a RunEnemyTurn para iniciar el turno de los enemigos. Esta función es esencial para manejar la transición entre el turno del jugador y el turno de los enemigos, garantizando una experiencia de usuario clara y fluida durante la batalla.
        /// </summary>
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
        
        /// <summary>
        /// Ejecuta el turno de los enemigos, reproduciendo las animaciones de ataque, aplicando las acciones de los enemigos y luego regresando el control al jugador. Este método se encarga de esperar un breve momento antes de iniciar el turno para permitir que las animaciones se reproduzcan correctamente, luego itera a través de cada enemigo vivo para reproducir su animación de ataque. Después de un breve retraso para mostrar las animaciones, ejecuta las acciones de los enemigos utilizando el controlador de batalla y agrega los resultados al registro. Finalmente, reproduce la animación de espera (idle) para cada enemigo, refresca la interfaz y verifica si la batalla ha terminado. Si la batalla no ha terminado, activa los controles del jugador, muestra los comandos principales y comienza el turno del jugador. Esta función es crucial para manejar la lógica del turno de los enemigos y garantizar una experiencia de usuario clara y fluida durante la batalla.
        /// </summary>
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
        
        /// <summary>
        /// Inicia el turno del jugador, refrescando la interfaz de usuario, activando los controles de batalla y agregando un mensaje al registro para guiar al jugador en su turno. Este método se llama después de que los enemigos han completado su turno para devolver el control al jugador. Se encarga de asegurarse de que la interfaz esté actualizada con la información más reciente sobre la salud, mana y otros atributos del jugador y los enemigos, y luego activa los controles para que el jugador pueda seleccionar sus acciones. Finalmente, agrega un mensaje al registro para indicar que es el turno del jugador y que debe elegir una acción. Esta función es esencial para manejar la transición entre el turno de los enemigos y el turno del jugador, garantizando una experiencia de usuario clara y fluida durante la batalla.
        /// </summary>
        private void StartPlayerTurn()
        {
            if (_battleEnded || _player == null)
                return;

            RefreshUi();
            ToggleBattleControls(true);
            AddLog("Tu turno: elige una accion.");
        }

        /// <summary>
        /// Intenta finalizar la batalla verificando si el jugador o los enemigos han sido derrotados, y si es así, muestra la escena de victoria o derrota correspondiente. Este método se llama después de cada acción del jugador y de los enemigos para verificar si la batalla ha llegado a su fin. Si el jugador está vivo pero no quedan enemigos vivos, se considera una victoria, mientras que si el jugador ha sido derrotado, se considera una derrota. En ambos casos, se construye un resultado de batalla utilizando el controlador de batalla, se agrega un mensaje al registro con el resultado y luego se muestra la escena de victoria o derrota según corresponda. Finalmente, se emite una señal indicando que la batalla ha terminado y se libera la escena de batalla. Esta función es crucial para manejar el final de la batalla y garantizar una experiencia de usuario satisfactoria al concluir el enfrentamiento.
        /// </summary>
        /// <returns>
        /// Un valor booleano que indica si la batalla ha terminado o no. Retorna true si la batalla ha finalizado (ya sea por victoria o derrota), y false si la batalla aún continúa. Este valor se utiliza para controlar el flujo de la batalla, asegurando que una vez que se determina un resultado, no se permitan más acciones y se proceda a mostrar la escena correspondiente. Si la batalla no ha terminado, el juego continuará permitiendo acciones del jugador y de los enemigos hasta que se alcance un resultado definitivo.
        /// </returns>
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
        
        /// <summary>
        /// Muestra la escena de victoria al jugador, proporcionando información sobre el oro ganado y permitiendo que el jugador cierre la escena para regresar al menú principal o continuar con otras acciones. Este método se encarga de cargar la escena de victoria desde el recurso correspondiente, instanciarla y agregarla como hijo a la escena actual. Luego, llama al método StartWin de la escena de victoria para iniciar cualquier animación o lógica relacionada con la victoria, pasando el jugador y el oro ganado como parámetros. Finalmente, se suscribe al evento WinClosed de la escena de victoria para manejar el cierre de la escena y emitir la señal de que la batalla ha terminado. Esta función es esencial para proporcionar una experiencia satisfactoria al jugador después de ganar una batalla, celebrando su éxito y permitiéndole avanzar en el juego.
        /// </summary>
        /// <param name="earnedGold">
        /// La cantidad de oro que el jugador ha ganado como resultado de la victoria en la batalla. Este valor se calcula utilizando el controlador de batalla y se pasa a la escena de victoria para mostrarlo al jugador como parte de la recompensa por su éxito. El oro ganado puede ser utilizado posteriormente en el juego para comprar objetos, mejorar habilidades u otras acciones relacionadas con la progresión del jugador. Este parámetro es fundamental para proporcionar una sensación de recompensa y progreso al jugador después de ganar una batalla, incentivándolo a seguir avanzando en el juego.
        /// </param>
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
        
        /// <summary>
        /// Muestra la escena de derrota al jugador, proporcionando información sobre la derrota y permitiendo que el jugador cierre la escena para regresar al menú principal o intentar otra batalla. Este método se encarga de cargar la escena de derrota desde el recurso correspondiente, instanciarla y agregarla como hijo a la escena actual. Luego, llama al método StartLose de la escena de derrota para iniciar cualquier animación o lógica relacionada con la derrota, pasando el jugador como parámetro. Finalmente, se suscribe al evento LoseClosed de la escena de derrota para manejar el cierre de la escena y emitir la señal de que la batalla ha terminado. Esta función es esencial para proporcionar una experiencia adecuada al jugador después de perder una batalla, permitiéndole reflexionar sobre su derrota y motivándolo a intentarlo nuevamente.
        /// </summary>
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

        /// <summary>
        /// Maneja el cierre de la escena de victoria, emitiendo la señal de que la batalla ha terminado con un resultado de victoria y liberando la escena de batalla. Este método se suscribe al evento WinClosed de la escena de victoria para ser notificado cuando el jugador cierre la escena después de ganar. Al recibir esta notificación, emite la señal BattleFinished con un valor de true para indicar que el jugador ganó, junto con la cantidad de oro ganado como resultado de la victoria. Finalmente, llama a QueueFree para liberar la escena de batalla y permitir que el juego continúe con otras acciones, como regresar al menú principal o avanzar a la siguiente batalla. Esta función es esencial para manejar el flujo del juego después de una victoria y garantizar una experiencia de usuario satisfactoria.
        /// </summary>
        /// <param name="earnedGold">
        /// La cantidad de oro que el jugador ha ganado como resultado de la victoria en la batalla. Este valor se pasa a la señal BattleFinished para informar a otros sistemas del juego sobre la recompensa obtenida por el jugador, lo que puede ser utilizado para actualizar la interfaz de usuario, otorgar recompensas adicionales o realizar otras acciones relacionadas con la progresión del jugador. Este parámetro es fundamental para proporcionar una sensación de recompensa y progreso al jugador después de ganar una batalla, incentivándolo a seguir avanzando en el juego.
        /// </param>
        private void OnWinClosed(int earnedGold)
        {
            EmitSignal(SignalName.BattleFinished, true, earnedGold);
            QueueFree();
        }
        
        /// <summary>
        /// Maneja el cierre de la escena de derrota, emitiendo la señal de que la batalla ha terminado con un resultado de derrota y liberando la escena de batalla. Este método se suscribe al evento LoseClosed de la escena de derrota para ser notificado cuando el jugador cierre la escena después de perder. Al recibir esta notificación, emite la señal BattleFinished con un valor de false para indicar que el jugador perdió, junto con un valor de 0 para el oro ganado, ya que no se otorgan recompensas en caso de derrota. Finalmente, llama a QueueFree para liberar la escena de batalla y permitir que el juego continúe con otras acciones, como regresar al menú principal o intentar otra batalla. Esta función es esencial para manejar el flujo del juego después de una derrota y garantizar una experiencia de usuario adecuada.
        /// </summary>
        private void OnLoseClosed()
        {
            EmitSignal(SignalName.BattleFinished, false, 0);
            QueueFree();
        }
        
        /// <summary>
        /// Desconecta a los combatientes (jugador y enemigos) del campo de batalla, eliminándolos de la escena para limpiar el estado después de que la batalla ha terminado. Este método se encarga de verificar si el jugador y cada enemigo están actualmente conectados al campo de batalla (es decir, si son hijos del nodo del campo de batalla) y, en caso afirmativo, los elimina como hijos del campo de batalla. Para los enemigos, también llama a QueueFree para liberar sus recursos y asegurarse de que no queden referencias a ellos en la escena. Esta función es crucial para mantener la limpieza y el orden en la escena después de que la batalla ha concluido, evitando posibles problemas de rendimiento o errores relacionados con objetos que ya no deberían estar activos en la escena.
        /// </summary>
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
        
        /// <summary>
        /// Refresca la interfaz de usuario para mostrar la información más reciente sobre el jugador y los enemigos, incluyendo salud, mana, nombres y estados. Este método se llama después de cada acción del jugador o de los enemigos para asegurarse de que la interfaz refleje el estado actual de la batalla. Se encarga de actualizar los paneles de información del jugador y los enemigos, mostrando sus nombres, puntos de vida actuales, puntos de mana y cualquier otro atributo relevante. Además, ajusta la apariencia de los nodos de los enemigos en el campo de batalla para reflejar su estado (por ejemplo, cambiando su modulación si están heridos o derrotados). Esta función es esencial para mantener al jugador informado sobre el estado de la batalla y garantizar una experiencia de usuario clara y fluida durante el enfrentamiento.
        /// </summary>
        private void RefreshUi()
        {
            RefreshPlayerUi();
            RefreshEnemyUi();
        }

        /// <summary>
        /// Refresca la interfaz de usuario del jugador para mostrar su información más reciente, incluyendo salud, mana, nombre y estado. Este método se encarga de actualizar los elementos de la interfaz relacionados con el jugador, como su nombre, puntos de vida actuales, puntos de mana y cualquier otro atributo relevante. Además, ajusta la apariencia del nodo del jugador en el campo de batalla para reflejar su estado (por ejemplo, cambiando su modulación si está herido o derrotado). Esta función es esencial para mantener al jugador informado sobre su propio estado durante la batalla y garantizar una experiencia de usuario clara y fluida.
        /// </summary>
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

        /// <summary>
        /// Refresca la interfaz de usuario de los enemigos para mostrar su información más reciente, incluyendo salud, mana, nombres y estados. Este método se encarga de actualizar los elementos de la interfaz relacionados con cada enemigo, como sus nombres, puntos de vida actuales, puntos de mana y cualquier otro atributo relevante. Además, ajusta la apariencia de los nodos de los enemigos en el campo de batalla para reflejar su estado (por ejemplo, cambiando su modulación si están heridos o derrotados). Esta función es esencial para mantener al jugador informado sobre el estado de los enemigos durante la batalla y garantizar una experiencia de usuario clara y fluida durante el enfrentamiento.
        /// </summary>
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

        /// <summary>
        /// Activa o desactiva los controles de batalla (botones de comandos, habilidades, objetos y selección de objetivos) según el estado del turno del jugador. Este método se llama para habilitar los controles cuando es el turno del jugador y para deshabilitarlos durante el turno de los enemigos o cuando la batalla ha terminado. Al desactivar los controles, se evita que el jugador realice acciones adicionales mientras no tenga el control, garantizando que la lógica de la batalla se mantenga coherente y evitando posibles errores o comportamientos no deseados. Esta función es esencial para manejar la interacción del jugador con la interfaz de usuario durante la batalla y garantizar una experiencia de usuario clara y fluida.
        /// </summary>
        /// <param name="enabled">
        /// Un valor booleano que indica si los controles de batalla deben estar habilitados o deshabilitados. Si se establece en true, los botones de comandos, habilidades, objetos y selección de objetivos estarán activos y el jugador podrá interactuar con ellos. Si se establece en false, estos controles estarán desactivados y no responderán a las acciones del jugador, lo que es útil durante el turno de los enemigos o cuando la batalla ha terminado para evitar acciones adicionales. Este parámetro es fundamental para controlar la interacción del jugador con la interfaz de usuario durante la batalla y garantizar una experiencia de usuario clara y fluida.
        /// </param>
        private void ToggleBattleControls(bool enabled)
        {
            SetButtonsEnabled(_commandGrid, enabled);
            SetButtonsEnabled(_skillGrid, enabled);
            SetButtonsEnabled(_itemGrid, enabled);
            SetButtonsEnabled(_targetGrid, enabled);
            if (_backButton != null)
                _backButton.Disabled = !enabled;
        }

        /// <summary>
        /// Habilita o deshabilita todos los botones dentro de un contenedor específico, utilizado para controlar la interactividad de los botones de comandos, habilidades, objetos y selección de objetivos. Este método se llama desde ToggleBattleControls para aplicar el estado habilitado o deshabilitado a todos los botones dentro del contenedor proporcionado. Al iterar a través de los hijos del contenedor, verifica si cada hijo es un botón y ajusta su propiedad Disabled según el valor del parámetro enabled. Esta función es esencial para manejar la interactividad de los controles de batalla de manera eficiente y garantizar una experiencia de usuario clara y fluida durante la batalla.
        /// </summary>
        /// <param name="container">
        /// El contenedor que contiene los botones que se deben habilitar o deshabilitar. Este parámetro se utiliza para identificar el grupo específico de botones (comandos, habilidades, objetos o selección de objetivos) a los que se les aplicará el estado habilitado o deshabilitado. El método iterará a través de los hijos del contenedor y ajustará la propiedad Disabled de cada botón según el valor del parámetro enabled. Este parámetro es fundamental para controlar la interactividad de los diferentes grupos de botones en la interfaz de usuario durante la batalla y garantizar una experiencia de usuario clara y fluida.
        /// </param>
        /// <param name="enabled">
        /// Un valor booleano que indica si los botones dentro del contenedor deben estar habilitados o deshabilitados. Si se establece en true, los botones dentro del contenedor estarán activos y el jugador podrá interactuar con ellos. Si se establece en false, estos botones estarán desactivados y no responderán a las acciones del jugador, lo que es útil durante el turno de los enemigos o cuando la batalla ha terminado para evitar acciones adicionales. Este parámetro es fundamental para controlar la interactividad de los botones dentro del contenedor y garantizar una experiencia de usuario clara y fluida durante la batalla.
        /// </param>
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
        
        /// <summary>
        /// Elimina todos los nodos hijos de un contenedor específico, utilizado para limpiar la interfaz de usuario antes de actualizarla con nueva información sobre los enemigos o el registro de batalla. Este método se llama desde RefreshEnemyUi para eliminar los paneles de información de los enemigos antes de crear nuevos paneles con la información actualizada. Al iterar a través de los hijos del contenedor, llama a QueueFree para eliminar cada nodo hijo, asegurándose de que no queden referencias a ellos en la escena y evitando posibles problemas de rendimiento o errores relacionados con objetos que ya no deberían estar activos en la escena. Esta función es esencial para mantener la limpieza y el orden en la interfaz de usuario durante la batalla y garantizar una experiencia de usuario clara y fluida.
        /// </summary>
        /// <param name="container">
        /// El contenedor del cual se deben eliminar todos los nodos hijos. Este parámetro se utiliza para identificar el grupo específico de nodos (por ejemplo, los paneles de información de los enemigos o las entradas del registro de batalla) que se deben limpiar antes de actualizar la interfaz con nueva información. El método iterará a través de los hijos del contenedor y llamará a QueueFree para eliminar cada nodo hijo, asegurándose de que no queden referencias a ellos en la escena. Este parámetro es fundamental para mantener la limpieza y el orden en la interfaz de usuario durante la batalla y garantizar una experiencia de usuario clara y fluida.
        /// </param>
        private static void ClearChildren(Container container)
        {
            if (container == null)
                return;

            foreach (Node child in container.GetChildren())
            {
                child.QueueFree();
            }
        }
        
        /// <summary>
        /// Agrega un mensaje al registro de batalla, dividiendo el mensaje en líneas y asegurándose de que cada línea se agregue correctamente al registro. Este método se llama para mostrar información relevante sobre las acciones del jugador y los enemigos durante la batalla, como ataques, habilidades usadas, objetos consumidos y resultados de las acciones. Al dividir el mensaje en líneas utilizando el carácter de nueva línea ('\n'), se asegura de que cada parte del mensaje se muestre en una línea separada en el registro, mejorando la legibilidad y la claridad de la información presentada al jugador. Esta función es esencial para mantener al jugador informado sobre el desarrollo de la batalla y garantizar una experiencia de usuario clara y fluida durante el enfrentamiento.
        /// </summary>
        /// <param name="message">
        /// El mensaje que se desea agregar al registro de batalla. Este mensaje puede contener información sobre las acciones del jugador y los enemigos, como ataques realizados, habilidades usadas, objetos consumidos y resultados de las acciones. El método dividirá el mensaje en líneas utilizando el carácter de nueva línea ('\n') para asegurarse de que cada parte del mensaje se muestre en una línea separada en el registro, mejorando la legibilidad y la claridad de la información presentada al jugador. Este parámetro es fundamental para mantener al jugador informado sobre el desarrollo de la batalla y garantizar una experiencia de usuario clara y fluida durante el enfrentamiento.
        /// </param>
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
        
        /// <summary>
        /// Alterna la visibilidad del registro de batalla, mostrando u ocultando el panel de registro y ajustando la interfaz en consecuencia. Este método se llama cuando el jugador presiona el botón para mostrar u ocultar el registro de batalla, permitiéndole controlar cuánto espacio ocupa el registro en la pantalla. Al alternar la visibilidad, se ajusta el texto del botón para reflejar la acción actual (mostrar u ocultar) y se modifica el anclaje del panel de registro para ocupar más o menos espacio en la pantalla según corresponda. Esta función es esencial para proporcionar una experiencia de usuario personalizada, permitiendo al jugador decidir cuánto espacio desea dedicar al registro de batalla mientras mantiene una interfaz clara y fluida durante el enfrentamiento.
        /// </summary>
        private void ToggleLogVisibility()
        {
            _isLogVisible = !_isLogVisible;
            ApplyLogVisibility();
        }

        /// <summary>
        /// Aplica la visibilidad actual del registro de batalla a los elementos de la interfaz relacionados, mostrando u ocultando el panel de registro y ajustando el texto del botón y el anclaje del panel según corresponda. Este método se llama desde ToggleLogVisibility para actualizar la interfaz después de que se ha cambiado el estado de visibilidad del registro. Al verificar si los elementos relacionados con el registro (como el label, el botón y el panel) no son nulos, se asegura de que la interfaz se actualice correctamente sin causar errores. Esta función es esencial para mantener una experiencia de usuario clara y fluida al mostrar u ocultar el registro de batalla según las preferencias del jugador.
        /// </summary>
        private void ApplyLogVisibility()
        {
            if (_logLabel == null || _toggleLogButton == null || _logPanel == null)
                return;

            _logLabel.Visible = _isLogVisible;
            _toggleLogButton.Text = _isLogVisible ? "Ocultar" : "Mostrar";
            _logPanel.AnchorBottom = _isLogVisible ? 0.24f : 0.09f;
            ApplyResponsiveLayout();
        }
        
        /// <summary>
        /// Crea un botón de menú con un texto específico, configurando su tamaño mínimo y sus flags de tamaño para que se ajuste correctamente en la interfaz. Este método se utiliza para crear los botones de comandos, habilidades, objetos y selección de objetivos en la interfaz de batalla, asegurándose de que tengan un tamaño adecuado y se expandan correctamente dentro de sus contenedores. Al establecer un tamaño mínimo personalizado y los flags de tamaño horizontal y vertical, se garantiza que los botones sean lo suficientemente grandes para ser fácilmente interactuables por el jugador, al mismo tiempo que se adaptan al espacio disponible en la interfaz. Esta función es esencial para mantener una experiencia de usuario clara y fluida durante la batalla, proporcionando botones bien diseñados y fáciles de usar.
        /// </summary>
        /// <param name="text">
        /// El texto que se mostrará en el botón de menú. Este texto debe ser claro y descriptivo para indicar la función del botón, como "Atacar", "Habilidades", "Objetos" o "Seleccionar objetivo". Al proporcionar un texto específico para cada botón, se mejora la claridad de la interfaz y se facilita la comprensión de las opciones disponibles para el jugador durante la batalla. Este parámetro es fundamental para garantizar que los botones sean intuitivos y fáciles de usar, contribuyendo a una experiencia de usuario satisfactoria durante el enfrentamiento.
        /// </param>
        /// <returns>
        /// Un nuevo botón de menú configurado con el texto proporcionado, un tamaño mínimo personalizado y flags de tamaño que permiten que el botón se expanda correctamente dentro de su contenedor. Este botón está listo para ser agregado a la interfaz de batalla y utilizado para las acciones del jugador, como seleccionar comandos, habilidades, objetos o objetivos. La función garantiza que el botón tenga un diseño adecuado para la interacción del jugador, contribuyendo a una experiencia de usuario clara y fluida durante la batalla. Este valor de retorno es esencial para construir la interfaz de usuario de la batalla de manera eficiente y mantener una apariencia coherente en toda la escena.
        /// </returns>
        private static Button CreateMenuButton(string text)
        {
            return new Button
            {
                Text = text,
                CustomMinimumSize = new Vector2(96, 42),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.Fill
            };
        }
        
        /// <summary>
        /// Crea un estilo de panel para la información del jugador, configurando colores de fondo y borde, anchos de borde, radios de esquina y márgenes de contenido para lograr una apariencia atractiva y coherente con el tema de la batalla. Este método se utiliza para crear el estilo del panel que muestra la información del jugador durante la batalla, asegurándose de que tenga un diseño adecuado para resaltar la información importante como el nombre, la salud y el mana del jugador. Al configurar los colores, anchos de borde, radios de esquina y márgenes de contenido, se logra una apariencia visualmente atractiva que mejora la experiencia de usuario durante la batalla. Esta función es esencial para mantener una interfaz clara y fluida, proporcionando un diseño coherente con el tema general del juego.
        /// </summary>
        /// <returns>
        /// Un nuevo StyleBoxFlat configurado con colores de fondo y borde, anchos de borde, radios de esquina y márgenes de contenido específicos para el panel de información del jugador. Este estilo está listo para ser aplicado al panel correspondiente en la interfaz de batalla, contribuyendo a una apariencia atractiva y coherente con el tema del juego. La función garantiza que el estilo tenga un diseño adecuado para resaltar la información importante del jugador, mejorando la experiencia de usuario durante la batalla. Este valor de retorno es esencial para construir la interfaz de usuario de la batalla de manera eficiente y mantener una apariencia coherente en toda la escena.
        /// </returns>
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
        /// <summary>
        /// Crea un estilo de panel para la información de los enemigos, configurando colores de fondo y borde, anchos de borde, radios de esquina y márgenes de contenido para lograr una apariencia atractiva y coherente con el tema de la batalla. Este método se utiliza para crear el estilo del panel que muestra la información de los enemigos durante la batalla, asegurándose de que tenga un diseño adecuado para resaltar la información importante como los nombres, la salud y el mana de los enemigos. Al configurar los colores, anchos de borde, radios de esquina y márgenes de contenido, se logra una apariencia visualmente atractiva que mejora la experiencia de usuario durante la batalla. Esta función es esencial para mantener una interfaz clara y fluida, proporcionando un diseño coherente con el tema general del juego.
        /// </summary>
        /// <returns>
        /// Un nuevo StyleBoxFlat configurado con colores de fondo y borde, anchos de borde, radios de esquina y márgenes de contenido específicos para el panel de información de los enemigos. Este estilo está listo para ser aplicado al panel correspondiente en la interfaz de batalla, contribuyendo a una apariencia atractiva y coherente con el tema del juego. La función garantiza que el estilo tenga un diseño adecuado para resaltar la información importante de los enemigos, mejorando la experiencia de usuario durante la batalla. Este valor de retorno es esencial para construir la interfaz de usuario de la batalla de manera eficiente y mantener una apariencia coherente en toda la escena.
        /// </returns>
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
        
        /// <summary>
        /// Crea un estilo de panel para la información de los enemigos, configurando colores de fondo y borde, anchos de borde, radios de esquina y márgenes de contenido para lograr una apariencia atractiva y coherente con el tema de la batalla. Este método se utiliza para crear el estilo del panel que muestra la información de los enemigos durante la batalla, asegurándose de que tenga un diseño adecuado para resaltar la información importante como los nombres, la salud y el mana de los enemigos. Al configurar los colores, anchos de borde, radios de esquina y márgenes de contenido, se logra una apariencia visualmente atractiva que mejora la experiencia de usuario durante la batalla. Esta función es esencial para mantener una interfaz clara y fluida, proporcionando un diseño coherente con el tema general del juego.
        /// </summary>
        /// <returns>
        /// Un nuevo StyleBoxFlat configurado con colores de fondo y borde, anchos de borde, radios de esquina y márgenes de contenido específicos para el panel de información de los enemigos. Este estilo está listo para ser aplicado al panel correspondiente en la interfaz de batalla, contribuyendo a una apariencia atractiva y coherente con el tema del juego. La función garantiza que el estilo tenga un diseño adecuado para resaltar la información importante de los enemigos, mejorando la experiencia de usuario durante la batalla. Este valor de retorno es esencial para construir la interfaz de usuario de la batalla de manera eficiente y mantener una apariencia coherente en toda la escena.
        /// </returns>
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

