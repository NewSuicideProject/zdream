using System.Collections.Generic;
using UnityEngine;

namespace AgentBody.Scripts {
    public struct JointBlock {
        public float[] Angles;
        public float[] AngularVelocities;
        public float IsSevered;
    }


    public class Proprioception : MonoBehaviour {
        [SerializeField] private float expectedMaxSpeed = 10f;
        private ZombieBody _zombieBody;

        private void Awake() {
            _zombieBody = GetComponent<ZombieBody>();
            if (!_zombieBody) {
                Debug.LogError("ZombieBody component not found!");
            }
        }

        private static float[] GetJointAngles(ArticulationBody body) {
            float[] angles = new float[body.dofCount];

            for (int i = 0; i < body.dofCount; i++) {
                angles[i] = body.jointPosition[i];
            }

            return angles;
        }

        private static float[] GetJointAngularVelocities(ArticulationBody body) {
            float[] velocities = new float[body.dofCount];

            for (int i = 0; i < body.dofCount; i++) {
                velocities[i] = body.jointVelocity[i];
            }

            return velocities;
        }

        private float NormalizeSpeed(float speed) => Normalization.Tanh(speed, expectedMaxSpeed);

        private static float[] GetNormJointAngles(ArticulationBody body, JointLimitCache[] limitCache) {
            float[] normalizedAngles = new float[body.dofCount];

            for (int i = 0; i < body.dofCount; i++) {
                normalizedAngles[i] = Normalization.LinearMinMax(
                    body.jointPosition[i],
                    limitCache[i].LowerLimit,
                    limitCache[i].UpperLimit
                );
            }

            return normalizedAngles;
        }

        private float[] GetNormJointAngularVelocities(ArticulationBody body) {
            float[] normalizedVelocities = new float[body.dofCount];

            for (int i = 0; i < body.dofCount; i++) {
                normalizedVelocities[i] = NormalizeSpeed(body.jointVelocity[i]);
            }

            return normalizedVelocities;
        }


        public JointBlock[] GetJointBlocks() {
            List<JointBlock> result = new();

            if (_zombieBody?.RootJointConfig != null) {
                CollectJointBlocks(_zombieBody.RootJointConfig, result);
            }

            return result.ToArray();
        }

        private void CollectJointBlocks(JointConfig config, List<JointBlock> result) {
            JointBlock block = new() {
                Angles = GetJointAngles(config.ArticulationBody),
                AngularVelocities = GetJointAngularVelocities(config.ArticulationBody),
                IsSevered = config.IsSevered ? 1.0f : 0.0f
            };
            result.Add(block);

            foreach (JointConfig child in config.Children) {
                CollectJointBlocks(child, result);
            }
        }

        public JointBlock[] GetNormJointBlocks() {
            List<JointBlock> result = new();

            if (_zombieBody?.RootJointConfig != null) {
                CollectNormJointBlocks(_zombieBody.RootJointConfig, result);
            }

            return result.ToArray();
        }

        private void CollectNormJointBlocks(JointConfig config, List<JointBlock> result) {
            JointBlock block = new() {
                Angles = GetNormJointAngles(config.ArticulationBody, config.JointLimitCaches),
                AngularVelocities = GetNormJointAngularVelocities(config.ArticulationBody),
                IsSevered = config.IsSevered ? 1.0f : 0.0f
            };
            result.Add(block);

            foreach (JointConfig child in config.Children) {
                CollectNormJointBlocks(child, result);
            }
        }

        public Vector3 GetCoMDiff() =>
            // TODO
            Vector3.zero;

        public Vector3 GetGravity() =>
            _zombieBody.RootJointConfig.ArticulationBody.transform.InverseTransformDirection(Physics.gravity)
                .normalized;

        public Vector3 GetStraightGravity() =>
            Quaternion.Inverse(_zombieBody.RootStraightQuat) * Physics.gravity.normalized;

        public Vector3 GetAngularVelocity() =>
            _zombieBody.RootJointConfig.ArticulationBody.angularVelocity;

        public Vector3 GetLinearVelocity() =>
            _zombieBody.RootJointConfig.ArticulationBody.linearVelocity;

        public Vector3 GetPosition() =>
            _zombieBody.RootJointConfig.ArticulationBody.transform.position;

        public float GetIntegrity() =>
            // TODO
            1.0f;

        public Vector3 GetForward() =>
            _zombieBody.RootJointConfig.ArticulationBody.transform.forward;

        public float[] GetContacts() =>
            new float[4];

        public float[] GetAttaches() =>
            new float[4];


        private void OnDrawGizmos() {
            if (!_zombieBody.RootJointConfig.ArticulationBody) {
                return;
            }

            Vector3 pelvisPosition = _zombieBody.RootJointConfig.ArticulationBody.transform.position;

            Vector3 gravityVector = GetGravity();
            Gizmos.color = Color.red;
            Gizmos.DrawRay(pelvisPosition, gravityVector);

            Vector3 straightGravityVector = GetStraightGravity();
            Gizmos.color = Color.green;
            Gizmos.DrawRay(pelvisPosition, straightGravityVector);
        }
    }
}
