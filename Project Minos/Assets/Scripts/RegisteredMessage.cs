using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class RegisteredMessage {
    public string Type;
    public Registered Payload;
    public static RegisteredMessage CreateFromJSON(string jsonString)
    {
        Debug.Log(jsonString);
        return JsonUtility.FromJson<RegisteredMessage>(jsonString);
    }
}
