using UnityEngine;
using Unity.Mathematics;

static public class MathFunctions
{
    static public float CurveSlowStartEndPeakMiddle(float x)
    {
        // Function mapping 0 to 1 values with start end multipliers 0.5 and middle peak of 1.25 on a curve, where the integral value of 0 to 1 is 1:
        // -3 * (x - 0.5)^{2} + 1.25
        return -3 * Mathf.Pow(x - 0.5f, 2) + 1.25f;
    }

    static public float DecayFunction(float expMult, float expComponent, float waveComponent, float waveFrequency, float time)
    {
        if (expMult > 1.0f) expMult = 1.0f;
        else if (expMult < 0.0f) expMult = 0.0f;
        return expMult * Mathf.Exp(-expComponent * time) + (1 - expMult) * Mathf.Exp(-waveComponent * time) * Mathf.Cos(waveFrequency * time);
    }
}