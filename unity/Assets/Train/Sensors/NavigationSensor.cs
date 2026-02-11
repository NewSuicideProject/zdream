using Train.Navigation.Scripts;
using Unity.MLAgents.Sensors;
using UnityEngine;

namespace Train.Sensors {
    public class NavigationSensor : ISensor {
        private readonly Navigator _navigator;
        private readonly Proprioception _proprioception;
        private readonly ObservationSpec _observationSpec;

        private readonly float _expectedMaxDistance;
        private const int _tokenSize = 5;
        private readonly int _maxToken;

        private int Size => _maxToken * _tokenSize;

        public string GetName() => "navigation";

        public NavigationSensor(Navigator navigator, Proprioception proprioception,
            int maxToken = 3, float expectedMaxDistance = 20.0f) {
            _navigator = navigator;
            _proprioception = proprioception;
            _maxToken = maxToken;
            _expectedMaxDistance = expectedMaxDistance;
            _observationSpec = ObservationSpec.VariableLength(maxToken, _tokenSize);
        }

        private float NormalizeDistance(float distance) => Normalization.Tanh(distance, _expectedMaxDistance);

        public int Write(ObservationWriter writer) {
            int idx = 0;

            Vector3 position = _proprioception.Position;
            Vector3 forward = _proprioception.Forward;

            Vector3 projectedForward = Vector3.ProjectOnPlane(forward, Vector3.up).normalized;
            if (Mathf.Approximately(projectedForward.sqrMagnitude, 0f)) {
                projectedForward = forward;
            }

            Quaternion yawQuat = Quaternion.LookRotation(projectedForward, Vector3.up);
            Matrix4x4 inverseMatrix = Matrix4x4.TRS(position, yawQuat, Vector3.one).inverse;

            Vector3[] corners = _navigator.Corners;

            int tokenCount = Mathf.Min(corners.Length, _maxToken);

            for (int i = 0; i < tokenCount; i++) {
                Vector3 tokenPosition = corners[i];
                Vector3 tokenDirection =
                    i + 1 < corners.Length ? (corners[i + 1] - corners[i]).normalized : Vector3.zero;


                Vector3 localPosition = inverseMatrix.MultiplyPoint3x4(tokenPosition);
                Vector3 localDirection = inverseMatrix.MultiplyVector(tokenDirection);

                writer[idx++] = NormalizeDistance(localPosition.z);
                writer[idx++] = NormalizeDistance(localPosition.x);
                writer[idx++] = NormalizeDistance(localPosition.y);
                writer[idx++] = localDirection.x;
                writer[idx++] = localDirection.z;
            }

            for (int i = tokenCount; i < _maxToken; i++) {
                for (int j = 0; j < _tokenSize; j++) {
                    writer[idx++] = 0f;
                }
            }

            return Size;
        }

        public byte[] GetCompressedObservation() => null;
        public ObservationSpec GetObservationSpec() => _observationSpec;

        public CompressionSpec GetCompressionSpec() => CompressionSpec.Default();

        public void Update() { }
        public void Reset() { }
    }
}
