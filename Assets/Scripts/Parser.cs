using System;
using Pdoxcl2Sharp;
using System.Collections.Generic;

public partial class GuiTypesWrapper : IParadoxRead, IParadoxWrite
{
    public GuiTypes guiTypes { get; set; }

    public GuiTypesWrapper()
    {
    }

    public void TokenCallback(ParadoxParser parser, string token)
    {
        switch (token)
        {
            case "guiTypes": guiTypes = parser.Parse(new GuiTypes()); break;
        }
    }

    public void Write(ParadoxStreamWriter writer)
    {
        if (guiTypes != null)
        {
            writer.Write("guiTypes", guiTypes);
        }
    }
}

public partial class GuiTypes : IParadoxRead, IParadoxWrite
{
    public IList<WindowType> windowType { get; set; }

    public GuiTypes()
    {
        windowType = new List<WindowType>();
    }

    public void TokenCallback(ParadoxParser parser, string token)
    {
        switch (token)
        {
            case "windowType": windowType.Add(parser.Parse(new WindowType())); break;
        }
    }

    public void Write(ParadoxStreamWriter writer)
    {
        foreach (var val in windowType)
        {
            writer.Write("windowType", val);
        }
    }
}

public partial class WindowType : IParadoxRead, IParadoxWrite
{
    public string name { get; set; }
    public Coordinates position { get; set; }
    public Coordinates size { get; set; }
    public IList<IconType> iconType { get; set; }
    public IList<InstantTextBoxType> instantTextBoxType { get; set; }
    public IList<TextBoxType> textBoxType { get; set; }
    public IList<GuiButtonType> guiButtonType { get; set; }

    public WindowType()
    {
        iconType = new List<IconType>();
        instantTextBoxType = new List<InstantTextBoxType>();
        textBoxType = new List<TextBoxType>();
        guiButtonType = new List<GuiButtonType>();
        position = new Coordinates();
        position.x = 0;
        position.y = 0;
    }

    public void TokenCallback(ParadoxParser parser, string token)
    {
        switch (token)
        {
            case "name": name = parser.ReadString(); break;
            case "position": position = parser.Parse(new Coordinates()); break;
            case "size": size = parser.Parse(new Coordinates()); break;
            case "iconType": iconType.Add(parser.Parse(new IconType())); break;
            case "textBoxType": textBoxType.Add(parser.Parse(new TextBoxType())); break;
            case "instantTextBoxType": instantTextBoxType.Add(parser.Parse(new InstantTextBoxType())); break;
            case "guiButtonType": guiButtonType.Add(parser.Parse(new GuiButtonType())); break;
        }
    }

    public void Write(ParadoxStreamWriter writer)
    {
        if (name != null)
        {
            writer.WriteLine("name", name, ValueWrite.Quoted);
        }
        if (position != null)
        {
            writer.Write("position", position);
        }
        if (size != null)
        {
            writer.Write("size", size);
        }
        foreach (var val in iconType)
        {
            writer.Write("iconType", val);
        }
        foreach (var val in textBoxType)
        {
            writer.Write("textBoxType", val);
        }
        foreach (var val in instantTextBoxType)
        {
            writer.Write("instantTextBoxType", val);
        }
        foreach (var val in guiButtonType)
        {
            writer.Write("guiButtonType", val);
        }
    }
}

public partial class IconType : IParadoxRead, IParadoxWrite
{
    public string name { get; set; }
    public string spriteType { get; set; }
    public Coordinates position { get; set; }

    public IconType()
    {
        position = new Coordinates();
        position.x = 0;
        position.y = 0;
    }

    public void TokenCallback(ParadoxParser parser, string token)
    {
        switch (token)
        {
            case "name": name = parser.ReadString(); break;
            case "spriteType": spriteType = parser.ReadString(); break;
            case "position": position = parser.Parse(new Coordinates()); break;
        }
    }

    public void Write(ParadoxStreamWriter writer)
    {
        if (name != null)
        {
            writer.WriteLine("name", name, ValueWrite.Quoted);
        }
        if (spriteType != null)
        {
            writer.WriteLine("spriteType", spriteType, ValueWrite.Quoted);
        }
        if (position != null)
        {
            writer.Write("position", position);
        }
    }
}

public partial class TextBoxType : IParadoxRead, IParadoxWrite
{
    public string name { get; set; }
    public Coordinates position { get; set; }

    public TextBoxType()
    {
        position = new Coordinates();
        position.x = 0;
        position.y = 0;
    }

    public void TokenCallback(ParadoxParser parser, string token)
    {
        switch (token)
        {
            case "name": name = parser.ReadString(); break;
            case "position": position = parser.Parse(new Coordinates()); break;
        }
    }

    public void Write(ParadoxStreamWriter writer)
    {
        if (name != null)
        {
            writer.WriteLine("name", name, ValueWrite.Quoted);
        }
        if (position != null)
        {
            writer.Write("position", position);
        }
    }
}

public partial class InstantTextBoxType : IParadoxRead, IParadoxWrite
{
    public string name { get; set; }
    public Coordinates position { get; set; }

    public InstantTextBoxType()
    {
        position = new Coordinates();
        position.x = 0;
        position.y = 0;
    }

    public void TokenCallback(ParadoxParser parser, string token)
    {
        switch (token)
        {
            case "name": name = parser.ReadString(); break;
            case "position": position = parser.Parse(new Coordinates()); break;
        }
    }

    public void Write(ParadoxStreamWriter writer)
    {
        if (name != null)
        {
            writer.WriteLine("name", name, ValueWrite.Quoted);
        }
        if (position != null)
        {
            writer.Write("position", position);
        }
    }
}

