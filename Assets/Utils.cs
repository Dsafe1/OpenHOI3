using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System;

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
}