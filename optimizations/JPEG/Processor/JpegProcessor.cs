using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
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
    [
        0, 1, 8, 16, 9, 2, 3, 10,
        17, 24, 32, 25, 18, 11, 4, 5,
        12, 19, 26, 33, 40, 48, 41, 34,
        27, 20, 13, 6, 7, 14, 21, 28,
        35, 42, 49, 56, 57, 50, 43, 36,
        29, 22, 15, 23, 30, 37, 44, 51,
        58, 59, 52, 45, 38, 31, 39, 46,
        53, 60, 61, 54, 47, 55, 62, 63
    ];

    private static readonly int[] ZigZagUnOrder =
    {
        0, 1, 5, 6, 14, 15, 27, 28,
        2, 4, 7, 13, 16, 26, 29, 42,
        3, 8, 12, 17, 25, 30, 41, 43,
        9, 11, 18, 24, 31, 40, 44, 53,
        10, 19, 23, 32, 39, 45, 52, 54,
        20, 22, 33, 38, 46, 51, 55, 60,
        21, 34, 37, 47, 50, 56, 59, 61,
        35, 36, 48, 49, 57, 58, 62, 63
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
                var tmp = new float[DctSize, DctSize];
                byte[] result = new byte[DctSize * DctSize];
                var subMatrix = new float[DctSize, DctSize];

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
                for (byte channelIndex = 0; channelIndex < channelCount; channelIndex++)
                {
                    GetSubMatrix(pixels, DctSize, channelIndex, subMatrix);
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
        var image = CompressedImage.Load(compressedImagePath);

        using var allQuantizedBytes =
            new MemoryStream(HuffmanCodec.Decode(image.CompressedBytes, image.DecodeTable, image.BitsCount));

        var blocksY = image.Height / DctSize;
        var blocksX = image.Width / DctSize;
        const int channelCount = 3;

        var blockData = new byte[blocksY * blocksX][];
        var globalY = new double[image.Height, image.Width];
        var globalCb = new double[image.Height, image.Width];
        var globalCr = new double[image.Height, image.Width];

        for (var i = 0; i < blocksY * blocksX; i++)
        {
            blockData[i] = new byte[DctSize * DctSize * channelCount];
            allQuantizedBytes.ReadExactly(blockData[i], 0, blockData[i].Length);
        }

        Parallel.For(0, blocksY * blocksX, index =>
        {
            var y = (index / blocksX) * DctSize;
            var x = (index % blocksX) * DctSize;

            var _y = new float[DctSize, DctSize];
            var cb = new float[DctSize, DctSize];
            var cr = new float[DctSize, DctSize];
            var tmp = new float[DctSize, DctSize];
            
            var quantizedBytes = blockData[index];

            for (var channelIndex = 0; channelIndex < channelCount; channelIndex++)
            {
                var channelBytes = new byte[DctSize * DctSize];
                Buffer.BlockCopy(quantizedBytes, channelIndex * DctSize * DctSize, channelBytes, 0, DctSize * DctSize);

                ZigZagUnScanAndDeQuantize(channelBytes, tmp);
                dct.IDCT2D(tmp, channelIndex == 0 ? _y : (channelIndex == 1 ? cb : cr));
                ShiftMatrixValues(channelIndex == 0 ? _y : (channelIndex == 1 ? cb : cr), 128);
            }


            for (var i = 0; i < DctSize; i++)
            for (var j = 0; j < DctSize; j++)
            {
                globalY[i + y, j + x] = _y[i, j];
                globalCb[i + y, j + x] = cb[i, j];
                globalCr[i + y, j + x] = cr[i, j];
            }
        });


        using var resultBmp = GetBitmap(image.Width, image.Height, globalY, globalCb, globalCr);
        resultBmp.Save(uncompressedImagePath, ImageFormat.Bmp);
    }

    private static void ShiftMatrixValues(float[,] subMatrix, int shiftValue)
    {
        for (var y = 0; y < DctSize; y++)
        for (var x = 0; x < DctSize; x++)
            subMatrix[y, x] += shiftValue;
    }

    private static void GetSubMatrix(Color[,] matrix, byte length, byte componentSelector, float[,] result)
    {
        for (var j = 0; j < length; j++)
        for (var i = 0; i < length; i++)
        {
            var pixel = matrix[i, j];
            if (componentSelector == 0)
                result[j, i] = (float)(0.299 * pixel.R + 0.587 * pixel.G + 0.114 * pixel.B - 128);
            else if (componentSelector == 1)
                result[j, i] = (float)(-0.168736 * pixel.R - 0.331264 * pixel.G + 0.5 * pixel.B + 128 - 128);
            else if (componentSelector == 2)
                result[j, i] = (float)(0.5 * pixel.R - 0.418688 * pixel.G - 0.081312 * pixel.B + 128 - 128);
        }
    }

    private static void ZigZagUnScanAndDeQuantize(byte[] quantizedBytes, float[,] result)
    {
        for (int i = 0; i < ZigZagUnOrder.Length; i++)
        {
            int y = i / DctSize;
            int x = i % DctSize;

            result[y, x] = (sbyte)quantizedBytes[ZigZagUnOrder[i]] * QuantizationMatrix[y, x];
        }
    }

    private static void QuantizeAndZigZagScan(float[,] channelFreqs, byte[] result)
    {
        for (int i = 0; i < ZigzagOrder.Length; i++)
        {
            int y = ZigzagOrder[i] / DctSize;
            int x = ZigzagOrder[i] % DctSize;

            result[i] = (byte)(channelFreqs[y, x] / QuantizationMatrix[y, x]);
        }
    }

    private static Bitmap GetBitmap(int width, int height, double[,] _y, double[,] cr, double[,] cb)
    {
        var bitmap = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

        var bitmapData = bitmap.LockBits(
            new Rectangle(0, 0, width, height),
            ImageLockMode.WriteOnly,
            System.Drawing.Imaging.PixelFormat.Format32bppArgb
        );

        unsafe
        {
            byte* scan0 = (byte*)bitmapData.Scan0;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    byte r = (byte)ToByte(_y[y, x] + 1.402 * (cr[y, x] - 128));
                    byte g = (byte)ToByte(_y[y, x] - 0.344136 * (cb[y, x] - 128) - 0.714136 * (cr[y, x] - 128));
                    byte b = (byte)ToByte(_y[y, x] + 1.772 * (cb[y, x] - 128));

                    int offset = y * bitmapData.Stride + x * 4;

                    scan0[offset] = r;
                    scan0[offset + 1] = g;
                    scan0[offset + 2] = b;
                    scan0[offset + 3] = 255;
                }
            }
        }

        bitmap.UnlockBits(bitmapData);

        return bitmap;
    }

    private static int ToByte(double d)
    {
        var val = (int)d;
        if (val > byte.MaxValue)
            return byte.MaxValue;
        if (val < byte.MinValue)
            return byte.MinValue;
        return val;
    }
}