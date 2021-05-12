using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.IO;

using Pdoxcl2Sharp;

using UnityEngine;

public partial class Game : MonoBehaviour
{
    //GUI Prefabs
    public GameObject buttonPrefab;
    public GameObject imagePrefab;
    public GameObject textPrefab;
    public GameObject windowPrefab;

    //GUI sprites by name
    public static Dictionary<string, Texture2D> sprites = new Dictionary<string, Texture2D>();

    //GUI windows by name
    public static Dictionary<string, GameObject> windows = new Dictionary<string, GameObject>();

    private void LoadGUISprites()
    {
        string dataPath = BaseDir + "Data/";
        string path = BaseDir + "Data/interface/";

        string[] gfxFiles = Directory.GetFiles(path, "*.gfx*", SearchOption.AllDirectories);

        for (int i = 0; i < gfxFiles.Length; i++)
        {
            using (FileStream fs = new FileStream(gfxFiles[i], FileMode.Open))
            {
                SpriteTypesWrapper spritesWrapper = ParadoxParser.Parse<SpriteTypesWrapper>(fs, new SpriteTypesWrapper());
                SpriteTypes sprites = spritesWrapper.spriteTypes;

                if (sprites == null)
                    continue;

                for (int s = 0; s < sprites.spriteTypes.Count; s++)
                {
                    //Load texture file
                    //(for some reason sometimes files are defined as dds but are actually tga, and vice-versa)
                    //(similarly, some files defined dont exist at all)
                    if (sprites.spriteTypes[s].texturefile != null)
                    {
                        string actualFile = sprites.spriteTypes[s].texturefile;

                        if (!File.Exists(dataPath + actualFile))
                        {
                            if(actualFile.Contains(".dds"))
                                actualFile = actualFile.Replace(".dds", ".tga");
                            else
                                actualFile = actualFile.Replace(".tga", ".dds");
                        }

                        bool existsFile = File.Exists(dataPath + actualFile);

                        if (existsFile)
                        {
                            if (actualFile.Contains(".dds"))
                                Game.sprites.Add(sprites.spriteTypes[s].name, Utils.LoadTextureDXT(dataPath + actualFile, TextureFormat.DXT5));
                            if (actualFile.Contains(".tga"))
                                Game.sprites.Add(sprites.spriteTypes[s].name, Utils.LoadTextureTGA(dataPath + actualFile));
                        }
                    }
                }
            }
        }
    }

    private void LoadGUIWindows()
    {
        string path = BaseDir + "Data/interface/";

        string[] guiFiles = Directory.GetFiles(path, "*.gui*", SearchOption.AllDirectories);

        for (int i = 0; i < guiFiles.Length; i++)
        {
            using (FileStream fs = new FileStream(guiFiles[i], FileMode.Open))
            {
                GuiTypesWrapper guiTypesWrapper = ParadoxParser.Parse<GuiTypesWrapper>(fs, new GuiTypesWrapper());
                GuiTypes guiTypes = guiTypesWrapper.guiTypes;

                for (int w = 0; w < guiTypes.windowType.Count; w++)
                {
                    WindowType windowType = guiTypes.windowType[w];

                    //Instance
                    GameObject windowInstance = Instantiate(windowPrefab, Vector3.zero, Quaternion.identity);

                    //Set dimensions
                    windowInstance.GetComponent<RectTransform>().position = new Vector3(windowType.position.x, Screen.height-windowType.position.y, 0);
                    windowInstance.GetComponent<RectTransform>().sizeDelta = new Vector2(windowType.size.x, windowType.size.y);

                    //Set name
                    windowInstance.name = windowType.name;

                    //Add to Canvas
                    windowInstance.transform.SetParent(GameObject.Find("Canvas").transform);

                    //TODO children GUI

                    //TODO add to dicitonary
                }
            }
        }
    }
}