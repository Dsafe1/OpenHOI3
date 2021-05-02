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
    public static Dictionary<string, Province> nameToProvince = new Dictionary<string, Province>();

    //Base game directory (TODO may change on build)
    public static string BaseDir = Application.dataPath.Substring(0, Application.dataPath.LastIndexOf('/')+1);

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
            nameToProvince.Add(province.name, province);
        }
    }

    public void FullLoad()
    {
        LoadProvinceDefinition();

        // Load Map and associate with provinces

        // Generate adjacencies (and load custom ones)

        // Load Province history
    }

}