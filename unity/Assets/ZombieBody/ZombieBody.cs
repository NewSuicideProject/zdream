using System.Collections.Generic;
using UnityEngine;

namespace ZombieBody {
    public struct JointBlock {
        public float[] Angles;
        public float[] AngularVelocities;
        public float IsSevered;
    }

    public class ZombieBody : MonoBehaviour {
        [SerializeField] private float expectedMaxSpeed = 10f;
        [SerializeField] private List<Collider> contacts = new();
        [SerializeField] private float contactRange = 0.01f;

        private ArticulationBody _pelvis;
        private float[] _contactsCache;
        private Collider[] _overlapBuffer;
        private Quaternion _pelvisInitialRotation;
        private readonly List<ArticulationBody> _articulationBodies = new();

        private struct JointLimitCache {
            public float LowerLimit;
            public float UpperLimit;
        }

        private readonly List<JointLimitCache[]> _jointLimitCaches = new();

        private void Awake() {
            _articulationBodies.Clear();
            _jointLimitCaches.Clear();

            _contactsCache = new float[contacts.Count];
            _overlapBuffer = new Collider[10];

            ArticulationBody[] articulationBodies = GetComponentsInChildren<ArticulationBody>();

            _pelvis = articulationBodies[0];
            _pelvisInitialRotation = _pelvis.transform.rotation;

            string log = "Zombie Body Structure:\n";
            log += $"{_pelvis.name} {_pelvis.dofCount}DoF\n";
            for (int i = 1; i < articulationBodies.Length; i++) {
                ArticulationBody body = articulationBodies[i];

                if (body.dofCount <= 0) {
                    continue;
                }

                _articulationBodies.Add(body);
                log += $"{body.name} {body.dofCount}DoF\n";

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

        private void FixedUpdate() => UpdateContacts();

        private void UpdateContacts() {
            for (int i = 0; i < contacts.Count; i++) {
                Collider contactCollider = contacts[i];

                int overlapCount = Physics.OverlapSphereNonAlloc(
                    contactCollider.bounds.center,
                    contactRange,
                    _overlapBuffer
                );

                bool hasContact = false;
                ArticulationBody selfBody = contactCollider.GetComponentInParent<ArticulationBody>();

                for (int j = 0; j < overlapCount; j++) {
                    Collider overlap = _overlapBuffer[j];

                    if (overlap == contactCollider) {
                        continue;
                    }

                    ArticulationBody otherBody = overlap.GetComponentInParent<ArticulationBody>();
                    if (otherBody != null && otherBody == selfBody) {
                        continue;
                    }

                    hasContact = true;
                    break;
                }

                _contactsCache[i] = hasContact ? 1f : 0f;
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

        private static ArticulationDrive GetDriveForAxis(ArticulationBody body, int axisIndex) =>
            axisIndex switch {
                0 => body.xDrive,
                1 => body.yDrive,
                2 => body.zDrive,
                _ => throw new System.ArgumentOutOfRangeException(nameof(axisIndex), $"Invalid axis index {axisIndex}")
            };

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

            foreach (ArticulationBody body in _articulationBodies) {
                JointBlock block = new() {
                    Angles = GetJointAngles(body),
                    AngularVelocities = GetJointAngularVelocities(body),
                    IsSevered = 0.0f // TODO
                };
                result.Add(block);
            }

            return result.ToArray();
        }

        public JointBlock[] GetNormJointBlocks() {
            List<JointBlock> result = new();
            int arrayIndex = 0;

            foreach (ArticulationBody body in _articulationBodies) {
                JointLimitCache[] limitCache = _jointLimitCaches[arrayIndex++];
                JointBlock block = new() {
                    Angles = GetNormJointAngles(body, limitCache),
                    AngularVelocities = GetNormJointAngularVelocities(body),
                    IsSevered = 0.0f // TODO
                };
                result.Add(block);
            }

            return result.ToArray();
        }

        public Vector3 GetCoMDiff() =>
            // TODO
            Vector3.zero;

        public Vector3 GetGravity() =>
            _pelvis.transform.InverseTransformDirection(Physics.gravity).normalized;

        public Vector3 GetStraightGravity() =>
            Quaternion.Inverse(_pelvisInitialRotation) * Physics.gravity.normalized;

        public Vector3 GetAngularVelocity() =>
            _pelvis.angularVelocity;

        public Vector3 GetLinearVelocity() =>
            _pelvis.linearVelocity;

        public Vector3 GetPosition() =>
            _pelvis.transform.position;

        public float GetIntegrity() =>
            // TODO
            1.0f;

        public Vector3 GetForward() =>
            _pelvis.transform.forward;

        public float[] GetContacts() =>
            _contactsCache;

        public float[] GetAttaches() =>
            new float[contacts.Count];

        private void OnDrawGizmos() {
            if (!_pelvis) {
                return;
            }

            Vector3 pelvisPosition = _pelvis.transform.position;

            Vector3 gravityVector = GetGravity();
            Gizmos.color = Color.red;
            Gizmos.DrawRay(pelvisPosition, gravityVector);

            Vector3 straightGravityVector = GetStraightGravity();
            Gizmos.color = Color.green;
            Gizmos.DrawRay(pelvisPosition, straightGravityVector);


            for (int i = 0; i < contacts.Count; i++) {
                Collider contactCollider = contacts[i];
                Gizmos.color = _contactsCache[i] > 0.5f ? Color.green : Color.red;
                Gizmos.DrawSphere(contactCollider.bounds.center, 0.025f);
            }
        }
    }
}
