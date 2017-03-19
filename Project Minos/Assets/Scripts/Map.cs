using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class Map  {
    public int Width;
    public int Height;
    public List<int> Cells;
    public static Map CreateFromJSON(string jsonString)
    {
        Debug.Log(jsonString);
        return JsonUtility.FromJson<Map>(jsonString);
    }
}
