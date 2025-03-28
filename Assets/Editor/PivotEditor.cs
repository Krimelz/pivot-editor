using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace Editor
{
    public class PivotEditor : EditorWindow
    {
        private Vector3 _min;
        private Vector3 _max;
        private Vector3 _shift;
        private Vector3[] _points;
        private Vector3[] _aligns;
        private int _selectedLevel;
        private int _selectedCorner;
        
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
        
        [MenuItem("Tools/Pivot Editor")]
        public static void ShowWindow()
        {
            var window = GetWindow<PivotEditor>();
            window.titleContent = new GUIContent("Pivot Editor");
            window.Show();
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;
            Selection.selectionChanged += Repaint;

            _shift = Vector3.zero;
            _points = new Vector3[8];
            _aligns = new Vector3[_levels.Length * _corners.Length];
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            Selection.selectionChanged -= Repaint;

            _shift = Vector3.zero;
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
            
            GUILayout.BeginHorizontal();
            
            GUILayout.BeginVertical();
            GUILayout.Label("Select level:", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            var selectedLevel = GUILayout.SelectionGrid(_selectedLevel, _levels, 1);
            if (EditorGUI.EndChangeCheck())
            {
                _selectedLevel = selectedLevel;
                DrawPivot();
            }
            GUILayout.EndVertical();
            
            GUILayout.BeginVertical();
            GUILayout.Label("Select corner:", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            var selectedCorner = GUILayout.SelectionGrid(_selectedCorner, _corners, 3);
            if (EditorGUI.EndChangeCheck())
            {
                _selectedCorner = selectedCorner;
                DrawPivot();
            }
            GUILayout.EndVertical();
        
            GUILayout.EndHorizontal();
            
            var prefabAsset = PrefabUtility.GetCorrespondingObjectFromSource(selected);

            if (GUILayout.Button("Save"))
            {
                AlignPivot(selected);

                if (prefabAsset)
                {
                    SavePrefab(selected, prefabAsset.gameObject);
                    ApplyChangesInScene(selected, prefabAsset.gameObject);
                }
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

        private void DrawPivot()
        {
            var align = _aligns[_selectedLevel * _corners.Length + _selectedCorner];
            
            Handles.color = Color.red;
            Handles.DrawWireCube(align, Vector3.one * 0.1f);
        }
        
        private void AlignPivot(Transform selected)
        {
            var align = _aligns[_selectedLevel * _corners.Length + _selectedCorner];
            _shift = selected.InverseTransformPoint(align);
            
            for (var i = 0; i < selected.childCount; i++)
            {
                var child = selected.GetChild(i);
                child.position += selected.position - align;
            }

            selected.position = align;

			for (var i = 0; i < selected.childCount; i++)
			{
				var child = selected.GetChild(i);

				var meshFilter = child.GetComponent<MeshFilter>();
				if (meshFilter == null) continue;

				var rootLocalPosition = child.InverseTransformPoint(selected.position);

				var mesh = Instantiate(meshFilter.sharedMesh);
				var vertices = mesh.vertices;

				for (int j = 0; j < vertices.Length; j++)
				{
					vertices[j] -= rootLocalPosition;
				}

				mesh.vertices = vertices;
				mesh.RecalculateBounds();
				meshFilter.sharedMesh = mesh;

				child.localPosition += rootLocalPosition;
			}
		}

        private void SavePrefab(Transform selected, GameObject prefabAsset)
        {
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

        private void ApplyChangesInScene(Transform selected, GameObject prefabAsset)
        {
            var instances = PrefabUtility.FindAllInstancesOfPrefab(prefabAsset.gameObject);
            
            foreach (var instance in instances)
            {
                if (instance.gameObject != selected.gameObject)
                {
                    instance.transform.position += instance.transform.rotation * _shift;
                }
            }
        }
    }
}