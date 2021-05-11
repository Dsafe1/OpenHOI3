using System;
using Pdoxcl2Sharp;
using System.Collections.Generic;

namespace GeneratedParsers
{
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
            case "sprite_type": spriteTypes.Add(parser.Parse(new SpriteType())); break;
            }
        }

        public void Write(ParadoxStreamWriter writer)
        {
            foreach(var val in spriteTypes)
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