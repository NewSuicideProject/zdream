using Unity.MLAgents.Sensors;

namespace Train.Sensor {
    public class TerrainSensor : ISensor {
        private readonly int _gridSize;
        private readonly ObservationSpec _observationSpec;
        private readonly int _size;
        private readonly Terrain _terrain;

        public TerrainSensor(Terrain terrain, int gridSize) {
            _terrain = terrain;
            _gridSize = gridSize;
            _size = _gridSize * _gridSize;
            _observationSpec = ObservationSpec.Vector(_size);
        }

        public string GetName() => "terrain";

        public int Write(ObservationWriter writer) {
            int idx = 0;
            float agentHeight = _terrain.transform.position.y;

            for (int z = 0; z < _gridSize; z++) {
                for (int x = 0; x < _gridSize; x++) {
                    float relativeHeight = _terrain.HeightMap[x, z] - agentHeight;
                    writer[idx++] = Normalization.NormalizeHeight(relativeHeight);
                }
            }

            return _size;
        }

        public byte[] GetCompressedObservation() => null;
        public CompressionSpec GetCompressionSpec() => CompressionSpec.Default();

        public ObservationSpec GetObservationSpec() => _observationSpec;

        public void Update() { }
        public void Reset() { }
    }
}
