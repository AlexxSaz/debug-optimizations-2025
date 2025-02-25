namespace JPEG.Processor;

public interface IJpegProcessor
{
	void Compress(string imagePath, string compressedImagePath);
	void CompressP(string imagePath, string compressedImagePath);

	void Uncompress(string compressedImagePath, string uncompressedImagePath);
	void UncompressP(string compressedImagePath, string uncompressedImagePath);
}