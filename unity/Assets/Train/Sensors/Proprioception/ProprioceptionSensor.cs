using Unity.MLAgents.Sensors;
using UnityEngine;

namespace Train.Sensors.Proprioception
{
    public class ProprioceptionSensor : ISensor, MonoBehaviour
    {
        private readonly Proprioception _prop;
        private readonly ObservationSpec _spec;

        public ProprioceptionSensor(Proprioception prop)
        {
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

        public ObservationSpec GetObservationSpec() => _spec;

        public int Write(ObservationWriter writer)
        {
            writer.AddRange(_prop.Gravity);

            Vector3 comDiff = _prop.Com - _prop.InitialCoM;
            writer.AddRange(comDiff);

            writer.AddRange(_prop.AngularVelocity);
            writer.AddRange(_prop.LinearVelocity);
            writer.AddRange(_prop.Position);
            writer.AddRange(_prop.Forward);

            writer.Add(_prop.Integrity);

            writer.AddRange(_prop.Contacts);
            writer.AddRange(_prop.Attaches);

            writer.AddRange(_prop.JointBlocks);
            writer.AddRange(_prop.NormalizedJointBlocks);

            return _spec.Shape[0];
        }

        public byte[] GetCompressedObservation() => null;
        public CompressionSpec GetCompressionSpec() => CompressionSpec.Default();
        public void Update() { }
        public void Reset() { }

        public string GetName() => "proprioception";
    }

    public class ProprioceptionSensorComponent : SensorComponent
    {
        public override ISensor[] CreateSensors()
        {
            var prop = GetComponent<Proprioception>();

            return new ISensor[]
            {
                new ProprioceptionSensor(prop)
            };
        }
    }
}
