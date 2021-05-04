using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Connection
{
    public bool isLand;
    public Province neighbor;
    //TODO rivers
    public enum Type
    {
        //Automatic
        REGULAR,
        //Special - adjacencies.csv
        SEA,
        CANAL
    }
    public Type type;
    //Province passed through if special type of connection
    public Province through;
}

public class Province
{
    //Definition
    public int id;
    public Color32 color;
    public string name;

    public List<Connection> connections = new List<Connection>();

    public GeneratedParsers.ProvinceHistory provinceHistory;

    public List<Province> GetConnectedProvinces()
    {
        List<Province> result = new List<Province>();
        foreach (Connection connection in connections)
        {
            result.Add(connection.neighbor);
        }
        return result;
    }
}
