using System;
using UnityEngine;
using Unity.MLAgents.Sensors; // Required for ISensor and ObservationSpec
using Train.Navigation.Scripts;

namespace Train.Sensors.Navigation {
    public class NavigationSensor : MonoBehaviour, ISensor {
        [Header("Dependencies")] [SerializeField]
        private Navigator navigator;

        [SerializeField] private Proprioception proprioception;

        [Header("Encoder Specs")] [SerializeField]
        private int maxToken = 3;

        [Header("Normalization")] [SerializeField]
        private float expectedMaxCoordinate = 20.0f;

        private const int _inputDim = 5;

        private float[] _navigationBuffer;

        private ObservationSpec _spec;

        public ObservationSpec GetObservationSpec() => _spec;

        private void Awake() {
            _spec = ObservationSpec.VariableLength(maxToken, _inputDim);
            _navigationBuffer = new float[maxToken * _inputDim];
        }

        public string GetName() => "navigation";

        public void Reset() {
        }

        public CompressionSpec GetCompressionSpec() => CompressionSpec.Default();

        public int Write(ObservationWriter writer) {
            UpdateNavigationData();
            writer.AddList(_navigationBuffer);
            return _navigationBuffer.Length;
        }

        public byte[] GetCompressedObservation() => null;
        public void Update() => UpdateNavigationData();

        private float NormalizeDistance(float distance) => (float)Math.Tanh(distance / expectedMaxCoordinate);

        private void UpdateNavigationData() {
            if (!navigator || !proprioception) {
                return;
            }

            Vector3 agentPos = proprioception.Position;
            Vector3 rawForward = proprioception.Forward;

            Vector3 projectedForward = Vector3.ProjectOnPlane(rawForward, Vector3.up).normalized;
            if (projectedForward.sqrMagnitude < 0.001f) {
                projectedForward = rawForward;
            }

            Quaternion stableRotation = Quaternion.LookRotation(projectedForward, Vector3.up);
            Matrix4x4 worldToStable = Matrix4x4.TRS(agentPos, stableRotation, Vector3.one).inverse;

            Vector3[] corners = navigator.Corners;
            int bufferIdx = 0;

            Vector3 lastValidPoint = agentPos;
            Vector3 lastValidDir = projectedForward;

            for (int i = 0; i < maxToken; i++) {
                Vector3 worldPos;
                Vector3 worldDir;

                if (corners == null || i >= corners.Length) {
                    worldPos = lastValidPoint;
                    worldDir = lastValidDir;
                } else {
                    worldPos = corners[i];

                    if (i + 1 < corners.Length) {
                        worldDir = (corners[i + 1] - corners[i]).normalized;
                    } else if (i > 0) {
                        worldDir = (corners[i] - corners[i - 1]).normalized;
                    } else {
                        worldDir = projectedForward;
                    }

                    lastValidPoint = worldPos;
                    lastValidDir = worldDir;
                }

                Vector3 localPos = worldToStable.MultiplyPoint3x4(worldPos);
                Vector3 localDir = worldToStable.MultiplyVector(worldDir);

                _navigationBuffer[bufferIdx++] = NormalizeDistance(localPos.z);
                _navigationBuffer[bufferIdx++] = NormalizeDistance(localPos.x);
                _navigationBuffer[bufferIdx++] = NormalizeDistance(localPos.y);
                _navigationBuffer[bufferIdx++] = localDir.x;
                _navigationBuffer[bufferIdx++] = localDir.z;
            }
        }
    }
}
