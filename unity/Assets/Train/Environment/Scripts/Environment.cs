using UnityEngine;

namespace Train.Environment.Scripts {
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

        [SerializeField] private int seed;

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

        [Header("Height")] [SerializeField] private int minRoomLevel = -2;
        [SerializeField] private int maxRoomLevel = 2;

        [Min(0.01f)] [SerializeField] private float levelStepHeight = 1.0f;
        [SerializeField] private float floorThickness = 0.2f;

        private Map _map;
        private System.Random _rng;

        private OrganicShaper _organicShaper;
        private Visualizer _visualizer;
        private RoomGenerator _roomGenerator;
        private RoadGenerator _roadGenerator;

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

            _organicShaper = null;
            _roomGenerator = null;

            _roadGenerator = new RoadGenerator();
        }

        private void Start() => Generate();

        public void Generate() {
            _organicShaper = new OrganicShaper(
                organicIterations,
                organicCarveRatio,
                organicGrowRatio,
                organicGrowMaxTriesPerCell
            );
            _roomGenerator = new RoomGenerator(_organicShaper);

            _rng = new System.Random(seed == 0 ? System.Environment.TickCount : seed);

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

            foreach (Room room in _map.Rooms) {
                _roomGenerator.WriteRoomToGrid(_map, room, levelStepHeight);
            }

            _roadGenerator.ConnectRoomsAndRoadHeight(
                _map,
                _rng,
                roadWidth,
                randomizeLTurnOrder,
                levelStepHeight
            );

            _map.ApplyBorderWalls();

            _visualizer.Rebuild(_map);
            _visualizer.PlaceSpawnMarkers(_map, zombieSpawnMarker, targetSpawnMarker);
        }
    }
}
