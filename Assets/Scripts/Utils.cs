using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System;

using B83.Image.BMP;

using UnityEngine;

public static class Utils
{
    public static bool SameColor32(Color32 c1, Color32 c2)
    {
        return c1.r == c2.r && c1.g == c2.g && c1.b == c2.b;
    }

    public class PixelMatrix
    {

        public Color32[] pixels;
        public int width;
        public int height;

        public PixelMatrix(Texture2D texture)
        {

            this.pixels = texture.GetPixels32();
            this.width = texture.width;
            this.height = texture.height;

        }

        public Color32 GetPixel(int y, int x)
        {
            return pixels[y * width + x];
        }

        public Color32 GetPixelPreOffset(int yoffset, int x)
        {
            return pixels[yoffset + x];
        }

        public void SetPixel(int y, int x, Color32 cl)
        {
            pixels[y * width + x] = cl;
        }

        public void SetPixelPreOffset(int yoffset, int x, Color32 cl)
        {
            pixels[yoffset + x] = cl;
        }

    }

    public static string ReadLineUncommented(string line)
    {
        if (line == null)
            return null;
        return line.Split('#')[0];
    }

    public static string RemoveWhitespace(string input)
    {
        return new string(input.ToCharArray()
            .Where(c => !Char.IsWhiteSpace(c))
            .ToArray());
    }

    public static Stream GenerateStreamFromString(string s)
    {
        var stream = new MemoryStream();
        var writer = new StreamWriter(stream);
        writer.Write(s);
        writer.Flush();
        stream.Position = 0;
        return stream;
    }

    public static Texture2D LoadTextureBMP(string filePath)
    {
        Texture2D tex = null;
        byte[] fileData;

        if (File.Exists(filePath))
        {
            fileData = File.ReadAllBytes(filePath);

            BMPLoader bmpLoader = new BMPLoader();
            //bmpLoader.ForceAlphaReadWhenPossible = true; //Uncomment to read alpha too

            //Load the BMP data
            BMPImage bmpImg = bmpLoader.LoadBMP(fileData);

            //Convert the Color32 array into a Texture2D
            tex = bmpImg.ToTexture2D();
        }
        return tex;
    }

    public static Texture2D LoadTextureTGA(string fileName)
    {
        using (var imageFile = File.OpenRead(fileName))
        {
            return LoadTextureTGA(imageFile);
        }
    }

    public static Texture2D LoadTextureTGA(Stream TGAStream)
    {

        using (BinaryReader r = new BinaryReader(TGAStream))
        {
            // Skip some header info we don't care about.
            // Even if we did care, we have to move the stream seek point to the beginning,
            // as the previous method in the workflow left it at the end.
            r.BaseStream.Seek(12, SeekOrigin.Begin);

            short width = r.ReadInt16();
            short height = r.ReadInt16();
            int bitDepth = r.ReadByte();

            // Skip a byte of header information we don't care about.
            r.BaseStream.Seek(1, SeekOrigin.Current);

            Texture2D tex = new Texture2D(width, height);
            Color32[] pulledColors = new Color32[width * height];

            if (bitDepth == 32)
            {
                for (int i = 0; i < width * height; i++)
                {
                    byte red = r.ReadByte();
                    byte green = r.ReadByte();
                    byte blue = r.ReadByte();
                    byte alpha = r.ReadByte();

                    pulledColors[i] = new Color32(blue, green, red, alpha);
                }
            }
            else if (bitDepth == 24)
            {
                for (int i = 0; i < width * height; i++)
                {
                    byte red = r.ReadByte();
                    byte green = r.ReadByte();
                    byte blue = r.ReadByte();

                    pulledColors[i] = new Color32(blue, green, red, 1);
                }
            }
            else
            {
                throw new Exception("TGA texture had non 32/24 bit depth.");
            }

            tex.SetPixels32(pulledColors);
            tex.Apply();
            return tex;

        }
    }

    public static Texture2D LoadTextureDXT(string file, TextureFormat textureFormat)
    {
        if (textureFormat != TextureFormat.DXT1 && textureFormat != TextureFormat.DXT5)
            throw new Exception("Invalid TextureFormat. Only DXT1 and DXT5 formats are supported by this method.");

        byte[] ddsBytes = File.ReadAllBytes(file);

        byte ddsSizeCheck = ddsBytes[4];
        if (ddsSizeCheck != 124)
            throw new Exception("Invalid DDS DXTn texture. Unable to read");  //this header byte should be 124 for DDS image files

        int height = ddsBytes[13] * 256 + ddsBytes[12];
        int width = ddsBytes[17] * 256 + ddsBytes[16];

        int DDS_HEADER_SIZE = 128;
        byte[] dxtBytes = new byte[ddsBytes.Length - DDS_HEADER_SIZE];
        Buffer.BlockCopy(ddsBytes, DDS_HEADER_SIZE, dxtBytes, 0, ddsBytes.Length - DDS_HEADER_SIZE);

        Texture2D texture = new Texture2D(width, height, textureFormat, false);
        texture.LoadRawTextureData(dxtBytes);
        texture.Apply();

        return (texture);
    }
}