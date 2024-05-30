using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using static TriangleMeshAPI;

public class UIEventManager : MonoBehaviour
{
    private TriangleMeshAPI api;
    public GameObject grid;
    private MeshManager meshManager;
    private DbContentsManager db;

    private UIDocument document;
    private Toggle meshModeToggle;
    private DoubleField xInput;
    private DoubleField yInput;
    private DoubleField zInput;
    private Toggle previewModeToggle;
    private Button addPointButton;
    private Button addTriangleButton;
    private Button addMeshButton;
    private Button deleteButton;
    private Button getDistanceButton;
    private Button getAreaButton;
    private Button getVolumeButton;
    private Button isPointOnTriangleOrMeshButton;
    private Button isPointInsideMeshButton;
    private Label outputText;
    private Label hoverInformationText;


    private class DbContentsManager
    {
        private MeshManager meshManager;
        private List<Point> points;
        private List<Triangle> triangles;
        private List<TriangleMeshAPI.Mesh> meshes;

        public DbContentsManager(MeshManager meshManager)
        {
            this.meshManager = meshManager;
            points = new List<Point>();
            triangles = new List<Triangle>();
            meshes = new List<TriangleMeshAPI.Mesh>();
        }

        public List<Point> getPoints() 
        {
            return points;
        }

        public void setPoints(List<Point> points)
        {
            this.points = points;
            meshManager.createPoints(points);
        }

        public List<Triangle> getTriangles()
        {
            return triangles;
        }

        public void setTriangles(List<Triangle> triangles)
        {
            this.triangles = triangles;
            meshManager.createTriangles(triangles);
        }

        public List<TriangleMeshAPI.Mesh> getMeshes()
        {
            return meshes;
        }

        public void setMeshes(List<TriangleMeshAPI.Mesh> meshes) 
        { 
            this.meshes = meshes;
            meshManager.createMeshes(meshes);
        }

        public void addPoint(Point point)
        {
            points.Add(point);
            meshManager.addPoint(point);
        }

        public void addTriangle(Triangle triangle)
        {
            triangles.Add(triangle);
            meshManager.addTriangle(triangle);
        }

        public void addMesh(TriangleMeshAPI.Mesh mesh)
        {
            meshes.Add(mesh);
        }
    }


