using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.ProBuilder;
using System.Collections.Generic;
using System.Linq;

public class PivotEditor : EditorWindow
{
    private Vector3 _min;
    private Vector3 _max;
    private Vector3 _align;
    private Vector3[] _points;
    private Vector3[] _aligns;
    private int _selectedLevel;
    private int _selectedCorner;
    private bool _alignChildrenToRootPosition;
    private bool _alignChildrenToRootRotation;
    private bool _enabledCustomPivot;
    private Vector3 _pivotOffset;

	private readonly string[] _levels =
    {
        "Top", 
        "Mid", 
        "Bot",
    };
    private readonly string[] _corners =
    {
        "*", "*", "*",
        "*", "*", "*",
        "*", "*", "*",
    };

    private const string WINDOW_NAME = "Pivot Editor";
    private const string UNDO_GROUP_NAME = "Pivot Changing";
        
    [MenuItem("Tools/Pivot Editor")]
    public static void ShowWindow()
    {
        GetWindow<PivotEditor>(WINDOW_NAME).Show();
    }

    private void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
        Selection.selectionChanged += Repaint;

		_points = new Vector3[8];
        _aligns = new Vector3[_levels.Length * _corners.Length];
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        Selection.selectionChanged -= Repaint;

		_points = null;
        _aligns = null;
	}

    private void OnGUI()
    {
        var selected = Selection.activeTransform;
            
        if (!selected)
        {
            EditorGUILayout.HelpBox("Select object!", MessageType.Info);
            return;
        }

		EditorGUI.BeginDisabledGroup(_enabledCustomPivot);
		EditorGUILayout.BeginHorizontal();

		EditorGUILayout.BeginVertical();
        GUILayout.Label("Select level (Y):", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();
        var selectedLevel = GUILayout.SelectionGrid(_selectedLevel, _levels, 1);
        if (EditorGUI.EndChangeCheck())
        {
            _selectedLevel = selectedLevel;
            DrawPivot();
        }
		EditorGUILayout.EndVertical();

		EditorGUILayout.BeginVertical();
        GUILayout.Label("Select corner (XZ):", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();
        var selectedCorner = GUILayout.SelectionGrid(_selectedCorner, _corners, 3);
        if (EditorGUI.EndChangeCheck())
        {
            _selectedCorner = selectedCorner;
            DrawPivot();
        }
		EditorGUILayout.EndVertical();

		EditorGUILayout.EndHorizontal();
		EditorGUI.EndDisabledGroup();

		EditorGUI.BeginDisabledGroup(!_enabledCustomPivot);
		_pivotOffset = EditorGUILayout.Vector3Field("Pivot Position", _pivotOffset);
		EditorGUI.EndDisabledGroup();

		EditorGUILayout.Separator();
		_enabledCustomPivot = EditorGUILayout.Toggle("Custom Pivot", _enabledCustomPivot);
		EditorGUILayout.Separator();

		EditorGUILayout.LabelField("Align Children:", EditorStyles.boldLabel);
		_alignChildrenToRootPosition = EditorGUILayout.Toggle("To Root Position", _alignChildrenToRootPosition);

		EditorGUI.BeginDisabledGroup(true);
		_alignChildrenToRootRotation = EditorGUILayout.Toggle("To Root Rotation", _alignChildrenToRootRotation);
		EditorGUI.EndDisabledGroup();

		EditorGUILayout.Separator();

		if (GUILayout.Button("Change and Save"))
        {
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName(UNDO_GROUP_NAME);

            CalcPivot(selected);

            var group = Undo.GetCurrentGroup();
			var shift = AlignPivot(selected, _align);

            AlignChildrenPivotToRoot(selected);

			var prefabAsset = PrefabUtility.GetCorrespondingObjectFromSource(selected);

			if (prefabAsset)
            {
                SavePrefab(selected, prefabAsset.gameObject);
                ApplyChangesInScene(selected, prefabAsset.gameObject, shift);
            }

            _pivotOffset = Vector3.zero;

            Undo.CollapseUndoOperations(group);
		}
    }

	private void OnSceneGUI(SceneView sceneView)
    {
        var selected = Selection.activeTransform;

        if (selected == null)
        {
            return;
        }

        var renderers = selected.GetComponentsInChildren<Renderer>();

        if (renderers.Length == 0)
        {
            return;
        }
            
        _min = selected.InverseTransformPoint(renderers[0].bounds.center);
        _max = selected.InverseTransformPoint(renderers[0].bounds.center);
            
        foreach (var renderer in renderers)
        {
            CalcBounds(renderer, selected);
        }

        CalcCorners(selected);
        DrawBounds();

        CalcAligns();
        DrawAligns();

        CalcPivot(selected);
        DrawPivot();
            
        sceneView.Repaint();
    }

    private void CalcBounds(Renderer renderer, Transform root)
    {
        var extents = renderer.localBounds.extents;
        var center = renderer.localBounds.center;
        var localCorners = new Vector3[]
        {
            new( extents.x,  extents.y,  extents.z),
            new( extents.x,  extents.y, -extents.z),
            new( extents.x, -extents.y,  extents.z),
            new( extents.x, -extents.y, -extents.z),
            new(-extents.x,  extents.y,  extents.z),
            new(-extents.x,  extents.y, -extents.z),
            new(-extents.x, -extents.y,  extents.z),
            new(-extents.x, -extents.y, -extents.z),
        };
            
        Handles.color = Color.white;
            
        foreach (var localCorner in localCorners)
        {
            var worldCorner = renderer.transform.TransformPoint(center + localCorner);
            var localToRootCorner = root.InverseTransformPoint(worldCorner);
                
            _min = Vector3.Min(_min, localToRootCorner);
            _max = Vector3.Max(_max, localToRootCorner);
        }
    }

    private void CalcCorners(Transform selected)
    {
        _points[0] = selected.TransformPoint(new Vector3(_min.x, _min.y, _min.z));
        _points[1] = selected.TransformPoint(new Vector3(_min.x, _min.y, _max.z));
        _points[2] = selected.TransformPoint(new Vector3(_max.x, _min.y, _max.z));
        _points[3] = selected.TransformPoint(new Vector3(_max.x, _min.y, _min.z));
        _points[4] = selected.TransformPoint(new Vector3(_min.x, _max.y, _min.z));
        _points[5] = selected.TransformPoint(new Vector3(_min.x, _max.y, _max.z));
        _points[6] = selected.TransformPoint(new Vector3(_max.x, _max.y, _max.z));
        _points[7] = selected.TransformPoint(new Vector3(_max.x, _max.y, _min.z));
    }

    private void CalcAligns()
    {
        _aligns[0] = _points[5];
        _aligns[1] = (_points[5] + _points[6]) * 0.5f;
        _aligns[2] = _points[6];
        _aligns[3] = (_points[4] + _points[5]) * 0.5f;
        _aligns[4] = (_points[4] + _points[6]) * 0.5f;
        _aligns[5] = (_points[6] + _points[7]) * 0.5f;
        _aligns[6] = _points[4];
        _aligns[7] = (_points[4] + _points[7]) * 0.5f;
        _aligns[8] = _points[7];
             
        _aligns[9]  = (_points[1] + _points[5]) * 0.5f;
        _aligns[10] = (_points[1] + _points[6]) * 0.5f;
        _aligns[11] = (_points[2] + _points[6]) * 0.5f;
        _aligns[12] = (_points[1] + _points[4]) * 0.5f;
        _aligns[13] = (_points[1] + _points[7]) * 0.5f;
        _aligns[14] = (_points[2] + _points[7]) * 0.5f;
        _aligns[15] = (_points[0] + _points[4]) * 0.5f;
        _aligns[16] = (_points[0] + _points[7]) * 0.5f;
        _aligns[17] = (_points[3] + _points[7]) * 0.5f;
            
        _aligns[18]  = _points[1];
        _aligns[19]  = (_points[1] + _points[2]) * 0.5f;
        _aligns[20]  = _points[2];
        _aligns[21]  = (_points[0] + _points[1]) * 0.5f;
        _aligns[22]  = (_points[0] + _points[2]) * 0.5f;
        _aligns[23]  = (_points[2] + _points[3]) * 0.5f;
        _aligns[24]  = _points[0];
        _aligns[25]  = (_points[0] + _points[3]) * 0.5f;
        _aligns[26]  = _points[3];
    }

    private void DrawBounds()
    {
        Handles.color = Color.cyan;
        Handles.DrawLine(_points[0], _points[1]);
        Handles.DrawLine(_points[1], _points[2]);
        Handles.DrawLine(_points[2], _points[3]);
        Handles.DrawLine(_points[3], _points[0]);
            
        Handles.DrawLine(_points[4], _points[5]);
        Handles.DrawLine(_points[5], _points[6]);
        Handles.DrawLine(_points[6], _points[7]);
        Handles.DrawLine(_points[7], _points[4]);
            
        Handles.DrawLine(_points[0], _points[4]);
        Handles.DrawLine(_points[1], _points[5]);
        Handles.DrawLine(_points[2], _points[6]);
        Handles.DrawLine(_points[3], _points[7]);
    }

    private void DrawAligns()
    {
        Handles.color = Color.yellow;

        foreach (var align in _aligns)
        {
            Handles.DrawWireCube(align, Vector3.one * 0.1f);
        }
    }

    private void CalcPivot(Transform selected)
    {
		if (_enabledCustomPivot)
		{
			_align = selected.position + selected.rotation * _pivotOffset;
		}
		else
		{
			_align = _aligns[_selectedLevel * _corners.Length + _selectedCorner];
		}
	}

    private void DrawPivot()
    {
		Handles.color = Color.red;
        Handles.DrawWireCube(_align, Vector3.one * 0.1f);
    }
        
    private Vector3 AlignPivot(Transform selected, Vector3 align)
    {
		var children = selected.GetComponentsInChildren<Transform>();
		Undo.RecordObjects(children.ToArray(), "Align Pivot");

		var shift = selected.InverseTransformPoint(align);

		for (var i = 0; i < selected.childCount; i++)
        {
            var child = selected.GetChild(i);
			child.position += selected.position - align;
        }

        selected.position = align;

        return shift;
	}

    private void AlignChildrenPivotToRoot(Transform selected)
    {
        var objectsToRecord = new List<Object>();
        for (var i = 0; i < selected.childCount; i++)
        {
            var child = selected.GetChild(i);
            objectsToRecord.Add(child);

            if (child.TryGetComponent(out ProBuilderMesh proBuilderMesh))
            {
                objectsToRecord.Add(proBuilderMesh);
            }
            else if (child.TryGetComponent(out MeshFilter meshFilter))
            {
                objectsToRecord.Add(meshFilter.sharedMesh);
            }
        }

        Undo.RecordObjects(objectsToRecord.ToArray(), "Align Children Pivot");

        if (_alignChildrenToRootPosition)
        {
            AlignChildrenPivotToRootPosition(selected);
        }

        if (_alignChildrenToRootRotation)
        {
			AlignChildrenPivotToRootRotation(selected);
        }
	}

	private void AlignChildrenPivotToRootPosition(Transform selected)
	{
		for (var i = 0; i < selected.childCount; i++)
		{
			var child = selected.GetChild(i);
			var shift = child.localPosition;
			child.localPosition = Vector3.zero;

			if (child.TryGetComponent(out ProBuilderMesh proBuilderMesh))
			{
				var vertices = proBuilderMesh.positions.ToArray();

				for (int j = 0; j < vertices.Length; j++)
				{
					vertices[j] += child.InverseTransformPoint(selected.TransformPoint(shift)) - child.localPosition;
				}

				proBuilderMesh.positions = vertices;
				proBuilderMesh.ToMesh();
				proBuilderMesh.Refresh();
			}
			else if (child.TryGetComponent(out MeshFilter meshFilter))
			{
				var vertices = meshFilter.sharedMesh.vertices;

				for (int j = 0; j < vertices.Length; j++)
				{
					vertices[j] += child.InverseTransformPoint(selected.TransformPoint(shift)) - child.localPosition;
				}

				meshFilter.sharedMesh.vertices = vertices;
				meshFilter.sharedMesh.RecalculateBounds();
			}
		}
	}

	private void AlignChildrenPivotToRootRotation(Transform selected)
	{
		for (var i = 0; i < selected.childCount; i++)
		{
			var child = selected.GetChild(i);
			var originalPosition = child.position;
			var originalRotation = child.rotation;
			var originalScale = child.localScale;

			var deltaRotation = selected.rotation * Quaternion.Inverse(originalRotation);

			if (child.TryGetComponent(out ProBuilderMesh proBuilderMesh))
			{
				var vertices = proBuilderMesh.positions.ToArray();

				for (int j = 0; j < vertices.Length; j++)
				{
					var worldVertex = child.TransformPoint(vertices[j]);
					worldVertex = originalPosition + deltaRotation * (worldVertex - originalPosition);
					vertices[j] = child.InverseTransformPoint(worldVertex);
				}

				proBuilderMesh.positions = vertices;
				proBuilderMesh.ToMesh();
				proBuilderMesh.Refresh();
			}
			else if (child.TryGetComponent(out MeshFilter meshFilter))
			{
				var vertices = meshFilter.sharedMesh.vertices;

				for (int j = 0; j < vertices.Length; j++)
				{
					var worldVertex = child.TransformPoint(vertices[j]);
					worldVertex = originalPosition + deltaRotation * (worldVertex - originalPosition);
					vertices[j] = child.InverseTransformPoint(worldVertex);
				}

				meshFilter.sharedMesh.vertices = vertices;
				meshFilter.sharedMesh.RecalculateBounds();
			}

			child.rotation = selected.rotation;
			child.position = originalPosition;
		}
	}

	private void SavePrefab(Transform selected, GameObject prefabAsset)
    {
		Undo.RegisterCompleteObjectUndo(selected.gameObject, "Save Prefab");

		PrefabUtility.ApplyPrefabInstance(selected.gameObject, InteractionMode.UserAction);
        EditorSceneManager.SaveOpenScenes();
            
        var prefabPath = AssetDatabase.GetAssetPath(prefabAsset);
        var prefabStage = PrefabStageUtility.OpenPrefab(prefabPath);
            
        if (!prefabStage)
        {
            return;
        }
                
        var prefabRoot = prefabStage.prefabContentsRoot;

		prefabRoot.transform.position = Vector3.zero;
        prefabRoot.transform.rotation = Quaternion.identity;
            
        EditorSceneManager.MarkSceneDirty(prefabStage.scene);
        StageUtility.GoToMainStage();
    }

    private void ApplyChangesInScene(Transform selected, GameObject prefabAsset, Vector3 shift)
    {
        var instances = PrefabUtility.FindAllInstancesOfPrefab(prefabAsset.gameObject);
		var transformsToRecord = instances
			.Where(instance => instance.gameObject != selected.gameObject)
			.Select(instance => instance.transform)
			.ToArray();

		Undo.RecordObjects(transformsToRecord, "Apply Changes in Scene");

		foreach (var instance in instances)
        {
            if (instance.gameObject != selected.gameObject)
            {
				instance.transform.position += instance.transform.rotation * shift;
            }
        }
    }
}