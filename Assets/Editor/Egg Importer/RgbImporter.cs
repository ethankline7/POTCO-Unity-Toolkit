using UnityEngine;
using UnityEditor;
using UnityEditor.AssetImporters;
using System.IO;
using POTCO.Editor;

[ScriptedImporter(1, "rgb")]
public class RgbImporter : ScriptedImporter
{
    public override void OnImportAsset(AssetImportContext ctx)
    {
        try
        {
            byte[] sgiData = File.ReadAllBytes(ctx.assetPath);
            
            if (sgiData == null || sgiData.Length < 512)
            {
                DebugLogger.LogWarningEggImporter($"Invalid SGI .rgb file: {ctx.assetPath}");
                return;
            }

            // Parse SGI header (big-endian format)
            ushort magic = (ushort)((sgiData[0] << 8) | sgiData[1]);
            if (magic != 0x01DA)
            {
                DebugLogger.LogWarningEggImporter($"Invalid SGI magic number in {ctx.assetPath}");
                return;
            }

            byte storage = sgiData[2]; // 0 = uncompressed, 1 = RLE compressed
            byte bpc = sgiData[3]; // bytes per channel (1 or 2)
            ushort dimension = (ushort)((sgiData[4] << 8) | sgiData[5]);
            ushort width = (ushort)((sgiData[6] << 8) | sgiData[7]);
            ushort height = (ushort)((sgiData[8] << 8) | sgiData[9]);
            ushort channels = (ushort)((sgiData[10] << 8) | sgiData[11]);

            DebugLogger.LogEggImporter($"SGI file {ctx.assetPath}: {width}x{height}, {channels} channels, storage={storage}, bpc={bpc}");

            if (bpc != 1 || dimension < 2 || width == 0 || height == 0)
            {
                DebugLogger.LogWarningEggImporter($"Unsupported SGI format in {ctx.assetPath}");
                return;
            }

            // Create texture  
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.name = Path.GetFileNameWithoutExtension(ctx.assetPath);

            // Initialize all pixels to transparent black
            Color[] pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = new Color(0, 0, 0, 1);
            }

            if (storage == 0) // Uncompressed
            {
                DecodeSgiUncompressed(sgiData, pixels, width, height, channels);
            }
            else // RLE compressed
            {
                DecodeSgiRLE(sgiData, pixels, width, height, channels);
            }

            texture.SetPixels(pixels);
            texture.Apply();
            
            texture.wrapMode = TextureWrapMode.Repeat;
            texture.filterMode = FilterMode.Point; // Use point filtering to avoid blurred alpha edges
            
            ctx.AddObjectToAsset("Texture", texture);
            ctx.SetMainObject(texture);
            
            DebugLogger.LogEggImporter($"Imported SGI texture: {ctx.assetPath} as {width}x{height}");
        }
        catch (System.Exception ex)
        {
            DebugLogger.LogErrorEggImporter($"Failed to import SGI file {ctx.assetPath}: {ex.Message}");
        }
    }

    private void DecodeSgiUncompressed(byte[] data, Color[] pixels, int width, int height, int channels)
    {
        int offset = 512; // Skip header
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int pixelIndex = (height - 1 - y) * width + x; // Flip Y
                if (channels == 1)
                {
                    float gray = data[offset++] / 255.0f;
                    pixels[pixelIndex] = new Color(gray, gray, gray, 1.0f);
                }
                else
                {
                    float r = data[offset++] / 255.0f;
                    float g = data[offset++] / 255.0f;
                    float b = data[offset++] / 255.0f;
                    pixels[pixelIndex] = new Color(r, g, b, 1.0f);
                }
            }
        }
    }

    private void DecodeSgiRLE(byte[] data, Color[] pixels, int width, int height, int channels)
    {
        // SGI RLE uses offset tables at the beginning
        int tableOffset = 512;
        uint[] startTable = new uint[height * channels];
        
        // Read start offset table (big-endian)
        for (int i = 0; i < startTable.Length; i++)
        {
            startTable[i] = (uint)((data[tableOffset] << 24) | (data[tableOffset + 1] << 16) | 
                                  (data[tableOffset + 2] << 8) | data[tableOffset + 3]);
            tableOffset += 4;
        }

        // For SGI format, channels are stored as separate planes
        // For grayscale (1 channel), decode each row
        for (int y = 0; y < height; y++)
        {
            int rowIndex = y; // For 1 channel, just use row index
            if (rowIndex < startTable.Length)
            {
                int offset = (int)startTable[rowIndex];
                DecodeRLERow(data, pixels, offset, width, height - 1 - y, 0, channels); // Flip Y
            }
        }
    }

    private void DecodeRLERow(byte[] data, Color[] pixels, int offset, int width, int y, int channel, int totalChannels)
    {
        int x = 0;
        while (x < width && offset < data.Length)
        {
            byte count = data[offset++];
            if ((count & 0x80) != 0) // Copy literal bytes
            {
                int literalCount = count & 0x7F;
                for (int i = 0; i < literalCount && x < width && offset < data.Length; i++, x++)
                {
                    int pixelIndex = y * width + x;
                    float value = data[offset++] / 255.0f;
                    
                    if (totalChannels == 1)
                        pixels[pixelIndex] = new Color(value, value, value, 1.0f);
                    else
                        SetChannelValue(ref pixels[pixelIndex], channel, value);
                }
            }
            else // Run of repeated bytes
            {
                if (offset >= data.Length) break;
                byte value = data[offset++];
                float fValue = value / 255.0f;
                
                for (int i = 0; i < count && x < width; i++, x++)
                {
                    int pixelIndex = y * width + x;
                    if (totalChannels == 1)
                        pixels[pixelIndex] = new Color(fValue, fValue, fValue, 1.0f);
                    else
                        SetChannelValue(ref pixels[pixelIndex], channel, fValue);
                }
            }
        }
    }

    private void SetChannelValue(ref Color pixel, int channel, float value)
    {
        // For grayscale alpha masks, set all RGB channels to the same value
        if (channel == 0) // First/only channel becomes grayscale
        {
            pixel.r = value;
            pixel.g = value; 
            pixel.b = value;
            pixel.a = 1.0f;
        }
        else
        {
            switch (channel)
            {
                case 1: pixel.g = value; break;
                case 2: pixel.b = value; break;
                case 3: pixel.a = value; break;
            }
        }
    }
}