    void Awake()
    {
        meshManager = grid.GetComponent<MeshManager>();
        meshManager.setUiObserver(this);
        api = new TriangleMeshAPI(new ConfigReader().readConnectionString());
        db = new DbContentsManager(meshManager);

        db.setPoints(api.fetchPoints());
        db.setTriangles(api.fetchTriangles(db.getPoints()));
        db.setMeshes(api.fetchMeshes(db.getTriangles()));

        document = GetComponent<UIDocument>();

        meshModeToggle = document.rootVisualElement.Q("meshModeToggle") as Toggle;
        xInput = document.rootVisualElement.Q("xInput") as DoubleField;
        yInput = document.rootVisualElement.Q("yInput") as DoubleField;
        zInput = document.rootVisualElement.Q("zInput") as DoubleField;
        previewModeToggle = document.rootVisualElement.Q("previewModeToggle") as Toggle;
        addPointButton = document.rootVisualElement.Q("addPointButton") as Button;
        addTriangleButton = document.rootVisualElement.Q("addTriangleButton") as Button;
        addMeshButton = document.rootVisualElement.Q("addMeshButton") as Button;
        deleteButton = document.rootVisualElement.Q("deleteButton") as Button;
        getDistanceButton = document.rootVisualElement.Q("getDistanceButton") as Button;
        getAreaButton = document.rootVisualElement.Q("getAreaButton") as Button;
        getVolumeButton = document.rootVisualElement.Q("getVolumeButton") as Button;
        isPointOnTriangleOrMeshButton = document.rootVisualElement.Q("isPointOnTriangleOrMeshButton") as Button;
        isPointInsideMeshButton = document.rootVisualElement.Q("isPointInsideMeshButton") as Button;
        outputText = document.rootVisualElement.Q("outputText") as Label;
        hoverInformationText = document.rootVisualElement.Q("hoverInformationText") as Label;

        meshModeToggle.RegisterCallback<ClickEvent>(OnMeshModeToggleClick);
        xInput.RegisterCallback<ChangeEvent<double>>(OnXInputChange);
        yInput.RegisterCallback<ChangeEvent<double>>(OnYInputChange);
        zInput.RegisterCallback<ChangeEvent<double>>(OnZInputChange);
        previewModeToggle.RegisterCallback<ClickEvent>(OnPreviewModeToggleClick);
        addPointButton.RegisterCallback<ClickEvent>(OnAddPointButtonClick);
        addTriangleButton.RegisterCallback<ClickEvent>(OnAddTriangleButtonClick);
        addMeshButton.RegisterCallback<ClickEvent>(OnAddMeshButtonClick);
        deleteButton.RegisterCallback<ClickEvent>(OnDeleteButtonClick);
        getDistanceButton.RegisterCallback<ClickEvent>(OnGetDistanceButtonClick);
        getAreaButton.RegisterCallback<ClickEvent>(OnGetAreaButtonClick);
        getVolumeButton.RegisterCallback<ClickEvent>(OnGetVolumeButtonClick);
        isPointOnTriangleOrMeshButton.RegisterCallback<ClickEvent>(OnIsPointOnTriangleOrMeshButtonClick);
        isPointInsideMeshButton.RegisterCallback<ClickEvent>(OnIsPointInsideMeshButtonClick);
    }

    public void updateHoverText(MeshObject meshObject)
    {
        hoverInformationText.text = "Hovered over: \n";

        switch (meshObject)
        {
            case Point p:
                hoverInformationText.text += "Point " + p.id + " [" + p.x + "; " + p.y + "; " + p.z + "]";
                break;
            case Triangle t:
                hoverInformationText.text += "Triangle " + t.id;
                break;
            case TriangleMeshAPI.Mesh m:
                hoverInformationText.text += "Mesh " + m.id;
                break;
            case null:
                break;
        }
    }

    private void setOuputText(string outputText)
    {
        this.outputText.text = outputText;
    }

    private void setFailOutput(List<string> requirements)
    {
        setOuputText("Failed. Need selected:\n" + string.Join('\n', requirements));
    }

    private void OnMeshModeToggleClick(ClickEvent evt) 
    {
        fireMeshModeToggleClickAction();
    }

    private void fireMeshModeToggleClickAction()
    {
        if (db.getMeshes().Count > 0)
        {
            meshManager.toggleMeshMode(meshModeToggle.value);
        }
        else
        {
            meshModeToggle.value = false;
            setOuputText("There are no meshes in the database. Create one first.");
        }
    }

    private void OnXInputChange(ChangeEvent<double> xInputVal)
    {
        meshManager.movePreviewPoint(xInputVal.newValue, yInput.value, zInput.value);
    }

    private void OnYInputChange(ChangeEvent<double> yInputVal)
    {
        meshManager.movePreviewPoint(xInput.value, yInputVal.newValue, zInput.value);
    }

    private void OnZInputChange(ChangeEvent<double> zInputVal)
    {
        meshManager.movePreviewPoint(xInput.value, yInput.value, zInputVal.newValue);
    }

    private void OnPreviewModeToggleClick(ClickEvent evt)
    {
        if (previewModeToggle.value)
        {
            meshManager.showPreviewPoint(xInput.value, yInput.value, zInput.value);
        }
        else
        {
            meshManager.hidePreviewPoint();
        }
    }

