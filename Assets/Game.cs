using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Game : MonoBehaviour
{

    public MeshCollider mapShape;
    public Texture2D colourMap;

    public static Data data;

    public Province ProvinceOnMouse()
    {
        var ray = Camera.main.ScreenPointToRay(Input.mousePosition);

        // Not on map
        if (!mapShape.Raycast(ray, out RaycastHit hit, float.PositiveInfinity))
            return null;

        // Map the hit point on the quad to a pixel coordinate
        var mapSize = new Vector2(colourMap.width, colourMap.height);
        var hitPoint = Vector2Int.RoundToInt(Vector2.Scale(hit.textureCoord, mapSize));

        // Fetch the colour of the pixel we hit.
        var color = (Color32)colourMap.GetPixel(hitPoint.x, hitPoint.y);

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

        countryTex = new RenderTexture(colourMap.width, colourMap.height, 0, RenderTextureFormat.ARGB32);
        countryTex.filterMode = FilterMode.Point;
        countryTex.antiAliasing = 1;
        countryTex.anisoLevel = 0;
        countryTex.Create();

        countryTex2 = new RenderTexture(colourMap.width, colourMap.height, 0, RenderTextureFormat.ARGB32);
        countryTex2.filterMode = FilterMode.Point;
        countryTex2.antiAliasing = 1;
        countryTex2.anisoLevel = 0;
        countryTex2.Create();
    }

    RenderTexture countryTex;
    RenderTexture countryTex2;
    public Material colorReplacer;

    // Update is called once per frame
    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Province selectedProvince = ProvinceOnMouse();
            if (selectedProvince != null)
            {
                Debug.Log(selectedProvince.id + " - " + selectedProvince.controller + " - " + selectedProvince.color.ToString());
            }
        }

        //TODO do this for all at the start and then do once per controller change
        //(find position in province map that matches color to replace but write to country map that is already painted)
        //(is dynamic to allow stripes difference between owner and controller like hoi3)
        if (Input.GetMouseButtonDown(1))
        {
            colorReplacer.SetTexture("_ProvincesTex", colourMap);
            //TODO Optimization, dont need to do this for sea or white provinces
            //TODO Optimization, shader cant do too much branching but surely can replace 5/10/15 colors at a time (benchmark)
            for (int i = 0; i < Data.provinces.Count; i++)
            {
                if(Data.provinces[i].controller == null)
                    continue;

                colorReplacer.SetColor("_SearchColor", Data.provinces[i].color);
                colorReplacer.SetColor("_NewColor", Data.tagToCountry[Data.provinces[i].controller].colorProper);

                Graphics.Blit(countryTex, countryTex2, colorReplacer);

                RenderTexture tmp = countryTex;
                countryTex = countryTex2;
                countryTex2 = tmp;
            }
            GameObject.Find("CountryMap").GetComponent<Renderer>().material.mainTexture = countryTex2;
        }
    }
}
