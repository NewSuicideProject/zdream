using Unity.MLAgents.Sensors;
using UnityEngine;

namespace Train.Sensor {
    public class NavigationSensor : ISensor {
        private const int _tokenSize = 6;
        // relative position (3), relative direction (2), valid flag (1)

        private readonly int _maxToken;
        private readonly Navigation _navigation;
        private readonly ObservationSpec _observationSpec;

        private readonly int _size;

        public NavigationSensor(Navigation navigation) {
            _navigation = navigation;
            _maxToken = Config.NavigationSensor.MaxToken;
            _size = _maxToken * _tokenSize;
            _observationSpec = ObservationSpec.Vector(_maxToken * _tokenSize);
        }

        public string GetName() => "navigation";

        public int Write(ObservationWriter writer) {
            int idx = 0;

            foreach (Corner corner in _navigation.Corners) {
                writer[idx++] = Normalization.NormalizeDistance(corner.RelativePosition.z);
                writer[idx++] = Normalization.NormalizeDistance(corner.RelativePosition.x);
                writer[idx++] = Normalization.NormalizeDistance(corner.RelativePosition.y);
                writer[idx++] = corner.Direction.x;
                writer[idx++] = corner.Direction.z;
                writer[idx++] = 1f;
            }

            for (int i = Mathf.Min(_navigation.Corners.Count, _maxToken); i < _maxToken; i++) {
                for (int j = 0; j < _tokenSize; j++) {
                    writer[idx++] = 0f;
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
