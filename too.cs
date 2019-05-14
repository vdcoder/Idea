using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

[ExecuteAlways]
public class TrivialObjectEditorServices : MonoBehaviour
{
    public int nBaseDelayMS = 500; // Minimum and multiple for animation delays

    public string sFileName;
    public string sScriptName;
    public bool   bAnimate;

    public class ScriptType
    {
        public string               sName;
        public List<List<string>>   Cmds = new List<List<string>>(); 
    }

    public class PointType
    {
        public float x, y, xd, yd; 
    }

    public class PathType
    {
        public string               sName;
        public List<PointType>      Points = new List<PointType>(); 
    }

    public class TextureInsertType
    {
        public int nStart, nTxt; 
    }

    public class TimerTickType
    {
        public int      nMS;
        public float    nCountDown; 
    }

    public Dictionary<string, ScriptType>   Scripts  = new Dictionary<string, ScriptType>();
    public Dictionary<string, PathType>     Paths    = new Dictionary<string, PathType>();

    public Vector3 StarPos, StarTarget, StarRight;

    public string debug;
    public int nd = 0;
    public bool reload;

    void Start()
    {
        if (bAnimate)
            StartCoroutine(Animate());
        if (Application.isPlaying)
            Process(sFileName);
    }

    void Update()
    {
        if (!Application.isPlaying)
            Process(sFileName);
    }

    void OnDrawGizmosSelected()
    {
        if (m_GizmosPoints.Count > 0)
        {
            Gizmos.color = Color.blue;
            foreach (var p in m_GizmosPoints)
            {
                Gizmos.DrawLine(new Vector3(p.x, p.y, -1000), new Vector3(p.x, p.y, 1000));
                Gizmos.DrawLine(new Vector3(p.x, -1000, p.z), new Vector3(p.x, 1000, p.z));
                Gizmos.DrawLine(new Vector3(-1000, p.y, p.z), new Vector3(1000, p.y, p.z));
            }
        }
    }

    IEnumerator Animate()
    {
        while (true)
        {
            yield return new WaitForSeconds(nBaseDelayMS * 0.001f);
            m_nAnimationPointer++;
            if (m_nAnimationPointer == long.MaxValue)
                m_nAnimationPointer = 0;
            if (m_sFileContentsCache != "")
                ProcessAnimate();
        }
    }

    void Process(string a_sFileName)
    {
        ProcessInitialize();
        var Cmds = ProcessLoadScript(a_sFileName, sScriptName);

        debug = "";
        foreach (var Cmd in Cmds)
            ProcessCommand(Cmd);
        ProcessFinalize();
    }

    void ProcessAnimate()
    {
        Process("");
    }

    void ProcessInitialize()
    {
        m_TxtOffset = new Vector2[16];
        for (int i = 0; i < 16; i++)
            m_TxtOffset[i] = new Vector2((3 - ((15 - i) % 4)) * 0.25f, ((15 - i) / 4) * 0.25f);
        m_TxtScale = new Vector2[16];
        float nMaxSide = 50f;
        float nSafeScale = Mathf.Sqrt(2 * nMaxSide * nMaxSide);
        float nSafeScaleTo025 = 1 / (nSafeScale * 4);
        for (int i = 0; i < 16; i++)
            m_TxtScale[i] = new Vector2(nSafeScaleTo025, nSafeScaleTo025);

        m_Mesh = new Mesh();
        m_Verts = new List<Vector3>();
        m_Tris = new List<int>();
        m_Normals = new List<Vector3>();
        m_Uvs = new List<Vector2>();

        m_nMirrorFlag = 1;
        m_bWasClosed = false;
        m_bOpenOnFirst = false;
        m_Pos = new Vector3(0, 0, 0);
        m_Target = new Vector3(0, 0, -1);
        m_Right = new Vector3(-1, 0, 0);
        m_Txt = new List<TextureInsertType>();
        m_Txt.Add(new TextureInsertType() { nStart=0, nTxt=0 });

        m_GizmosPoints.Clear();
    }

