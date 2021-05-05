using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.IO;

using Pdoxcl2Sharp;

using UnityEngine;

public class Data
{
    //Countries
    public static List<Country> countries = new List<Country>();
    public static Dictionary<string, Country> tagToCountry = new Dictionary<string, Country>();

    //Provinces
    public static List<Province> provinces = new List<Province>();
    public static Dictionary<Color32, Province> colorToProvince = new Dictionary<Color32, Province>();
    public static Dictionary<int, Province> idToProvince = new Dictionary<int, Province>();

    //Base game directory (TODO may change on build)
    public static string BaseDir = Application.dataPath.Substring(0, Application.dataPath.LastIndexOf('/') + 1);

    //Load province definitions and create them - definition.csv
    private void LoadProvinceDefinition()
    {
        string path = BaseDir + "Data/map/definition.csv";

        //Create provinces
        System.IO.StreamReader file = new System.IO.StreamReader(path);
        string line;
        while ((line = Utils.ReadLineUncommented(file.ReadLine())) != null)
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

    //Load custom province adjacencies - adjacencies.csv
    private void LoadProvinceAdjacencies()
    {
        string path = BaseDir + "Data/map/adjacencies.csv";

        System.IO.StreamReader file = new System.IO.StreamReader(path);
        string line;
        while ((line = Utils.ReadLineUncommented(file.ReadLine())) != null)
        {
            // Check first char is number (ignore first line and others)
            char firstChar = line[0];
            if (char.IsDigit(firstChar))
            {
                string[] parts = line.Split(';');

                // One Way

                Connection way1 = new Connection();
                way1.neighbor = idToProvince[int.Parse(parts[1])];
                switch (parts[2])
                {
                    case "sea":
                        way1.type = Connection.Type.SEA;
                        break;
                    case "canal":
                        way1.type = Connection.Type.CANAL;
                        break;
                    //Unknown type, ignore
                    default:
                        continue;
                }
                way1.through = idToProvince[int.Parse(parts[3])];

                idToProvince[int.Parse(parts[0])].connections.Add(way1);

                // Two Way

                Connection way2 = new Connection();
                way2.neighbor = idToProvince[int.Parse(parts[0])];
                switch (parts[2])
                {
                    case "sea":
                        way2.type = Connection.Type.SEA;
                        break;
                    case "canal":
                        way2.type = Connection.Type.CANAL;
                        break;
                    //Unknown type, ignore
                    default:
                        continue;
                }
                way2.through = idToProvince[int.Parse(parts[3])];

                idToProvince[int.Parse(parts[1])].connections.Add(way2);
            }
        }
    }

    //Load country definition - countries.txt
    private void LoadCountriesDefinition()
    {
        string path = BaseDir + "Data/common/countries.txt";

        System.IO.StreamReader file = new System.IO.StreamReader(path);
        string line;
        while ((line = Utils.ReadLineUncommented(file.ReadLine())) != null)
        {
            string[] parts = line.Split('=');

            if (parts.Length != 2)
                continue;

            string tag = Utils.RemoveWhitespace(parts[0]);

            string countryFile = parts[1].Substring(parts[1].IndexOf('/') + 1, parts[1].IndexOf('.') - parts[1].IndexOf('/')-1);

            Country country = new Country();
            country.name = countryFile;
            country.tag = tag;
            countries.Add(country);
        }

        //Add to dictionaries
        foreach (Country country in countries)
        {
            tagToCountry.Add(country.tag, country);
        }
    }

    //Load countries proper - countries/[country].txt
    private void LoadCountriesProper()
    {
        string path = BaseDir + "Data/common/countries/";

        foreach (var country in countries)
        {
            Parsers.Country partialCountry;
            using (FileStream fs = new FileStream(path + country.name + ".txt", FileMode.Open))
            {
                partialCountry = ParadoxParser.Parse<Parsers.Country>(fs, new Parsers.Country());
            }

            //TODO load rest of data
            country.color = new Color32(byte.Parse(partialCountry.color[0]), byte.Parse(partialCountry.color[1]), byte.Parse(partialCountry.color[2]), 255);
        }
    }

    //Generate pixel province connections
    private void GenerateProvinceAdjancencies()
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
            //Pre calculate y offset - PERFORMANCE
            int yOffset = y * texWidth;

            for (int x = 0; x < texWidth; x++)
            {
                //Current color
                Color32 currColor = tex.GetPixelPreOffset(yOffset, x);

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
                neighborColors[0] = tex.GetPixelPreOffset(yOffset - 1, x);
                neighborColors[1] = tex.GetPixelPreOffset(yOffset + 1, x);

                //Special cases wrap around horizontally on x axis
                if (x == 0)
                    neighborColors[2] = tex.GetPixelPreOffset(yOffset, texWidth - 1);
                else
                    neighborColors[2] = tex.GetPixelPreOffset(yOffset, x - 1);

                if (x == texWidth - 1)
                    neighborColors[3] = tex.GetPixelPreOffset(yOffset, 0);
                else
                    neighborColors[3] = tex.GetPixelPreOffset(yOffset, x + 1);

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
                            connection.type = Connection.Type.REGULAR;
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

        LoadProvinceAdjacencies();

        LoadCountriesDefinition();

        LoadCountriesProper();

        //TODO Load Province history
    }

}