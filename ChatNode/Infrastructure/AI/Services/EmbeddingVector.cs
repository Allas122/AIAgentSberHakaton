using System.Runtime.InteropServices;

namespace ChatNode.Infrastructure.AI.Services;

public static class EmbeddingVector
{
    public static byte[] ToBytes(IReadOnlyList<double> vector)
    {
        var floats = new float[vector.Count];
        var sum = 0.0;

        for (var i = 0; i < vector.Count; i++) sum += vector[i] * vector[i];

        var norm = Math.Sqrt(sum);

        for (var i = 0; i < vector.Count; i++)
        {
            floats[i] = norm > 0 ? (float)(vector[i] / norm) : 0f;
        }

        return MemoryMarshal.AsBytes(floats.AsSpan()).ToArray();
    }

    public static float[] ToFloats(IReadOnlyList<double> vector)
    {
        var bytes = ToBytes(vector);
        return FromBytes(bytes);
    }

    public static float[] FromBytes(byte[] stored)
    {
        if (stored.Length == 0 || stored.Length % sizeof(float) != 0) return [];

        var floats = new float[stored.Length / sizeof(float)];
        Buffer.BlockCopy(stored, 0, floats, 0, stored.Length);

        return floats;
    }

    public static double CosineDistance(float[] left, float[] right)
    {
        if (left.Length == 0 || left.Length != right.Length) return 2.0;

        var dot = 0.0;
        var leftNorm = 0.0;
        var rightNorm = 0.0;

        for (var i = 0; i < left.Length; i++)
        {
            dot += (double)left[i] * right[i];
            leftNorm += (double)left[i] * left[i];
            rightNorm += (double)right[i] * right[i];
        }

        if (leftNorm == 0 || rightNorm == 0) return 2.0;

        var similarity = dot / (Math.Sqrt(leftNorm) * Math.Sqrt(rightNorm));

        return 1.0 - Math.Clamp(similarity, -1.0, 1.0);
    }

    public static byte[] Normalize(byte[] stored)
    {
        var floats = new float[stored.Length / sizeof(float)];
        Buffer.BlockCopy(stored, 0, floats, 0, stored.Length);

        var sum = 0.0;
        foreach (var value in floats) sum += (double)value * value;

        var norm = Math.Sqrt(sum);
        if (norm is 0 or 1) return stored;

        for (var i = 0; i < floats.Length; i++) floats[i] = (float)(floats[i] / norm);

        return MemoryMarshal.AsBytes(floats.AsSpan()).ToArray();
    }
}