    List<List<string>> ProcessLoadScript(string a_sFileName, string a_sScriptName)
    {
        if (a_sFileName != "")
            m_sFileContentsCache = File.ReadAllText(a_sFileName);
        string[] Blocks = m_sFileContentsCache.Split(';');
        foreach (var Block in Blocks)
        {
            string[] Cmds = Block.Split(' ');
            if (Cmds[0].StartsWith("script,", StringComparison.InvariantCulture)) // script
            {
                ScriptType ScriptObj = new ScriptType();
                for (int i = 0; i < Cmds.Length; i++)
                {
                    var Cmd = Cmds[i];
                    string[] Terms = Cmd.Split(',');
                    if (i == 0)
                        ScriptObj.sName = Terms[1];
                    else
                    {
                        // Check if next cmd is repeat
                        int nRepeat = 1;
                        if (i + 1 < Cmds.Length)
                        {
                            string[] NextCmdTerms = Cmds[i + 1].Split(',');
                            if (NextCmdTerms[0] == "r")
                            {
                                nRepeat = int.Parse(NextCmdTerms[1]);
                                i++;
                            }
                        }
                        for (int j = 0; j < nRepeat; j++) // Repeat
                        {
                            if (Terms[0] == "b" || Terms[0] == "c") // Block include or block cycle
                            {
                                // Get script name
                                var sScriptObjInclude = Terms[1];
                                if (Terms[0] == "c")
                                {
                                    int nRepeatScale = 1 + (int.Parse(Terms[1]) - 1) / nBaseDelayMS;
                                    sScriptObjInclude = Terms[2 + (m_nAnimationPointer % ((Terms.Length - 2) * nRepeatScale)) / nRepeatScale];
                                }

                                // Add script block body
                                var ScriptObjInclude = Scripts[sScriptObjInclude];
                                for (int k = 0; k < ScriptObjInclude.Cmds.Count; k++)
                                {
                                    ScriptObj.Cmds.Add(new List<string>());
                                    foreach (string sTerm in ScriptObjInclude.Cmds[k])
                                        ScriptObj.Cmds[ScriptObj.Cmds.Count - 1].Add(sTerm);
                                }
                            }
                            else // regular command
                            {
                                ScriptObj.Cmds.Add(new List<string>());
                                foreach (var Term in Terms)
                                    ScriptObj.Cmds[ScriptObj.Cmds.Count - 1].Add(Term);
                            }
                        }
                    }
                }
                Scripts[ScriptObj.sName] = ScriptObj;
            }
            else // path
            { 
                PathType PathObj = new PathType();
                var Cmd = Cmds[0];
                string[] Terms = Cmd.Split(',');
                PathObj.sName = Terms[1];
                int nPoints = int.Parse(Terms[2]);
                for (int i = 0; i < nPoints; i++)
                    PathObj.Points.Add(new PointType() { x = float.Parse(Terms[3 + i * 4]), y = float.Parse(Terms[4 + i * 4]), xd = float.Parse(Terms[5 + i * 4]), yd = float.Parse(Terms[6 + i * 4])});
                Paths[PathObj.sName] = PathObj;
                if (m_sPathName == "") // point to first path by default
                    m_sPathName = PathObj.sName;
            }
        }
        return Scripts[a_sScriptName].Cmds; // return desired script commands
    }

    void ProcessFinalize()
    {
        m_Mesh.vertices = m_Verts.ToArray();
        m_Mesh.triangles = m_Tris.ToArray();
        m_Mesh.normals = m_Normals.ToArray();
        m_Mesh.uv = m_Uvs.ToArray();
        m_Mesh.RecalculateBounds();

        var MeshFilter = GetComponent<MeshFilter>();
        MeshFilter.mesh = m_Mesh;

        MeshCollider MCollider = GetComponent<MeshCollider>();
        MCollider.sharedMesh = null;
        MCollider.sharedMesh = m_Mesh;
    }

