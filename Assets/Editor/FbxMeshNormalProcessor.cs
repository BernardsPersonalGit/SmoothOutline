using System;
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using Autodesk.Fbx;
using Mikktspace.NET;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

public static class FbxMeshNormalProcessor
{
    public static void FbxModelNormalSmoothTool(Object[] selectionObjects, int storeUvChannel)
    {
        if (selectionObjects.Length < 1)
        {
            return;
        }
        
        FbxManager fbxManager = FbxManager.Create();
        FbxIOSettings fbxIOSettings = FbxIOSettings.Create(fbxManager, Globals.IOSROOT);
        fbxManager.SetIOSettings(fbxIOSettings);

        int fbxFileCount = 0;
        int smoothedCount = 0;
        foreach (Object asset in selectionObjects)
        {
            string assetPath = AssetDatabase.GetAssetPath(asset);
            string oldExt = Path.GetExtension(assetPath).ToLower();
            if (!oldExt.Equals(".fbx"))
            {
                continue;
            }

            FbxImporter fbxImporter = FbxImporter.Create(fbxManager, "");
            if (!fbxImporter.Initialize(assetPath, -1, fbxIOSettings))
            {
                Debug.Log(fbxImporter.GetStatus().GetErrorString());
                continue;
            }
            FbxScene fbxScene = FbxScene.Create(fbxManager, "myScene");
            fbxImporter.Import(fbxScene);
            fbxImporter.Destroy();
            FbxNode rootNode = fbxScene.GetRootNode();
            if (rootNode == null)
            {
                continue;
            }

            fbxFileCount++;
            SearchMeshNode(rootNode, storeUvChannel);
            
            FbxExporter fbxExporter = FbxExporter.Create(fbxManager, "");
            if (!fbxExporter.Initialize(assetPath.Substring(0, assetPath.Length - 4) + "Smooth.fbx", -1, fbxIOSettings))
            {
                Debug.Log(fbxExporter.GetStatus().GetErrorString());
                continue;
            }

            smoothedCount++;
            fbxExporter.Export(fbxScene);
            fbxExporter.Destroy();
        }
        fbxManager.Destroy();
        AssetDatabase.Refresh();
        Debug.Log($"Fbx模型法线平滑化完成!\n共选中{fbxFileCount}个有效的FBX模型文件, 已成功平滑化处理{smoothedCount}个模型文件。");
    }

    static void SearchMeshNode(FbxNode currentNode, int storeUvChannel)
    {
        FbxNodeAttribute nodeAttribute = currentNode.GetNodeAttribute();
        
        if (nodeAttribute != null)
        {
            FbxNodeAttribute.EType attributeType = nodeAttribute.GetAttributeType();
            if (attributeType == FbxNodeAttribute.EType.eMesh)
            {
                SmoothMeshNode(currentNode.GetMesh(), storeUvChannel);
            }
        }
        for (int i = 0; i < currentNode.GetChildCount(); i++)
        {
            SearchMeshNode(currentNode.GetChild(i), storeUvChannel);
        }
    }

    struct VertexInfo
    {
        public int vertexIndex;
        public FbxVector4 normal;
        public double weight;
    }

