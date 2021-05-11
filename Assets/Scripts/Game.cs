using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.IO;

using Pdoxcl2Sharp;

using UnityEngine;

public partial class Game : MonoBehaviour
{
    public MeshCollider mapShape;

    public static Texture2D provinceMap;

    //Country Map
    public Material singleColorReplacer;
    public Material multiColorReplacer;
    public static RenderTexture countryMapTemp;
    public static RenderTexture countryMap;

    //Selected Province Map
    public Material provinceSelector;
    public static RenderTexture selectedProvinceMapTemp;
    public static RenderTexture selectedProvinceMap;

    //Border Map
    public Material border;
    public static RenderTexture borderMapTemp;
    public static RenderTexture borderMap;

    //Countries
    public static List<Country> countries = new List<Country>();
    public static Dictionary<string, Country> tagToCountry = new Dictionary<string, Country>();

    //Provinces
    public static List<Province> provinces = new List<Province>();
    public static Dictionary<Color32, Province> colorToProvince = new Dictionary<Color32, Province>();
    public static Dictionary<int, Province> idToProvince = new Dictionary<int, Province>();

    //Base game directory
    public static string BaseDir;

    //Get province under mouse
    public Province ProvinceOnMouse()
    {
        var ray = Camera.main.ScreenPointToRay(Input.mousePosition);

        // Not on map
        if (!mapShape.Raycast(ray, out RaycastHit hit, float.PositiveInfinity))
            return null;

        // Map the hit point on the quad to a pixel coordinate
        var mapSize = new Vector2(provinceMap.width, provinceMap.height);
        var hitPoint = Vector2Int.RoundToInt(Vector2.Scale(hit.textureCoord, mapSize));

        // Fetch the colour of the pixel we hit.
        var color = (Color32)provinceMap.GetPixel(hitPoint.x, hitPoint.y);

        // White is special color for void
        if (color == Color.white)
            return null;

        return colorToProvince[color];
    }

    //Change single province controller
    private void ChangeSingleProvinceControllerMap(Color32 search, Color32 replace)
    {
        singleColorReplacer.SetColor("_SearchColor", search);
        singleColorReplacer.SetColor("_NewColor", replace);
        Graphics.Blit(countryMapTemp, countryMap, singleColorReplacer);
        GameObject.Find("CountryMap").GetComponent<Renderer>().material.mainTexture = countryMap;
    }

    //Full load
    public void FullLoad()
    {
        LoadMapImages();

        LoadProvinceDefinition();

        GenerateProvinceAdjancencies();

        CalculateProvinceCentroids();

        LoadProvinceAdjacencies();

        LoadCountriesDefinition();

        LoadCountriesProper();

        LoadProvinceHistory();

        GenerateStartingCountryMap();

        GenerateProvinceBorderMap();

        LoadGUISprites();
    }

    void Awake()
    {
        //Setup base dir
        BaseDir = Application.dataPath.Substring(0, Application.dataPath.LastIndexOf('/') + 1);
    }

    void Start()
    {
        FullLoad();
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Province selectedProvince = ProvinceOnMouse();
            if (selectedProvince != null)
            {
                Debug.Log(selectedProvince.id + " - " + selectedProvince.controller + " - " + selectedProvince.color.ToString());

                //Province selector effect
                provinceSelector.SetColor("_SearchColor", selectedProvince.color);
                Graphics.Blit(selectedProvinceMapTemp, selectedProvinceMap, provinceSelector);
                GameObject.Find("SelectedProvinceMap").GetComponent<Renderer>().material.mainTexture = selectedProvinceMap;
            }
        }
    }
}