    private void OnAddPointButtonClick(ClickEvent evt)
    {
        meshManager.hidePreviewPoint();
        int id = api.insertPoint(xInput.value, yInput.value, zInput.value);
        db.addPoint(new Point(id, xInput.value, yInput.value, zInput.value));
        setOuputText("Added point with id = " + id);
        previewModeToggle.value = false;
    }

    private void OnAddTriangleButtonClick(ClickEvent evt)
    {
        List<MeshObject> selectedObjects = meshManager.getSelectedObjects();

        if (selectedObjects.Count == 3 && selectedObjects.All(obj => obj is Point))
        {
            Point p1 = (Point)selectedObjects[0];
            Point p2 = (Point)selectedObjects[1];
            Point p3 = (Point)selectedObjects[2];

            try
            {
                int id = api.insertTriangle(p1, p2, p3);
                Triangle t = new Triangle(id, p1, p2, p3);
                db.addTriangle(t);
                setOuputText("Added triangle with id = " + id);
            } 
            catch (Exception)
            {
                setFailOutput(new List<string> { "3 points that don't form an already existing triangle" });
            }
        }
        else
        {
            setFailOutput(new List<string>{ "3 points" });
        }
    }

    private void OnAddMeshButtonClick(ClickEvent evt)
    {
        List<MeshObject> selectedObjects = meshManager.getSelectedObjects();

        if (selectedObjects.All(o => o is Triangle))
        {
            List<Triangle> triangles = selectedObjects.Cast<Triangle>().ToList();

            int id = api.insertMesh(triangles);

            db.addMesh(new TriangleMeshAPI.Mesh(id, triangles));

            setOuputText("Created mesh with id = " + id);
        }
        else if (selectedObjects.All(o => o is TriangleMeshAPI.Mesh))
        {
            List<int> meshIds = selectedObjects.Select(m => m.id).ToList();

            List<TriangleMeshAPI.Mesh> newMeshes = api.mergeMeshes(meshIds, db.getTriangles());

            exitMeshMode();

            db.setMeshes(newMeshes);

            setOuputText("Successfully merged meshes into a new one");
        }
        else
        {
            setFailOutput(new List<string> { "n triangles or n meshes" });
        }
    }

    private void exitMeshMode()
    {
        if (meshModeToggle.value)
        {
            fireMeshModeToggleClickAction();
        }
        meshModeToggle.value = false;
    }

    private void OnDeleteButtonClick(ClickEvent evt)
    {
        List<MeshObject> selectedObjects = meshManager.getSelectedObjects();

        if (selectedObjects.Count != 0)
        {
            List<int> pointIds = selectedObjects.OfType<Point>().Select(p => p.id).ToList();
            List<int> triangleIds = selectedObjects.OfType<Triangle>().Select(t => t.id).ToList();
            List<int> meshIds = selectedObjects.OfType<TriangleMeshAPI.Mesh>().Select(m => m.id).ToList();

            List<Point> newPoints = new List<Point>();
            List<Triangle> newTriangles = new List<Triangle>();
            List<TriangleMeshAPI.Mesh> newMeshes = new List<TriangleMeshAPI.Mesh>();

            try
            {
                api.deletePoints(pointIds, out newPoints, out newTriangles, out newMeshes);
                api.deleteTriangles(triangleIds, newPoints, out newTriangles, out newMeshes);
                api.deleteMeshes(meshIds);

                newMeshes.RemoveAll(m => meshIds.Contains(m.id));

                if (meshModeToggle.value)
                {
                    exitMeshMode();
                }

                meshManager.nuke();

                db.setPoints(newPoints);
                db.setTriangles(newTriangles);
                db.setMeshes(newMeshes);


                setOuputText("Successfully deleted objects.");
            }
            catch (Exception)
            {
                setOuputText("Deleting failed.");
            }

        }
        else
        {
            setFailOutput(new List<string> { "anything, in any number" });
        }
    }

