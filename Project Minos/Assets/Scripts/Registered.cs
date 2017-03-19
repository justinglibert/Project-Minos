using System.Collections;
using System.Collections.Generic;
using UnityEngine;
[System.Serializable]
public class Registered  {
    public string ID;
    public int Character;
   
    public Position Position;
    
    public static Registered CreateFromJSON(string jsonString)
    {
        Debug.Log(jsonString);
        return JsonUtility.FromJson<Registered>(jsonString);
    }
}
