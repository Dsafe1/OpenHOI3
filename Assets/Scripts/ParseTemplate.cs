using System;
using Pdoxcl2Sharp;
using System.Collections.Generic;

namespace GeneratedParsers
{
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
            case "gui_types": guiTypes = parser.Parse(new GuiTypes()); break;
            }
        }

        public void Write(ParadoxStreamWriter writer)
        {
            if (guiTypes != null)
            {
                writer.Write("gui_types", guiTypes);
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
            case "window_type": windowType.Add(parser.Parse(new WindowType())); break;
            }
        }

        public void Write(ParadoxStreamWriter writer)
        {
            foreach(var val in windowType)
            {
                writer.Write("window_type", val);
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
        }

        public void TokenCallback(ParadoxParser parser, string token)
        {
            switch (token)
            {
            case "name": name = parser.ReadString(); break;
            case "position": position = parser.Parse(new Coordinates()); break;
            case "size": size = parser.Parse(new Coordinates()); break;
            case "icon_type": iconType.Add(parser.Parse(new IconType())); break;
            case "instant_text_box_type": instantTextBoxType.Add(parser.Parse(new InstantTextBoxType())); break;
            case "text_box_type": textBoxType.Add(parser.Parse(new TextBoxType())); break;
            case "gui_button_type": guiButtonType.Add(parser.Parse(new GuiButtonType())); break;
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
            foreach(var val in iconType)
            {
                writer.Write("icon_type", val);
            }
            foreach(var val in instantTextBoxType)
            {
                writer.Write("instant_text_box_type", val);
            }
            foreach(var val in textBoxType)
            {
                writer.Write("text_box_type", val);
            }
            foreach(var val in guiButtonType)
            {
                writer.Write("gui_button_type", val);
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
        }

        public void TokenCallback(ParadoxParser parser, string token)
        {
            switch (token)
            {
            case "name": name = parser.ReadString(); break;
            case "sprite_type": spriteType = parser.ReadString(); break;
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
                writer.WriteLine("sprite_type", spriteType, ValueWrite.Quoted);
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
        }

        public void TokenCallback(ParadoxParser parser, string token)
        {
            switch (token)
            {
            case "name": name = parser.ReadString(); break;
            case "quad_texture_sprite": quadTextureSprite = parser.ReadString(); break;
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
                writer.WriteLine("quad_texture_sprite", quadTextureSprite, ValueWrite.Quoted);
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
            case "sprite_types": spriteTypes = parser.Parse(new SpriteTypes()); break;
            }
        }

        public void Write(ParadoxStreamWriter writer)
        {
            if (spriteTypes != null)
            {
                writer.Write("sprite_types", spriteTypes);
            }
        }
    }

    public partial class SpriteTypes : IParadoxRead, IParadoxWrite
    {
        public IList<SpriteType> spriteType { get; set; }

        public SpriteTypes()
        {
            spriteType = new List<SpriteType>();
        }

        public void TokenCallback(ParadoxParser parser, string token)
        {
            switch (token)
            {
            case "sprite_type": spriteType.Add(parser.Parse(new SpriteType())); break;
            }
        }

        public void Write(ParadoxStreamWriter writer)
        {
            foreach(var val in spriteType)
            {
                writer.Write("sprite_type", val);
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

    public partial class ProvinceHistory : IParadoxRead, IParadoxWrite
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

        public ProvinceHistory()
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
        public string graphical_culture { get; set; }

        public Country()
        {
        }

        public void TokenCallback(ParadoxParser parser, string token)
        {
            switch (token)
            {
            case "color": color = parser.ReadStringList(); break;
            case "graphical_culture": graphical_culture = parser.ReadString(); break;
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
            if (graphical_culture != null)
            {
                writer.WriteLine("graphical_culture", graphical_culture);
            }
        }
    }

}