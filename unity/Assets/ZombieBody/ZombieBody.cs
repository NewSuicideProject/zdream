using System.Collections.Generic;
using UnityEngine;

namespace ZombieBody {
    public class ZombieBody : MonoBehaviour {
        [SerializeField] private float expectedMaxSpeed = 10f;

        private ArticulationBody _pelvis;
        private readonly List<ArticulationBody> _articulationBodies = new();

        private struct JointLimitCache {
            public float LowerLimit;
            public float UpperLimit;
        }

        private readonly List<JointLimitCache[]> _jointLimitCaches = new();

        private void Awake() {
            _articulationBodies.Clear();
            _jointLimitCaches.Clear();

            ArticulationBody[] articulationBodies = GetComponentsInChildren<ArticulationBody>();

            _pelvis = articulationBodies[0];

            string log = "Zombie Body Structure:\n";
            log += $"{_pelvis.name} {_pelvis.dofCount}DoF\n";
            for (int i = 1; i < articulationBodies.Length; i++) {
                ArticulationBody body = articulationBodies[i];
                _articulationBodies.Add(body);
                log += $"{body.name} {body.dofCount}DoF\n";

                if (body.dofCount <= 0) {
                    continue;
                }

                JointLimitCache[] limitCache = new JointLimitCache[body.dofCount];

                for (int j = 0; j < body.dofCount; j++) {
                    ArticulationDrive drive = GetDriveForAxis(body, j);
                    limitCache[j] = new JointLimitCache {
                        LowerLimit = drive.lowerLimit * Mathf.Deg2Rad, UpperLimit = drive.upperLimit * Mathf.Deg2Rad
                    };
                }

                _jointLimitCaches.Add(limitCache);
            }

            Debug.Log(log);
        }

        public float[][] GetJointAngleArray() {
            List<float[]> result = new();

            foreach (ArticulationBody body in _articulationBodies) {
                float[] currentAngles = new float[body.dofCount];

                for (int i = 0; i < body.dofCount; i++) {
                    currentAngles[i] = body.jointPosition[i];
                }

                result.Add(currentAngles);
            }

            return result.ToArray();
        }

        public float[][] GetJointAngularVelocityArray() {
            List<float[]> result = new();

            foreach (ArticulationBody body in _articulationBodies) {
                float[] currentVelocities = new float[body.dofCount];

                for (int i = 0; i < body.dofCount; i++) {
                    currentVelocities[i] = body.jointVelocity[i];
                }

                result.Add(currentVelocities);
            }

            return result.ToArray();
        }

        private static ArticulationDrive GetDriveForAxis(ArticulationBody body, int axisIndex) =>
            axisIndex switch {
                0 => body.xDrive,
                1 => body.yDrive,
                _ => body.zDrive
            };

        public float[][] GetNormJointAngleArray() {
            List<float[]> result = new();
            int arrayIndex = 0;

            foreach (ArticulationBody body in _articulationBodies) {
                float[] normalizedAngles = new float[body.dofCount];
                JointLimitCache[] limitCache = _jointLimitCaches[arrayIndex++];

                for (int i = 0; i < body.dofCount; i++) {
                    normalizedAngles[i] = Normalization.LinearMinMax(
                        body.jointPosition[i],
                        limitCache[i].LowerLimit,
                        limitCache[i].UpperLimit
                    );
                }

                result.Add(normalizedAngles);
            }

            return result.ToArray();
        }

        private float NormalizeSpeed(float speed) => Normalization.Tanh(speed, expectedMaxSpeed);

        public float[][] GetNormJointAngularVelocityArray() {
            List<float[]> result = new();

            foreach (ArticulationBody body in _articulationBodies) {
                float[] normalizedVelocities = new float[body.dofCount];

                for (int i = 0; i < body.dofCount; i++) {
                    normalizedVelocities[i] = NormalizeSpeed(body.jointVelocity[i]);
                }

                result.Add(normalizedVelocities);
            }

            return result.ToArray();
        }
    }
}
