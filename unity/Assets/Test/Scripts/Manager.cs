using UnityEngine;

namespace Test.Scripts {
    public class Manager : MonoBehaviour {
        [SerializeField] private GameObject environmentPrefab;
        [SerializeField] private float timeScale = 2f;

        private void Awake() {
            Time.timeScale = timeScale;
            Instantiate(environmentPrefab, Vector3.zero, Quaternion.identity);
        }
    }
}
