using UnityEngine;

namespace Train.AgentBody.Scripts {
    [RequireComponent(typeof(AgentBody))]
    public class Proprioception : MonoBehaviour {
        private AgentBody _agentBody;

        private void Awake() => _agentBody = GetComponent<AgentBody>();

        public float[] GetJointBlocks(bool normalize = false) {
            float[] jointBlocks = new float[(_agentBody.DoFCount * 2) + _agentBody.Configs.Count];
            int index = 0;

            foreach (JointConfig config in _agentBody.Configs) {
                if (config.IsSevered) {
                    for (int i = 0; i < config.Body.dofCount; i++) {
                        jointBlocks[index++] = 0.0f;
                    }

                    for (int i = 0; i < config.Body.dofCount; i++) {
                        jointBlocks[index++] = 0.0f;
                    }

                    jointBlocks[index++] = 1.0f;
                } else {
                    float[] jointPositions = config.GetJointPositions(normalize);
                    foreach (float position in jointPositions) {
                        jointBlocks[index++] = position;
                    }

                    float[] jointVelocities = config.GetJointVelocities(normalize);
                    foreach (float velocity in jointVelocities) {
                        jointBlocks[index++] = velocity;
                    }

                    jointBlocks[index++] = 0.0f;
                }
            }

            return jointBlocks;
        }

        public Vector3 GetCoMDiff() =>
            // TODO
            Vector3.zero;

        public Vector3 GetGravity() =>
            _agentBody.RootJointConfig.Body.transform.InverseTransformDirection(Physics.gravity)
                .normalized;

        public Vector3 GetStraightGravity() =>
            Quaternion.Inverse(_agentBody.RootStraightQuat) * Physics.gravity.normalized;

        public Vector3 GetAngularVelocity() =>
            _agentBody.RootJointConfig.Body.angularVelocity;

        public Vector3 GetLinearVelocity() =>
            _agentBody.RootJointConfig.Body.linearVelocity;

        public Vector3 GetPosition() =>
            _agentBody.RootJointConfig.Body.transform.position;

        public float GetIntegrity() =>
            // TODO
            1.0f;

        public Vector3 GetForward() =>
            _agentBody.RootJointConfig.Body.transform.forward;

        public float[] GetContacts() =>
            new float[4];

        public float[] GetAttaches() =>
            new float[4];


        private void OnDrawGizmos() {
            if (!_agentBody.RootJointConfig.Body) {
                return;
            }

            Vector3 pelvisPosition = _agentBody.RootJointConfig.Body.transform.position;

            Vector3 gravityVector = GetGravity();
            Gizmos.color = Color.red;
            Gizmos.DrawRay(pelvisPosition, gravityVector);

            Vector3 straightGravityVector = GetStraightGravity();
            Gizmos.color = Color.green;
            Gizmos.DrawRay(pelvisPosition, straightGravityVector);
        }
    }
}