    static void SmoothMeshNode(FbxMesh fbxMesh, int storeUvChannel)
    {
        int controlPointsCount = fbxMesh.GetControlPointsCount();
        List<List<VertexInfo>> controlIndexToVertexInfos = new List<List<VertexInfo>>();
        for (int i = 0; i < controlPointsCount; i++)
        {
            controlIndexToVertexInfos.Add(new List<VertexInfo>());
        }

        FbxVector4[] meshTangents = GetMeshTangents(fbxMesh);
        
        int vertexIndex = 0;
        for (int polygonIndex = 0; polygonIndex < fbxMesh.GetPolygonCount(); polygonIndex++)
        {
            int vertexCountInPolygon = fbxMesh.GetPolygonSize(polygonIndex);

            for (int vertexIndexInPolygon = 0; vertexIndexInPolygon < vertexCountInPolygon; vertexIndexInPolygon++)
            {
                int lastVertexIndex = (vertexIndexInPolygon - 1 + vertexCountInPolygon) % vertexCountInPolygon;
                int nextVertexIndex = (vertexIndexInPolygon + 1) % vertexCountInPolygon;
                
                int controlIndex = fbxMesh.GetPolygonVertex(polygonIndex, vertexIndexInPolygon);
                int lastControlIndex = fbxMesh.GetPolygonVertex(polygonIndex, lastVertexIndex);
                int nextControlIndex = fbxMesh.GetPolygonVertex(polygonIndex, nextVertexIndex);
                
                FbxVector4 controlPoint = fbxMesh.GetControlPointAt(controlIndex);
                FbxVector4 lastControlPoint = fbxMesh.GetControlPointAt(lastControlIndex);
                FbxVector4 nextControlPoint = fbxMesh.GetControlPointAt(nextControlIndex);
                
                fbxMesh.GetPolygonVertexNormal(polygonIndex, vertexIndexInPolygon, out FbxVector4 vertexNormal);
                vertexNormal /= vertexNormal.Length();
                
                FbxVector4 edge0 = lastControlPoint - controlPoint;
                FbxVector4 edge1 = nextControlPoint - controlPoint;

                edge0 /= edge0.Length();
                edge1 /= edge1.Length();
                
                double radian = Math.Acos(edge0.DotProduct(edge1));
                
                List<VertexInfo> vertexNormals = controlIndexToVertexInfos[controlIndex];
                vertexNormals.Add(new VertexInfo
                {
                    vertexIndex = vertexIndex++,
                    normal = vertexNormal,
                    weight = radian
                });
            }
        }

        int layerCount = fbxMesh.GetLayerCount();
        for (int i = 0; i < storeUvChannel - layerCount + 1; i++)
        {
            int layerIndex = fbxMesh.CreateLayer();
            FbxLayer layer = fbxMesh.GetLayer(layerIndex);
            layer.SetUVs(FbxLayerElementUV.Create(fbxMesh, ""));
        }

        FbxLayer targetLayer = fbxMesh.GetLayer(storeUvChannel);
        FbxLayerElementUV targetUv = targetLayer.GetUVs();
        // if (targetUv == null)
        // {
        //     targetUv = FbxLayerElementUV.Create(fbxMesh, "");
        //     targetLayer.SetUVs(targetUv);
        // }
        targetUv.SetMappingMode(FbxLayerElement.EMappingMode.eByPolygonVertex);
        targetUv.SetReferenceMode(FbxLayerElement.EReferenceMode.eDirect);
        FbxLayerElementArrayTemplateFbxVector2 uvDirectArray = targetUv.GetDirectArray();
        uvDirectArray.SetCount(vertexIndex);

        for (int controlIndex = 0; controlIndex < controlPointsCount; controlIndex++)
        {
            List<VertexInfo> vertexInfos = controlIndexToVertexInfos[controlIndex];

            FbxVector4 smoothNormal = new FbxVector4();
            foreach (VertexInfo vertexInfo in vertexInfos)
            {
                smoothNormal += vertexInfo.weight * vertexInfo.normal;
            }
            smoothNormal /= smoothNormal.Length();

            foreach (VertexInfo vertexInfo in vertexInfos)
            {
                FbxVector4 tangent = meshTangents[vertexInfo.vertexIndex];
                FbxVector4 binormal = vertexInfo.normal.CrossProduct(tangent) * tangent.W;
                double smoothNormalTS_X = smoothNormal.DotProduct(tangent);
                double smoothNormalTS_Y = smoothNormal.DotProduct(binormal);
                double smoothNormalTS_Z = smoothNormal.DotProduct(vertexInfo.normal);
                FbxVector4 smoothNormalTS = new FbxVector4(smoothNormalTS_X, smoothNormalTS_Y, smoothNormalTS_Z);
                smoothNormalTS /= smoothNormalTS.Length();
                uvDirectArray.SetAt(vertexInfo.vertexIndex, UnitVectorToOctahedron(smoothNormalTS));
            }
        }
    }

