using System;
using UnityEngine;

namespace Train.Joint {
    public class Force : MonoBehaviour {
        [SerializeField] [ReadOnly] private Vector3 value = Vector3.zero;
        public Vector3 Value => value;

        private void OnCollisionStay(Collision collision) {
            Vector3 accumulated = Vector3.zero;

            foreach (ContactPoint contact in collision.contacts) {
                accumulated += contact.normal * contact.impulse.magnitude;
            }

            value += accumulated;
        }

        private void FixedUpdate() => value = Vector3.zero;

        private void OnDrawGizmos() {
            if (!(value.sqrMagnitude > 0.001f)) {
                return;
            }

            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, transform.position + value);
        }
    }
}
