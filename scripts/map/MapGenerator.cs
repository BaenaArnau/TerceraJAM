using Godot;
using Godot.Collections;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SpellsAndRooms.scripts.map
{
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
