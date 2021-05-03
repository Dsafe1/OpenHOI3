using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Data
{
    //Provinces
    public static List<Province> provinces = new List<Province>();
    public static Dictionary<Color32, Province> colorToProvince = new Dictionary<Color32, Province>();
    public static Dictionary<int, Province> idToProvince = new Dictionary<int, Province>();

    //Base game directory (TODO may change on build)
    public static string BaseDir = Application.dataPath.Substring(0, Application.dataPath.LastIndexOf('/') + 1);

    private void LoadProvinceDefinition()
    {
        string path = BaseDir + "/Data/map/definition.csv";

        //Create provinces
        System.IO.StreamReader file = new System.IO.StreamReader(path);
        string line;
        while ((line = file.ReadLine()) != null)
        {
            // Check first char is number (ignore first line and others)
            char firstChar = line[0];
            if (char.IsDigit(firstChar))
            {
                string[] parts = line.Split(';');

                Province newProvince = new Province();

                newProvince.id = int.Parse(parts[0]);
                newProvince.color = new Color32(byte.Parse(parts[1]), byte.Parse(parts[2]), byte.Parse(parts[3]), 255);
                newProvince.name = parts[4];

                provinces.Add(newProvince);
            }
        }

        //Add to dictionaries
        foreach (Province province in provinces)
        {
            colorToProvince.Add(province.color, province);
            idToProvince.Add(province.id, province);
        }
    }

    public void GenerateProvinceAdjancencies()
    {
        //Get pixel mat
        Utils.PixelMatrix tex = new Utils.PixelMatrix(GameObject.Find("Game").GetComponent<Game>().colourMap);
        int texWidth = tex.width;
        int texHeight = tex.height;

        //Re-use dictionary - PERFORMANCE
        Dictionary<Color32, Province> reuseColorToProvince = new Dictionary<Color32, Province>();

        //Previous pixel data - PERFORMANCE
        Color32 prevColor = Color.black;
        Province prevProvince = null;

        for (int y = 1; y < texHeight - 1; y++)
        {
            for (int x = 0; x < texWidth; x++)
            {
                //Current color
                Color32 currColor = tex.GetPixel(y, x);

                //Ignore white color
                if (currColor.r == 255 && currColor.g == 255 && currColor.b == 255)
                    continue;

                //Province with current color
                Province currProvince = null;

                //Look at previous province color - PERFORMANCE
                if (Utils.SameColor32(prevColor, currColor))
                    currProvince = prevProvince;

                //Look at reuse provinces dictionary - PERFORMANCE
                bool wasInReuse = false;
                if (currProvince == null)
                {
                    try
                    {
                        currProvince = reuseColorToProvince[currColor];
                    }
                    catch { }
                }

                //Look at complete dictionary
                if (currProvince == null)
                {
                    try
                    {
                        currProvince = colorToProvince[currColor];
                    }
                    catch { }
                }
                else
                    wasInReuse = true;

                //No province with this color
                if (currProvince == null)
                    continue;

                //Neighbor colors (NSEW)
                Color32[] neighborColors = new Color32[4];
                neighborColors[0] = tex.GetPixel(y - 1, x);
                neighborColors[1] = tex.GetPixel(y + 1, x);

                //Special cases wrap around horizontally on x axis
                if (x == 0)
                    neighborColors[2] = tex.GetPixel(y, texWidth - 1);
                else
                    neighborColors[2] = tex.GetPixel(y, x - 1);

                if (x == texWidth - 1)
                    neighborColors[3] = tex.GetPixel(y, 0);
                else
                    neighborColors[3] = tex.GetPixel(y, x + 1);

                //Setup neighbor connections
                for (int i = 0; i < neighborColors.Length; i++)
                {
                    Color32 cl = neighborColors[i];
                    if (!Utils.SameColor32(currColor, cl))
                    {
                        Province neighbor = null;

                        if (Utils.SameColor32(prevColor, cl))
                            neighbor = prevProvince;

                        if (neighbor == null)
                        {
                            try
                            {
                                neighbor = reuseColorToProvince[cl];
                            }
                            catch { }
                        }

                        if (neighbor == null)
                        {
                            try
                            {
                                neighbor = colorToProvince[cl];
                            }
                            catch { }
                        }

                        if (neighbor == null)
                            continue;

                        if (!currProvince.GetConnectedProvinces().Contains(neighbor))
                        {
                            Connection connection = new Connection();
                            connection.neighbor = neighbor;
                            currProvince.connections.Add(connection);
                        }
                    }
                }

                //Store in reuse dictionary
                if (!wasInReuse)
                    reuseColorToProvince[currColor] = currProvince;

                //Current becomes previous
                prevColor = currColor;
                prevProvince = currProvince;
            }
        }
    }

    public void FullLoad()
    {
        LoadProvinceDefinition();

        GenerateProvinceAdjancencies();

        // TODO Load costum adjancencies.csv

        // TODO Load Province history
    }

}