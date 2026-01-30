using UnityEngine;

namespace Test.Scripts
{
    public class ParallelTestManager : MonoBehaviour
    {
        [SerializeField] private GameObject testEnvironmentPrefab;
        [SerializeField] private float timeScale = 1f;
        [SerializeField] private int gridWidth = 3;
        [SerializeField] private int gridHeight = 3;
        [SerializeField] private float gridOffset = 75f;

        private void Awake()
        {
            Time.timeScale = timeScale;

            var startX = -(gridWidth - 1) * gridOffset / 2f;
            var startZ = -(gridHeight - 1) * gridOffset / 2f;

            for (var x = 0; x < gridWidth; x++)
            for (var z = 0; z < gridHeight; z++)
            {
                var position = new Vector3(
                    startX + x * gridOffset,
                    0f,
                    startZ + z * gridOffset
                );

                Instantiate(testEnvironmentPrefab, position, Quaternion.identity);
            }
        }
    }
}