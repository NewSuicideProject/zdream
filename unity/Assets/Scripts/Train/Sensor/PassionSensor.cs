using Unity.MLAgents.Sensors;

namespace Train.Sensor {
    public class PassionSensor : ISensor {
        private readonly Passion _passion;
        private readonly ObservationSpec _observationSpec;

        public PassionSensor(Passion passion) {
            _passion = passion;
            _observationSpec = ObservationSpec.Vector(1);
        }

        public string GetName() => "passion";

        public int Write(ObservationWriter writer) {
            writer[0] = _passion.Value;
            return 1;
        }

        public byte[] GetCompressedObservation() => null;
        public ObservationSpec GetObservationSpec() => _observationSpec;
        public CompressionSpec GetCompressionSpec() => CompressionSpec.Default();

        public void Update() {
        }

        public void Reset() {
        }
    }
}
