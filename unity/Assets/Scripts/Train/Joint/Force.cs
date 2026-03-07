using UnityEngine;

namespace Train.Joint {
    public class Force : MonoBehaviour {
        [SerializeField] [ReadOnly] private Vector3 value = Vector3.zero;

        private Collider _collider;

        private Vector3 _buffer = Vector3.zero;

        public Vector3 Value => value;

        private void Awake() => _collider = GetComponentInChildren<Collider>();

        private void FixedUpdate() {
            value = _buffer;
            _buffer = Vector3.zero;
        }

        private void OnCollisionStay(Collision collision) {
            Vector3 impulseSum = Vector3.zero;

            foreach (ContactPoint contact in collision.contacts) {
                if (contact.thisCollider != _collider) {
                    continue;
                }

                float normalImpulse = Vector3.Dot(contact.impulse, contact.normal);
                normalImpulse = Mathf.Max(normalImpulse, 0f);
                impulseSum += contact.normal * normalImpulse;
            }

            _buffer += _collider.transform.InverseTransformDirection(impulseSum / Time.fixedDeltaTime);
        }

        private void OnDrawGizmos() {
            if (!(value.sqrMagnitude > 0.001f)) {
                return;
            }

            Gizmos.color = Color.red;
            Vector3 normalizedValue = Vector3.zero;
            for (int i = 0; i < 3; i++) {
                normalizedValue[i] = Normalize.Force(value[i]);
            }

            Gizmos.DrawLine(
                _collider.transform.position,
                _collider.transform.position + _collider.transform.TransformDirection(normalizedValue)
            );
        }
    }
}
