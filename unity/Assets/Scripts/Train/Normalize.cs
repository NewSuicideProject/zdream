namespace Train {
    public static class Normalize {
        public static float Speed(float speed) =>
            global::Normalize.Tanh(speed, Config.Normalization.ExpectedMaxSpeed);

        public static float Distance(float distance) =>
            global::Normalize.Tanh(distance, Config.Normalization.ExpectedMaxDistance);

        public static float Thickness(float thickness) =>
            global::Normalize.Tanh(thickness, Config.Normalization.ExpectedMaxThickness);

        public static float Height(float height) =>
            global::Normalize.Tanh(height, Config.Normalization.ExpectedMaxHeight);

        public static float JointPosition(float position, float min, float max) =>
            global::Normalize.LinearMinMax(position, min, max);

        public static float Force(float force) =>
            global::Normalize.Tanh(force, Config.Normalization.ExpectedMaxForce);
    }

    public static class Denormalize {
        public static float JointPosition(float position, float min, float max) =>
            global::Denormalize.LinearMinMax(position, min, max);
    }
}
