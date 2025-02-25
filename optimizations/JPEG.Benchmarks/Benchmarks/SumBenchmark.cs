using BenchmarkDotNet.Attributes;

namespace JPEG.Benchmarks.Benchmarks;

public class SumBenchmark
{
    private readonly int from = 1;
    private readonly int to = 1000;

    [Benchmark]
    public double SumLinq() => SumLinq(from, to, x => x * 1.0);

    [Benchmark]
    public double SumManual() => SumManual(from, to, x => x * 1.0);

    private static double SumLinq(int from, int to, Func<int, double> function)
    {
        return Enumerable.Range(from, to - from).Sum(function);
    }

    private static double SumManual(int from, int to, Func<int, double> function)
    {
        double sum = 0.0;
        for (int i = from; i < to; i++)
        {
            sum += function(i);
        }
        return sum;
    }
}