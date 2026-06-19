using System;

namespace Smol_Randomizer.Settings;

/// <summary>
/// Class containing a range of integer values.
/// 
/// Adapted from a StackOverflow question.
/// </summary>
public class IntRange
{
    private int min;
    private int max;

    /// <summary>
    /// The minimum value. Will check if it is less than the max value when set.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// Is thrown if the value is greater than or equal to the max value.
    /// </exception>
    public int Min
    {
        get => min;
        set
        {
            int x = value.CompareTo(max);
            min = x <= 0 ? value : throw new ArgumentException($"{value} is greater than the maximum value ({max})");
        }
    }

    /// <summary>
    /// The maximum value. Will check if it is greater than the min value when set.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// Is thrown if the value is less than or equal to the min value.
    /// </exception>
    public int Max
    {
        get => max;
        set
        {
            int x = value.CompareTo(min);
            max = x >= 0 ? value : throw new ArgumentException($"{value} is less than the minimum value ({min})");
        }
    }

    /// <summary>
    /// Returns the range as a Tuple.
    /// </summary>
    /// <returns>(min, max)</returns>
    public (int min, int max) AsTuple()
    {
        return (min, max);
    }

    /// <summary>
    /// Creates a new Range with the given values
    /// </summary>
    /// <param name="min">The minimum value of the range.</param>
    /// <param name="max">The maximum value of the range.</param>
    /// <exception cref="ArgumentException">Is thrown if the given values are invalid.</exception>
    public IntRange(int min, int max)
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
    /// Checks to see if a given value is within the bounds of the Range
    /// </summary>
    /// <param name="value">Value to test.</param>
    /// <returns>True the value is within the range, otherwise false.</returns>
    public bool ContainsValue(int value)
    {
        return value.CompareTo(min) >= 0 && value.CompareTo(max) <= 0;
    }

    /// <summary>
    /// The range.
    /// </summary>
    /// <returns>The range formatted as 'min - max'</returns>
    public override string ToString()
    {
        return $"[{min} - {max}]";
    }

    /// <summary>
    /// Parses a given string into a float range
    /// </summary>
    /// <param name="s">The string to parse.</param>
    /// <returns>A new FloatRange.</returns>
    public static IntRange Parse(string s)
    {
        string[] ints = s.Split(['[', ' ', '-', ']'], StringSplitOptions.RemoveEmptyEntries);
        return new IntRange(int.Parse(ints[0]), int.Parse(ints[1]));
    }

    /// <summary>
    /// Parses a given pair of strings into a float range
    /// </summary>
    /// <param name="s1">The string to parse as the minimum.</param>
    /// <param name="s2">The string to parse as the maximum.</param>
    /// <returns>A new FloatRange.</returns>
    public static IntRange Parse(string s1, string s2)
    {
        int min = int.Parse(s1);
        int max = int.Parse(s2);
        return new IntRange(min, max);
    }

    /// <summary>
    /// Compares this to a given object.
    /// </summary>
    /// <param name="obj">The object to be compared to, will only work with another FloatRange</param>
    /// <returns>true if the two values are the same, otherwise false</returns>
    public override bool Equals(object obj)
    {
        if (obj is IntRange range)
        {
            if (range.Min == Min && range.Max == Max)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Returns a hash of this object.
    /// 
    /// <para>Auto Generated</para>
    /// </summary>
    /// <returns>the hash</returns>
    public override int GetHashCode()
    {
        return HashCode.Combine(min, max);
    }
}
