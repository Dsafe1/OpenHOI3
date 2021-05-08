using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MapPicker : MonoBehaviour
{

    //Collider to raycast
    public MeshCollider mapShape;

    //Texture to lookup
    public Texture2D provinceMap;

    //Province color dictionary
    public static Dictionary<Color32, Province> provinceColorDictionary;

    void Start() {
        // Setup province dictionary
        provinceColorDictionary = new Dictionary<Color32, Province>(20000);

        // Generate province for each unique color
        var pixels = provinceMap.GetPixels32();
        foreach(var pixel in pixels) {
            if (provinceColorDictionary.ContainsKey(pixel))
                continue;

            provinceColorDictionary.Add(pixel, new Province());
        }
    }

    void Update() {

        // TODO transform this into a request system by clicks detected elsewhere

        if (!Input.GetMouseButtonDown(0))
            return;

        var ray = Camera.main.ScreenPointToRay(Input.mousePosition);

        // Not on map
        if (!mapShape.Raycast(ray, out RaycastHit hit, float.PositiveInfinity))
            return;

        // Map the hit point on the quad to a pixel coordinate
        var mapSize = new Vector2(provinceMap.width, provinceMap.height);
        var hitPoint = Vector2Int.RoundToInt(Vector2.Scale(hit.textureCoord, mapSize));

        // Fetch the colour of the pixel we hit.
        var colour = (Color32)provinceMap.GetPixel(hitPoint.x, hitPoint.y);

        // Get province
        var territory = provinceColorDictionary[colour];

        // TODO: do something with the province we have clicked
        Debug.Log(territory + " " + colour);
    }
}