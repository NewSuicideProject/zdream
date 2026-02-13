namespace Train {
    public static class Normalization {
        public static float ExpectedMaxSpeed = 20f;
        public static float ExpectedMaxDistance = 10f;
        public static float ExpectedMaxThickness = 2.5f;
        public static float ExpectedMaxHeight = 10f;

        public static float NormalizeSpeed(float speed) => global::Normalization.Tanh(speed, ExpectedMaxSpeed);

        public static float NormalizeDistance(float distance) =>
            global::Normalization.Tanh(distance, ExpectedMaxDistance);

        public static float NormalizeThickness(float thickness) =>
            global::Normalization.Tanh(thickness, ExpectedMaxThickness);

        public static float NormalizeHeight(float height) =>
            global::Normalization.Tanh(height, ExpectedMaxHeight);
    }
}
