using UnityEngine;

public sealed class Environment : MonoBehaviour {
    [SerializeField] private Transform basePart;

    [SerializeField] private Transform wallParent;
    [SerializeField] private GameObject wallPrefab;
    [SerializeField] private GameObject floorPrefab;

    [SerializeField] private float wallCenterY = 2.5f;

    [SerializeField] private Transform zombieSpawnMarker;
    [SerializeField] private Transform targetSpawnMarker;

    [Min(8)] [SerializeField] private int gridWidth = 64;
    [Min(8)] [SerializeField] private int gridHeight = 64;
    [Min(0.1f)] [SerializeField] private float cellSize = 1f;

    [SerializeField] private bool autoGenerateOnPlay = true;
    [SerializeField] private int seed = 0; // 0 => random each play
    [SerializeField] private bool keepBorderWalls = true;

    [Min(2)] [SerializeField] private int roomCount = 14;
    [Min(1)] [SerializeField] private int maxRoomRerolls = 60;
    [Range(0f, 1f)] [SerializeField] private float circleRoomChance = 0.35f;

    [Min(3)] [SerializeField] private int rectMinW = 5;
    [Min(3)] [SerializeField] private int rectMaxW = 12;
    [Min(3)] [SerializeField] private int rectMinH = 5;
    [Min(3)] [SerializeField] private int rectMaxH = 12;

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

    [Min(0.01f)] [SerializeField] private float levelStepHeight = 1.0f; // 1 level -> world Y height
    [SerializeField] private float floorThickness = 0.2f;

    private MapData _map;
    private System.Random _rng;

    private OrganicShaper _organicShaper;
    private Visualizer _visualizer;

    private RoomGenerator _roomGenerator;
    private RoadGenerator _roadGenerator;

    private void Awake() {
        if (wallParent == null) {
            wallParent = transform;
        }

        _organicShaper = new OrganicShaper();

        _visualizer = new Visualizer(
            wallParent,
            wallPrefab,
            wallCenterY,
            floorPrefab,
            floorThickness
        );

        _roomGenerator = new RoomGenerator(_organicShaper);
        _roadGenerator = new RoadGenerator();
    }

    private void Start() {
        if (autoGenerateOnPlay) {
            Generate();
        }
    }

    public void Generate() {
        int actualSeed = seed == 0 ? System.Environment.TickCount : seed;
        _rng = new System.Random(actualSeed);

        _map = new MapData {
            Width = gridWidth,
            Height = gridHeight,
            CellSize = cellSize,
            Origin = GetGridOrigin(gridWidth, gridHeight, cellSize),

            WallMatrix = new bool[gridHeight, gridWidth],
            RoomIdMatrix = new int[gridHeight, gridWidth],
            TileHeight = new float[gridHeight, gridWidth],
            Rooms = new System.Collections.Generic.List<Room>(roomCount)
        };

        InitializeMatrices();

        OrganicShaper.Config organicCfg = new(
            organicIterations,
            organicCarveRatio,
            organicGrowRatio,
            organicGrowMaxTriesPerCell
        );

        _roomGenerator.PlaceRooms(
            _map,
            _rng,
            roomCount,
            maxRoomRerolls,
            circleRoomChance,
            rectMinW,
            rectMaxW,
            rectMinH,
            rectMaxH,
            circleMinR,
            circleMaxR,
            roomPadding,
            minRoomLevel,
            maxRoomLevel,
            organicCfg
        );

        _roomGenerator.WriteRoomsToGrid(_map, levelStepHeight);

        _roadGenerator.ConnectRoomsMstAndPaintRoadHeights(
            _map,
            _rng,
            roadWidth,
            randomizeLTurnOrder,
            levelStepHeight
        );

        ApplyBorderWallsIfNeeded();

        _visualizer.Rebuild(_map);
        _visualizer.PlaceSpawnMarkers(_map, zombieSpawnMarker, targetSpawnMarker);
    }

    private void InitializeMatrices() {
        for (int y = 0; y < _map.Height; y++)
        for (int x = 0; x < _map.Width; x++) {
            _map.WallMatrix[y, x] = true;
            _map.RoomIdMatrix[y, x] = -1;
            _map.TileHeight[y, x] = 0f;
        }
    }

    private void ApplyBorderWallsIfNeeded() {
        if (!keepBorderWalls) {
            return;
        }

        for (int x = 0; x < _map.Width; x++) {
            _map.WallMatrix[0, x] = true;
            _map.WallMatrix[_map.Height - 1, x] = true;
        }

        for (int y = 0; y < _map.Height; y++) {
            _map.WallMatrix[y, 0] = true;
            _map.WallMatrix[y, _map.Width - 1] = true;
        }
    }

    private Vector3 GetGridOrigin(int width, int height, float cellWorldSize) {
        Vector3 center = basePart != null ? basePart.position : transform.position;

        float totalW = width * cellWorldSize;
        float totalH = height * cellWorldSize;

        return new Vector3(center.x - (totalW * 0.5f), 0f, center.z - (totalH * 0.5f));
    }
}