    void ProcessCommand(List<string> a_Terms)
    {
        List<TextureInsertType> NewTxt = null;
        switch (a_Terms[0])
        {
            case "//": // comment, return
                return;
            case "*":
                StarPos = m_Pos;
                StarTarget = m_Target;
                StarRight = m_Right;
                break;
            case "sa": // ["Set All", "Path Name", "Mirror Flag", "Scale X", "Scale Max Radius X", "Scale Y", "Scale Max Radius Y", "Position X", "Position Y", "Position Z",
                       //  "Target X", "Target Y", "Target Z", "Right X", "Right Y", "Right Z", "Texture", ("Point Index", "Texture")...]
                m_sPathName = a_Terms[1];
                m_nMirrorFlag = a_Terms[2] == "1" ? -1 : 1;
                m_nScaleX = float.Parse(a_Terms[3]);
                m_nScaleMaxRadiusX = float.Parse(a_Terms[4]);
                m_nScaleY = float.Parse(a_Terms[5]);
                m_nScaleMaxRadiusY = float.Parse(a_Terms[6]);
                m_Pos.x = float.Parse(a_Terms[7]);
                m_Pos.y = float.Parse(a_Terms[8]);
                m_Pos.z = float.Parse(a_Terms[9]);
                m_Target.x = float.Parse(a_Terms[10]);
                m_Target.y = float.Parse(a_Terms[11]);
                m_Target.z = float.Parse(a_Terms[12]);
                m_Right.x = float.Parse(a_Terms[13]);
                m_Right.y = float.Parse(a_Terms[14]);
                m_Right.z = float.Parse(a_Terms[15]);
                m_Txt.Clear();
                m_Txt.Add(new TextureInsertType() { nStart = 0, nTxt = int.Parse(a_Terms[16]) });
                for (int i = 17; i < a_Terms.Count; i += 2)
                     m_Txt.Add(new TextureInsertType() { nStart = int.Parse(a_Terms[i]), nTxt = int.Parse(a_Terms[i + 1]) });
                break;
            case "st": // ["Set Texture", "Texture", ("Point Index", "Texture")...]
                NewTxt = new List<TextureInsertType>();
                NewTxt.Add(new TextureInsertType() { nStart = 0, nTxt = (a_Terms[1] == "-1" ? m_Txt[0].nTxt : int.Parse(a_Terms[1])) });
                for (int i = 2; i < a_Terms.Count; i += 2)
                     NewTxt.Add(new TextureInsertType() { 
                        nStart  = (a_Terms[i    ] == "-1" ? m_Txt[i / 2].nStart : int.Parse(a_Terms[i    ])), 
                        nTxt    = (a_Terms[i + 1] == "-1" ? m_Txt[i / 2].nTxt   : int.Parse(a_Terms[i + 1])) });
                m_Txt = NewTxt;
                break;
            case "sp": // ["Set Path", "Path Name", "Mirror Flag", "Scale X", "Scale Max Radius X", "Scale Y", "Scale Max Radius Y"]
                m_sPathName = a_Terms[1];
                m_nMirrorFlag = a_Terms[2] == "1" ? -1 : 1;
                m_nScaleX = float.Parse(a_Terms[3]);
                m_nScaleMaxRadiusX = float.Parse(a_Terms[4]);
                m_nScaleY = float.Parse(a_Terms[5]);
                m_nScaleMaxRadiusY = float.Parse(a_Terms[6]);
                break;
            case "spos": // ["Set Position", "Position X", "Position Y", "Position Z"]
                m_Pos.x = a_Terms[1] == "p" ? m_Pos.x : float.Parse(a_Terms[1]);
                m_Pos.y = a_Terms[2] == "p" ? m_Pos.y : float.Parse(a_Terms[2]);
                m_Pos.z = a_Terms[3] == "p" ? m_Pos.z : float.Parse(a_Terms[3]);
                break;
            case "str": // ["Set Target and Right", "Target X", "Target Y", "Target Z", "Right X", "Right Y", "Right Z"]
                m_Target.x = float.Parse(a_Terms[1]);
                m_Target.y = float.Parse(a_Terms[2]);
                m_Target.z = float.Parse(a_Terms[3]);
                m_Right.x = float.Parse(a_Terms[4]);
                m_Right.y = float.Parse(a_Terms[5]);
                m_Right.z = float.Parse(a_Terms[6]);
                break;
            case "sf": // ["Step Forward", "Distance"]
                m_Pos += m_Target * float.Parse(a_Terms[1]);
                break;
            case "sr": // ["Step Rotate AB", "Distance", "A Angle (Degrees)", "B Angle (Degrees)"]
                {
                    var RotationQuat = Quaternion.Euler(float.Parse(a_Terms[3]), float.Parse(a_Terms[2]), 0);
                    m_Target = RotationQuat * m_Target;
                    m_Right = RotationQuat * m_Right;
                    m_Pos += m_Target * float.Parse(a_Terms[1]);
                }
                break;
            case "srabc": // ["Step Rotate ABC", "Distance", "A Angle (Degrees)", "B Angle (Degrees)", "C Angle (Degrees)"]
                {
                    var RotationQuat = Quaternion.Euler(float.Parse(a_Terms[3]), float.Parse(a_Terms[2]), float.Parse(a_Terms[4]));
                    m_Target = RotationQuat * m_Target;
                    m_Right = RotationQuat * m_Right;
                    m_Pos += m_Target * float.Parse(a_Terms[1]);
                }
                break;
            case "cl": // ["Close"]
                break;
            case "op": // ["Open"]
                m_bOpenOnFirst = true;
                break;
        }

        // Precalculate
        var PathObj = Paths[m_sPathName];
        var PathPoints = PathObj.Points;
        int nVerts = PathPoints.Count * 2;
        int nVertsh = PathPoints.Count;
        Vector3 Up = Vector3.Cross(m_Target, m_Right); // Left handed

        // Generate
        if (a_Terms[0] == "st" || a_Terms[0] == "sp" || a_Terms[0] == "str" || a_Terms[0] == "op") // No op
            ;
        else if (a_Terms[0] == "*")
        {
            m_GizmosPoints.Add(transform.position + m_Pos);
        }
        else if (m_Verts.Count == 0 || m_bWasClosed) // First time
        {
            debug += ",first-time";
            if (m_bOpenOnFirst)
            {
                debug += "-with-open";
                m_Verts.AddRange(GenVerts(m_Right, Up));
                GenNormals(m_Target, m_Right, Up);
                SetNormals("OP");
                GenEmptyUVs();

                // Gen open tris
                var p = m_Verts.Count - nVerts;
                for (int i = 0; i < nVertsh - 1; i++)
                {
                    int p1 = p + i, p2 = p + i + 1, p3 = (m_Verts.Count - 1) - (i + 1), p4 = (m_Verts.Count - 1) - i;
                    m_Tris.AddRange(new int[] { p1, m_nMirrorFlag == 1 ? p2 : p3, m_nMirrorFlag == 1 ? p3 : p2 });
                    m_Tris.AddRange(new int[] { p3, m_nMirrorFlag == 1 ? p4 : p1, m_nMirrorFlag == 1 ? p1 : p4 });
                }
            }

            m_Verts.AddRange(GenVerts(m_Right, Up));
            GenNormals(m_Target, m_Right, Up);
            GenEmptyUVs();

            m_bWasClosed = false;
            m_bOpenOnFirst = false;
        }
        else if (a_Terms[0] == "cl") // Has prev verts, close
        {
            debug += ",close";
            SetNormals("CL");

            // Gen close tris
            var p = m_Verts.Count - nVerts;
            for (int i = 0; i < nVertsh - 1; i++)
            {
                int p1 = p + i, p2 = p + i + 1, p3 = (m_Verts.Count - 1) - (i + 1), p4 = (m_Verts.Count - 1) - i;
                m_Tris.AddRange(new int[] { p1, m_nMirrorFlag == 1 ? p3 : p2, m_nMirrorFlag == 1 ? p2 : p3 });
                m_Tris.AddRange(new int[] { p3, m_nMirrorFlag == 1 ? p1 : p4, m_nMirrorFlag == 1 ? p4 : p1 });
            }

            m_bWasClosed = true;
        }
        else // Has prev verts, step
        {
            debug += ",step";
            nd++;
            DupLastNPoints(); SetNormals("XX", nVerts); SetNormals("X");
            m_Verts.AddRange(GenVerts(m_Right, Up));
            m_Verts.AddRange(GenVerts(m_Right, Up));
            GenNormals(m_Target, m_Right, Up); SetNormals("XX");
            GenNormals(m_Target, m_Right, Up); SetNormals("X");
            GenEmptyUVs(); GenEmptyUVs();

            // Pick the right texture 
            int[] Txt = new int[nVerts];
            for (int i = 0; i < nVerts; i++)
            {
                int j = 0;
                for (; j < m_Txt.Count && i >= m_Txt[j].nStart; j++);
                Txt[i] = m_Txt[j - 1 < m_Txt.Count ? j - 1 : m_Txt.Count - 1].nTxt;
                debug += Txt[i].ToString() + ":";
            }

            // Gen step tris
            var p = m_Verts.Count - nVerts; int pp = p - nVerts;
            var b = pp - nVerts; int bb = b - nVerts;
            for (int i = 0; i < nVerts; i++) // obj tris
            {
                int p1 = p + i, p2 = b + i, p3 = bb + i + 1, p4 = pp + i + 1;
                if (i == nVerts - 1) { p3 = bb + 0; p4 = pp + 0; }
                m_Tris.AddRange(new int[] { p1, m_nMirrorFlag == 1 ? p3 : p2, m_nMirrorFlag == 1 ? p2 : p3 }); // backwards?
                m_Tris.AddRange(new int[] { p3, m_nMirrorFlag == 1 ? p1 : p4, m_nMirrorFlag == 1 ? p4 : p1 });

                // Gen UVs
                var QuadUVs = CalculateQuadUVs(m_Verts[p1], m_Verts[p2], m_Verts[p3], m_Verts[p4], Txt[i]);
                m_Uvs[p1] = QuadUVs[0]; m_Uvs[p2] = QuadUVs[1]; m_Uvs[p3] = QuadUVs[2]; m_Uvs[p4] = QuadUVs[3];
            }

            // Gen forward
            m_Verts.AddRange(GenVerts(m_Right, Up));
            GenNormals(m_Target, m_Right, Up);
            GenEmptyUVs();
        }
    }

