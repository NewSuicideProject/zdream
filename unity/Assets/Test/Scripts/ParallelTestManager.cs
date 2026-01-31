using UnityEngine;

namespace Test.Scripts {
    public class ParallelTestManager : MonoBehaviour {
        [SerializeField] private GameObject testEnvironmentPrefab;
        [SerializeField] private float timeScale = 1f;
        [SerializeField] private int gridWidth = 3;
        [SerializeField] private int gridHeight = 3;
        [SerializeField] private float gridOffset = 75f;

        private Transform _containerTransform;

        private void Awake() {
            Time.timeScale = timeScale;

            _containerTransform = new GameObject("EnvironmentContainer").transform;

            float startX = -(gridWidth - 1) * gridOffset / 2f;
            float startZ = -(gridHeight - 1) * gridOffset / 2f;

            for (int x = 0; x < gridWidth; x++)
            for (int z = 0; z < gridHeight; z++) {
                Vector3 position = new(
                    startX + (x * gridOffset),
                    0f,
                    startZ + (z * gridOffset)
                );

                Instantiate(testEnvironmentPrefab, position, Quaternion.identity, _containerTransform);
            }
        }
    }
}
