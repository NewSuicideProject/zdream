namespace Train {
    public static class Normalization {
        public static float NormalizeSpeed(float speed) =>
            global::Normalization.Tanh(speed, Config.Normalization.ExpectedMaxSpeed);

        public static float NormalizeDistance(float distance) =>
            global::Normalization.Tanh(distance, Config.Normalization.ExpectedMaxDistance);

        public static float NormalizeThickness(float thickness) =>
            global::Normalization.Tanh(thickness, Config.Normalization.ExpectedMaxThickness);

        public static float NormalizeHeight(float height) =>
            global::Normalization.Tanh(height, Config.Normalization.ExpectedMaxHeight);

        public static float NormalizeJointPosition(float position, float min, float max) =>
            global::Normalization.LinearMinMax(position, min, max);

        public static float NormalizeForce(float force) =>
            global::Normalization.Tanh(force);
    }
}