    private void OnGetDistanceButtonClick(ClickEvent evt)
    {
        List<MeshObject> selectedObjects = meshManager.getSelectedObjects();

        if (selectedObjects.Count == 2 && selectedObjects.All(obj => obj is Point))
        {
            Point p1 = (Point)selectedObjects[0];
            Point p2 = (Point)selectedObjects[1];
            
            double distance = api.getDistanceBetweenPoints(p1.id, p2.id);
            setOuputText("Distance between points = " + distance);
        }
        else
        {
            setFailOutput(new List<string> { "2 points" });
        }
    }

    private void OnGetAreaButtonClick(ClickEvent evt)
    {
        List<MeshObject> selectedObjects = meshManager.getSelectedObjects();

        if (selectedObjects.Count == 1 && !(selectedObjects[0] is Point))
        {
            double area;
            switch (selectedObjects[0])
            {
                case Triangle t:
                    area = api.getTriangleArea(t);
                    setOuputText("Area of triangle = " + area);
                    break;

                case TriangleMeshAPI.Mesh m:
                    area = api.getMeshArea(m.id);
                    setOuputText("Area of mesh = " + area);
                    break;
            }
        }
        else
        {
            setFailOutput(new List<string> { "1 triangle or mesh" });
        }
    }

    private void OnGetVolumeButtonClick(ClickEvent evt)
    {
        List<MeshObject> selectedObjects = meshManager.getSelectedObjects();

        if (selectedObjects.Count == 1 && selectedObjects[0] is TriangleMeshAPI.Mesh m)
        {
            try
            {
                double volume = api.getMeshVolume(m.id);
                setOuputText("Volume of mesh = " + volume);
            }
            catch (Exception)
            {
                setFailOutput(new List<string> { "1 manifold mesh" });
            }
        }
        else
        {
            setFailOutput(new List<string> { "1 mesh" });
        }
    }

    private void OnIsPointOnTriangleOrMeshButtonClick(ClickEvent evt)
    {
        List<MeshObject> selectedObjects = meshManager.getSelectedObjects();

        if (selectedObjects.Count == 2 && selectedObjects.Any(o => o is Point) && selectedObjects.Any(o => !(o is Point)))
        {

            Point p = selectedObjects.OfType<Point>().First();
            Triangle t = selectedObjects.OfType<Triangle>().DefaultIfEmpty().First(); 
            TriangleMeshAPI.Mesh m = selectedObjects.OfType<TriangleMeshAPI.Mesh>().DefaultIfEmpty().First();
            bool isOnSurface;

            if (m is null)
            {
                isOnSurface = api.isPointOnTriangle(p.id, t.id);
            } 
            else
            {
                isOnSurface = api.isPointOnMesh(p.id, m.id);
            }
            
            if (isOnSurface)
            {
                setOuputText("Point is on surface");
            }
            else
            {
                setOuputText("Point is NOT on surface");
            }
            
        }
        else
        {
            setFailOutput(new List<string> { "1 point", "1 triangle or mesh" });
        }
    }

    private void OnIsPointInsideMeshButtonClick(ClickEvent evt)
    {
        List<MeshObject> selectedObjects = meshManager.getSelectedObjects();

        if (selectedObjects.Count == 2 && selectedObjects.Any(o => o is Point p) && selectedObjects.Any(o => o is TriangleMeshAPI.Mesh m))
        {
            try
            {
                Point p = selectedObjects.OfType<Point>().First();
                TriangleMeshAPI.Mesh m = selectedObjects.OfType<TriangleMeshAPI.Mesh>().First();

                bool isInside = api.isPointInsideMesh(p.id, m.id);

                if (isInside)
                {
                    setOuputText("Point is inside mesh");
                }
                else
                {
                    setOuputText("Point is NOT inside mesh");
                }
            }
            catch (Exception)
            {
                setFailOutput(new List<string> { "1 point", "1 manifold mesh" });
            }
        }
        else
        {
            setFailOutput(new List<string> { "1 point", "1 mesh"});
        }
    }
}
