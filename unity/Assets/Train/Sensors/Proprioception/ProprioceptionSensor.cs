using Unity.MLAgents.Sensors;
using UnityEngine;
using Train;

namespace Train.Sensors.Proprioception
{
    public class ProprioceptionSensor : ISensor
    {
        private readonly Train.Proprioception _prop;
        private ObservationSpec _spec;


        public ProprioceptionSensor(Train.Proprioception prop)
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



        public ObservationSpec GetObservationSpec()
        {
            return _spec;
        }

        public int Write(ObservationWriter writer)
        {

            int offset = 0;
            writer.Add(_prop.Gravity, offset);
            offset += 3;

            Vector3 comDiff = _prop.Com - _prop.InitialCoM;
            writer.Add(comDiff, offset);
            offset += 3;

            writer.Add(_prop.AngularVelocity, offset);
            offset += 3;
            writer.Add(_prop.LinearVelocity, offset);
            offset += 3;
            writer.Add(_prop.Position, offset);
            offset += 3;
            writer.Add(_prop.Forward, offset);
            offset += 3;

            writer[offset] = _prop.Integrity;
            offset += 1;

            offset += WriteList(writer, _prop.Contacts, offset);
            offset += WriteList(writer, _prop.Attaches, offset);

            offset += WriteList(writer, _prop.JointBlocks, offset);
            offset += WriteList(writer, _prop.NormalizedJointBlocks, offset);

            return offset;
        }

        public byte[] GetCompressedObservation() => null;
        public CompressionSpec GetCompressionSpec() => CompressionSpec.Default();
        public void Update() { }
        public void Reset() { }
        public string GetName() => "proprioception";

        private static int WriteList(ObservationWriter writer, float[] values, int writeOffset)
        {
            writer.AddList(values, writeOffset);
            return values.Length;
        }
    }
}
