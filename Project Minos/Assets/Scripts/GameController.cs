using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using WebSocketSharp;
using System;

public class GameController : MonoBehaviour
{
    public Transform WallPrefab;
    public Transform PlayerPrefab;
    public Transform King;
    public Transform Servant;
    public string server_url;
    private WebSocket ws;
    private string myID;
    private int counter;
    public List<Transform> players = new List<Transform>();
    // Use this for initialization
    void Start()
    {
        ws = new WebSocket(server_url);
        SetupServer();
    }

    // Update is called once per frame
    void Update()
    {
        counter++;
        if(counter < 10)
        {
            return;
        }
        counter = 0;
        Debug.Log("Sended");
        Debug.Log(GameObject.Find("OVRPlayerController").transform.position.x);
        Debug.Log(GameObject.Find("OVRPlayerController").transform.position.z);
        ws.Send("{ \"Type\":\"pos\", \"Payload\":{ \"X\":" + GameObject.Find("OVRPlayerController").transform.position.x + ",\"Z\":" + GameObject.Find("OVRPlayerController").transform.position.z + "} }");
    }
    /**
    void CreateMaze2(JSONObject maze)
    {
        //x and z
        int i = 0;
        foreach (JSONObject obj in maze.list)
        {
            int j = 0;
            foreach (((int)cell.n) in obj.list)
            {
                
                if (cell.n == 1)
                {
                    Vector3 p = new Vector3(i, 0, j);
                    Instantiate(WallPrefab, p, Quaternion.identity);
                }
                j++;
            }
            i++;
        }
    }
    **/
    IEnumerator CreateMaze(int[,] maze)

    {

        Debug.Log("Creating");
        bool workDone = false;

        while (!workDone)
        {
            // Let the engine run for a frame.
            yield return null;
            for (int k = 0; k < maze.GetLength(0); k++)
            {

                for (int l = 0; l < maze.GetLength(1); l++)
                {
                    if (maze[k, l] == 0)
                    {

                        Vector3 p = new Vector3(k, 0, l);

                        Instantiate(WallPrefab, p, Quaternion.identity);
                    }

                }
            }
            workDone = true;

        }
       
    }
    IEnumerator CreatePlayer(float x,float z)

    {

        Debug.Log("Creating PLayer");
        bool workDone = false;

        while (!workDone)
        {
            // Let the engine run for a frame.
            yield return null;
            Vector3 p = new Vector3(x, 1, z);
            var playerGlobal = GameObject.Find("OVRPlayerController").transform;
            var playerLocal = playerGlobal.Find("OVRCameraRig/TrackingSpace/CenterEyeAnchor");
            playerGlobal.transform.position = p;
            workDone = true;

        }

    }
    IEnumerator CreateOtherPlayer(string id, float x, float z, bool king)

    {

        Debug.Log("Creating Other PLayer");
        bool workDone = false;

        while (!workDone)
        {
            // Let the engine run for a frame.
            yield return null;
            Vector3 p = new Vector3(x, 0, z);
            if (king)
            {
                Transform t = King;
                t.GetComponent<Player>().ID = id;
                t.GetComponent<Player>().isKing = true;
                t.GetComponent<Player>().lastPosition = p;
                players.Add(Instantiate(t , p, Quaternion.identity));
            }
            else
            {
                Transform t = Servant;
                t.GetComponent<Player>().ID = id;
                t.GetComponent<Player>().isKing = false;
                t.GetComponent<Player>().lastPosition = p;
                players.Add(Instantiate(t, p, Quaternion.identity));
            }
            workDone = true;

        }

    }
    IEnumerator UpdatePosition(string id, float x, float z)

    {

        Debug.Log("Updating position of other player");
        bool workDone = false;

        while (!workDone)
        {
            yield return null;
            foreach (Transform t in players)
            {
                if (t.GetComponent<Player>().ID == id)
                {
                    Debug.Log("Position of " + id + " updated");
                    Vector3 p = new Vector3(x, 0,z);
                    t.position = p;
                }
            }
            workDone = true;

        }

    }
    IEnumerator KillPlayer(string id)

