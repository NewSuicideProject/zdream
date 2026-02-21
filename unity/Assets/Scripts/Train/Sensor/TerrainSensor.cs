using Unity.MLAgents.Sensors;

namespace Train.Sensor {
    public class TerrainSensor : ISensor {
        private readonly int _resolution;
        private readonly ObservationSpec _observationSpec;
        private readonly int _size;
        private readonly Terrain _terrain;

        public TerrainSensor(Terrain terrain) {
            _terrain = terrain;
            _resolution = Config.Terrain.Resolution;
            _size = _resolution * _resolution;
            _observationSpec = ObservationSpec.Vector(_size);
        }

        public string GetName() => "terrain";

        public int Write(ObservationWriter writer) {
            int idx = 0;
            float agentHeight = _terrain.transform.position.y;

            for (int z = 0; z < _resolution; z++) {
                for (int x = 0; x < _resolution; x++) {
                    float relativeHeight = _terrain.HeightMap[x, z] - agentHeight;
                    writer[idx++] = Normalize.Height(relativeHeight);
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
