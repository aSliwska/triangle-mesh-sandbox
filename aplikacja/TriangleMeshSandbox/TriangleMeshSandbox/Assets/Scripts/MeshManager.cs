using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using static TriangleMeshAPI;

public class MeshManager : MonoBehaviour
{
    public GameObject pointPrefab;
    public Material triangleMaterial;

    private Camera m_Camera;
    private TriangleMeshAPI api;

    private ObjectToApiTwoWayDictionary objectMap;
    private HashSet<GameObject> clickedObjects;
    private (GameObject obj, Color color) mainHoverTarget;
    private HashSet<(GameObject obj, Color color)> hoverNeighboursObjects;

    private List<Point> dbPoints;
    private List<Triangle> dbTriangles;
    private List<TriangleMeshAPI.Mesh> dbMeshes;

    private Color blue, lightBlue, red, white;


    private class ObjectToApiTwoWayDictionary
    {
        private Dictionary<GameObject, MeshObject> gameObjectToApiObjectMap;
        private Dictionary<MeshObject, GameObject> apiObjectToGameObjectMap;

        public ObjectToApiTwoWayDictionary()
        {
            gameObjectToApiObjectMap = new Dictionary<GameObject, MeshObject>();
            apiObjectToGameObjectMap = new Dictionary<MeshObject, GameObject>();
        }

        public void add(GameObject gameObject, MeshObject apiObject)
        {
            gameObjectToApiObjectMap.Add(gameObject, apiObject);
            apiObjectToGameObjectMap.Add(apiObject, gameObject);
        }

        public void remove(GameObject gameObject)
        {
            MeshObject apiObject = gameObjectToApiObjectMap[gameObject];
            gameObjectToApiObjectMap.Remove(gameObject);
            apiObjectToGameObjectMap.Remove(apiObject);
        }

        public void remove(MeshObject apiObject)
        {
            GameObject gameObject = apiObjectToGameObjectMap[apiObject];
            gameObjectToApiObjectMap.Remove(gameObject);
            apiObjectToGameObjectMap.Remove(apiObject);
        }

        public GameObject get(MeshObject apiObject)
        {
            return apiObjectToGameObjectMap[apiObject];
        }

        public MeshObject get(GameObject gameObject)
        {
            return gameObjectToApiObjectMap[gameObject];
        }
    }


