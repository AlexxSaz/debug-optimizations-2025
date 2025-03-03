using System;
using System.Runtime.CompilerServices;

namespace JPEG;

public class DCT : IAlgorithm
{
    private readonly float[,] DctMatrix;
    private readonly float[] AlphaValues;
    private readonly float BetaValue;
    private readonly byte DCTSize;
    private readonly float[,] Cos;

    public DCT(byte dctSize)
    {
        Cos = new float[dctSize, dctSize];
        DctMatrix = new float[dctSize, dctSize];
        AlphaValues = new float[dctSize];
        BetaValue = 1f / dctSize + 1f / dctSize;
        DCTSize = dctSize;

        for (int u = 0; u < dctSize; u++)
        {
            AlphaValues[u] = Alpha(u);
        }

        for (int u = 0; u < dctSize; u++)
        {
            for (int v = 0; v < dctSize; v++)
            {
                DctMatrix[u, v] = BetaValue * AlphaValues[u] * AlphaValues[v];
                Cos[u, v] = (float)Math.Cos(((2d * v + 1d) * u * Math.PI) / (2 * dctSize));
            }
        }
    }

    public void Forward(float[,] input, float[,] result)
    {
        for (var u = 0; u < DCTSize; u++)
        {
            for (var v = 0; v < DCTSize; v++)
            {
                var sum = 0f;

                for (var x = 0; x < DCTSize; x++)
                {
                    for (var y = 0; y < DCTSize; y++)
                    {
                        sum += input[x, y] * Cos[u, x] * Cos[v, y];
                    }
                }

                result[u, v] = sum * DctMatrix[u, v];
            }
        }
    }

    public void Backward(float[,] coeffs, float[,] output)
    {
        for (var x = 0; x < DCTSize; x++)
        {
            for (var y = 0; y < DCTSize; y++)
            {
                var sum = 0f;

                for (var u = 0; u < DCTSize; u++)
                {
                    for (var v = 0; v < DCTSize; v++)
                    {
                        sum += coeffs[u, v] * Cos[u, x] * Cos[v, y] * AlphaValues[u] * AlphaValues[v];
                    }
                }
                
                output[x, y] = sum * BetaValue;
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float Alpha(int u)
    {
        return u == 0 ? (float)(1 / Math.Sqrt(2)) : 1;
    }
}