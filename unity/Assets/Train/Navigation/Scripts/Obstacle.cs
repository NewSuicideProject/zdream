using System.Collections.Generic;
using UnityEngine;

namespace Train.Navigation.Scripts {
    public class Obstacle : MonoBehaviour {
        private List<Transform> _waypoints;
        [SerializeField] private float speed = 5f;
        [SerializeField] private Transform mesh;
        [SerializeField] private Transform waypointContainer;

        private int _waypointIndex;
        private bool _beat = true;

        private void Start() {
            _waypoints = new List<Transform>();
            foreach (Transform waypoint in waypointContainer) {
                if (waypoint == null) {
                    continue;
                }

                _waypoints.Add(waypoint);
            }

            float minDst = float.MaxValue;

            int closestIndex = 0;

            for (int i = 0; i < _waypoints.Count; i++) {
                float dst = Vector3.Distance(transform.position, _waypoints[i].position);

                if (!(dst < minDst)) {
                    continue;
                }

                minDst = dst;
                closestIndex = i;
            }

            _waypointIndex = closestIndex;
        }


        private void Update() {
            Transform targetWaypoint = _waypoints[_waypointIndex];

            mesh.position =
                Vector3.MoveTowards(mesh.position, targetWaypoint.position, speed * Time.deltaTime);

            if (Mathf.Approximately(Vector3.Distance(mesh.position, targetWaypoint.position), 0f)) {
                SetNextWaypoint();
            }
        }

        private void SetNextWaypoint() {
            if (_beat) {
                if (_waypointIndex >= _waypoints.Count - 1) {
                    _beat = false;
                    _waypointIndex--;
                } else {
                    _waypointIndex++;
                }
            } else {
                if (_waypointIndex <= 0) {
                    _beat = true;
                    _waypointIndex++;
                } else {
                    _waypointIndex--;
                }
            }
        }

        private void OnDrawGizmos() {
            if (_waypoints == null || _waypoints.Count == 0) {
                return;
            }

            Gizmos.color = Color.blue;

            for (int i = 0; i < _waypoints.Count - 1; i++) {
                if (_waypoints[i] == null) {
                    continue;
                }

                Gizmos.DrawSphere(_waypoints[i].position, 0.05f);

                if (_waypoints[i + 1] != null) {
                    Gizmos.DrawLine(_waypoints[i].position, _waypoints[i + 1].position);
                }
            }

            Gizmos.DrawSphere(_waypoints[^1].position, 0.05f);
        }
    }
}
