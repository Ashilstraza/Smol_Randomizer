using BepInEx.Configuration;

using System;

namespace Smol_Randomizer.Settings;

/// <summary>
/// Class containing a range of float values.
///
/// Adapted from a StackOverflow question.
/// </summary>
public class FloatRange
{
    /// <summary>The minimum value.</summary>
    private float min;

    /// <summary>The maximum value.</summary>
    private float max;

    /// <summary>The minimum value. Will check if it is less than the max value when set.</summary>
    /// <exception cref="ArgumentException">Is thrown if the value is greater than or equal to the max value.</exception>
    public float Min
    {
        get => min;
        set
        {
            int x = value.CompareTo(max);
            min = x <= 0 ? value : throw new ArgumentException($"{value} is greater than the maximum value ({max})");
        }
    }

    /// <summary>The maximum value. Will check if it is greater than the min value when set.</summary>
    /// <exception cref="ArgumentException">Is thrown if the value is less than or equal to the min value.</exception>
    public float Max
    {
        get => max;
        set
        {
            int x = value.CompareTo(min);
            max = x >= 0 ? value : throw new ArgumentException($"{value} is less than the minimum value ({min})");
        }
    }

    /// <summary>Workaround to allow casting a <see langword="float" /> to a <see cref="FloatRange" /></summary>
    private float Both
    {
        get => min == max ? min : max / min; // Doesn't get used
        set
        {
            min = value;
            max = value;
        }
    }

    /// <summary>Returns the range as a <see cref="Tuple" />.</summary>
    /// <returns>(min, max)</returns>
    public (float min, float max) AsTuple()
    {
        return (min, max);
    }

    /// <summary>Creates a new <see cref="FloatRange" /> with the given values</summary>
    /// <param name="min">The minimum value of the range.</param>
    /// <param name="max">The maximum value of the range.</param>
    /// <exception cref="ArgumentException">Is thrown if the given values are invalid.</exception>
    public FloatRange(float min, float max)
    {
        int x = min.CompareTo(max);
        if (x <= 0)
        {
            this.min = min;
            this.max = max;
        }
        else
        {
            throw new ArgumentException($"{min} is greater than the maximum value ({max})");
        }
    }

    /// <summary>
    /// Cast a <see langword="float" /> to a <see cref="FloatRange" /> where both the minimum and maximum values equal
    /// the <see langword="float" />.
    /// </summary>
    /// <param name="fr">The <see cref="FloatRange" /></param>
    public static implicit operator float(FloatRange fr) => fr.Both;

    /// <summary>
    /// Cast a <see langword="float" /> to a <see cref="FloatRange" /> where both the minimum and maximum values equal
    /// the <see langword="float" />
    /// </summary>
    /// <param name="f">The <see langword="float" /></param>
    public static explicit operator FloatRange(float f) => new(f, f);

    /// <summary>Checks to see if a given value is within the bounds of the <see cref="FloatRange" /></summary>
    /// <param name="value"><see langword="float" /> to test.</param>
    /// <returns><see langword="true" /> the value is within the <see cref="FloatRange" />, otherwise <see langword="false" />.</returns>
    public bool ContainsValue(float value)
    {
        return value.CompareTo(min) >= 0 && value.CompareTo(max) <= 0;
    }

    /// <summary>The range.</summary>
    /// <returns>The range formatted as 'min - max'</returns>
    public override string ToString()
    {
        return $"[{min} - {max}]";
    }

    /// <summary>Parses a given <see langword="string" /> into a <see cref="FloatRange" /></summary>
    /// <param name="s">The <see langword="string" /> to parse.</param>
    /// <returns>A new <see cref="FloatRange" />.</returns>
    public static FloatRange Parse(string s)
    {
        string[] floats = s.Split(['[', ' ', '-', ']'], StringSplitOptions.RemoveEmptyEntries);
        return new FloatRange(float.Parse(floats[0]), float.Parse(floats[1]));
    }

