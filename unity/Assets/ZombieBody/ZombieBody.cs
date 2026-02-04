using System.Collections.Generic;
using UnityEngine;

namespace ZombieBody {
    public class ZombieBody : MonoBehaviour {
        [SerializeField] private float expectedMaxSpeed = 10f;

        private ArticulationBody _pelvis;
        private readonly List<ArticulationBody> _articulationBodies = new();

        private void Awake() {
            _articulationBodies.Clear();

            ArticulationBody[] articulationBodies = GetComponentsInChildren<ArticulationBody>();

            _pelvis = articulationBodies[0];

            string log = "Zombie Body Structure:\n";
            log += $"{_pelvis.name} {_pelvis.dofCount}DoF\n";
            for (int i = 1; i < articulationBodies.Length; i++) {
                _articulationBodies.Add(articulationBodies[i]);
                log += $"{articulationBodies[i].name} {articulationBodies[i].dofCount}DoF\n";
            }

            Debug.Log(log);
        }

        public float[][] GetJointAngleArray() {
            List<float[]> result = new();

            foreach (ArticulationBody body in _articulationBodies) {
                if (body.dofCount == 0) {
                    continue;
                }

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
                if (body.dofCount == 0) {
                    continue;
                }

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

            foreach (ArticulationBody body in _articulationBodies) {
                if (body.dofCount == 0) {
                    continue;
                }

                ArticulationDrive[] drives = new ArticulationDrive[body.dofCount];
                float[] currentAngles = new float[body.dofCount];

                for (int i = 0; i < body.dofCount; i++) {
                    currentAngles[i] = body.jointPosition[i];

                    drives[i] = GetDriveForAxis(body, i);
                }

                float[] normalizedAngles = new float[body.dofCount];

                for (int i = 0; i < body.dofCount; i++) {
                    if (drives[i].lowerLimit >= drives[i].upperLimit) {
                        Debug.LogError($"Joint {body.name} drive {i} is not Limited!");
                        return null;
                    }

                    normalizedAngles[i] = Normalization.LinearMinMax(
                        currentAngles[i],
                        drives[i].lowerLimit * Mathf.Deg2Rad,
                        drives[i].upperLimit * Mathf.Deg2Rad
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
                if (body.dofCount == 0) {
                    continue;
                }

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
