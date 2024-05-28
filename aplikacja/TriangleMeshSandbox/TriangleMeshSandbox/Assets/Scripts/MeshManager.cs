using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using static TriangleMeshAPI;

public class MeshManager : MonoBehaviour
{
    public GameObject pointPrefab;
    public Material triangleMaterial;

    private Camera m_Camera;
    private UIEventManager uiObserver;

    // these are used before the class awakens, so they need to be declared early
    private ObjectToApiTwoWayDictionary objectMap = new ObjectToApiTwoWayDictionary();
    private List<Point> dbPoints = new List<Point>();
    private List<Triangle> dbTriangles = new List<Triangle>();
    private List<TriangleMeshAPI.Mesh> dbMeshes = new List<TriangleMeshAPI.Mesh>();


    private HashSet<GameObject> selectedObjects;
    private (GameObject obj, Color color) mainHoverTarget;
    private HashSet<(GameObject obj, Color color)> hoverNeighboursObjects;

    private Color blue, lightBlue, red, white, green;

    private GameObject previewPoint;

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


    void Awake()
    {
        m_Camera = Camera.main;
        selectedObjects = new HashSet<GameObject>();
        mainHoverTarget = (null, white);
        hoverNeighboursObjects = new HashSet<(GameObject obj, Color color)>();
        previewPoint = null;

        blue = new Color(0.259f, 0.466f, 0.679f, 0.251f);
        lightBlue = new Color(0.545f, 0.815f, 0.924f, 0.251f);
        red = new Color(1f, 0.4f, 0.4f, 0.251f);
        white = new Color(1f, 1f, 1f, 0.251f);
        green = new Color(0.466f, 0.717f, 0.369f, 0.251f);
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
                    selectedObjects.Remove(hit.collider.gameObject);
                }
                else
                {
                    // add selection
                    hit.collider.gameObject.GetComponent<Renderer>().material.color = red;
                    mainHoverTarget.color = red;
                    selectedObjects.Add(hit.collider.gameObject);
                }
            }
            else if (Input.GetKey(KeyCode.LeftAlt))
            {
                // remove all selections
                foreach (var obj in selectedObjects)
                {
                    obj.GetComponent<Renderer>().material.color = white;
                }
                selectedObjects.Clear();
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
                    MeshObject mainHoverTargetAPIObject = objectMap.get(hit.collider.gameObject);

                    switch (mainHoverTargetAPIObject)
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

                    // set text in UI
                    notifyUiObserverAboutHoverChange(mainHoverTargetAPIObject);
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

    public void setUiObserver(UIEventManager uiObserver)
    {
        this.uiObserver = uiObserver;
    }

    public void createPoints(List<Point> dbPoints)
    {
        foreach (Point p in dbPoints)
        {
            addPoint(p);
        }
    }

    public void createTriangles(List<Triangle> dbTriangles)
    {
        foreach (Triangle t in dbTriangles)
        {
            addTriangle(t);
        }
    }

    public void createMeshes(List<TriangleMeshAPI.Mesh> dbMeshes)
    {
        this.dbMeshes = dbMeshes;
    }
    
    public List<MeshObject> getSelectedObjects()
    {
        return selectedObjects.Select(gameObject => objectMap.get(gameObject)).ToList();
    }

    public void addPoint(Point point)
    {
        dbPoints.Add(point);

        GameObject obj = Instantiate(pointPrefab, new Vector3((float)point.x, (float)point.y, (float)point.z), Quaternion.identity);
        
        objectMap.add(obj, point);
    }

    public void addTriangle(Triangle triangle)
    {
        dbTriangles.Add(triangle);

        GameObject obj = new GameObject();
        MeshRenderer renderer = obj.AddComponent<MeshRenderer>();
        MeshFilter filter = obj.AddComponent<MeshFilter>();

        renderer.material = triangleMaterial;
        filter.mesh = new UnityEngine.Mesh
        {
            vertices = new Vector3[] {
                    new Vector3((float)triangle.a.x, (float)triangle.a.y, (float)triangle.a.z),
                    new Vector3((float)triangle.b.x, (float)triangle.b.y, (float)triangle.b.z),
                    new Vector3((float)triangle.c.x, (float)triangle.c.y, (float)triangle.c.z)
                },
            uv = new Vector2[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1) },
            triangles = new int[] { 0, 1, 2, 2, 1, 0 }
        };
        obj.AddComponent<MeshCollider>();

        objectMap.add(obj, triangle);
    }

    public void showPreviewPoint(double x, double y, double z)
    {
        previewPoint = Instantiate(pointPrefab, new Vector3((float)x, (float)y, (float)z), Quaternion.identity);
        previewPoint.GetComponent<Renderer>().material.color = green;
        previewPoint.GetComponent<Collider>().enabled = false;
    }

    public void hidePreviewPoint()
    {
        Destroy(previewPoint);
        previewPoint = null;
    }

    public void movePreviewPoint(double x, double y, double z)
    {
        if (previewPoint != null)
        {
            previewPoint.GetComponent<Renderer>().transform.position = new Vector3((float)x, (float)y, (float)z);
        }
    }

    //////

    private void notifyUiObserverAboutHoverChange(MeshObject hoveredObject)
    {
        uiObserver.updateHoverText(hoveredObject);
    }

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
}