    void Start()
    {
        m_Camera = Camera.main;
        api = new TriangleMeshAPI(new ConfigReader().readConnectionString());
        objectMap = new ObjectToApiTwoWayDictionary();
        clickedObjects = new HashSet<GameObject>();
        mainHoverTarget = (null, white);
        hoverNeighboursObjects = new HashSet<(GameObject obj, Color color)>();

        blue = new Color(0.259f, 0.466f, 0.679f, 0.251f);
        lightBlue = new Color(0.545f, 0.815f, 0.924f, 0.251f);
        red = new Color(1f, 0.4f, 0.4f, 0.251f);
        white = new Color(1f, 1f, 1f, 0.251f);

        fetchPointsFromDB();
        fetchTrianglesFromDB();
        fetchMeshesFromDB();
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            // select coloring logic

            Vector3 mousePosition = Input.mousePosition;
            Ray ray = m_Camera.ScreenPointToRay(mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                if (Input.GetKey(KeyCode.LeftAlt))
                {
                    // remove selection
                    hit.collider.gameObject.GetComponent<Renderer>().material.color = white;
                    mainHoverTarget.color = white;
                    clickedObjects.Remove(hit.collider.gameObject);
                }
                else
                {
                    // add selection
                    hit.collider.gameObject.GetComponent<Renderer>().material.color = red;
                    mainHoverTarget.color = red;
                    clickedObjects.Add(hit.collider.gameObject);
                }
            }
            else
            {
                // remove all selections
                foreach (var obj in clickedObjects)
                {
                    obj.GetComponent<Renderer>().material.color = white;
                }
                clickedObjects.Clear();
            }
        }
        else if (!Input.GetMouseButton(1))
        {
            // hover coloring logic

            Vector3 mousePosition = Input.mousePosition;
            Ray ray = m_Camera.ScreenPointToRay(mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                if (mainHoverTarget.obj != hit.collider.gameObject)
                {
                    // uncolor previous hover targets to their original color
                    if (mainHoverTarget.obj != null)
                    {
                        mainHoverTarget.obj.GetComponent<Renderer>().material.color = mainHoverTarget.color;

                        foreach (var neighbour in hoverNeighboursObjects)
                        {
                            neighbour.obj.GetComponent<Renderer>().material.color = neighbour.color;
                        }
                        hoverNeighboursObjects.Clear();
                    }

                    // save current hover targets and their colors
                    mainHoverTarget = (hit.collider.gameObject, hit.collider.gameObject.GetComponent<Renderer>().material.color);
                    MeshObject mainHoverTargetObject = objectMap.get(hit.collider.gameObject);

                    switch (mainHoverTargetObject)
                    {
                        case Point point:
                            foreach (Triangle triangle in dbTriangles)
                            {
                                addTriangleAsNeighbourIfHasPoint(triangle, point);
                            }
                            break;

                        case Triangle triangle:
                            GameObject obj = objectMap.get(triangle.a);
                            hoverNeighboursObjects.Add((obj, obj.GetComponent<Renderer>().material.color));
                            obj = objectMap.get(triangle.b);
                            hoverNeighboursObjects.Add((obj, obj.GetComponent<Renderer>().material.color));
                            obj = objectMap.get(triangle.c);
                            hoverNeighboursObjects.Add((obj, obj.GetComponent<Renderer>().material.color));

                            foreach (Triangle otherTriangle in dbTriangles)
                            {
                                int pointsShared = 0;
                                foreach (Point point in new List<Point> { triangle.a, triangle.b, triangle.c }) {
                                    foreach (Point otherPoint in new List<Point> { otherTriangle.a, otherTriangle.b, otherTriangle.c })
                                    {
                                        if (point.Equals(otherPoint))
                                        {
                                            pointsShared++;
                                            break;
                                        }
                                    }
                                }
                                if (pointsShared > 1)
                                {
                                    obj = objectMap.get(otherTriangle);
                                    hoverNeighboursObjects.Add((obj, obj.GetComponent<Renderer>().material.color));
                                }
                            }
                            break;
                    }

                    hoverNeighboursObjects.Remove((hit.collider.gameObject, hit.collider.gameObject.GetComponent<Renderer>().material.color));

                    // recolor current hover targets
                    foreach (var neighbour in hoverNeighboursObjects)
                    {
                        neighbour.obj.GetComponent<Renderer>().material.color = lightBlue;
                    }
                    mainHoverTarget.obj.GetComponent<Renderer>().material.color = blue;
                }
            }
            else if (mainHoverTarget.obj != null)
            {
                // uncolor previous hover targets to their original color
                foreach (var neighbour in hoverNeighboursObjects)
                {
                    neighbour.obj.GetComponent<Renderer>().material.color = neighbour.color;
                }
                mainHoverTarget.obj.GetComponent<Renderer>().material.color = mainHoverTarget.color;

                // reset hover targets
                mainHoverTarget = (null, white);
                hoverNeighboursObjects.Clear();
            }
        }
    }

    // todo: move api logic to a different class, leave UI (coloring) here
    // todo: then move coloring logic to separate methods

    private void addTriangleAsNeighbourIfHasPoint(Triangle triangle, Point point)
    {
        if (point == triangle.a || point == triangle.b || point == triangle.c)
        {
            GameObject obj = objectMap.get(triangle);
            hoverNeighboursObjects.Add((obj, obj.GetComponent<Renderer>().material.color));
            obj = objectMap.get(triangle.a);
            hoverNeighboursObjects.Add((obj, obj.GetComponent<Renderer>().material.color));
            obj = objectMap.get(triangle.b);
            hoverNeighboursObjects.Add((obj, obj.GetComponent<Renderer>().material.color));
            obj = objectMap.get(triangle.c);
            hoverNeighboursObjects.Add((obj, obj.GetComponent<Renderer>().material.color));
        }
    }

    private void fetchPointsFromDB()
    {
        dbPoints = api.fetchPoints();

        foreach (Point p in dbPoints)
        {
            GameObject obj = Instantiate(pointPrefab, new Vector3((float) p.x, (float) p.y, (float) p.z), Quaternion.identity);
            objectMap.add(obj, p);
        }
    }

    private void fetchTrianglesFromDB()
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

            objectMap.add(obj, t);
        }
    }

    private void fetchMeshesFromDB()
    {
        dbMeshes = api.fetchMeshes(dbTriangles);
    }
}