public partial class GuiButtonType : IParadoxRead, IParadoxWrite
{
    public string name { get; set; }
    public string quadTextureSprite { get; set; }
    public Coordinates position { get; set; }

    public GuiButtonType()
    {
        position = new Coordinates();
        position.x = 0;
        position.y = 0;
    }

    public void TokenCallback(ParadoxParser parser, string token)
    {
        switch (token)
        {
            case "name": name = parser.ReadString(); break;
            case "quadTextureSprite": quadTextureSprite = parser.ReadString(); break;
            case "position": position = parser.Parse(new Coordinates()); break;
        }
    }

    public void Write(ParadoxStreamWriter writer)
    {
        if (name != null)
        {
            writer.WriteLine("name", name, ValueWrite.Quoted);
        }
        if (quadTextureSprite != null)
        {
            writer.WriteLine("quadTextureSprite", quadTextureSprite, ValueWrite.Quoted);
        }
        if (position != null)
        {
            writer.Write("position", position);
        }
    }
}

public partial class Coordinates : IParadoxRead, IParadoxWrite
{
    public int x { get; set; }
    public int y { get; set; }

    public Coordinates()
    {
    }

    public void TokenCallback(ParadoxParser parser, string token)
    {
        switch (token)
        {
            case "x": x = parser.ReadInt32(); break;
            case "y": y = parser.ReadInt32(); break;
        }
    }

    public void Write(ParadoxStreamWriter writer)
    {
        writer.WriteLine("x", x);
        writer.WriteLine("y", y);
    }
}

public partial class SpriteTypesWrapper : IParadoxRead, IParadoxWrite
{
    public SpriteTypes spriteTypes { get; set; }

    public SpriteTypesWrapper()
    {
    }

    public void TokenCallback(ParadoxParser parser, string token)
    {
        switch (token)
        {
            case "spriteTypes": spriteTypes = parser.Parse(new SpriteTypes()); break;
        }
    }

    public void Write(ParadoxStreamWriter writer)
    {
        if (spriteTypes != null)
        {
            writer.Write("spriteTypes", spriteTypes);
        }
    }
}

public partial class SpriteTypes : IParadoxRead, IParadoxWrite
{
    public IList<SpriteType> spriteTypes { get; set; }

    public SpriteTypes()
    {
        spriteTypes = new List<SpriteType>();
    }

    public void TokenCallback(ParadoxParser parser, string token)
    {
        switch (token)
        {
            case "spriteType": spriteTypes.Add(parser.Parse(new SpriteType())); break;
        }
    }

    public void Write(ParadoxStreamWriter writer)
    {
        foreach (var val in spriteTypes)
        {
            writer.Write("spriteType", val);
        }
    }
}

public partial class SpriteType : IParadoxRead, IParadoxWrite
{
    public string name { get; set; }
    public string texturefile { get; set; }

    public SpriteType()
    {
    }

    public void TokenCallback(ParadoxParser parser, string token)
    {
        switch (token)
        {
            case "name": name = parser.ReadString(); break;
            case "texturefile": texturefile = parser.ReadString(); break;
        }
    }

    public void Write(ParadoxStreamWriter writer)
    {
        if (name != null)
        {
            writer.WriteLine("name", name, ValueWrite.Quoted);
        }
        if (texturefile != null)
        {
            writer.WriteLine("texturefile", texturefile, ValueWrite.Quoted);
        }
    }
}

public partial class Province : IParadoxRead, IParadoxWrite
{
    public string owner { get; set; }
    public string controller { get; set; }
    public string add_core { get; set; }
    public int points { get; set; }
    public double metal { get; set; }
    public double energy { get; set; }
    public double rare_materials { get; set; }
    public int industry { get; set; }
    public int infra { get; set; }

    public Province()
    {
    }

    public void TokenCallback(ParadoxParser parser, string token)
    {
        switch (token)
        {
            case "owner": owner = parser.ReadString(); break;
            case "controller": controller = parser.ReadString(); break;
            case "add_core": add_core = parser.ReadString(); break;
            case "points": points = parser.ReadInt32(); break;
            case "metal": metal = parser.ReadDouble(); break;
            case "energy": energy = parser.ReadDouble(); break;
            case "rare_materials": rare_materials = parser.ReadDouble(); break;
            case "industry": industry = parser.ReadInt32(); break;
            case "infra": infra = parser.ReadInt32(); break;
        }
    }

    public void Write(ParadoxStreamWriter writer)
    {
        if (owner != null)
        {
            writer.WriteLine("owner", owner);
        }
        if (controller != null)
        {
            writer.WriteLine("controller", controller);
        }
        if (add_core != null)
        {
            writer.WriteLine("add_core", add_core);
        }
        writer.WriteLine("points", points);
        writer.WriteLine("metal", metal);
        writer.WriteLine("energy", energy);
        writer.WriteLine("rare_materials", rare_materials);
        writer.WriteLine("industry", industry);
        writer.WriteLine("infra", infra);
    }
}

public partial class Country : IParadoxRead, IParadoxWrite
{
    public IList<string> color { get; set; }

    public Country()
    {
    }

    public void TokenCallback(ParadoxParser parser, string token)
    {
        switch (token)
        {
            case "color": color = parser.ReadStringList(); break;
        }
    }

    public void Write(ParadoxStreamWriter writer)
    {
        if (color != null)
        {
            writer.Write("color={ ");
            foreach (var val in color)
            {
                writer.Write(val);
                writer.Write(" ");
            }
            writer.WriteLine("}");
        }
    }
}