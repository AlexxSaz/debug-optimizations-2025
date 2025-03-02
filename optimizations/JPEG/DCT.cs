using System;
using System.Runtime.CompilerServices;
using JPEG.Utilities;

namespace JPEG;

public class DCT
{
    private readonly double[,] DctMatrix;

    public DCT(int dctSize)
    {
        DctMatrix = new double[dctSize, dctSize];

        for (int u = 0; u < dctSize; u++)
        {
            for (int v = 0; v < dctSize; v++)
            {
                DctMatrix[u, v] = Beta(dctSize, dctSize) * Alpha(u) * Alpha(v);
            }
        }
    }

    public void DCT2D(double[,] input, double[,] result)
    {
        var height = input.GetLength(0);
        var width = input.GetLength(1);

        for (var u = 0; u < width; u++)
        for (var v = 0; v < height; v++)
        {
            var sum = 0d;
            for (var x = 0; x < width; x++)
            for (var y = 0; y < height; y++)
            {
                sum += BasisFunction(input[x, y], u, v, x, y, height, width);
            }

            result[u, v] = sum * DctMatrix[u, v];
        }
    }

    public static void IDCT2D(double[,] coeffs, double[,] output)
    {
        var height = coeffs.GetLength(0);
        var width = coeffs.GetLength(1);
        
        for (var x = 0; x < width; x++)
        {
            for (var y = 0; y < height; y++)
            {
                var sum = 0d;
                for (var u = 0; u < width; u++)
                for (var v = 0; v < height; v++)
                {
                    sum += BasisFunction(coeffs[u, v], u, v, x, y, coeffs.GetLength(0), coeffs.GetLength(1)) *
                           Alpha(u) * Alpha(v);
                }

                output[x, y] = sum * Beta(coeffs.GetLength(0), coeffs.GetLength(1));
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double BasisFunction(double a, double u, double v, double x, double y, int height, int width)
    {
        var b = Math.Cos(((2d * x + 1d) * u * Math.PI) / (2 * width));
        var c = Math.Cos(((2d * y + 1d) * v * Math.PI) / (2 * height));

        return a * b * c;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double Alpha(int u)
    {
        if (u == 0)
            return 1 / Math.Sqrt(2);
        return 1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double Beta(int height, int width)
    {
        return 1d / width + 1d / height;
    }
}