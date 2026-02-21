using UnityEngine;

namespace Train.Joint {
    public class ForceReceiver : MonoBehaviour {
        public Vector3 Force { get; private set; }

        private void OnCollisionStay(Collision collision) {
            Vector3 accumulated = Vector3.zero;
            foreach (ContactPoint contact in collision.contacts) {
                accumulated += contact.normal * contact.impulse.magnitude;
            }
            Force += accumulated;
        }

        private void FixedUpdate() => Force = Vector3.zero;
    }
}

