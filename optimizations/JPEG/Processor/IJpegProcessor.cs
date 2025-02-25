namespace JPEG.Processor;

public interface IJpegProcessor
{
	void Compress(string imagePath, string compressedImagePath);

	void Uncompress(string compressedImagePath, string uncompressedImagePath);
}