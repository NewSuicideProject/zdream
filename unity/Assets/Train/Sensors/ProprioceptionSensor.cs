using Unity.MLAgents.Sensors;
using UnityEngine;

namespace Train.Sensors {
    public class ProprioceptionSensor : ISensor {
        private readonly Proprioception _proprioception;
        private readonly ObservationSpec _observationSpec;

        public string GetName() => "proprioception";

        public ProprioceptionSensor(Proprioception proprioception) {
            _proprioception = proprioception;

            int size =
                3 + // gravity
                3 + // CoM diff
                3 + // angular velocity
                3 + // linear velocity
                3 + // position
                3 + // forward
                1 + // integrity
                _proprioception.Contacts.Length +
                _proprioception.Attaches.Length +
                _proprioception.JointBlocks.Length +
                _proprioception.NormalizedJointBlocks.Length;

            _observationSpec = ObservationSpec.Vector(size);
        }

        public int Write(ObservationWriter writer) {
            writer.Add(_proprioception.Gravity);

            Vector3 comDiff = _proprioception.Com - _proprioception.InitialCoM;
            writer.Add(comDiff);
            writer.Add(_proprioception.AngularVelocity);
            writer.Add(_proprioception.LinearVelocity);
            writer.Add(_proprioception.Position);
            writer.Add(_proprioception.Forward);

            writer[17] = _proprioception.Integrity;

            writer.AddList(_proprioception.Contacts);
            writer.AddList(_proprioception.Attaches);
            writer.AddList(_proprioception.NormalizedJointBlocks);


            return _observationSpec.Shape[0];
        }

        public byte[] GetCompressedObservation() => null;
        public CompressionSpec GetCompressionSpec() => CompressionSpec.Default();

        public ObservationSpec GetObservationSpec() => _observationSpec;

        public void Update() { }
        public void Reset() { }
    }
}
