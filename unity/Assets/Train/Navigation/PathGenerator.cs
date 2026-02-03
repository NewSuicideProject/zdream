using UnityEngine;
using UnityEngine.AI;

namespace Train.Navigation
{
    /// <summary>
    /// AI Navigation 패키지를 사용하여 장애물을 회피하는 경로를 생성합니다.
    /// </summary>
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

            // 1. 최종 목표 지점 출력
            Debug.Log($"[Target] Final Position: {FinalTargetPosition}");

            // 2. 경로 배열(World Position) 전체 출력
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine($"[Path] Waypoints Count: {WorldPathArray.Length}");

            for (int i = 0; i < WorldPathArray.Length; i++)
            {
                // 각 지점의 인덱스와 좌표(X, Y, Z)를 포맷팅
                sb.AppendLine($"  - Point [{i}]: {WorldPathArray[i].ToString("F2")}");
            }

            Debug.Log(sb.ToString());
        }
    }
}
