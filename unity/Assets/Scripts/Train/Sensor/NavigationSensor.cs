using Unity.MLAgents.Sensors;
using UnityEngine;

namespace Train.Sensor {
    public class NavigationSensor : ISensor {
        private const int _tokenSize = 6;
        // relative position (3), relative direction (2), valid flag (1)

        private readonly Navigation _navigation;
        private readonly ObservationSpec _observationSpec;

        private readonly int _size;

        public NavigationSensor(Navigation navigation) {
            _navigation = navigation;
            _size = Config.Navigation.MaxMaxTokens * _tokenSize;
            _observationSpec = ObservationSpec.Vector(_size);
        }

        public string GetName() => "navigation";

        public int Write(ObservationWriter writer) {
            int idx = 0;

            for (int i = 0; i < Mathf.Min(_navigation.Corners.Count, Config.Navigation.MaxTokens); i++) {
                Corner corner = _navigation.Corners[i];
                writer[idx++] = Normalize.Distance(corner.RelativePosition.z);
                writer[idx++] = Normalize.Distance(corner.RelativePosition.x);
                writer[idx++] = Normalize.Distance(corner.RelativePosition.y);
                writer[idx++] = corner.Direction.x;
                writer[idx++] = corner.Direction.z;
                writer[idx++] = 1f;
            }

            while (idx < _size) {
                writer[idx++] = 0f;
            }

            return _size;
        }

        public byte[] GetCompressedObservation() => null;
        public ObservationSpec GetObservationSpec() => _observationSpec;

        public CompressionSpec GetCompressionSpec() => CompressionSpec.Default();

        public void Update() {
        }

        public void Reset() {
        }
    }
}
