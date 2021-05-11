using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.IO;

using Pdoxcl2Sharp;

using UnityEngine;

public partial class Game : MonoBehaviour
{
    //GUI sprites by name
    public Dictionary<string, Texture2D> sprites = new Dictionary<string, Texture2D>();

    private void LoadGUISprites()
    {
        string dataPath = BaseDir + "Data/";
        string path = BaseDir + "Data/interface/";

        string[] gfxFiles = Directory.GetFiles(path, "*.gfx*", SearchOption.AllDirectories);

        for (int i = 0; i < gfxFiles.Length; i++)
        {
            using (FileStream fs = new FileStream(gfxFiles[i], FileMode.Open))
            {
                SpriteTypes sprites = ParadoxParser.Parse<SpriteTypes>(fs, new SpriteTypes());
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
                                this.sprites.Add(sprites.spriteTypes[s].name, Utils.LoadTextureDXT(dataPath + actualFile, TextureFormat.DXT5));
                            if (actualFile.Contains(".tga"))
                                this.sprites.Add(sprites.spriteTypes[s].name, Utils.LoadTextureTGA(dataPath + actualFile));
                        }
                    }
                }
            }
        }
    }
}