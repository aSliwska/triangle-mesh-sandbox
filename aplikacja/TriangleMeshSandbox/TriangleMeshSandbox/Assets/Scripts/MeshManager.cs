using System;
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
    private List<Triangle> dbTriangles = new List<Triangle>();
    private List<TriangleMeshAPI.Mesh> dbMeshes = new List<TriangleMeshAPI.Mesh>();


    private HashSet<GameObject> selectedObjects;
    private MeshSelector selectedMeshes;
    private (GameObject obj, Color color) mainHoverTarget;
    private HashSet<(GameObject obj, Color color)> hoverNeighboursObjects;

    private Color blue, lightBlue, red, white, green, orange;
    private GameObject previewPoint;
    private bool isInMeshMode;
    private int currentMeshIndex;

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

        public void destroyEverything()
        {
            foreach (GameObject obj in gameObjectToApiObjectMap.Keys)
            {
                Destroy(obj);
            }

            gameObjectToApiObjectMap = new Dictionary<GameObject, MeshObject>();
            apiObjectToGameObjectMap = new Dictionary<MeshObject, GameObject>();
        }
    }

    private class MeshSelector
    {
        private Dictionary<Triangle, int> triangleCounts;
        private HashSet<TriangleMeshAPI.Mesh> selectedMeshes;

        public MeshSelector()
        {
            triangleCounts = new Dictionary<Triangle, int>();
            selectedMeshes = new HashSet<TriangleMeshAPI.Mesh>();
        }

        public void Add(TriangleMeshAPI.Mesh mesh)
        {
            if (selectedMeshes.Contains(mesh))
            {
                return;
            }

            selectedMeshes.Add(mesh);

            foreach (Triangle triangle in mesh.triangles)
            {
                if (triangleCounts.ContainsKey(triangle))
                {
                    triangleCounts[triangle] += 1;
                }
                else
                {
                    triangleCounts.Add(triangle, 1);
                }
            }
        }

        public void Remove(TriangleMeshAPI.Mesh mesh)
        {
            if (!selectedMeshes.Contains(mesh))
            {
                return;
            }

            selectedMeshes.Remove(mesh);

            foreach (Triangle triangle in mesh.triangles)
            {
                if (triangleCounts.ContainsKey(triangle))
                {
                    triangleCounts[triangle] -= 1;

                    if (triangleCounts[triangle] < 1)
                    {
                        triangleCounts.Remove(triangle);
                    }
                }
            }
        }

        public void Clear()
        {
            selectedMeshes.Clear();
            triangleCounts.Clear();
        }

        public bool Contains(TriangleMeshAPI.Mesh mesh)
        {
            return selectedMeshes.Contains(mesh);
        }
        
        public List<Triangle> getTriangles()
        {
            return triangleCounts.Keys.ToList();
        }

        public HashSet<TriangleMeshAPI.Mesh> getMeshes()
        {
            return selectedMeshes;
        }
    }
  
    /////// drawing related methods

    void Awake()
    {
        m_Camera = Camera.main;
        selectedObjects = new HashSet<GameObject>();
        selectedMeshes = new MeshSelector();
        mainHoverTarget = (null, white);
        hoverNeighboursObjects = new HashSet<(GameObject obj, Color color)>();
        previewPoint = null;
        isInMeshMode = false;

        blue = new Color(0.259f, 0.466f, 0.679f, 0.251f);
        lightBlue = new Color(0.545f, 0.815f, 0.924f, 0.251f);
        red = new Color(1f, 0.4f, 0.4f, 0.251f);
        white = new Color(1f, 1f, 1f, 0.251f);
        green = new Color(0.466f, 0.717f, 0.369f, 0.251f);
        orange = new Color(0.877f, 0.576f, 0.186f, 0.251f);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.LeftArrow) && isInMeshMode)
        {
            cycleMeshes(mod(currentMeshIndex - 1, dbMeshes.Count));
        }
        else if (Input.GetKeyDown(KeyCode.RightArrow) && isInMeshMode)
        {
            cycleMeshes(mod(currentMeshIndex + 1, dbMeshes.Count));
        }
        else if (Input.GetMouseButtonDown(0))
        {
            // select coloring logic

            Ray ray = m_Camera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                if (mainHoverTarget.obj != null)
                {
                    uncolorHover();
                    hoverNeighboursObjects.Clear();
                }

                if (Input.GetKey(KeyCode.LeftAlt))
                {
                    deselectObject(hit.collider.gameObject);
                }
                else
                {
                    selectObject(hit.collider.gameObject);
                }
            }
            else if (Input.GetKey(KeyCode.LeftAlt))
            {
                deselectEverything();
            }
        }
        else if (!Input.GetMouseButton(1))
        {
            // hover coloring logic

            Ray ray = m_Camera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                if (mainHoverTarget.obj != hit.collider.gameObject)
                {
                    if (mainHoverTarget.obj != null)
                    {
                        uncolorHover();
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
                uncolorHover();

                // reset hover targets
                mainHoverTarget = (null, white);
                hoverNeighboursObjects.Clear();

                // set text in UI
                notifyUiObserverAboutHoverChange(null);
            }
        }
    }

    ////// 

    private void cycleMeshes(int nextMeshIndex)
    {
        // uncolor previous mesh
        if (!selectedMeshes.Contains(dbMeshes[currentMeshIndex]))
        {
            // get all triangles that should be red
            List<Triangle> selectedTriangles = selectedMeshes.getTriangles();

            foreach (Triangle triangle in dbMeshes[currentMeshIndex].triangles)
            {
                if (selectedTriangles.Contains(triangle))
                {
                    objectMap.get(triangle).GetComponent<MeshRenderer>().material.color = red;
                }
                else
                {
                    objectMap.get(triangle).GetComponent<MeshRenderer>().material.color = white;
                }
            }
        }

        // change index
        currentMeshIndex = nextMeshIndex;

        // color next mesh
        if (selectedMeshes.Contains(dbMeshes[currentMeshIndex]))
        {
            foreach (Triangle triangle in dbMeshes[currentMeshIndex].triangles)
            {
                objectMap.get(triangle).GetComponent<MeshRenderer>().material.color = red;
            }
        }
        else
        {
            foreach (Triangle triangle in dbMeshes[currentMeshIndex].triangles)
            {
                objectMap.get(triangle).GetComponent<MeshRenderer>().material.color = orange; 
            }
        }

        // recolor hover next frame
        if (mainHoverTarget.obj != null)
        {
            uncolorHover();
            hoverNeighboursObjects.Clear();
            mainHoverTarget.obj = null;
        }
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

    private void deselectObject(GameObject obj)
    {
        if (!isInMeshMode || objectMap.get(obj) is Point)
        {
            obj.GetComponent<Renderer>().material.color = white;
            mainHoverTarget.color = white;
            selectedObjects.Remove(obj);
        }
        else if (selectedMeshes.Contains(dbMeshes[currentMeshIndex]) && dbMeshes[currentMeshIndex].triangles.Contains(objectMap.get(obj)))
        {
            foreach (Triangle triangle in dbMeshes[currentMeshIndex].triangles)
            {
                objectMap.get(triangle).GetComponent<MeshRenderer>().material.color = orange;
            }
            mainHoverTarget.color = orange;
            selectedMeshes.Remove(dbMeshes[currentMeshIndex]);
        }
    }

    private void selectObject(GameObject obj)
    {
        if (!isInMeshMode || objectMap.get(obj) is Point)
        {
            obj.GetComponent<Renderer>().material.color = red;
            mainHoverTarget.color = red;
            selectedObjects.Add(obj);
        }
        else if (!selectedMeshes.Contains(dbMeshes[currentMeshIndex]) && dbMeshes[currentMeshIndex].triangles.Contains(objectMap.get(obj)))
        {
            foreach (Triangle triangle in dbMeshes[currentMeshIndex].triangles)
            {
                objectMap.get(triangle).GetComponent<MeshRenderer>().material.color = red;
            }
            mainHoverTarget.color = red;
            selectedMeshes.Add(dbMeshes[currentMeshIndex]);
        }
    }

    private void deselectEverything()
    {
        foreach (var obj in selectedObjects)
        {
            obj.GetComponent<Renderer>().material.color = white;
        }
        selectedObjects.Clear();

        if (isInMeshMode)
        {
            foreach (Triangle triangle in selectedMeshes.getTriangles())
            {
                objectMap.get(triangle).GetComponent<MeshRenderer>().material.color = white;
            }

            selectedMeshes.Clear();
        }
    }

    private void uncolorHover()
    {
        mainHoverTarget.obj.GetComponent<Renderer>().material.color = mainHoverTarget.color;

        foreach (var neighbour in hoverNeighboursObjects)
        {
            neighbour.obj.GetComponent<Renderer>().material.color = neighbour.color;
        }
    }

    ///// ui related methods

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
        this.dbTriangles = dbTriangles;
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
        List<MeshObject> allSelectedObjects = new List<MeshObject>();
        allSelectedObjects.AddRange(selectedMeshes.getMeshes());
        allSelectedObjects.AddRange(selectedObjects.Select(gameObject => objectMap.get(gameObject)));
        return allSelectedObjects;
    }

    public void addPoint(Point point)
    {
        GameObject obj = Instantiate(pointPrefab, new Vector3((float)point.x, (float)point.y, (float)point.z), Quaternion.identity);
        
        objectMap.add(obj, point);
    }

    public void addTriangle(Triangle triangle)
    {
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

    public void toggleMeshMode(bool value)
    {
        deselectEverything();

        isInMeshMode = value;

        if (isInMeshMode)
        {
            currentMeshIndex = 0;
            foreach (Triangle triangle in dbMeshes[currentMeshIndex].triangles)
            {
                objectMap.get(triangle).GetComponent<MeshRenderer>().material.color = orange;
            }
        }
        else
        {
            foreach (Triangle triangle in dbMeshes[currentMeshIndex].triangles)
            {
                objectMap.get(triangle).GetComponent<MeshRenderer>().material.color = white;
            }
        }
    }

    public void nuke()
    {
        hoverNeighboursObjects.Clear();
        mainHoverTarget = (null, white);
        selectedMeshes.Clear();
        selectedObjects.Clear();
        dbMeshes = new List<TriangleMeshAPI.Mesh>();
        dbTriangles = new List<Triangle>();
        objectMap.destroyEverything();
    }

    //////

    private void notifyUiObserverAboutHoverChange(MeshObject hoveredObject)
    {
        uiObserver.updateHoverText(hoveredObject);
    }

    private int mod(int x, int m)
    {
        int r = x % m;
        return r < 0 ? r + m : r;
    }
    
}
