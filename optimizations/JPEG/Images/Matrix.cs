using System.Drawing;
using System.Drawing.Imaging;

namespace JPEG.Images;

record Matrix
{
	public readonly Pixel[,] Pixels;
	public readonly int Height;
	public readonly int Width;

	public Matrix(int height, int width)
	{
		Height = height;
		Width = width;

		Pixels = new Pixel[height, width];
		for (var i = 0; i < height; ++i)
		for (var j = 0; j < width; ++j)
			Pixels[i, j] = new Pixel(0, 0, 0, PixelFormat.RGB);
	}

	public static explicit operator Matrix(Bitmap bmp)
	{
		var height = bmp.Height - bmp.Height % 8;
		var width = bmp.Width - bmp.Width % 8;
		var matrix = new Matrix(height, width);

		for (var j = 0; j < height; j++)
		{
			for (var i = 0; i < width; i++)
			{
				var pixel = bmp.GetPixel(i, j);
				matrix.Pixels[j, i] = new Pixel(pixel.R, pixel.G, pixel.B, PixelFormat.RGB);
			}
		}

		return matrix;
	}

	public static explicit operator Bitmap(Matrix matrix)
	{
		var height = matrix.Height;
		var width = matrix.Width;
		
		var bitmap = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

		// Блокируем Bitmap в памяти
		var bitmapData = bitmap.LockBits(
			new Rectangle(0, 0, width, height),
			ImageLockMode.WriteOnly,
			System.Drawing.Imaging.PixelFormat.Format32bppArgb
		);

		unsafe
		{
			// Получаем указатель на данные
			byte* scan0 = (byte*)bitmapData.Scan0;

			// Заполняем Bitmap данными из массивов
			for (int y = 0; y < height; y++)
			{
				for (int x = 0; x < width; x++)
				{
					var pixel = matrix.Pixels[y, x];
					// Преобразуем значения double в byte (0-255)
					byte r = (byte)pixel.R;
					byte g = (byte)pixel.G;
					byte b = (byte)pixel.B;

					// Вычисляем позицию пикселя в памяти
					int offset = y * bitmapData.Stride + x * 4; // 4 байта на пиксель (ARGB)

					// Записываем цветовые компоненты
					scan0[offset + 2] = r; // Красный
					scan0[offset + 1] = g; // Зеленый
					scan0[offset] = b;     // Синий
					scan0[offset + 3] = 255; // Альфа-канал (непрозрачность)
				}
			}
		}

		// Разблокируем Bitmap
		bitmap.UnlockBits(bitmapData);

		return bitmap;
	}

	public static int ToByte(double d)
	{
		var val = (int)d;
		if (val > byte.MaxValue)
			return byte.MaxValue;
		if (val < byte.MinValue)
			return byte.MinValue;
		return val;
	}
}