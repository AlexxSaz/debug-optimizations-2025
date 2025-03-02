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

        var blockData = new byte[blocksY * blocksX][];

        unsafe
        {
            byte* p = (byte*)scan0;

            Parallel.For(0, blocksY * blocksX, index =>
            {
                var y = (index / blocksX) * DctSize;
                var x = (index % blocksX) * DctSize;

                var pixels = new Color[DctSize, DctSize];
                var tmp = new double[DctSize, DctSize];

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

                using var quantizedStream = new MemoryStream();
                for (byte selector = 0; selector < channelCount; selector++)
                {
                    var subMatrix = GetSubMatrix(pixels, DctSize, selector);
                    dct.DCT2D(subMatrix, tmp);
                    var quantizedFreqs = Quantize(tmp);
                    var quantizedBytes = ZigZagScan(quantizedFreqs);
                    quantizedStream.Write(quantizedBytes, 0, quantizedBytes.Length);
                }

                blockData[index] = quantizedStream.ToArray();
            });
        }

        image.UnlockBits(bitmapData);

        using var allQuantizedBytes = new MemoryStream();
        foreach (var block in blockData)
        {
            allQuantizedBytes.Write(block, 0, block.Length);
        }

        var compressedBytes = 
            HuffmanCodec.Encode(allQuantizedBytes.ToArray(), out var decodeTable, out var bitsCount);

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

    private static double[,] GetSubMatrix(Color[,] matrix, byte length, byte componentSelector)
    {
        var result = new double[length, length];
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

        return result;
    }

    private static byte[] ZigZagScan(byte[,] channelFreqs)
    {
        return
        [
            channelFreqs[0, 0], channelFreqs[0, 1], channelFreqs[1, 0], channelFreqs[2, 0], channelFreqs[1, 1],
            channelFreqs[0, 2], channelFreqs[0, 3], channelFreqs[1, 2],
            channelFreqs[2, 1], channelFreqs[3, 0], channelFreqs[4, 0], channelFreqs[3, 1], channelFreqs[2, 2],
            channelFreqs[1, 3], channelFreqs[0, 4], channelFreqs[0, 5],
            channelFreqs[1, 4], channelFreqs[2, 3], channelFreqs[3, 2], channelFreqs[4, 1], channelFreqs[5, 0],
            channelFreqs[6, 0], channelFreqs[5, 1], channelFreqs[4, 2],
            channelFreqs[3, 3], channelFreqs[2, 4], channelFreqs[1, 5], channelFreqs[0, 6], channelFreqs[0, 7],
            channelFreqs[1, 6], channelFreqs[2, 5], channelFreqs[3, 4],
            channelFreqs[4, 3], channelFreqs[5, 2], channelFreqs[6, 1], channelFreqs[7, 0], channelFreqs[7, 1],
            channelFreqs[6, 2], channelFreqs[5, 3], channelFreqs[4, 4],
            channelFreqs[3, 5], channelFreqs[2, 6], channelFreqs[1, 7], channelFreqs[2, 7], channelFreqs[3, 6],
            channelFreqs[4, 5], channelFreqs[5, 4], channelFreqs[6, 3],
            channelFreqs[7, 2], channelFreqs[7, 3], channelFreqs[6, 4], channelFreqs[5, 5], channelFreqs[4, 6],
            channelFreqs[3, 7], channelFreqs[4, 7], channelFreqs[5, 6],
            channelFreqs[6, 5], channelFreqs[7, 4], channelFreqs[7, 5], channelFreqs[6, 6], channelFreqs[5, 7],
            channelFreqs[6, 7], channelFreqs[7, 6], channelFreqs[7, 7]
        ];
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

    private static byte[,] Quantize(double[,] channelFreqs)
    {
        var result = new byte[channelFreqs.GetLength(0), channelFreqs.GetLength(1)];

        for (int y = 0; y < channelFreqs.GetLength(0); y++)
        {
            for (int x = 0; x < channelFreqs.GetLength(1); x++)
            {
                result[y, x] = (byte)(channelFreqs[y, x] / QuantizationMatrix[y, x]);
            }
        }

        return result;
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