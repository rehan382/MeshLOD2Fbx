using System;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace sc.meshlod2fbx.editor
{
    public class ExporterWindow : EditorWindow
    {
        [MenuItem("Tools/Mesh LOD Exporter")]
        public static void ShowWindow() => GetWindow<ExporterWindow>("Export Mesh LODs");
        
        public Mesh sourceMesh;
        public bool autoSelect;
        private static string exportPath
        {
            get => EditorPrefs.GetString(PlayerSettings.productName + "_MESHLOD_EXPORT_PATH", "");
            set => EditorPrefs.SetString(PlayerSettings.productName + "_MESHLOD_EXPORT_PATH", value);
        }

        public int maxLODCount = 1;
        public bool skipOdds;
        public bool recalculateNormals;

        private Mesh[] lods = Array.Empty<Mesh>();
        
        private Vector2 scrollPosition;
        private MeshPreview sourceMeshPreview;
        private const float previewSize = 200f;
        
        private Mesh previousSelection;
        
        private void OnEnable()
        {
            sourceMeshPreview = new MeshPreview(new Mesh());
        }
        
        void OnGUI()
        {
            #if !FBX_EXPORTER
            EditorGUILayout.HelpBox("FBX Exporter package requires to be installed through the Package Manager", MessageType.Error);
            return;
            #endif
            
            #if UNITY_6000_2_OR_NEWER
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledGroupScope(autoSelect))
                {
                    sourceMesh = EditorGUILayout.ObjectField("Source Mesh", sourceMesh, typeof(Mesh), false) as Mesh;
                }
                
                autoSelect = EditorGUILayout.ToggleLeft("Auto", autoSelect, GUILayout.Width(60f));
            }

            if (autoSelect && Selection.activeObject)
            {
                Type selectionType = Selection.activeObject.GetType();

                if (selectionType == typeof(Mesh))
                {
                    sourceMesh = (Mesh)Selection.activeObject;
                }
                else
                {
                    //EditorGUILayout.HelpBox("Selected object is not a mesh", MessageType.Warning);
                }
            }

            if (!sourceMesh) return;
            
            if (sourceMesh != previousSelection)
            {
                previousSelection = sourceMesh;
                
                GenerateLODs();
            }
            
            EditorGUILayout.LabelField(MeshPreview.GetInfoString(sourceMesh), EditorStyles.miniLabel);
            
            EditorGUILayout.Separator();
            
            EditorGUILayout.LabelField("LOD generation:", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            {
                maxLODCount = EditorGUILayout.IntSlider("Levels (max)", maxLODCount, 2, 6);

                EditorGUI.indentLevel++;
                skipOdds = EditorGUILayout.Toggle("Skip odd", skipOdds);
                EditorGUI.indentLevel--;
            }
            recalculateNormals = EditorGUILayout.Toggle("Recalculate normals", recalculateNormals);
            if (EditorGUI.EndChangeCheck()) GenerateLODs();
            
            EditorGUILayout.Separator();

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(previewSize + 25f));
            using (new EditorGUILayout.HorizontalScope(EditorStyles.textArea))
            {
                
                float height = this.position.width;
                for (int i = 0; i < lods.Length; i++)
                {
                    sourceMeshPreview.mesh = lods[i];

                    Rect previewRect = GUILayoutUtility.GetRect(previewSize, previewSize, GUILayout.Width(previewSize), GUILayout.Height(previewSize));
                    
                    var previewMouseOver = previewRect.Contains(Event.current.mousePosition);
                    var meshPreviewFocus = previewMouseOver && (Event.current.type == EventType.MouseDown || Event.current.type == EventType.MouseDrag);

                    if (meshPreviewFocus)
                    {
                        sourceMeshPreview.OnPreviewGUI(previewRect, GUIStyle.none);
                    }
                    else
                    {
                        if (Event.current.type == EventType.Repaint)
                        {
                            GUI.DrawTexture(previewRect, sourceMeshPreview.RenderStaticPreview((int)previewRect.width, (int)previewRect.height));
                        }
                    }

                    previewRect.y += previewRect.height - 22f;
                    previewRect.x += 5f;
                    previewRect.height = 22f;

                    //GUI.Label(previewRect, MeshPreview.GetInfoString(lods[i]), EditorStyles.miniLabel);
                    GUI.Label(previewRect, $"LOD {i}. Triangles: {lods[i].triangles.Length.ToString()}", EditorStyles.miniLabel);

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        //sourceMeshPreview.OnPreviewSettings();
                    }
                    
                    GUILayout.Space(5f);
                }
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.LabelField($"Generated {lods.Length} LODs", EditorStyles.miniLabel);
            
            EditorGUILayout.Separator();
            
            EditorGUILayout.LabelField("Save path:", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                exportPath = EditorGUILayout.TextField(exportPath, Styles.PathField);

                if (GUILayout.Button("Browse", GUILayout.MaxWidth(80f)))
                {
                    string filePath = EditorUtility.SaveFilePanel("FBX file target location", "Assets", sourceMesh.name, "fbx");

                    if (filePath != string.Empty)
                    {
                        //Convert to "Assets/" root folder
                        //filePath = filePath.Replace(Application.dataPath.Replace("/Assets", string.Empty), string.Empty);
                        //filePath = filePath.Substring(1);
                
                        exportPath = filePath;
                    }
                }
            }
        
            EditorGUILayout.Separator();

            using (new EditorGUI.DisabledGroupScope(exportPath.Length == 0 || lods == null || lods.Length == 0))
            {
                if (GUILayout.Button("- Export to FBX -", GUILayout.Height(50f)))
                {
                    Export();
                }
            }
            #else
            EditorGUILayout.HelpBox("Unity 6.2 or newer is required!", MessageType.Error);
            #endif
        }

        private void Export()
        {
            GameObject gameObject = MeshLODExporter.CreateObjects(lods);
            MeshLODExporter.ExportToFBX(gameObject, exportPath);
            Object.DestroyImmediate(gameObject);
        }

        private void GenerateLODs()
        {
            lods = MeshLODExporter.GenerateLODs(sourceMesh, maxLODCount, skipOdds, recalculateNormals);
        }

        private void OnDisable()
        {
            sourceMeshPreview.Dispose();
        }
        
        private class Styles
        {
            private static GUIStyle _PathField;
            public static GUIStyle PathField
            {
                get
                {
                    if (_PathField == null)
                    {
                        _PathField = new GUIStyle(GUI.skin.textField)
                        {
                            alignment = TextAnchor.MiddleRight,
                            stretchWidth = true
                        };
                    }

                    return _PathField;
                }
            }
        }
    }
}