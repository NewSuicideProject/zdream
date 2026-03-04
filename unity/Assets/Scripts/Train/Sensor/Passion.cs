using Unity.MLAgents.Sensors;
using UnityEngine;

namespace Train.Sensor {
    public class Passion : SensorComponent {
        [SerializeField] [ReadOnly] private float value = 0.5f;

        private PassionSensor _passionSensor;

        public override ISensor[] CreateSensors() {
            _passionSensor = new PassionSensor(this);
            return new ISensor[] { _passionSensor };
        }

        public float Value => value;

        public void OnEpisodeBegin() =>
            value = 0.5f + (Random.Range(-0.5f, 0.5f) * Config.Passion.Ratio);
    }
}
