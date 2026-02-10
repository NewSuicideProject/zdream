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
            writer.Add(_prop.Gravity);
            offset += 3;

            Vector3 comDiff = _prop.Com - _prop.InitialCoM;
            writer.Add(comDiff);
            writer.Add(_prop.AngularVelocity);
            writer.Add(_prop.LinearVelocity);
            writer.Add(_prop.Position);
            writer.Add(_prop.Forward);


            writer[offset] = _prop.Integrity;



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
