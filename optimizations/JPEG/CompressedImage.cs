using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace JPEG;

public class CompressedImage
{
    public int Width { get; set; }
    public int Height { get; set; }

    public int Quality { get; set; }

    public Dictionary<BitsWithLength, byte> DecodeTable { get; set; }

    public long BitsCount { get; set; }
    public byte[] CompressedBytes { get; set; }

    public void Save(string path)
    {
        using var sw = new FileStream(path, FileMode.Create);
        Span<byte> buffer = stackalloc byte[8];

        var width = Width;
        MemoryMarshal.Write(buffer, ref width);
        sw.Write(buffer.Slice(0, 4));

        var height = Height;
        MemoryMarshal.Write(buffer, ref height);
        sw.Write(buffer.Slice(0, 4));

        var quality = Quality;
        MemoryMarshal.Write(buffer, ref quality);
        sw.Write(buffer.Slice(0, 4));

        int decodeTableCount = DecodeTable.Count;
        MemoryMarshal.Write(buffer, ref decodeTableCount);
        sw.Write(buffer.Slice(0, 4));

        foreach (var kvp in DecodeTable)
        {
            int bits = kvp.Key.Bits;
            MemoryMarshal.Write(buffer, ref bits);
            sw.Write(buffer.Slice(0, 4));

            int bitsCount = kvp.Key.BitsCount;
            MemoryMarshal.Write(buffer, ref bitsCount);
            sw.Write(buffer.Slice(0, 4));

            sw.WriteByte(kvp.Value);
        }

        var count = BitsCount;
        MemoryMarshal.Write(buffer, ref count);
        sw.Write(buffer.Slice(0, 8));

        int compressedBytesLength = CompressedBytes.Length;
        MemoryMarshal.Write(buffer, ref compressedBytesLength);
        sw.Write(buffer.Slice(0, 4));

        sw.Write(CompressedBytes);
    }

    public static CompressedImage Load(string path)
    {
        var result = new CompressedImage();
        using var sr = new FileStream(path, FileMode.Open);
        
        Span<byte> buffer = stackalloc byte[16];

        sr.Read(buffer.Slice(0, 4));
        result.Width = MemoryMarshal.Read<int>(buffer);

        sr.Read(buffer.Slice(0, 4));
        result.Height = MemoryMarshal.Read<int>(buffer);

        sr.Read(buffer.Slice(0, 4));
        result.Quality = MemoryMarshal.Read<int>(buffer);

        sr.Read(buffer.Slice(0, 4));
        int decodeTableSize = MemoryMarshal.Read<int>(buffer);
        result.DecodeTable = new Dictionary<BitsWithLength, byte>(decodeTableSize, new BitsWithLength.Comparer());

        for (var i = 0; i < decodeTableSize; i++)
        {
            sr.Read(buffer.Slice(0, 4));
            var bits = MemoryMarshal.Read<int>(buffer);

            sr.Read(buffer.Slice(0, 4));
            var bitsCount = MemoryMarshal.Read<int>(buffer);

            var mappedByte = (byte)sr.ReadByte();
            result.DecodeTable[new BitsWithLength { Bits = bits, BitsCount = bitsCount }] = mappedByte;
        }

        sr.Read(buffer.Slice(0, 8));
        result.BitsCount = MemoryMarshal.Read<long>(buffer);

        sr.Read(buffer.Slice(0, 4));
        int compressedBytesCount = MemoryMarshal.Read<int>(buffer);

        result.CompressedBytes = new byte[compressedBytesCount];
        int totalRead = 0;
        while (totalRead < compressedBytesCount)
        {
            totalRead += sr.Read(result.CompressedBytes.AsSpan(totalRead, compressedBytesCount - totalRead));
        }

        return result;
    }
}