    static FbxVector4[] GetMeshTangents(FbxMesh fbxMesh)
    {
        int polygonCount = fbxMesh.GetPolygonCount();
        void getPosition(int polygonIndex, int indexInPolygon, out float x, out float y, out float z)
        {
            int controlIndex = fbxMesh.GetPolygonVertex(polygonIndex, indexInPolygon);
            FbxVector4 position = fbxMesh.GetControlPointAt(controlIndex);
            x = (float) position.X;
            y = (float) position.Y;
            z = (float) position.Z;
        }
        
        void getNormal(int polygonIndex, int indexInPolygon, out float x, out float y, out float z)
        {
            fbxMesh.GetPolygonVertexNormal(polygonIndex, indexInPolygon, out FbxVector4 normal);
            normal /= normal.Length();
            x = (float) normal.X;
            y = (float) normal.Y;
            z = (float) normal.Z;
        }
        
        List<List<int>> uvIndexsInPolygons = new List<List<int>>();
        FbxLayer layer = fbxMesh.GetLayer(0);
        FbxLayerElementUV uvs = layer.GetUVs();
        FbxLayerElementArrayTemplateFbxVector2 directArray = uvs.GetDirectArray();
        if (uvs.GetReferenceMode() == FbxLayerElement.EReferenceMode.eDirect)
        {
            int uvIndex = 0;
            for (int i = 0; i < polygonCount; i++)
            {
                List<int> uvIndexs = new List<int>();
                for (int j = 0; j < fbxMesh.GetPolygonSize(i); j++)
                {
                    uvIndexs.Add(uvIndex++);
                }
                uvIndexsInPolygons.Add(uvIndexs);
            }
        }
        else
        {
            FbxLayerElementArrayTemplateInt indexArray = uvs.GetIndexArray();
            int uvIndex = 0;
            for (int i = 0; i < polygonCount; i++)
            {
                List<int> uvIndexs = new List<int>();
                for (int j = 0; j < fbxMesh.GetPolygonSize(i); j++)
                {
                    uvIndexs.Add(indexArray.GetAt(uvIndex++));
                }
                uvIndexsInPolygons.Add(uvIndexs);
            }
        }
        
        void getUV(int polygonIndex, int indexInPolygon, out float u, out float v)
        {
            FbxVector2 uv = directArray.GetAt(uvIndexsInPolygons[polygonIndex][indexInPolygon]);
            u = (float) uv.X;
            v = (float) uv.Y;
        }
        FbxVector4[,] polygonTangents = new FbxVector4[polygonCount, 4];

        void setTangent(int polygonIndex, int indexInPolygon, float tangentX, float tangentY, float tangentZ, float sign)
        {
            polygonTangents[polygonIndex, indexInPolygon] = new FbxVector4(tangentX, tangentY, tangentZ, sign);
        }
        
        MikkGenerator.GenerateTangentSpace(polygonCount, fbxMesh.GetPolygonSize, getPosition, getNormal, getUV,
            setTangent);
        
        FbxVector4[] tangents = new FbxVector4[polygonCount * 4];
        int index = 0;
        for (int i = 0; i < polygonCount; i++)
        {
            for (int j = 0; j < fbxMesh.GetPolygonSize(i); j++)
            {
                tangents[index++] = polygonTangents[i, j];
            }
        }

        return tangents;
    }

    static FbxVector2 UnitVectorToOctahedron(FbxVector4 unitVec)
    {
        var absX = Math.Abs(unitVec.X);
        var absY = Math.Abs(unitVec.Y);
        var absZ = Math.Abs(unitVec.Z);
        var absDotOne = absX + absY + absZ;
        FbxVector2 result = new FbxVector2(unitVec.X, unitVec.Y);
        result /= absDotOne;
        if (unitVec.Z <= 0)
        {
            result = new FbxVector2(
                (1 - Math.Abs(result.Y)) * (result.X >= 0 ? 1 : -1),
                (1 - Math.Abs(result.X)) * (result.Y >= 0 ? 1 : -1)
                );
        }
        return result;
    }
}

public class FbxMeshSmoothToolWindow : EditorWindow
{
    private static readonly Vector2 MIN_SIZE = new Vector2(320, 120);
    private static readonly Vector2 MAX_SIZE = new Vector2(320, 120);
    private static readonly string[] UV_CHANNELS =
    {
        "UV 2",
        "UV 3",
        "UV 4",
        "UV 5",
        "UV 6",
        "UV 7",
        "UV 8",
    };
    
    private static FbxMeshSmoothToolWindow instance;
    
    private Object[] objects;
    private int selectedChannel = 0;
    
    private GUIStyle labelStyle;
    
    [MenuItem("Assets/生成描边法线")]
    public static void OnpenWindow()
    {
        var selectedObjects = Selection.GetFiltered(typeof(Object), SelectionMode.Assets);
        if (selectedObjects.Length < 1)
        {
            Debug.Log("未选中任何资源文件");
            return;
        }
        instance = GetWindow<FbxMeshSmoothToolWindow>("生成描边法线", true);
        instance.minSize = MIN_SIZE;
        instance.maxSize = MAX_SIZE;
        
        instance.objects = selectedObjects;
    }

    private void OnGUI()
    {
        labelStyle ??= new GUIStyle(GUI.skin.label) {fontSize = 20, clipping = TextClipping.Overflow};
        EditorGUILayout.Space();
        EditorGUILayout.LabelField($"已选中 {objects.Length} 个资源文件", labelStyle);
        EditorGUILayout.Space();
        selectedChannel = EditorGUILayout.Popup("选择保存描边法线的UV通道", selectedChannel, UV_CHANNELS);
        EditorGUILayout.Space(20);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("确定", GUILayout.Width(100), GUILayout.Height(30)))
        {
            FbxMeshNormalProcessor.FbxModelNormalSmoothTool(objects, selectedChannel + 1);
            Close();
        }
        EditorGUILayout.Space();
        if (GUILayout.Button("取消", GUILayout.Width(100), GUILayout.Height(30)))
        {
            Close();
        }
        EditorGUILayout.EndHorizontal();
    }

    private void OnDisable()
    {
        objects = null;
    }
}