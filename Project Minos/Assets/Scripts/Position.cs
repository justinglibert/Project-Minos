using System.Collections;
using System.Collections.Generic;
using UnityEngine;
[System.Serializable]
public class Position  {
    public float X;
    public float Z;
    public static Position CreateFromJSON(string jsonString)
    {
        Debug.Log(jsonString);
        return JsonUtility.FromJson<Position>(jsonString);
    }
}
