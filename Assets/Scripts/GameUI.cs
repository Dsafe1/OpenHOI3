using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.IO;

using Pdoxcl2Sharp;

using UnityEngine;
using UnityEngine.UI;

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
                    //Load texture file (only tga allowed)
                    if (sprites.spriteTypes[s].texturefile != null)
                    {
                        if (File.Exists(dataPath + sprites.spriteTypes[s].texturefile))
                        {
                            Game.sprites.Add(sprites.spriteTypes[s].name, Utils.LoadTextureTGA(dataPath + sprites.spriteTypes[s].texturefile));
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
                    windowInstance.GetComponent<RectTransform>().position = new Vector3(windowType.position.x, Screen.height - windowType.position.y, 0);
                    windowInstance.GetComponent<RectTransform>().sizeDelta = new Vector2(windowType.size.x, windowType.size.y);

                    //Set name
                    windowInstance.name = windowType.name;

                    //Add to Canvas
                    windowInstance.transform.SetParent(GameObject.Find("Canvas").transform);

                    //Children Button
                    for (int c = 0; c < windowType.guiButtonType.Count; c++)
                    {
                        GuiButtonType guiButtonType = windowType.guiButtonType[c];

                        if (!sprites.ContainsKey(guiButtonType.quadTextureSprite))
                        {
                            Debug.Log(guiButtonType.quadTextureSprite + " texture not found");
                            continue;
                        }

                        Texture2D buttonText = sprites[guiButtonType.quadTextureSprite];

                        GameObject buttonInstance = Instantiate(buttonPrefab, Vector3.zero, Quaternion.identity);

                        buttonInstance.GetComponent<RectTransform>().position = new Vector3(guiButtonType.position.x, -guiButtonType.position.y, 0);
                        buttonInstance.GetComponent<RectTransform>().sizeDelta = new Vector2(buttonText.width, buttonText.height);

                        buttonInstance.GetComponent<Image>().sprite = Sprite.Create(buttonText, new Rect(0, 0, buttonText.width, buttonText.height), Vector2.zero);

                        buttonInstance.name = guiButtonType.name;

                        buttonInstance.transform.SetParent(windowInstance.transform);
                    }

                    //TODO Children gui

                    //TODO add to dicitonary
                }
            }
        }
    }
}