    /// <summary>Parses a given pair of <see langword="string" /> s into a <see cref="FloatRange" /></summary>
    /// <param name="s1">The <see langword="string" /> to parse as the minimum.</param>
    /// <param name="s2">The st <see langword="string" /> ring to parse as the maximum.</param>
    /// <returns>A new <see cref="FloatRange" />.</returns>
    public static FloatRange Parse(string s1, string s2)
    {
        float min = float.Parse(s1);
        float max = float.Parse(s2);
        return new FloatRange(min, max);
    }

    /// <summary>Compares this to a given <see langword="object" />.</summary>
    /// <param name="obj">The <see langword="object" /> to be compared to, will only work with another <see cref="FloatRange" /></param>
    /// <returns><see langword="true" /> if the two values are the same, otherwise <see langword="false" /></returns>
    public override bool Equals(object obj)
    {
        if (obj is FloatRange range)
        {
            if (range.Min == Min && range.Max == Max)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Returns a hash of this <see cref="FloatRange" />.
    /// <para>Auto Generated</para>
    /// </summary>
    /// <returns>the hash</returns>
    public override int GetHashCode()
    {
        return HashCode.Combine(min, max);
    }
}

/// <summary>
/// Class for holding the acceptable ranges for a <see cref="FloatRange" /> when used by a BepinEx config setting
/// </summary>
/// <param name="minValue">The minimum value for the setting</param>
/// <param name="maxValue">The maximum value for the setting</param>
public class AcceptableRangeforFloatRange(float minValue, float maxValue) : AcceptableValueBase(typeof(FloatRange))
{
    /// <summary>The minimum acceptable value</summary>
    public float MinValue { get; } = minValue;

    /// <summary>The maximum acceptable value</summary>
    public float MaxValue { get; } = maxValue;

    /// <summary>Clamps the given <see langword="object" /> to be within the minimum and maximum values</summary>
    /// <param name="value"><see cref="FloatRange" /> or <see langword="float" /> to be clamped</param>
    /// <returns>The clamped value</returns>
    /// <exception cref="ArgumentException">Thrown if the passed value is not a <see cref="FloatRange" /> or <see langword="float" /></exception>
    public override object Clamp(object value)
    {
        if (value.GetType() == typeof(FloatRange))
        {
            FloatRange range = (FloatRange)value;

            if (MinValue.CompareTo(range.Min) > 0)
            {
                range.Min = MinValue;
            }

            if (MaxValue.CompareTo(range.Max) < 0)
            {
                range.Max = MaxValue;
            }

            return range;
        }
        else if (value.GetType() == typeof(float))
        {
            float f = (float)value;
            if (MinValue.CompareTo(f) > 0)
            {
                return MinValue;
            }

            if (MaxValue.CompareTo(f) < 0)
            {
                return MaxValue;
            }

            return value;
        }
        else
            throw new ArgumentException("Unable to clamp an object that is not an FloatRange or float");
    }

    /// <summary>Checks to see if the given <see langword="object" /> is valid</summary>
    /// <param name="value"><see cref="FloatRange" /> or <see langword="float" /> to be checked</param>
    /// <returns><see langword="true" /> if the value is valid, <see langword="false" /> if it is not</returns>
    public override bool IsValid(object value)
    {
        if (value.GetType() == typeof(FloatRange))
        {
            FloatRange range = (FloatRange)value;

            if (MinValue.CompareTo(range.Min) <= 0)
            {
                return MaxValue.CompareTo(range.Max) >= 0;
            }
        }
        else if (value.GetType() == typeof(float))
        {
            float f = (float)value;

            if (MinValue.CompareTo(f) <= 0)
            {
                return MaxValue.CompareTo(f) >= 0;
            }
        }

        return false;
    }

    /// <summary>Converts the <see cref="FloatRange"/> to a description <see langword="string".</summary> <returns><see
    /// langword="string" /> describing the acceptable value range.</returns>
    public override string ToDescriptionString()
    {
        return $"# Acceptable value range: From {MinValue} to {MaxValue}";
    }
}