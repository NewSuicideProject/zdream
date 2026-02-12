using Unity.MLAgents.Sensors;

namespace Train.Sensors {
    public class TerrainSensor : ISensor {
        private readonly Terrain.Terrain _terrain;
        private readonly Proprioception _proprioception;
        private readonly ObservationSpec _observationSpec;
        private readonly int _gridSize;
        private readonly int _size;
        private readonly float _expectedMaxHeight;

        public TerrainSensor(Terrain.Terrain terrain, Proprioception proprioception, int gridSize,
            float expectedMaxHeight) {
            _terrain = terrain;
            _proprioception = proprioception;
            _gridSize = gridSize;
            _size = _gridSize * _gridSize;
            _observationSpec = ObservationSpec.Vector(_size);
            _expectedMaxHeight = expectedMaxHeight;
        }

        public string GetName() => "terrain";

        private float NormalizeHeight(float height) => Normalization.Tanh(height, _expectedMaxHeight);

        public int Write(ObservationWriter writer) {
            int idx = 0;
            float agentY = _proprioception.Position.y;

            for (int z = 0; z < _gridSize; z++) {
                for (int x = 0; x < _gridSize; x++) {
                    float relativeHeight = _terrain.HeightMap[x, z] - agentY;
                    writer[idx++] = NormalizeHeight(relativeHeight);
                }
            }

            return _size;
        }

        public byte[] GetCompressedObservation() => null;

        public ObservationSpec GetObservationSpec() => _observationSpec;

        public CompressionSpec GetCompressionSpec() => CompressionSpec.Default();

        public void Update() { }

        public void Reset() { }
    }
}
