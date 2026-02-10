using System;
using UnityEngine;
using Unity.MLAgents.Sensors;
using Train.Navigation.Scripts;

namespace Train.Sensors.Navigation {
    public class NavigationSensor : ISensor {
        private readonly Navigator _navigator;
        private readonly Proprioception _proprioception;
        private readonly int _maxToken;
        private readonly float _expectedMaxCoordinate;

        private const int _inputDim = 5;

        private float[] _navigationBuffer;

        private readonly ObservationSpec _spec;

        public ObservationSpec GetObservationSpec() => _spec;

        public NavigationSensor(Navigator navigator, Proprioception proprioception,
            int maxToken = 3, float expectedMaxCoordinate = 20.0f) {
            _navigator = navigator;
            _proprioception = proprioception;
            _maxToken = maxToken;
            _expectedMaxCoordinate = expectedMaxCoordinate;
            _spec = ObservationSpec.VariableLength(maxToken, _inputDim);
        }

        public string GetName() => "navigation";

        public void Update() {
        }

        public void Reset() {
        }

        public CompressionSpec GetCompressionSpec() => CompressionSpec.Default();
        private float NormalizeDistance(float distance) => (float)Math.Tanh(distance / _expectedMaxCoordinate);
        public byte[] GetCompressedObservation() => null;


        public int Write(ObservationWriter writer) {
            int idx = 0;

            if (!_navigator || !_proprioception) {
                for (int i = 0; i < _maxToken * _inputDim; i++) {
                    writer[idx++] = 0.0f;
                }

                return idx;
            }

            Vector3 agentPos = _proprioception.Position;
            Vector3 rawForward = _proprioception.Forward;

            Vector3 projectedForward = Vector3.ProjectOnPlane(rawForward, Vector3.up).normalized;
            if (projectedForward.sqrMagnitude < 0.001f) {
                projectedForward = rawForward;
            }

            Quaternion stableRotation = Quaternion.LookRotation(projectedForward, Vector3.up);
            Matrix4x4 worldToStable = Matrix4x4.TRS(agentPos, stableRotation, Vector3.one).inverse;

            Vector3[] corners = _navigator.Corners;

            Vector3 lastValidPoint = agentPos;
            Vector3 lastValidDir = projectedForward;

            for (int i = 0; i < _maxToken; i++) {
                Vector3 worldPos;
                Vector3 worldDir;

                if (corners == null || i >= corners.Length) {
                    worldPos = lastValidPoint;
                    worldDir = lastValidDir;
                } else {
                    worldPos = corners[i];

                    worldDir = i + 1 < corners.Length ? (corners[i + 1] - corners[i]).normalized : Vector3.zero;

                    lastValidPoint = worldPos;
                    lastValidDir = worldDir;
                }

                Vector3 localPos = worldToStable.MultiplyPoint3x4(worldPos);
                Vector3 localDir = worldToStable.MultiplyVector(worldDir);

                writer[idx++] = NormalizeDistance(localPos.z);
                writer[idx++] = NormalizeDistance(localPos.x);
                writer[idx++] = NormalizeDistance(localPos.y);
                writer[idx++] = localDir.x;
                writer[idx++] = localDir.z;
            }

            return idx;
        }
    }
}
