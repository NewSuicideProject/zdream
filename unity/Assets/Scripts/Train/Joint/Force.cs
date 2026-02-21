using UnityEngine;

namespace Train.Joint {
    public class Force : MonoBehaviour {
        [SerializeField] [ReadOnly] private Vector3 value = Vector3.zero;

        public Vector3 Value {
            get {
                Vector3 temp = value;
                value = Vector3.zero;
                return temp;
            }
        }

        private void OnCollisionStay(Collision collision) {
            Vector3 impulseSum = Vector3.zero;

            foreach (ContactPoint contact in collision.contacts) {
                float normalImpulse = Vector3.Dot(contact.impulse, contact.normal);
                normalImpulse = Mathf.Max(normalImpulse, 0f);
                impulseSum += contact.normal * normalImpulse;
            }

            value += impulseSum / Time.fixedDeltaTime;
        }

        private void OnDrawGizmos() {
            if (!(value.sqrMagnitude > 0.001f)) {
                return;
            }

            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, transform.position + value);
        }
    }
}
