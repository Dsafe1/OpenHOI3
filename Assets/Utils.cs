using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class Utils
{
    public static bool SameColor32(Color32 c1, Color32 c2)
    {
        return c1.r == c2.r && c1.g == c2.g && c1.b == c2.b;
    }

    //Array of pixels with facilitators to use with Unity SetPixels
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

        public PixelMatrix(PixelMatrix c)
        {

            this.width = c.width;
            this.height = c.height;
            this.pixels = (Color32[])c.pixels.Clone();

        }

        public PixelMatrix(int width, int height, Color32 clear)
        {

            //Initialize
            pixels = new Color32[width * height];
            this.width = width;
            this.height = height;

            //Clear with color
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = clear;
            }

        }

        public Color32 GetPixelSafe(int x, int y)
        {

            if (x < 0)
                x = 0;
            else if (x > width - 1)
                x = width - 1;

            if (y < 0)
                y = 0;
            else if (y > height - 1)
                y = height - 1;

            return pixels[x * width + y];

        }

        public Color32 GetPixel(int x, int y)
        {
            return pixels[x * width + y];
        }

        public void SetPixelSafe(int x, int y, Color32 cl)
        {

            //Check if outside
            if (x < 0 || x >= width || y < 0 || y >= height)
                return;

            pixels[x * width + y] = cl;

        }

        public void SetPixel(int x, int y, Color32 cl)
        {
            pixels[x * width + y] = cl;
        }

    }
}