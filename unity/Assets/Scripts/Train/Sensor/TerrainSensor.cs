using Unity.MLAgents.Sensors;

namespace Train.Sensor {
    public class TerrainSensor : ISensor {
        private readonly ObservationSpec _observationSpec;
        private readonly int _size;
        private readonly Terrain _terrain;

        public TerrainSensor(Terrain terrain) {
            _terrain = terrain;
            _size = Config.Terrain.MaxResolution * Config.Terrain.MaxResolution;
            _observationSpec = ObservationSpec.Vector(_size);
        }

        public string GetName() => "terrain";

        public int Write(ObservationWriter writer) {
            int idx = 0;

            for (int z = 0; z < Config.Terrain.Resolution; z++) {
                for (int x = 0; x < Config.Terrain.Resolution; x++) {
                    writer[idx++] = Normalize.Height(_terrain.HeightMap[x, z]);
                }
            }

            while (idx < _size) {
                writer[idx++] = 0f;
            }

            return _size;
        }

        public byte[] GetCompressedObservation() => null;
        public CompressionSpec GetCompressionSpec() => CompressionSpec.Default();

        public ObservationSpec GetObservationSpec() => _observationSpec;

        public void Update() {
        }

        public void Reset() {
        }
    }
}
