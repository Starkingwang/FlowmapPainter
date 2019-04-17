using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

#if UNITY_EDITOR
using UnityEditor;
#endif

[RequireComponent(typeof(MeshCollider))]
[ExecuteInEditMode]
public class DirmapPainter : MonoBehaviour
{
    public Texture2D viewBg;
    public Texture2D dirmap;
    public bool createMap;
    public bool createMesh;
    public bool clearMap;

    public Material materialViewer;
    public Material materialBaker;

    public bool showHandle;

    [Range(2, 10)]
    public int texelSize = 7;

    [Range(2, 8)]
    public int division = 7;

    [Range(0.001f, 1)]
    public float radius = 0.1f;

    [Range(0, 1)]
    public float strength = 0.1f;

    [Range(0, 1)]
    public float hard = 0.5f;

    static Matrix4x4 viewMatrix = new Matrix4x4()
    {
        m00 = 1,
        m01 = 0,
        m02 = 0,
        m03 = -0.5f,
        m10 = 0,
        m11 = -1.192093e-7f,
        m12 = 1,
        m13 = -0.5f,
        m20 = 0,
        m21 = 1,
        m22 = 1.192093e-7f,
        m23 = -1,
        m30 = 0,
        m31 = 0,
        m32 = 0,
        m33 = 1
    };

    static Matrix4x4 projMatrix = new Matrix4x4()
    {
        m00 = 2,
        m01 = 0,
        m02 = 0,
        m03 = 0,
        m10 = 0,
        m11 = 2,
        m12 = 0,
        m13 = 0,
        m20 = 0,
        m21 = 0,
        m22 = -1,
        m23 = -1,
        m30 = 0,
        m31 = 0,
        m32 = 0,
        m33 = 1
    };

    public Mesh Mesh
    {
        get
        {
            return GetComponent<MeshCollider>().sharedMesh;
        }
        set
        {
            GetComponent<MeshCollider>().sharedMesh = value;
        }
    }

    public Vector3 CurPos { get; set; }
    public Vector3 CurDir { get; set; }

    public void RebuildMesh(bool useCurDirmap = true)
    {
        if (Mesh)
        {
            DestroyImmediate(Mesh, false);
        }

        int pixel = 1 << division;
        int pixel0 = pixel - 1;
        int lastRowStart = pixel * pixel - pixel;
        var verts = new List<Vector3>();
        var uvs = new List<Vector2>();
        var tris = new List<int>();
        var normals = new List<Vector3>();
        var dirs = new List<Vector4>();
        int id = 0;
        for (int v = 0; v < pixel; v++)
        {
            for (int u = 0; u < pixel; u++)
            {
                var x = Mathf.InverseLerp(0, pixel0, u);
                var y = Mathf.InverseLerp(0, pixel0, v);
                verts.Add(new Vector3(x, 0, y));
                uvs.Add(new Vector2(x, y));
                normals.Add(Vector3.up);

                if (useCurDirmap && dirmap && dirmap.isReadable 
                    && (dirmap.format == TextureFormat.RGB24 
                    || dirmap.format == TextureFormat.ARGB32 
                    || dirmap.format == TextureFormat.RGBA32))
                {
                    var color = dirmap.GetPixelBilinear(x, y);
                    var dir = new Vector3(color.r, color.g, color.b);
                    dir.x = 1 - dir.x;
                    dir.z = 1 - dir.z;
                    dir = dir * 2 - Vector3.one;
                    dir.y = Mathf.Sqrt(1 - (dir.x * dir.x + dir.z * dir.z));
                    dirs.Add(dir);
                }
                else
                {
                    dirs.Add(Vector3.up);
                }

                if (id > 0)
                {
                    if ((id + 1) % pixel == 0)
                    {
                        id++;
                        continue;
                    }

                    if (id >= lastRowStart)
                    {
                        id++;
                        continue;
                    }
                }

                tris.Add(id);
                tris.Add(id + pixel);
                tris.Add(id + 1);
                tris.Add(id + pixel + 1);
                tris.Add(id + 1);
                tris.Add(id + pixel);

                id++;
            }
        }

        var mesh = new Mesh();
        mesh.SetVertices(verts);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.SetNormals(normals);
        mesh.SetTangents(dirs);

        Mesh = mesh;
    }

