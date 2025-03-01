using System;
using System.Diagnostics;
using JPEG.Processor;

namespace JPEG;

internal class Program
{
	static void Main(string[] args)
	{
		try
		{
			Console.WriteLine(IntPtr.Size == 8 ? "64-bit version" : "32-bit version");
			var processor = JpegProcessor.Init;
			var sw = Stopwatch.StartNew();
			var imagePath = @"sample.bmp";
			var compressedImagePath = imagePath + ".compressed." + 50;
			var uncompressedImagePath = imagePath + ".uncompressed." + 50 + ".bmp";

			sw.Restart();
			processor.Compress(imagePath, compressedImagePath);
			sw.Stop();
			Console.WriteLine("Compression: " + sw.ElapsedMilliseconds);

			sw.Restart();
			processor.Uncompress(compressedImagePath, uncompressedImagePath);
			sw.Stop();
			Console.WriteLine("Decompression: " + sw.ElapsedMilliseconds);
			Console.WriteLine($"Peak commit size: {MemoryMeter.PeakPrivateBytes() / (1024.0 * 1024):F2} MB");
			Console.WriteLine($"Peak working set: {MemoryMeter.PeakWorkingSet() / (1024.0 * 1024):F2} MB");
		}
		catch (Exception e)
		{
			Console.WriteLine(e);
		}
	}
}