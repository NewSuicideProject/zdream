using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Train.Joint {
    public class ArticulationCalculator : MonoBehaviour {
        public float errorDeg = 5f;
        public float dampingRatio = 1.0f;

        public void Calculate() {
            ArticulationBody rootBody = GetComponent<ArticulationBody>();
            foreach (Transform child in rootBody.transform) {
                Calculate(child, rootBody.mass, rootBody);
            }
        }

        private void Calculate(Transform targetTransform, float accumulatedMass, ArticulationBody parentBody) {
            ArticulationBody body = targetTransform.GetComponent<ArticulationBody>();
            ArticulationBody nextParent = parentBody;

            if (body != null) {
                accumulatedMass += body.mass;

                if (body.jointType != ArticulationJointType.FixedJoint) {
                    float errorRad = errorDeg * Mathf.Deg2Rad;

                    float leverArm = Vector3.Distance(parentBody.transform.position, body.transform.position);
                    leverArm = Mathf.Max(leverArm, 0.01f);

                    float stiffness = accumulatedMass * Physics.gravity.magnitude * leverArm / errorRad;
                    stiffness = Mathf.Clamp(stiffness, 10f, 10000f);

                    float inertia = accumulatedMass * (leverArm * leverArm);
                    float damping = 2f * dampingRatio * Mathf.Sqrt(stiffness * inertia);
                    float forceLimit = accumulatedMass * Physics.gravity.magnitude * 3f;

#if UNITY_EDITOR
                    Undo.RecordObject(body, "ArticulationCalculator: Calculate");
#endif

                    ApplyDriveToBody(body, stiffness, damping, forceLimit);
                }

                nextParent = body;
            }

            foreach (Transform child in targetTransform) {
                Calculate(child, accumulatedMass, nextParent);
            }
        }

        private void ApplyDriveToBody(ArticulationBody body, float stiffness, float damping, float forceLimit) {
            ArticulationDrive xDrive = body.xDrive;
            xDrive.stiffness = stiffness;
            xDrive.damping = damping;
            xDrive.forceLimit = forceLimit;
            body.xDrive = xDrive;

            ArticulationDrive yDrive = body.yDrive;
            yDrive.stiffness = stiffness;
            yDrive.damping = damping;
            yDrive.forceLimit = forceLimit;
            body.yDrive = yDrive;

            ArticulationDrive zDrive = body.zDrive;
            zDrive.stiffness = stiffness;
            zDrive.damping = damping;
            zDrive.forceLimit = forceLimit;
            body.zDrive = zDrive;
        }
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(ArticulationCalculator))]
    public class ArticulationCalculatorEditor : Editor {
        public override void OnInspectorGUI() {
            DrawDefaultInspector();

            ArticulationCalculator calculator = (ArticulationCalculator)target;

            EditorGUILayout.Space(10);

            GUIStyle buttonStyle = new(GUI.skin.button) { fontStyle = FontStyle.Bold };
            if (GUILayout.Button("Calculate", buttonStyle, GUILayout.Height(35))) {
                calculator.Calculate();

                EditorUtility.SetDirty(calculator.gameObject);

                ArticulationBody[] allBodies = calculator.GetComponentsInChildren<ArticulationBody>();
                foreach (ArticulationBody body in allBodies) {
                    EditorUtility.SetDirty(body);
                }
            }
        }
    }
#endif
}
