using System;
using System.Runtime.CompilerServices;

namespace JPEG;

public class DCT
{
    private readonly double[,] DctMatrix;
    private readonly double[] AlphaValues;
    private readonly double BetaValue;
    private readonly byte DCTSize;
    private readonly double[,] Cos;

    public DCT(byte dctSize)
    {
        Cos = new double[dctSize, dctSize];
        DctMatrix = new double[dctSize, dctSize];
        AlphaValues = new double[dctSize];
        BetaValue = 1d / dctSize + 1d / dctSize;
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
                Cos[u, v] = Math.Cos(((2d * v + 1d) * u * Math.PI) / (2 * dctSize));
            }
        }
    }

    public void DCT2D(double[,] input, double[,] result)
    {
        for (var u = 0; u < DCTSize; u++)
        {
            for (var v = 0; v < DCTSize; v++)
            {
                var sum = 0d;

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

    public void IDCT2D(double[,] coeffs, double[,] output)
    {
        for (var x = 0; x < DCTSize; x++)
        {
            for (var y = 0; y < DCTSize; y++)
            {
                var sum = 0d;

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
    private static double Alpha(int u)
    {
        return u == 0 ? 1 / Math.Sqrt(2) : 1;
    }
}