using System;
using System.IO;
using POTCO.Editor;
using UnityEditor.AssetImporters;
using UnityEngine;

[ScriptedImporter(2, "rgb")]
public class RgbImporter : ScriptedImporter
{
    private const int SgiHeaderSize = 512;
    private const ushort SgiMagic = 0x01DA;

    public override void OnImportAsset(AssetImportContext ctx)
    {
        try
        {
            byte[] sgiData = File.ReadAllBytes(ctx.assetPath);
            if (sgiData == null || sgiData.Length < SgiHeaderSize)
            {
                DebugLogger.LogWarningEggImporter($"Invalid SGI .rgb file: {ctx.assetPath}");
                return;
            }

            ushort magic = ReadUInt16BE(sgiData, 0);
            if (magic != SgiMagic)
            {
                DebugLogger.LogWarningEggImporter($"Invalid SGI magic number in {ctx.assetPath}");
                return;
            }

            byte storage = sgiData[2]; // 0 = uncompressed, 1 = RLE
            byte bpc = sgiData[3]; // bytes per channel sample (1 or 2)
            ushort dimension = ReadUInt16BE(sgiData, 4);
            ushort width = ReadUInt16BE(sgiData, 6);
            ushort height = ReadUInt16BE(sgiData, 8);
            ushort channels = ReadUInt16BE(sgiData, 10);

            DebugLogger.LogEggImporter(
                $"SGI file {ctx.assetPath}: {width}x{height}, {channels} channels, storage={storage}, bpc={bpc}");

            if (dimension < 2 || width == 0 || height == 0 || channels == 0 || (storage != 0 && storage != 1))
            {
                DebugLogger.LogWarningEggImporter($"Unsupported SGI header values in {ctx.assetPath}");
                return;
            }

            if (bpc != 1 && bpc != 2)
            {
                DebugLogger.LogWarningEggImporter($"Unsupported SGI bytes-per-channel ({bpc}) in {ctx.assetPath}");
                return;
            }

            var pixels = new Color32[width * height];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = new Color32(0, 0, 0, 255);
            }

            if (storage == 0)
            {
                DecodeUncompressed(sgiData, pixels, width, height, channels, bpc);
            }
            else
            {
                DecodeRle(sgiData, pixels, width, height, channels);
            }

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = Path.GetFileNameWithoutExtension(ctx.assetPath),
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear
            };

            texture.SetPixels32(pixels);
            texture.Apply(updateMipmaps: false, makeNoLongerReadable: false);

            ctx.AddObjectToAsset("Texture", texture);
            ctx.SetMainObject(texture);
            DebugLogger.LogEggImporter($"Imported SGI texture: {ctx.assetPath} as {width}x{height}");
        }
        catch (Exception ex)
        {
            DebugLogger.LogErrorEggImporter($"Failed to import SGI file {ctx.assetPath}: {ex.Message}");
        }
    }

    private static void DecodeUncompressed(
        byte[] data,
        Color32[] pixels,
        int width,
        int height,
        int channels,
        int bpc)
    {
        int offset = SgiHeaderSize;
        int sampleSize = bpc;

        // SGI stores data in planar order: [channel][row][column].
        for (int channel = 0; channel < channels; channel++)
        {
            for (int y = 0; y < height; y++)
            {
                int destinationRow = height - 1 - y;
                for (int x = 0; x < width; x++)
                {
                    if (offset + sampleSize > data.Length)
                    {
                        return;
                    }

                    byte value;
                    if (bpc == 1)
                    {
                        value = data[offset];
                    }
                    else
                    {
                        ushort sample16 = ReadUInt16BE(data, offset);
                        value = (byte)(sample16 >> 8);
                    }

                    offset += sampleSize;
                    int pixelIndex = destinationRow * width + x;
                    SetChannelValue(ref pixels[pixelIndex], channel, channels, value);
                }
            }
        }
    }

    private static void DecodeRle(
        byte[] data,
        Color32[] pixels,
        int width,
        int height,
        int channels)
    {
        int rowCount = height * channels;
        int startTableOffset = SgiHeaderSize;
        int lengthTableOffset = startTableOffset + (rowCount * 4);

        if (lengthTableOffset + (rowCount * 4) > data.Length)
        {
            return;
        }

        var rowBuffer = new byte[width];

        for (int channel = 0; channel < channels; channel++)
        {
            for (int y = 0; y < height; y++)
            {
                int tableIndex = channel * height + y;
                int start = (int)ReadUInt32BE(data, startTableOffset + (tableIndex * 4));
                int length = (int)ReadUInt32BE(data, lengthTableOffset + (tableIndex * 4));

                if (start < 0 || length <= 0 || start >= data.Length)
                {
                    continue;
                }

                int end = Math.Min(data.Length, start + length);
                Array.Clear(rowBuffer, 0, rowBuffer.Length);
                DecodeRleRow(data, start, end, rowBuffer, width);

                int destinationRow = height - 1 - y;
                for (int x = 0; x < width; x++)
                {
                    int pixelIndex = destinationRow * width + x;
                    SetChannelValue(ref pixels[pixelIndex], channel, channels, rowBuffer[x]);
                }
            }
        }
    }

    private static void DecodeRleRow(byte[] data, int start, int end, byte[] destination, int width)
    {
        int offset = start;
        int x = 0;

        while (x < width && offset < end && offset < data.Length)
        {
            byte packet = data[offset++];
            int count = packet & 0x7F;
            if (count == 0)
            {
                break;
            }

            if ((packet & 0x80) != 0)
            {
                // Literal packet: copy the next `count` bytes directly.
                int readable = Math.Min(count, Math.Min(end - offset, data.Length - offset));
                int writable = Math.Min(readable, width - x);
                if (writable > 0)
                {
                    Buffer.BlockCopy(data, offset, destination, x, writable);
                    x += writable;
                }

                offset += count;
            }
            else
            {
                // Run packet: repeat one value `count` times.
                if (offset >= end || offset >= data.Length)
                {
                    break;
                }

                byte value = data[offset++];
                int runLength = Math.Min(count, width - x);
                for (int i = 0; i < runLength; i++)
                {
                    destination[x++] = value;
                }
            }
        }
    }

    private static void SetChannelValue(ref Color32 pixel, int channelIndex, int totalChannels, byte value)
    {
        if (totalChannels == 1)
        {
            pixel.r = value;
            pixel.g = value;
            pixel.b = value;
            pixel.a = 255;
            return;
        }

        if (totalChannels == 2)
        {
            if (channelIndex == 0)
            {
                pixel.r = value;
                pixel.g = value;
                pixel.b = value;
            }
            else if (channelIndex == 1)
            {
                pixel.a = value;
            }
            return;
        }

        // 3+ channels are treated as RGB(A) in channel order.
        switch (channelIndex)
        {
            case 0:
                pixel.r = value;
                break;
            case 1:
                pixel.g = value;
                break;
            case 2:
                pixel.b = value;
                break;
            case 3:
                pixel.a = value;
                break;
        }
    }

    private static ushort ReadUInt16BE(byte[] data, int offset)
    {
        return (ushort)((data[offset] << 8) | data[offset + 1]);
    }

    private static uint ReadUInt32BE(byte[] data, int offset)
    {
        return (uint)(
            (data[offset] << 24) |
            (data[offset + 1] << 16) |
            (data[offset + 2] << 8) |
            data[offset + 3]);
    }
}
