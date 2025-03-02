namespace JPEG.Processor;

public interface IJpegProcessor
{
	void Compress(string imagePath, string compressedImagePath);

	void Decompress(string compressedImagePath, string uncompressedImagePath);
}