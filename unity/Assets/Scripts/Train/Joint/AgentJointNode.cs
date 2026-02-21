using System;
using Joint;
using UnityEngine;

namespace Train.Joint {
    public class AgentJointNode : JointNodeBase {
        private readonly Collider _collider;

        private readonly ArticulationReducedSpace _zeroSpace;
        public readonly ArticulationBody Body;
        public readonly Force Force;
        public readonly int DoF;

        public AgentJointNode(GameObject gameObject, AgentJointNode parent) : base(gameObject, parent) {
            Body = gameObject.GetComponent<ArticulationBody>();
            _collider = gameObject.GetComponentInChildren<Collider>();
            Force = gameObject.AddComponent<Force>();

            DoF = Body.dofCount;
            _zeroSpace = DoF switch {
                0 => new ArticulationReducedSpace(),
                1 => new ArticulationReducedSpace(0f),
                2 => new ArticulationReducedSpace(0f, 0f),
                3 => new ArticulationReducedSpace(0f, 0f, 0f),
                _ => throw new ArgumentOutOfRangeException(nameof(DoF), $"Unsupported DoF count {DoF}")
            };
        }

        public override void Sever() {
            if (IsSevered) {
                return;
            }

            IsSevered = true;
            _collider.enabled = false;
            Body.enabled = false;
            GameObject.transform.localScale = Vector3.zero;


            foreach (JointNodeBase child in Children) {
                child.Sever();
            }
        }

        private void ResetBody() {
            Body.jointPosition = _zeroSpace;
            Body.jointForce = _zeroSpace;
            Body.jointVelocity = _zeroSpace;
            Body.angularVelocity = Vector3.zero;
            Body.linearVelocity = Vector3.zero;
        }

        public override void Reset() {
            if (!IsSevered) {
                ResetBody();
            }

            base.Reset();
        }

        public override void Join() {
            if (!IsSevered) {
                return;
            }

            IsSevered = false;
            GameObject.transform.localScale = Vector3.one;
            Body.enabled = true;
            ResetBody();
            _collider.enabled = true;

            foreach (JointNodeBase child in Children) {
                child.Join();
            }
        }

        public ArticulationDrive GetDrive(int axisIndex) =>
            axisIndex switch {
                0 => Body.xDrive,
                1 => Body.yDrive,
                2 => Body.zDrive,
                _ => throw new ArgumentOutOfRangeException(nameof(axisIndex),
                    $"Invalid axis index {axisIndex}")
            };

        public void SetDrive(int axisIndex, ArticulationDrive drive) {
            switch (axisIndex) {
                case 0: Body.xDrive = drive; break;
                case 1: Body.yDrive = drive; break;
                case 2: Body.zDrive = drive; break;
                default: throw new ArgumentOutOfRangeException(nameof(axisIndex), $"Invalid axis index {axisIndex}");
            }
        }
    }
}