    void DupLastNPoints()
    {
        int n = Paths[m_sPathName].Points.Count * 2;
        for (int i = 0; i < n; i++)
        {
            m_Verts.Add(m_Verts[m_Verts.Count - n]);
            m_Normals.Add(m_Normals[m_Normals.Count - n]);
            m_Uvs.Add(m_Uvs[m_Uvs.Count - n]);
        }
    }

    float ScaleWithMax(float a_nValue, float a_nMaxDist, float a_nScale)
    {
        float nSign = a_nValue < 0 ? -1 : 1;
        if (Mathf.Abs(a_nValue) < a_nMaxDist)
            return a_nValue * a_nScale; // inside of max
        return a_nValue + nSign * a_nMaxDist * a_nScale; // outside of max
    }

    List<Vector3> GenVerts(Vector3 a_Right, Vector3 a_Up)
    {
        var PathPoints = Paths[m_sPathName].Points;
        List<Vector3> Verts = new List<Vector3>();
        for (int i = 0; i < PathPoints.Count; i++) // Add verts
            Verts.Add(PosTranslateRU(m_nMirrorFlag * ScaleWithMax(PathPoints[i].x, m_nScaleMaxRadiusX, m_nScaleX), ScaleWithMax(PathPoints[i].y, m_nScaleMaxRadiusY, m_nScaleY), a_Right, a_Up));
        for (int i = PathPoints.Count - 1; i >= 0; i--) // Add outside verts
            Verts.Add(PosTranslateRU(m_nMirrorFlag * ScaleWithMax(PathPoints[i].x + PathPoints[i].xd, m_nScaleMaxRadiusX, m_nScaleX), ScaleWithMax(PathPoints[i].y + PathPoints[i].yd, m_nScaleMaxRadiusY, m_nScaleY), a_Right, a_Up));
        return Verts;
    }

