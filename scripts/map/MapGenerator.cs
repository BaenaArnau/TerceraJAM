using Godot;
using Godot.Collections;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SpellsAndRooms.scripts.map
{
    /// <summary>
    /// Genera un mapa de habitaciones para el juego, creando una estructura de caminos desde el piso 0 hasta el piso 14, con reglas específicas para la conexión de habitaciones y la asignación de tipos (Monstruo, Tienda, Fogata, Tesoro, Jefe).
    /// </summary>
    public partial class MapGenerator : Node
    {
        private const int Floors = 15;
        private const int MapWidth = 7;
        private const int Paths = 6;

        // Distancias para el posicionamiento visual
        private const int XDistance = 30;
        private const int YDistance = 25;
        private const float PlacementRandomness = 5.0f;

        // Pesos para la aleatoriedad
        private const float MonsterWeight = 10.0f;
        private const float ShopWeight = 2.5f;
        private const float CampfireWeight = 4.0f;
        private const int TreasureFloor = Floors / 2;

        private System.Collections.Generic.Dictionary<Room.RoomType, float> _weights;
        private float _totalWeight;
        
        // El "Grid" del mapa
        private Array<Array<Room>> _mapData = new Array<Array<Room>>();

        /// <summary>
        /// Genera el mapa completo siguiendo los pasos:
        /// 1. Crear la rejilla base (7x15) con habitaciones "NotAssigned".
        /// 2. Obtener los índices de las columnas iniciales (piso 0).
        /// 3. Crear los caminos (Paths) desde el piso 0 hasta el 14, asegurando que no se crucen.
        /// 4. Reglas específicas: El Jefe en el piso 14, y las habitaciones del piso 13 (Campfire) deben ir al Boss.
        /// 5. Calcular pesos y asignar tipos (Monster, Shop, Campfire, Treasure) a las habitaciones restantes, asegurando que todas las habitaciones conectadas tengan un tipo asignado y que las no conectadas queden como "NotAssigned".
        /// </summary>
        /// <returns>
        /// El mapa generado como una matriz de habitaciones, donde cada habitación tiene su tipo, posición y conexiones definidas según las reglas establecidas.
        /// </returns>
        public Array<Array<Room>> GenerateMap()
        {
            // 1. Crear la rejilla base (7x15) con habitaciones "NotAssigned"
            _mapData = GenerateInitialGrid();
        
            // 2. Obtener los índices de las columnas iniciales (piso 0)
            Array<int> startingPoints = GetRandomStartingPoints();
        
            // 3. Crear los caminos (Paths) desde el piso 0 hasta el 14
            foreach (int startJ in startingPoints)
            {
                int currentJ = startJ;
                // Iteramos por todos los pisos menos el último (donde está el Boss)
                for (int i = 0; i < Floors - 1; i++)
                {
                    // SetupConnection conecta la habitación actual con una arriba
                    // y devuelve la columna de la habitación elegida para seguir el camino
                    currentJ = SetupConnection(i, currentJ);
                }
            }

            // 4. Reglas específicas: El Jefe
            SetupBossRoom();
        
            // 5. Calcular pesos y asignar tipos (Monster, Shop, Campfire...)
            SetupRoomWeights();
            SetupRoomTypes();
            
            return _mapData;
        }
        
        /// <summary>
        /// Obtiene una lista de índices de columnas aleatorias para los puntos de partida en el piso 0, asegurando que no se repitan y que su cantidad no exceda el número de caminos (Paths) o el ancho del mapa (MapWidth).
        /// </summary>
        /// <returns>
        /// Una lista de índices de columnas (enteros) que representan los puntos de partida para los caminos en el piso 0. La cantidad de índices devueltos será igual a la cantidad de caminos (Paths) o al ancho del mapa (MapWidth), lo que sea menor, y cada índice será único dentro de esa lista.
        /// </returns>
        private Array<int> GetRandomStartingPoints()
        {
            List<int> availableColumns = Enumerable.Range(0, MapWidth).ToList();
            var startingPoints = new Array<int>();
            int pointCount = Mathf.Min(Paths, MapWidth);

            for (int i = 0; i < pointCount; i++)
            {
                int index = (int)(GD.Randi() % (uint)availableColumns.Count);
                startingPoints.Add(availableColumns[index]);
                availableColumns.RemoveAt(index);
            }

            return startingPoints;
        }
        
        /// <summary>
        /// Conecta la habitación en la posición (i, j) con una habitación en el piso superior (i+1) siguiendo las reglas de conexión:
        /// - Se elige una columna adyacente (j-1, j, j+1) para conectar, asegurando que no se salga del mapa.
        /// - Se verifica que la conexión no cruce caminos existentes (es decir, no conecte a una habitación que ya esté conectada a la habitación opuesta en el piso superior).
        /// - Si no se encuentra una conexión válida después de varios intentos, se conecta a la habitación directamente arriba (misma columna).
        /// </summary>
        /// <param name="i">
        /// El índice del piso actual desde el cual se desea conectar hacia arriba. Este valor debe ser menor que Floors - 1, ya que no se puede conectar desde el último piso.
        /// </param>
        /// <param name="j">
        /// El índice de la columna de la habitación actual en el piso i desde la cual se desea conectar hacia arriba. Este valor debe estar dentro del rango de 0 a MapWidth - 1, inclusive, para asegurar que se refiere a una habitación válida dentro del mapa.
        /// </param>
        /// <returns>
        /// El índice de la columna de la habitación en el piso superior (i+1) a la cual se ha conectado la habitación actual. Este valor será igual a j, j-1 o j+1, dependiendo de la conexión realizada, y estará dentro del rango de 0 a MapWidth - 1, inclusive. Si no se pudo encontrar una conexión válida después de varios intentos, se devolverá j, indicando que se conectó a la habitación directamente arriba (misma columna).
        /// </returns>
        private int SetupConnection(int i, int j)
        {
            Room currentRoom = _mapData[i][j];

            for (int attempt = 0; attempt < 32; attempt++)
            {
                // Elegimos una columna adyacente (-1, 0, 1) arriba
                int nextJ = Mathf.Clamp(j + ((int)(GD.Randi() % 3u) - 1), 0, MapWidth - 1);
                Room nextRoomCandidate = _mapData[i + 1][nextJ];

                // Regla de "No cruzar caminos"
                if (!WouldCrossExistingPath(i, j, nextRoomCandidate))
                {
                    if (!currentRoom.NextRooms.Contains(nextRoomCandidate))
                        currentRoom.NextRooms.Add(nextRoomCandidate);
                    return nextJ;
                }
            }

            // Plan de escape si todas las opciones válidas se bloquearon
            Room fallbackRoom = _mapData[i + 1][j];
            if (!currentRoom.NextRooms.Contains(fallbackRoom))
                currentRoom.NextRooms.Add(fallbackRoom);
            return j;
        }
        
        /// <summary>
        /// Verifica si conectar la habitación actual (en la fila i, columna j) con una habitación candidata en el piso superior (i+1, columna nextColumn) cruzaría un camino existente. Un cruce ocurre si la habitación adyacente a la candidata (en la misma fila i) ya apunta a la habitación opuesta en el piso superior (i+1, columna j).
        /// </summary>
        /// <param name="row">
        /// El índice de la fila (piso) de la habitación actual desde la cual se desea conectar hacia arriba. Este valor debe ser menor que Floors - 1, ya que no se puede conectar desde el último piso.
        /// </param>
        /// <param name="column">
        /// El índice de la columna de la habitación actual en la fila dada desde la cual se desea conectar hacia arriba. Este valor debe estar dentro del rango de 0 a MapWidth - 1, inclusive, para asegurar que se refiere a una habitación válida dentro del mapa.
        /// </param>
        /// <param name="nextRoomCandidate">
        /// La habitación candidata en el piso superior (row + 1) a la cual se desea conectar la habitación actual. Esta habitación debe ser una instancia válida dentro del mapa, ubicada en la fila row + 1 y en una columna adyacente (column - 1, column, o column + 1) a la columna de la habitación actual. El método verificará si conectar a esta habitación candidata cruzaría un camino existente según las reglas establecidas.
        /// </param>
        /// <returns>
        /// Un valor booleano que indica si conectar la habitación actual con la habitación candidata cruzaría un camino existente. Devuelve true si se detecta un cruce (es decir, si la habitación adyacente a la candidata ya apunta a la habitación opuesta en el piso superior), y false si no se detecta ningún cruce, lo que significa que la conexión es válida según las reglas establecidas.
        /// </returns>
        private bool WouldCrossExistingPath(int row, int column, Room nextRoomCandidate)
        {
            int nextColumn = nextRoomCandidate.Column;

            // Si no se mueve de columna, no puede cruzar otra ruta.
            if (Math.Abs(nextColumn - column) != 1)
                return false;

            // Un cruce ocurre si la habitación adyacente ya apunta a la habitación opuesta.
            int adjacentColumn = nextColumn;
            Room adjacentRoom = _mapData[row][adjacentColumn];
            Room oppositeRoom = _mapData[row + 1][column];

            return adjacentRoom.NextRooms.Contains(oppositeRoom);
        }
        
        /// <summary>
        /// Configura la habitación del jefe en el piso 14, asegurando que todas las habitaciones del piso 13 (Campfire) estén conectadas a esta habitación del jefe. Esto garantiza que el jugador siempre tenga un camino claro hacia el jefe desde el piso anterior, cumpliendo con la regla específica de que el piso 13 debe conducir al jefe en el piso 14.
        /// </summary>
        private void SetupBossRoom()
        {
            int middle = Mathf.FloorToInt(MapWidth * 0.5f);
            Room bossRoom = _mapData[Floors - 1][middle];
            bossRoom.Type = Room.RoomType.Boss;

            // Todas las habitaciones del piso 13 (Campfire) deben ir al Boss
            foreach (Room room in _mapData[Floors - 2])
            {
                if (room.NextRooms.Count > 0)
                {
                    room.NextRooms.Clear();
                    room.NextRooms.Add(bossRoom);
                }
            }
        }
        
        /// <summary>
        /// Configura los pesos para la asignación aleatoria de tipos de habitaciones (Monstruo, Tienda, Fogata) en los pisos intermedios del mapa. Estos pesos se utilizan para influir en la probabilidad de que cada tipo de habitación sea asignado a las habitaciones conectadas durante el proceso de asignación de tipos. El método calcula el peso total sumando los pesos individuales, lo que permite realizar selecciones ponderadas posteriormente.
        /// </summary>
        private void SetupRoomWeights()
        {
            _weights = new System.Collections.Generic.Dictionary<Room.RoomType, float>
            {
                { Room.RoomType.Monster, MonsterWeight },
                { Room.RoomType.Campfire, CampfireWeight },
                { Room.RoomType.Shop, ShopWeight }
            };

            _totalWeight = 0.0f;
            foreach (float weight in _weights.Values)
            {
                _totalWeight += weight;
            }
        }

        private Room.RoomType GetWeightedRoomType()
        {
            float roll = (float)GD.RandRange(0.0, _totalWeight);
            float accumulatedWeight = 0.0f;

            foreach (var entry in _weights)
            {
                accumulatedWeight += entry.Value;
                if (roll <= accumulatedWeight)
                    return entry.Key;
            }

            return Room.RoomType.Monster;
        }

        private void SetupRoomTypes()
        {
            var incomingRooms = new HashSet<Room>();
            foreach (Array<Room> floor in _mapData)
            {
                foreach (Room room in floor)
                {
                    foreach (Room nextRoom in room.NextRooms)
                    {
                        incomingRooms.Add(nextRoom);
                    }
                }
            }

            for (int i = 0; i < Floors; i++)
            {
                for (int j = 0; j < MapWidth; j++)
                {
                    Room room = _mapData[i][j];
                    bool isConnected = room.NextRooms.Count > 0 || incomingRooms.Contains(room);

                    if (!isConnected)
                    {
                        room.Type = Room.RoomType.NotAssigned;
                        continue;
                    }

                    if (i == 0)
                        room.Type = Room.RoomType.Monster;
                    else if (i == Floors - 1)
                        room.Type = Room.RoomType.Boss;
                    else if (i == Floors - 2)
                        room.Type = Room.RoomType.Campfire;
                    else if (i == TreasureFloor)
                        room.Type = Room.RoomType.Treasure;
                    else if (i <= 2)
                        room.Type = Room.RoomType.Monster;
                    else
                        room.Type = GetWeightedRoomType();
                }
            }
        }

        private Array<Array<Room>> GenerateInitialGrid()
        {
            var result = new Array<Array<Room>>();

            for (int i = 0; i < Floors; i++)
            {
                var adjacentRooms = new Array<Room>();
                for (int j = 0; j < MapWidth; j++)
                {
                    Room currentRoom = new Room();
                    float offsetX = (float)GD.RandRange(0, PlacementRandomness);
                    float offsetY = (float)GD.RandRange(0, PlacementRandomness);

                    currentRoom.Row = i;
                    currentRoom.Column = j;
                    // En Godot, Y negativa va hacia arriba
                    currentRoom.Position = new Vector2(j * XDistance + offsetX, i * -YDistance + offsetY);
                    
                    adjacentRooms.Add(currentRoom);
                }
                result.Add(adjacentRooms);
            }
            return result;
        }
    }
}
