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
            _observationSpec = ObservationSpec.VariableLength(_maxToken, _tokenSize);
        }

        public string GetName() => "navigation";

        public int Write(ObservationWriter writer) {
            int idx = 0;

            Vector3 position = _navigation.transform.position;
            Vector3 forward = _navigation.transform.forward;

            Vector3 projectedForward = Vector3.ProjectOnPlane(forward, Vector3.up);

            if (projectedForward.sqrMagnitude < 0.001f) {
                projectedForward = Vector3.forward;
            } else {
                projectedForward.Normalize();
            }

            Quaternion yawQuat = Quaternion.LookRotation(projectedForward, Vector3.up);
            Matrix4x4 inverseMatrix = Matrix4x4.TRS(position, yawQuat, Vector3.one).inverse;

            Vector3[] corners = _navigation.Corners;

            int tokenCount = Mathf.Min(corners.Length, _maxToken);

            for (int i = 0; i < tokenCount; i++) {
                Vector3 tokenPosition = corners[i];
                Vector3 tokenDirection =
                    i + 1 < corners.Length ? (corners[i + 1] - corners[i]).normalized : Vector3.zero;


                Vector3 localPosition = inverseMatrix.MultiplyPoint3x4(tokenPosition);
                Vector3 localDirection = inverseMatrix.MultiplyVector(tokenDirection);

                writer[idx++] = Normalization.NormalizeDistance(localPosition.z);
                writer[idx++] = Normalization.NormalizeDistance(localPosition.x);
                writer[idx++] = Normalization.NormalizeDistance(localPosition.y);
                writer[idx++] = localDirection.x;
                writer[idx++] = localDirection.z;
                writer[idx++] = 1f;
            }

            for (int i = tokenCount; i < _maxToken; i++) {
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