    public void RebuildMap()
    {
        if (Mesh && materialBaker)
        {
            string path = "Assets/dirmap.png";

            if (dirmap)
            {
                if (!dirmap.isReadable
                    || (dirmap.format != TextureFormat.RGB24
                    && dirmap.format != TextureFormat.ARGB32
                    && dirmap.format != TextureFormat.RGBA32))
                {
                    Debug.LogError("Make sure map is readable!");
                    return;
                }
#if UNITY_EDITOR
                path = AssetDatabase.GetAssetPath(dirmap);
#endif
            }
            else
            {
                dirmap = new Texture2D(1 << texelSize, 1 << texelSize, TextureFormat.RGB24, false, true)
                {
                    filterMode = FilterMode.Bilinear
                };
            }

            var rt = RenderTexture.GetTemporary(dirmap.width, dirmap.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            rt.filterMode = FilterMode.Bilinear;
            var buffer = new CommandBuffer();
            buffer.Clear();
            buffer.SetRenderTarget(rt);
            buffer.ClearRenderTarget(true, true, Color.black);
            buffer.SetViewProjectionMatrices(viewMatrix, projMatrix);
            buffer.DrawMesh(Mesh, Matrix4x4.identity, materialBaker);
            Graphics.ExecuteCommandBuffer(buffer);
            var act = RenderTexture.active;
            RenderTexture.active = rt;
            dirmap.ReadPixels(new Rect(0, 0, dirmap.width, dirmap.height), 0, 0);
            RenderTexture.ReleaseTemporary(rt);
            dirmap.Apply();
            System.IO.File.WriteAllBytes(path, dirmap.EncodeToPNG());
#if UNITY_EDITOR
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            dirmap = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
#endif

            if (materialViewer)
            {
                materialViewer.SetTexture("_FlowMap", dirmap);
            }
        }
    }

    public void ClearMap()
    {
        RebuildMesh(false);
        RebuildMap();
    }

    private void Update()
    {
        if (Mesh && materialViewer)
        {
            Graphics.DrawMesh(Mesh, transform.localToWorldMatrix, materialViewer, 0);
        }
    }

    private void OnDrawGizmos()
    {
        if (Mesh)
        {
            var matrix = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;
            var verts = Mesh.vertices;
            var dirs = Mesh.tangents;
            var count = verts.Length;
            var scale = 1 << division;

            if (dirs.Length == count && showHandle)
            {
                for (int i = 0; i < count; i++)
                {
                    Vector4 dir = dirs[i];

                    if (Vector3.Dot(Vector3.up, (Vector3)dir) > 0.99f)
                    {
                        continue;
                    }

                    Color color = (dir + Vector4.one) * 0.5f;
                    color.a = 1;
                    Gizmos.color = color.linear;
                    Gizmos.DrawLine(verts[i], verts[i] + (Vector3)dir / scale);
                }
            }

            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(CurPos, CurPos + CurDir * radius);

            Gizmos.color = Color.white;
            Gizmos.matrix = matrix;
        }
    }

    private void OnDisable()
    {
        if (Mesh)
        {
            DestroyImmediate(Mesh, false);
            Mesh = null;
        }
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(DirmapPainter))]
public class DirmapPainterEditor : Editor
{
    RaycastHit _hit;

    Vector3 _prePos;
    int _dragState = 0;

