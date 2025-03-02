using System;
using System.Runtime.CompilerServices;

namespace JPEG;

public class DCT
{
    private readonly double[,] DctMatrix;
    private readonly double[] AlphaValues;
    private readonly double BetaValue;
    private readonly byte DCTSize;

    public DCT(byte dctSize)
    {
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
            }
        }
    }

    public void DCT2D(double[,] input, double[,] result)
    {
        var cosX = new double[DCTSize, DCTSize];
        var cosY = new double[DCTSize, DCTSize];

        for (int u = 0; u < DCTSize; u++)
        {
            for (int x = 0; x < DCTSize; x++)
            {
                cosX[u, x] = Math.Cos(((2d * x + 1d) * u * Math.PI) / (2 * DCTSize));
            }
        }

        for (int v = 0; v < DCTSize; v++)
        {
            for (int y = 0; y < DCTSize; y++)
            {
                cosY[v, y] = Math.Cos(((2d * y + 1d) * v * Math.PI) / (2 * DCTSize));
            }
        }

        for (var u = 0; u < DCTSize; u++)
        {
            for (var v = 0; v < DCTSize; v++)
            {
                var sum = 0d;

                for (var x = 0; x < DCTSize; x++)
                {
                    for (var y = 0; y < DCTSize; y++)
                    {
                        sum += input[x, y] * cosX[u, x] * cosY[v, y];
                    }
                }

                result[u, v] = sum * DctMatrix[u, v];
            }
        }
    }

    public void IDCT2D(double[,] coeffs, double[,] output)
    {
        var cosX = new double[DCTSize, DCTSize];
        var cosY = new double[DCTSize, DCTSize];

        for (int u = 0; u < DCTSize; u++)
        {
            for (int x = 0; x < DCTSize; x++)
            {
                cosX[u, x] = Math.Cos(((2d * x + 1d) * u * Math.PI) / (2 * DCTSize));
            }
        }

        for (int v = 0; v < DCTSize; v++)
        {
            for (int y = 0; y < DCTSize; y++)
            {
                cosY[v, y] = Math.Cos(((2d * y + 1d) * v * Math.PI) / (2 * DCTSize));
            }
        }

        for (var x = 0; x < DCTSize; x++)
        {
            for (var y = 0; y < DCTSize; y++)
            {
                var sum = 0d;

                for (var u = 0; u < DCTSize; u++)
                {
                    for (var v = 0; v < DCTSize; v++)
                    {
                        sum += coeffs[u, v] * cosX[u, x] * cosY[v, y] * AlphaValues[u] * AlphaValues[v];
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