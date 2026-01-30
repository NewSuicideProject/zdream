using UnityEngine;

namespace Test.Scripts
{
    public class TestManager : MonoBehaviour
    {
        [SerializeField] private TestEnvironment testEnvironmentPrefab;
        [SerializeField] private float timeScale = 2f;

        private void Awake()
        {
            Time.timeScale = timeScale;
            Instantiate(testEnvironmentPrefab, Vector3.zero, Quaternion.identity);
        }
    }
}