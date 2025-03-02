using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading.Tasks;
using JPEG.Images;
using PixelFormat = JPEG.Images.PixelFormat;

namespace JPEG.Processor;

public class JpegProcessor : IJpegProcessor
{
    public static readonly JpegProcessor Init = new();
    private const byte DctSize = 8;
    private readonly DCT dct = new(DctSize);

    private static readonly int[,] QuantizationMatrix = new[,]
    {
        { 8, 6, 5, 8, 12, 20, 26, 31 },
        { 6, 6, 7, 10, 13, 29, 30, 28 },
        { 7, 7, 8, 12, 20, 29, 35, 28 },
        { 7, 9, 11, 15, 26, 44, 40, 31 },
        { 9, 11, 19, 28, 34, 55, 52, 39 },
        { 12, 18, 28, 32, 41, 52, 57, 46 },
        { 25, 32, 39, 44, 52, 61, 60, 51 },
        { 36, 46, 48, 49, 56, 50, 52, 50 }
    };

    private static readonly int[] ZigzagOrder =
    {
        0, 1, 8, 16, 9, 2, 3, 10,
        17, 24, 32, 25, 18, 11, 4, 5,
        12, 19, 26, 33, 40, 48, 41, 34,
        27, 20, 13, 6, 7, 14, 21, 28,
        35, 42, 49, 56, 57, 50, 43, 36,
        29, 22, 15, 23, 30, 37, 44, 51,
        58, 59, 52, 45, 38, 31, 39, 46,
        53, 60, 61, 54, 47, 55, 62, 63
    };

    public void Compress(string imagePath, string compressedImagePath)
    {
        using var image = new Bitmap(imagePath);
        var height = image.Height;
        var width = image.Width;

        var bitmapData = image.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly,
            System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        var stride = bitmapData.Stride;
        var scan0 = bitmapData.Scan0;

        var blocksY = image.Height / DctSize;
        var blocksX = image.Width / DctSize;
        const int channelCount = 3;

        var blockSize = DctSize * DctSize * channelCount;
        var allQuantizedBytes = new byte[blocksY * blocksX * blockSize];

        unsafe
        {
            byte* p = (byte*)scan0;

            Parallel.For(0, blocksY * blocksX, index =>
            {
                var y = (index / blocksX) * DctSize;
                var x = (index % blocksX) * DctSize;

                var pixels = new Color[DctSize, DctSize];
                var tmp = new double[DctSize, DctSize];
                byte[] result = new byte[DctSize * DctSize];
                var subMatrix = new double[DctSize, DctSize];

                for (var i = x; i < x + DctSize; i++)
                {
                    for (var j = y; j < y + DctSize; j++)
                    {
                        var pixelIndex = j * stride + i * 4;
                        pixels[i % DctSize, j % DctSize] = Color.FromArgb(
                            p[pixelIndex + 3],
                            p[pixelIndex + 2],
                            p[pixelIndex + 1],
                            p[pixelIndex]
                        );
                    }
                }

                var offset = index * blockSize;
                for (byte selector = 0; selector < channelCount; selector++)
                {
                    GetSubMatrix(pixels, DctSize, selector, subMatrix);
                    dct.DCT2D(subMatrix, tmp);
                    QuantizeAndZigZagScan(tmp, result);
                    Buffer.BlockCopy(result, 0, allQuantizedBytes, offset, result.Length);
                    offset += result.Length;
                }
            });
        }

        image.UnlockBits(bitmapData);

        var compressedBytes =
            HuffmanCodec.Encode(allQuantizedBytes, out var decodeTable, out var bitsCount);

        var compressionResult = new CompressedImage
        {
            Quality = 50,
            CompressedBytes = compressedBytes,
            BitsCount = bitsCount,
            DecodeTable = decodeTable,
            Height = height,
            Width = width
        };

        compressionResult.Save(compressedImagePath);
    }

    public void Decompress(string compressedImagePath, string uncompressedImagePath)
    {
        var compressedImage = CompressedImage.Load(compressedImagePath);
        var uncompressedImage = Decompress(compressedImage);
        var resultBmp = (Bitmap)uncompressedImage;
        resultBmp.Save(uncompressedImagePath, ImageFormat.Bmp);
    }

    private Matrix Decompress(CompressedImage image)
    {
        var result = new Matrix(image.Height, image.Width);
        using var allQuantizedBytes =
            new MemoryStream(HuffmanCodec.Decode(image.CompressedBytes, image.DecodeTable, image.BitsCount));

        var blocksY = image.Height / DctSize;
        var blocksX = image.Width / DctSize;
        const int channelCount = 3;

        var blockData = new byte[blocksY * blocksX][];

        for (var i = 0; i < blocksY * blocksX; i++)
        {
            blockData[i] = new byte[DctSize * DctSize * channelCount];
            allQuantizedBytes.ReadAsync(blockData[i], 0, blockData[i].Length).Wait();
        }

        Parallel.For(0, blocksY * blocksX, index =>
        {
            var y = (index / blocksX) * DctSize;
            var x = (index % blocksX) * DctSize;

            var _y = new double[DctSize, DctSize];
            var cb = new double[DctSize, DctSize];
            var cr = new double[DctSize, DctSize];

            var quantizedBytes = blockData[index];

            for (var channelIndex = 0; channelIndex < channelCount; channelIndex++)
            {
                var channelBytes = new byte[DctSize * DctSize];
                Buffer.BlockCopy(quantizedBytes, channelIndex * DctSize * DctSize, channelBytes, 0, DctSize * DctSize);

                var quantizedFreqs = ZigZagUnScan(channelBytes);
                var channelFreqs = DeQuantize(quantizedFreqs);
                dct.IDCT2D(channelFreqs, channelIndex == 0 ? _y : (channelIndex == 1 ? cb : cr));
                ShiftMatrixValues(channelIndex == 0 ? _y : (channelIndex == 1 ? cb : cr), 128);
            }

            SetPixels(result, _y, cb, cr, PixelFormat.YCbCr, y, x);
        });

        return result;
    }