    private void OnSceneGUI()
    {
        var tar = target as DirmapPainter;

        Event e = Event.current;

        if (e.type == EventType.KeyDown)
        {
            if (e.keyCode == KeyCode.Minus || e.keyCode == KeyCode.LeftBracket)
            {
                tar.radius -= tar.radius * 0.1f;
            }
            else if (e.keyCode == KeyCode.Equals || e.keyCode == KeyCode.RightBracket)
            {
                tar.radius += tar.radius * 0.1f;
            }
            else if(e.keyCode == KeyCode.V)
            {
                tar.showHandle = !tar.showHandle;
            }
        }

        if (e.shift || e.alt || e.control || e.button != 0)
        {
            return;
        }

        HandleUtility.AddDefaultControl(0);

        if (e.type == EventType.MouseDown)
        {
            _dragState = 1;
        }
        else if (e.type == EventType.MouseUp)
        {
            _dragState = 0;
            tar.RebuildMap();
            tar.CurDir = Vector3.zero;
            return;
        }

        if (Physics.Raycast(HandleUtility.GUIPointToWorldRay(e.mousePosition), out _hit, 1000) 
            && _hit.collider is MeshCollider)
        {
            Handles.DrawWireDisc(_hit.point, _hit.normal, tar.radius);

            var mesh = (_hit.collider as MeshCollider).sharedMesh;

            if (!mesh.isReadable)
            {
                return;
            }

            if (_dragState > 0)
            {
                var uv = _hit.textureCoord;
                var pos = _hit.point;

                //>>> get brush range
                var rMin = uv - new Vector2(tar.radius, tar.radius);
                var rMax = uv + new Vector2(tar.radius, tar.radius);

                var pixel = 1 << tar.division;

                RectInt brushRange = new RectInt()
                {
                    xMin = (int)(Mathf.Clamp01(rMin.x) * pixel),
                    yMin = (int)(Mathf.Clamp01(rMin.y) * pixel),
                    xMax = (int)(Mathf.Clamp01(rMax.x) * pixel),
                    yMax = (int)(Mathf.Clamp01(rMax.y) * pixel)
                };
                //<<<

                //>>> get brush direction
                if (_dragState == 1)
                {
                    _prePos = pos;
                    _dragState = 2;
                }
                else if (_dragState == 2)
                {
                    var dir = pos - _prePos;
                    dir.y = 0;

                    if (dir.sqrMagnitude < 1e-16f)
                    {
                        return;
                    }

                    dir.Normalize();
                    tar.CurDir = dir;

                    var radSqr = tar.radius * tar.radius;
                    var dirs = mesh.tangents;

                    for (int y = brushRange.yMin; y < brushRange.yMax; y++)
                    {
                        var yr = (float)y / pixel - uv.y;
                        var yrSqr = yr * yr;

                        for (int x = brushRange.xMin; x < brushRange.xMax; x++)
                        {
                            var xr = (float)x / pixel - uv.x;
                            var xrSqr = xr * xr;

                            var sqrLen = xrSqr + yrSqr;

                            if (sqrLen > radSqr)
                            {
                                continue;
                            }

                            var len = Mathf.Sqrt(sqrLen);
                            var hard = Mathf.Max(0.001f, tar.hard * e.pressure);
                            var str = Mathf.SmoothStep(hard, 0, len / tar.radius) * tar.strength;

                            Vector3 oldDir = dirs[y * pixel + x];
                            Vector3 newDir = Vector3.Lerp(oldDir, dir, str).normalized;

                            dirs[y * pixel + x] = newDir;
                        }
                    }

                    mesh.tangents = dirs;

                    _prePos = pos;
                    tar.CurPos = pos;
                }
                //<<<
            }
        }
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        var tar = target as DirmapPainter;

        using (var c = new EditorGUI.ChangeCheckScope())
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("dirmap"), new GUIContent("Dirmap"));

            if (c.changed)
            {
                serializedObject.ApplyModifiedProperties();
                serializedObject.Update();     
            }
        }

        if (tar.dirmap)
        {
            if (!tar.dirmap.isReadable
                || (tar.dirmap.format != TextureFormat.RGB24
                && tar.dirmap.format != TextureFormat.ARGB32
                && tar.dirmap.format != TextureFormat.RGBA32))
            {
                if (GUILayout.Button("Fix Map"))
                {
                    var p = AssetDatabase.GetAssetPath(tar.dirmap);
                    var ti = AssetImporter.GetAtPath(p) as TextureImporter;
                    ti.isReadable = true;
                    ti.textureCompression = TextureImporterCompression.Uncompressed;
                    AssetDatabase.ImportAsset(p, ImportAssetOptions.ForceUpdate);
                }
            }
        }

        using (var c = new EditorGUI.ChangeCheckScope())
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("viewBg"), new GUIContent("Background"));

            if (c.changed)
            {
                serializedObject.ApplyModifiedProperties();
                serializedObject.Update();

                if (tar.materialViewer)
                {
                    tar.materialViewer.SetTexture("_MainTex", tar.viewBg);
                }
            }
        }

        using (var c = new EditorGUI.ChangeCheckScope())
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("materialViewer"), new GUIContent("Viewer"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("materialBaker"), new GUIContent("Baker"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("texelSize"), new GUIContent("Resolution"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("division"), new GUIContent("Subdivision"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("radius"), new GUIContent("Brush Size"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("strength"), new GUIContent("Brush Strength"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("hard"), new GUIContent("Brush Hardness"));

            if (c.changed)
            {
                serializedObject.ApplyModifiedProperties();
                serializedObject.Update();
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Create", GUILayout.Height(40)))
            {
                tar.RebuildMesh();
            }

            if (GUILayout.Button("Reset", GUILayout.Height(40)))
            {
                tar.ClearMap();
            }
        }

        EditorGUILayout.LabelField("", "(-)|(+) ([)|(]):Brush Size  (v):Show Dir Handle", "helpbox");
    }
}
#endif
