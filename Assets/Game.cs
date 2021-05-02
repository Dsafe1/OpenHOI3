using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Game : MonoBehaviour
{

    public static Data data;

    // Start is called before the first frame update
    void Start()
    {
        data = new Data();
        data.FullLoad();
    }

    // Update is called once per frame
    void Update()
    {

    }
}