    {

        Debug.Log("Killing player " + id);
        bool workDone = false;

        while (!workDone)
        {
            yield return null;
            foreach (Transform t in players)
            {
                if (t.GetComponent<Player>().ID == id)
                {
                    Destroy(t.gameObject);
                }
            }
            workDone = true;

        }

    }
    void SetupServer()
    {
        ws.Connect();
        ws.Send("{ \"Type\":\"ident\", \"Payload\":\"oculus\"}");
        ws.OnMessage += (sender, e) =>
        {
            var preDict = (Dictionary<string, object>)MiniJSON.Deserialize(e.Data);
            if (preDict["Type"].ToString() == "map")
            {
                Debug.Log("hey");
                var dict = (Dictionary<string, object>)MiniJSON.Deserialize(e.Data);
                
                var payload = (Dictionary<string, object>)dict["Payload"];
                var width = MapMessage.CreateFromJSON(e.Data).Payload.Width;
                var height = MapMessage.CreateFromJSON(e.Data).Payload.Height;
                Debug.Log(width);
                Debug.Log(height);
                var cells = (List<object>)payload["Cells"];
                int[,] map = new int[width,height];
                for (int row = 0; row < width; row++)
                {
                    var items = (List<object>)cells[row];
                    for (int col = 0; col < height; col++)
                    {
                        map[row,col] = Convert.ToInt32(items[col]);
                    }
                }
                UnityMainThreadDispatcher.Instance().Enqueue(CreateMaze(map));
            }
            if (preDict["Type"].ToString() == "registered")
            {
                JSONObject obj = new JSONObject(e.Data);
                myID = obj["Payload"]["You"]["ID"].str;
                var character = obj["Payload"]["Character"];
                var x_spawn = obj["Payload"]["You"]["Position"]["X"].n;
                var z_spawn = obj["Payload"]["You"]["Position"]["Z"].n;

                var other_1 = obj["Payload"]["Players"][0]["Position"]["Z"].n;


               for (int m = 0; m < 10; m++)
                {
                    if (!obj["Payload"]["Players"][m].IsNull && obj["Payload"]["Players"][m]["ID"].str != myID)
                    {
                        string id = obj["Payload"]["Players"][m]["ID"].str;
                        x_spawn = obj["Payload"]["Players"][m]["Position"]["X"].n;
                        z_spawn = obj["Payload"]["Players"][m]["Position"]["Z"].n;
                        UnityMainThreadDispatcher.Instance().Enqueue(CreateOtherPlayer(id, x_spawn, z_spawn, obj["Payload"]["Players"][m]["Character"].n == 2));
                    }
                }


                UnityMainThreadDispatcher.Instance().Enqueue(CreatePlayer(x_spawn,z_spawn));
    
            }
            if (preDict["Type"].ToString() == "joined")
            {
                JSONObject obj = new JSONObject(e.Data);
                var character = obj["Payload"]["Character"].n;
                Debug.Log("NEW USER DETECTED " + character.ToString());
                var x_spawn = obj["Payload"]["Position"]["X"].n;
                var z_spawn = obj["Payload"]["Position"]["Z"].n;
                string id = obj["Payload"]["ID"].str;
                Debug.Log("joined on the server");
                Debug.Log(x_spawn);
                Debug.Log(z_spawn);
                UnityMainThreadDispatcher.Instance().Enqueue(CreateOtherPlayer(id, x_spawn, z_spawn, character == 2));

            }
            if (preDict["Type"].ToString() == "player")
            {
                JSONObject obj = new JSONObject(e.Data);
               
                Debug.Log("Position of " + obj["Payload"]["ID"].str + " updated");
                string id = obj["Payload"]["ID"].str;
                float x_update = obj["Payload"]["Position"]["X"].n;
                float z_update = obj["Payload"]["Position"]["Z"].n;
                UnityMainThreadDispatcher.Instance().Enqueue(UpdatePosition(id, x_update, z_update));
            }
            if (preDict["Type"].ToString() == "dead")
            {
                JSONObject obj = new JSONObject(e.Data);

                Debug.Log("PLayer " + obj["Payload"]["ID"].str + " is dead");
                string id = obj["Payload"]["ID"].str;
                UnityMainThreadDispatcher.Instance().Enqueue(KillPlayer(id));
            }

            //Write join

        };
    }
}

