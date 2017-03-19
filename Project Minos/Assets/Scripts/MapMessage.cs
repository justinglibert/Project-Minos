using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class MapMessage {

    public string Type;
    public Map Payload;
    public static MapMessage CreateFromJSON(string jsonString)
    {
        Debug.Log(jsonString);
        return JsonUtility.FromJson<MapMessage>(jsonString);
    }
}
