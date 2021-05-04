using System.Collections;
using System.Collections.Generic;
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

    }

    public static string ReadLineUncommented(string line)
    {
        if (line == null)
            return null;
        return line.Split('#')[0];
    }
}