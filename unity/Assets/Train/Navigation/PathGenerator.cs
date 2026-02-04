using UnityEngine;
using UnityEngine.AI;

namespace Train.Navigation
{
    public class PathGenerator : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private Transform targetTransform;
        [SerializeField] private float updateInterval = 0.1f;

        [Header("Output Data")]
        public Vector3 FinalTargetPosition { get; private set; }
        public Vector3[] WorldPathArray { get; private set; }

        private NavMeshPath _navPath;
        private float _timer;

        private void Awake() => _navPath = new NavMeshPath();

        private void Update()
        {
            if (targetTransform == null)
            {
                return;
            }

            _timer += Time.deltaTime;
            if (_timer >= updateInterval)
            {
                UpdatePathData();
                LogPathData();
                _timer = 0f;
            }
        }

        public void UpdatePathData()
        {
            if (NavMesh.CalculatePath(transform.position, targetTransform.position, NavMesh.AllAreas, _navPath))
            {
                FinalTargetPosition = targetTransform.position;
                WorldPathArray = _navPath.corners;
                DrawPathDebug();
            }
            else
            {
                WorldPathArray = System.Array.Empty<Vector3>();
            }
        }

        private void DrawPathDebug()
        {
            if (WorldPathArray == null || WorldPathArray.Length < 2)
            {
                return;
            }

            for (int i = 0; i < WorldPathArray.Length - 1; i++)
            {
                Debug.DrawLine(WorldPathArray[i], WorldPathArray[i + 1], Color.green, updateInterval);
            }
        }

        public void LogPathData()
        {
            if (WorldPathArray == null || WorldPathArray.Length == 0)
            {
                Debug.Log("생성된 경로가 없습니다.");
                return;
            }

            Debug.Log($"[Target] Final Position: {FinalTargetPosition}");

            // 2. 경로 배열(World Position) 전체 출력
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine($"[Path] Waypoints Count: {WorldPathArray.Length}");

            for (int i = 0; i < WorldPathArray.Length; i++)
            {
                sb.AppendLine($"  - Point [{i}]: {WorldPathArray[i].ToString("F2")}");
            }

            Debug.Log(sb.ToString());
        }
    }
}
