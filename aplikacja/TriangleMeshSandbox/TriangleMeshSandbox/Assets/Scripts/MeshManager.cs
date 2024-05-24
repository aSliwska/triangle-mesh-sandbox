using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEditor.SceneManagement;
using UnityEngine;
using static TriangleMeshAPI;

public class MeshManager : MonoBehaviour
{
    public GameObject pointPrefab;
    public Material triangleMaterial;

    private Camera m_Camera;
    private TriangleMeshAPI api;

    private Dictionary<GameObject, MeshObject> objectMap;
    private List<GameObject> clickedObjects;

    private List<Point> dbPoints;
    private List<Triangle> dbTriangles;
    private List<TriangleMeshAPI.Mesh> dbMeshes;

    private GameObject meshobject;

    void Start()
    {
        m_Camera = Camera.main;
        api = new TriangleMeshAPI(@"DATA SOURCE=MSSQLServer; INITIAL CATALOG=TriangleMeshAPI; INTEGRATED SECURITY=SSPI; Persist Security Info=False; Server=GIENIO\SQLEXPRESS");
        objectMap = new Dictionary<GameObject, MeshObject>();
        clickedObjects = new List<GameObject>();

        fetchPointsFromDB();
        fetchTrianglesFromDB();
        fetchMeshesFromDB();

        
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Vector3 mousePosition = Input.mousePosition;
            Ray ray = m_Camera.ScreenPointToRay(mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                float alfa = hit.collider.gameObject.GetComponent<Renderer>().material.color.a;
                
                if (Input.GetKey(KeyCode.LeftAlt))
                {
                    hit.collider.gameObject.GetComponent<Renderer>().material.color = new Color(1f, 1f, 1f, alfa);
                    clickedObjects.Remove(hit.collider.gameObject);
                }
                else
                {
                    hit.collider.gameObject.GetComponent<Renderer>().material.color = new Color(1f, 0.4f, 0.4f, alfa);
                    clickedObjects.Add(hit.collider.gameObject);
                }
            }
            else
            {
                foreach (var obj in clickedObjects)
                {
                    float alfa = obj.GetComponent<Renderer>().material.color.a;
                    obj.GetComponent<Renderer>().material.color = new Color(1f, 1f, 1f, alfa);
                }
                clickedObjects.Clear();
            }
        }
    }

    void fetchPointsFromDB()
    {
        dbPoints = api.fetchPoints();

        foreach (Point p in dbPoints)
        {
            GameObject obj = Instantiate(pointPrefab, new Vector3((float) p.x, (float) p.y, (float) p.z), Quaternion.identity);
            objectMap.Add(obj, p);
        }
    }

    void fetchTrianglesFromDB()
    {
        dbTriangles = api.fetchTriangles(dbPoints);

        foreach (Triangle t in dbTriangles)
        {
            GameObject obj = new GameObject();
            MeshRenderer renderer = obj.AddComponent<MeshRenderer>();
            MeshFilter filter = obj.AddComponent<MeshFilter>();

            renderer.material = triangleMaterial;
            filter.mesh = new UnityEngine.Mesh
            {
                vertices = new Vector3[] { 
                    new Vector3((float)t.a.x, (float)t.a.y, (float)t.a.z), 
                    new Vector3((float)t.b.x, (float)t.b.y, (float)t.b.z), 
                    new Vector3((float)t.c.x, (float)t.c.y, (float)t.c.z) 
                },
                uv = new Vector2[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1) },
                triangles = new int[] { 0, 1, 2, 2, 1, 0 }
            };
            obj.AddComponent<MeshCollider>();

            objectMap.Add(obj, t);
        }
    }

    void fetchMeshesFromDB()
    {
        dbMeshes = api.fetchMeshes(dbTriangles);
    }
}