    void GenNormals(Vector3 a_Target, Vector3 a_Right, Vector3 a_Up)
    {
        m_Normals.Add(a_Target);
        m_Normals.Add(a_Right);
        int nVerts = Paths[m_sPathName].Points.Count * 2;
        for (int i = 2; i < nVerts; i++)
            m_Normals.Add(a_Up);
    }

    void SetNormals(string a_sMode, int a_nOffset = 0)
    {
        var PathPoints = Paths[m_sPathName].Points;
        int p = m_Normals.Count - (PathPoints.Count * 2) - a_nOffset;
        Vector3 Target = m_Normals[p];
        Vector3 Right = m_Normals[p + 1];
        Vector3 Up = m_Normals[p + 2];
        var Verts = GenVerts(Right, Up);
        if (a_sMode == "X") // Next segment normals
        {
            for (int i = 0; i < Verts.Count - 1; i++)
                m_Normals[p + i] = Rot90Deg3D(Verts[i + 1] - Verts[i], Target, m_nMirrorFlag != 1);
            m_Normals[p + Verts.Count - 1] = Rot90Deg3D(Verts[0] - Verts[Verts.Count - 1], Target, m_nMirrorFlag != 1);
        }
        else if (a_sMode == "XX") // Prev segment normals
        {
            m_Normals[p] = Rot90Deg3D(Verts[0] - Verts[Verts.Count - 1], Target, m_nMirrorFlag != 1);
            for (int i = 1; i < Verts.Count; i++)
                m_Normals[p + i] = Rot90Deg3D(Verts[i] - Verts[i - 1], Target, m_nMirrorFlag != 1);
        }
        else  // "CL"/"OP"
        {
            Vector3 Normal = (a_sMode == "CL" ? 1 : -1) * m_nMirrorFlag * Target;
            for (int i = 0; i < Verts.Count; i++)
                m_Normals[p + i] = Normal;
        }
    }

