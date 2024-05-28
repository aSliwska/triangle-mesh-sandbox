using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.Windows;
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
        Debug.Log(meshModeToggle.value);
        // todo
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
        Debug.Log("bop");
        // todo
    }

    private void OnDeleteButtonClick(ClickEvent evt)
    {
        Debug.Log("bop");
        // todo
    }

    private void OnGetDistanceButtonClick(ClickEvent evt)
    {
        Debug.Log("bop");
        // todo
    }

    private void OnGetAreaButtonClick(ClickEvent evt)
    {
        Debug.Log("bop");
        // todo
    }

    private void OnGetVolumeButtonClick(ClickEvent evt)
    {
        Debug.Log("bop");
        // todo
    }

    private void OnIsPointOnTriangleOrMeshButtonClick(ClickEvent evt)
    {
        Debug.Log("bop");
        // todo
    }

    private void OnIsPointInsideMeshButtonClick(ClickEvent evt)
    {
        Debug.Log("bop");
        // todo
    }
}
