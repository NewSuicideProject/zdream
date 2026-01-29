using UnityEngine;

namespace Test
{
    public class Rotator : MonoBehaviour
    {
        [SerializeField]
        private float rotationSpeed = 30f;
        
        private void Update()
        {
            transform.Rotate(new Vector3(0f, rotationSpeed, 0f) * Time.deltaTime);
        }
    }
}