    void GenEmptyUVs()
    {
        int nVerts = Paths[m_sPathName].Points.Count * 2;
        for (int i = 0; i < nVerts; i++)
            m_Uvs.Add(new Vector2(0, 0));
    }

    Vector2[] CalculateQuadUVs(Vector3 a_p1, Vector3 a_p2, Vector3 a_p3, Vector3 a_p4, int a_nTxt)
    {
        Vector2[] UVs = new Vector2[4];

        // Compute plannar axis
        Vector3 P2_P1 = a_p2 - a_p1;
        Vector3 BaseAxis1 = P2_P1.normalized;
        Vector3 Normal = Vector3.Cross(P2_P1, a_p3 - a_p2).normalized;
        Vector3 BaseAxis2 = Vector3.Cross(BaseAxis1, Normal);

        // Compute 2d points
        UVs[0].x = Vector3.Dot(a_p1, BaseAxis1); UVs[0].y = Vector3.Dot(a_p1, BaseAxis2);
        UVs[1].x = Vector3.Dot(a_p2, BaseAxis1); UVs[1].y = Vector3.Dot(a_p2, BaseAxis2);
        UVs[2].x = Vector3.Dot(a_p3, BaseAxis1); UVs[2].y = Vector3.Dot(a_p3, BaseAxis2);
        UVs[3].x = Vector3.Dot(a_p4, BaseAxis1); UVs[3].y = Vector3.Dot(a_p4, BaseAxis2);

        // Align with zero
        float nMinX = Mathf.Min(UVs[0].x, UVs[1].x, UVs[2].x, UVs[3].x);
        UVs[0].x -= nMinX; UVs[1].x -= nMinX; UVs[2].x -= nMinX; UVs[3].x -= nMinX;
        float nMinY = Mathf.Min(UVs[0].y, UVs[1].y, UVs[2].y, UVs[3].y);
        UVs[0].y -= nMinY; UVs[1].y -= nMinY; UVs[2].y -= nMinY; UVs[3].y -= nMinY;

        // Apply scale
        float nSx = m_TxtScale[a_nTxt].x; float nSy = m_TxtScale[a_nTxt].y;
        UVs[0].x *= nSx; UVs[1].x *= nSx; UVs[2].x *= nSx; UVs[3].x *= nSx;
        UVs[0].y *= nSy; UVs[1].y *= nSy; UVs[2].y *= nSy; UVs[3].y *= nSy;

        // Translate to texture
        float nOffX = m_TxtOffset[a_nTxt].x; float nOffY = m_TxtOffset[a_nTxt].y;
        UVs[0].x += nOffX; UVs[1].x += nOffX; UVs[2].x += nOffX; UVs[3].x += nOffX;
        UVs[0].y += nOffY; UVs[1].y += nOffY; UVs[2].y += nOffY; UVs[3].y += nOffY;

        return UVs;
    }

    Vector3 TranslateRU(float a_nR, float a_nU, Vector3 a_Right, Vector3 a_Up)
    {
        return a_nR * a_Right + a_nU * a_Up;
    }

    Vector3 PosTranslateRU(float a_nR, float a_nU, Vector3 a_Right, Vector3 a_Up)
    {
        return m_Pos + TranslateRU(a_nR, a_nU, a_Right, a_Up);
    }

    Vector2 RotMinus90Deg2D(Vector2 a_V)
    {
        return new Vector2(-a_V.y, a_V.x).normalized;
    }

    Vector3 Rot90Deg3D(Vector3 a_V, Vector3 a_Target, bool a_bPositive)
    {
        return Vector3.Cross(a_bPositive ? a_V : a_Target, a_bPositive ? a_Target : a_V).normalized; // Right handed
    }

    string m_sFileContentsCache = "";
    long m_nAnimationPointer = 0;
    List<Vector3> m_GizmosPoints = new List<Vector3>();
    bool m_bWasClosed, m_bOpenOnFirst;
    int m_nMirrorFlag;
    string m_sPathName = "";
    float m_nScaleX, m_nScaleMaxRadiusX, m_nScaleY, m_nScaleMaxRadiusY;
    Vector3 m_Pos, m_Target, m_Right;
    List<TextureInsertType> m_Txt;
    Vector2[] m_TxtOffset;
    Vector2[] m_TxtScale;
    Mesh m_Mesh;
    List<Vector3> m_Verts;
    List<int> m_Tris;
    List<Vector3> m_Normals;
    List<Vector2> m_Uvs;
}