    private static void ShiftMatrixValues(double[,] subMatrix, int shiftValue)
    {
        var height = subMatrix.GetLength(0);
        var width = subMatrix.GetLength(1);

        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
            subMatrix[y, x] += shiftValue;
    }

    private static void SetPixels(Matrix matrix, double[,] a, double[,] b, double[,] c, PixelFormat format,
        int yOffset, int xOffset)
    {
        var height = a.GetLength(0);
        var width = a.GetLength(1);

        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
            matrix.Pixels[yOffset + y, xOffset + x] = new Pixel(a[y, x], b[y, x], c[y, x], format);
    }

    private static void GetSubMatrix(Color[,] matrix, byte length, byte componentSelector, double[,] result)
    {
        for (var j = 0; j < length; j++)
        for (var i = 0; i < length; i++)
        {
            var pixel = matrix[i, j];
            if (componentSelector == 0)
                result[j, i] = 16.0 + (65.738 * pixel.R + 129.057 * pixel.G + 24.064 * pixel.B) / 256.0 - 128;
            else if (componentSelector == 1)
                result[j, i] = 128.0 + (-37.945 * pixel.R - 74.494 * pixel.G + 112.439 * pixel.B) / 256.0 - 128;
            else if (componentSelector == 2)
                result[j, i] = 128.0 + (112.439 * pixel.R - 94.154 * pixel.G - 18.285 * pixel.B) / 256.0 - 128;
        }
    }

    private static byte[,] ZigZagUnScan(IReadOnlyList<byte> quantizedBytes)
    {
        return new[,]
        {
            {
                quantizedBytes[0], quantizedBytes[1], quantizedBytes[5], quantizedBytes[6], quantizedBytes[14],
                quantizedBytes[15], quantizedBytes[27], quantizedBytes[28]
            },
            {
                quantizedBytes[2], quantizedBytes[4], quantizedBytes[7], quantizedBytes[13], quantizedBytes[16],
                quantizedBytes[26], quantizedBytes[29], quantizedBytes[42]
            },
            {
                quantizedBytes[3], quantizedBytes[8], quantizedBytes[12], quantizedBytes[17], quantizedBytes[25],
                quantizedBytes[30], quantizedBytes[41], quantizedBytes[43]
            },
            {
                quantizedBytes[9], quantizedBytes[11], quantizedBytes[18], quantizedBytes[24], quantizedBytes[31],
                quantizedBytes[40], quantizedBytes[44], quantizedBytes[53]
            },
            {
                quantizedBytes[10], quantizedBytes[19], quantizedBytes[23], quantizedBytes[32], quantizedBytes[39],
                quantizedBytes[45], quantizedBytes[52], quantizedBytes[54]
            },
            {
                quantizedBytes[20], quantizedBytes[22], quantizedBytes[33], quantizedBytes[38], quantizedBytes[46],
                quantizedBytes[51], quantizedBytes[55], quantizedBytes[60]
            },
            {
                quantizedBytes[21], quantizedBytes[34], quantizedBytes[37], quantizedBytes[47], quantizedBytes[50],
                quantizedBytes[56], quantizedBytes[59], quantizedBytes[61]
            },
            {
                quantizedBytes[35], quantizedBytes[36], quantizedBytes[48], quantizedBytes[49], quantizedBytes[57],
                quantizedBytes[58], quantizedBytes[62], quantizedBytes[63]
            }
        };
    }

    private static void QuantizeAndZigZagScan(double[,] channelFreqs, byte[] result)
    {
        for (int i = 0; i < ZigzagOrder.Length; i++)
        {
            int y = ZigzagOrder[i] / DctSize;
            int x = ZigzagOrder[i] % DctSize;

            result[i] = (byte)(channelFreqs[y, x] / QuantizationMatrix[y, x]);
        }
    }

    private static double[,] DeQuantize(byte[,] quantizedBytes)
    {
        var result = new double[quantizedBytes.GetLength(0), quantizedBytes.GetLength(1)];

        for (int y = 0; y < quantizedBytes.GetLength(0); y++)
        {
            for (int x = 0; x < quantizedBytes.GetLength(1); x++)
            {
                result[y, x] =
                    ((sbyte)quantizedBytes[y, x]) *
                    QuantizationMatrix[y, x];
            }
        }

        return result;
    }
}