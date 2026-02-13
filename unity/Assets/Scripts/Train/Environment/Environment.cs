using UnityEngine;
using Random = System.Random;

namespace Train.Environment {
    public class Environment : MonoBehaviour {
        [SerializeField] private Transform wallParent;
        [SerializeField] private GameObject wallPrefab;
        [SerializeField] private GameObject floorPrefab;

        [SerializeField] private float wallCenterY = 2.5f;

        [SerializeField] private Transform zombieSpawnMarker;
        [SerializeField] private Transform targetSpawnMarker;

        [Min(8)] [SerializeField] private int gridWidth = 64;
        [Min(8)] [SerializeField] private int gridHeight = 64;
        [Min(0.1f)] [SerializeField] private float cellSize = 1f;

        [SerializeField] private int initialSeed;

        [Min(2)] [SerializeField] private int roomCount = 14;
        [Min(1)] [SerializeField] private int maxRoomRerolls = 60;
        [Range(0f, 1f)] [SerializeField] private float circleRoomChance = 0.35f;

        [SerializeField] private RectInt rectSizeRange = new(5, 5, 12, 12);

        [Min(2)] [SerializeField] private int circleMinR = 3;
        [Min(2)] [SerializeField] private int circleMaxR = 7;

        [Min(0)] [SerializeField] private int roomPadding = 1;

        [Min(1)] [SerializeField] private int roadWidth = 1;
        [SerializeField] private bool randomizeLTurnOrder = true;

        [Range(0, 3)] [SerializeField] private int organicIterations = 1;
        [Range(0f, 0.25f)] [SerializeField] private float organicCarveRatio = 0.06f;
        [Range(0f, 0.25f)] [SerializeField] private float organicGrowRatio = 0.05f;
        [Min(1)] [SerializeField] private int organicGrowMaxTriesPerCell = 6;

        [Header("Height")] [SerializeField] private int minRoomLevel = -4;
        [SerializeField] private int maxRoomLevel = 4;

        [Min(0.01f)] [SerializeField] private float levelHeight = 1.0f;
        [SerializeField] private float floorThickness = 0.2f;

        [Min(0f)] [SerializeField] private float maxStepHeight = 0.5f;

        private Map _map;

        private OrganicShaper _organicShaper;
        private Random _rng;
        private RoadGenerator _roadGenerator;
        private RoomGenerator _roomGenerator;
        private Visualizer _visualizer;

        private void Awake() {
            if (wallParent == null) {
                wallParent = transform;
            }

            _visualizer = new Visualizer(
                wallParent,
                wallPrefab,
                wallCenterY,
                floorPrefab,
                floorThickness
            );

            _roadGenerator = new RoadGenerator();
        }

        private void Start() => Reset(initialSeed);

        public void Reset(int seed = 0) {
            _organicShaper = new OrganicShaper(
                organicIterations,
                organicCarveRatio,
                organicGrowRatio,
                organicGrowMaxTriesPerCell
            );

            _roomGenerator = new RoomGenerator(_organicShaper);

            _rng = new Random(seed == 0 ? System.Environment.TickCount : seed);
            _map = new Map(gridWidth, gridHeight, cellSize);

            _roomGenerator.PlaceRooms(
                _map,
                _rng,
                roomCount,
                maxRoomRerolls,
                circleRoomChance,
                rectSizeRange,
                circleMinR,
                circleMaxR,
                roomPadding,
                minRoomLevel,
                maxRoomLevel
            );

            _roomGenerator.AssignHeightsByRoadConstraintMst(
                _map,
                _rng,
                minRoomLevel,
                maxRoomLevel,
                levelHeight,
                maxStepHeight
            );

            foreach (Room room in _map.Rooms) {
                _roomGenerator.WriteRoomToGrid(_map, room, levelHeight);
            }

            _roadGenerator.ConnectRoomsAndRoadHeight(
                _map,
                _rng,
                roadWidth,
                randomizeLTurnOrder,
                levelHeight
            );

            _map.ApplyBorderWalls();
            _visualizer.Rebuild(_map);

            PlaceFarthestRoomSpawns();
        }

        private void PlaceFarthestRoomSpawns() {
            if (zombieSpawnMarker == null || targetSpawnMarker == null) {
                return;
            }

            if (_map == null || _map.Rooms == null || _map.Rooms.Count < 2) {
                return;
            }

            int a = 0, b = 1;
            long best = -1;

            for (int i = 0; i < _map.Rooms.Count; i++)
            for (int j = i + 1; j < _map.Rooms.Count; j++) {
                Vector2Int c1 = _map.Rooms[i].center;
                Vector2Int c2 = _map.Rooms[j].center;

                long dx = c1.x - c2.x;
                long dy = c1.y - c2.y;
                long d2 = (dx * dx) + (dy * dy);

                if (d2 > best) {
                    best = d2;
                    a = i;
                    b = j;
                }
            }

            _visualizer.PlaceSpawnMarkers(
                _map,
                _map.Rooms[a].center,
                _map.Rooms[b].center,
                zombieSpawnMarker,
                targetSpawnMarker
            );
        }
    }
}
