using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Game : MonoBehaviour
{

    public MeshCollider mapShape;
    public Texture2D colourMap;

    public static Data data;

    public Province ClickedProvince()
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

        return Data.colorToProvince[color];
    }

    // Start is called before the first frame update
    void Start()
    {
        data = new Data();
        data.FullLoad();
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Province selectedProvince = ClickedProvince();
            if (selectedProvince != null)
                Debug.Log(selectedProvince.name);
        }
    }
}
