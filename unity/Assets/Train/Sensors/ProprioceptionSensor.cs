using Unity.MLAgents.Sensors;
using UnityEngine;

namespace Train.Sensors {
    public class ProprioceptionSensor : ISensor {
        private readonly Proprioception _prop;
        private readonly ObservationSpec _spec;

        public string GetName() => "proprioception";

        public ProprioceptionSensor(Proprioception prop) {
            _prop = prop;

            int size =
                3 + // gravity
                3 + // CoM diff
                3 + // angular velocity
                3 + // linear velocity
                3 + // position
                3 + // forward
                1 + // integrity
                _prop.Contacts.Length +
                _prop.Attaches.Length +
                _prop.JointBlocks.Length +
                _prop.NormalizedJointBlocks.Length;

            _spec = ObservationSpec.Vector(size);
        }

        public int Write(ObservationWriter writer) {
            writer.Add(_prop.Gravity);

            Vector3 comDiff = _prop.Com - _prop.InitialCoM;
            writer.Add(comDiff);
            writer.Add(_prop.AngularVelocity);
            writer.Add(_prop.LinearVelocity);
            writer.Add(_prop.Position);
            writer.Add(_prop.Forward);

            writer[17] = _prop.Integrity;

            writer.AddList(_prop.Contacts);
            writer.AddList(_prop.Attaches);
            writer.AddList(_prop.NormalizedJointBlocks);


            return _spec.Shape[0];
        }

        public byte[] GetCompressedObservation() => null;
        public CompressionSpec GetCompressionSpec() => CompressionSpec.Default();

        public ObservationSpec GetObservationSpec() => _spec;

        public void Update() { }
        public void Reset() { }
    }
}
