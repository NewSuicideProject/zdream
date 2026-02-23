using System;
using UnityEngine;

public static class Normalize {
    public static float Tanh(float value, float scale = 1f) {
        if (Mathf.Approximately(scale, 0f)) {
            return 0f;
        }

        float x = value / scale;
        return (float)Math.Tanh(x);
    }

    public static float LinearMinMax(float value, float min, float max) {
        if (Mathf.Approximately(max - min, 0f)) {
            return 0f;
        }

        float clampedValue = Mathf.Clamp(value, min, max);
        return (2f * (clampedValue - min) / (max - min)) - 1f;
    }
}

public static class Denormalize {
    public static float LinearMinMax(float normalizedValue, float min, float max) {
        if (Mathf.Approximately(max - min, 0f)) {
            return min;
        }

        float clampedValue = Mathf.Clamp(normalizedValue, -1f, 1f);
        return ((clampedValue + 1f) / 2f * (max - min)) + min;
    }
}
