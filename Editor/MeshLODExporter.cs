using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace sc.meshlod2fbx.editor
{
    public static class MeshLODExporter
    {
        public static Mesh[] GenerateLODs(Mesh source, int maxCount, bool skipOdd, bool recalculateNormals)
        {
            #if UNITY_6000_2_OR_NEWER
            Mesh meshLOD = Object.Instantiate(source);
            meshLOD.name = source.name.Replace("_LOD0", string.Empty);
        
            int lodCount = Mathf.Max(1, maxCount);
            MeshLodUtility.GenerateMeshLods(meshLOD, skipOdd ? MeshLodUtility.LodGenerationFlags.DiscardOddLevels : (MeshLodUtility.LodGenerationFlags)0, lodCount-1);
        
            //Safeguard against error: The Mesh LOD index (#) must be less than the lodCount value (#)
            lodCount = Mathf.Min(lodCount, meshLOD.lodCount);
            
            //Extract triangles for new individual meshes
            Mesh[] lods = new Mesh[lodCount];
            for (int i = 0; i < lodCount; i++)
            {
                Mesh lodMesh = new Mesh();

                //Copy vertices, uvs, normals, etc...
                EditorUtility.CopySerialized(meshLOD, lodMesh);

                int subMeshCount = meshLOD.subMeshCount;
                for (int j = 0; j < subMeshCount; j++)
                {
                    int[] triangles = meshLOD.GetTriangles(j, i, false);
                    lodMesh.SetTriangles(triangles, j, false);
                }
                lodMesh.name = $"{meshLOD.name}_LOD{i}";
                
                if(recalculateNormals) lodMesh.RecalculateNormals();
                
                lods[i] = lodMesh;
            }
        
            Object.DestroyImmediate(meshLOD);

            return lods;
            #else
            return null;
            #endif
        }

        public static GameObject CreateObjects(Mesh[] lodMeshes)
        {
            GameObject root = new GameObject();
            
            LODGroup lodGroup = root.AddComponent<LODGroup>();
            LOD[] m_lods = new LOD[lodMeshes.Length];
            
            for (int i = 0; i < lodMeshes.Length; i++)
            {
                GameObject obj = new GameObject(lodMeshes[i].name, typeof(MeshFilter), typeof(MeshRenderer));
                obj.transform.SetParent(root.transform);
                
                MeshFilter filter = obj.GetComponent<MeshFilter>();
                filter.sharedMesh = lodMeshes[i];
                
                MeshRenderer renderer = obj.GetComponent<MeshRenderer>();

                float t = 1f-((float)i / (lodMeshes.Length));
                m_lods[i] = new LOD(t, new Renderer[] { renderer });
            }
            
            lodGroup.SetLODs(m_lods);

            return root;
        }
        
        public static void ExportToFBX(GameObject gameObject, string filePath)
        {
            #if FBX_EXPORTER
            if (filePath == string.Empty)
            {
                throw new Exception("Failed to save mesh(es) to an FBX file, file path is empty");
            }
            if (filePath.EndsWith(".fbx") == false)
            {
                throw new Exception("Failed to save mesh(es) to an FBX file, file path must end with \".fbx\"");
            }
            
            //Export (requires FBX Exporter package!)
            UnityEditor.Formats.Fbx.Exporter.ModelExporter.ExportObject(filePath, gameObject);
            
            string projectPath = Path.GetFullPath(Application.dataPath + "/..").Replace('\\', '/');
            if (filePath.StartsWith(projectPath))
            {
                //Convert to project-relative path (eg. Assets/Folder1/FileA)
                filePath = filePath.Substring(projectPath.Length + 1);
                
                AssetDatabase.ImportAsset(filePath, ImportAssetOptions.ForceSynchronousImport);
            }
            #else
            throw new Exception("Cannot export to FBX. \"FBX Exporter\" package requires to be installed");
            #endif
        }
    }
}