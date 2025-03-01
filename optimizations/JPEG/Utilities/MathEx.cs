using System;
using System.Linq;
using System.Runtime.CompilerServices;

namespace JPEG.Utilities;

public static class MathEx
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double SumByTwoVariables(int from1, int to1, int from2, int to2, Func<int, int, double> function)
    {
        double sum = 0.0;
        for (int i = from1; i < to1; i++)
        {
            for (int j = from2; j < to2; j++)
            {
                sum += function(i, j);
            }
        }

        return sum;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void LoopByTwoVariables(int from1, int to1, int from2, int to2, Action<int, int> function)
    {
        for (int i = from1; i < to1; i++)
        {
            for (int j = from2; j < to2; j++)
            {
                function(i, j);
            }
        }
    }
}