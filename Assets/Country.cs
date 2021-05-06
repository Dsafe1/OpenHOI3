using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public partial class Country
{
    //Definition
    public Color32 colorProper;
    public string tag;
    public string name;

    public Texture2D flag;

    public List<Province> controlled = new List<Province>();
}