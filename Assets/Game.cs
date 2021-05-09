using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Game : MonoBehaviour
{

    public MeshCollider mapShape;

    public static Texture2D provinceMap;

    public static Data data;

    //Country Map
    public Material colorReplacer;
    public RenderTexture countryMapTemp;
    public RenderTexture countryMap;

    //Selected Province Map
    public Material provinceSelector;
    public RenderTexture selectedProvinceMapTemp;
    public RenderTexture selectedProvinceMap;

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

        return Data.colorToProvince[color];
    }

    // Start is called before the first frame update
    void Start()
    {
        data = new Data();
        data.FullLoad();

        countryMapTemp = new RenderTexture(provinceMap.width, provinceMap.height, 0, RenderTextureFormat.ARGB32);
        countryMapTemp.filterMode = FilterMode.Point;
        countryMapTemp.antiAliasing = 1;
        countryMapTemp.anisoLevel = 0;
        countryMapTemp.Create();

        countryMap = new RenderTexture(provinceMap.width, provinceMap.height, 0, RenderTextureFormat.ARGB32);
        countryMap.filterMode = FilterMode.Point;
        countryMap.antiAliasing = 1;
        countryMap.anisoLevel = 0;
        countryMap.Create();

        colorReplacer.SetTexture("_ProvincesTex", provinceMap);

        selectedProvinceMap = new RenderTexture(provinceMap.width, provinceMap.height, 0, RenderTextureFormat.ARGB32);
        selectedProvinceMap.filterMode = FilterMode.Point;
        selectedProvinceMap.antiAliasing = 1;
        selectedProvinceMap.anisoLevel = 0;
        selectedProvinceMap.Create();

        selectedProvinceMapTemp = new RenderTexture(provinceMap.width, provinceMap.height, 0, RenderTextureFormat.ARGB32);
        selectedProvinceMapTemp.filterMode = FilterMode.Point;
        selectedProvinceMapTemp.antiAliasing = 1;
        selectedProvinceMapTemp.anisoLevel = 0;
        selectedProvinceMapTemp.Create();

        provinceSelector.SetTexture("_ProvincesTex", provinceMap);
    }

    // Update is called once per frame
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

        //TODO do this for all at the start and then do once per controller change
        //(find position in province map that matches color to replace but write to country map that is already painted)
        //(is dynamic to allow stripes difference between owner and controller like hoi3)
        if (Input.GetMouseButtonDown(1))
        {
            List<Province> toPaint = new List<Province>();
            RenderTexture tmp = countryMapTemp;
            for (int i = 0; i < Data.provinces.Count; i += 1)
            {
                //Is land so add to paint list
                if (Data.provinces[i].isLand)
                {
                    toPaint.Add(Data.provinces[i]);
                }

                //Once have N to paint or reached last province
                if (toPaint.Count == 8 || i == Data.provinces.Count-1)
                {
                    for (int j = 0; j < toPaint.Count; j++)
                    {
                        colorReplacer.SetColor("_SearchColor" + (j+1), toPaint[j].color);
                        colorReplacer.SetColor("_NewColor" + (j+1), Data.tagToCountry[toPaint[j].controller].colorProper);
                    }

                    Graphics.Blit(countryMapTemp, countryMap, colorReplacer);

                    tmp = countryMapTemp;
                    countryMapTemp = countryMap;
                    countryMap = tmp;

                    toPaint.Clear();
                }
            }

            GameObject.Find("CountryMap").GetComponent<Renderer>().material.mainTexture = countryMap;
        }
    }
}
