using System;
using System.Collections.Generic;
using UnityEngine;

namespace UTJ.FbxExporter
{
    public partial class FbxExporter
    {
        ExportOptions m_opt = ExportOptions.defaultValue;
        Context m_ctx;
        Dictionary<Transform, Node> m_nodes;

        public FbxExporter(ExportOptions opt)
        {
            m_opt = opt;
        }

        ~FbxExporter()
        {
            Release();
        }

        public void Release()
        {
            fbxeReleaseContext(m_ctx);
            m_ctx = Context.Null;
        }

        public bool CreateScene(string name)
        {
            Release();
            if (!m_ctx)
                m_ctx = fbxeCreateContext(ref m_opt);
            m_nodes = new Dictionary<Transform, Node>();
            return fbxeCreateScene(m_ctx, name);
        }

        public void AddNode(GameObject go)
        {
            if (go)
                FindOrCreateNodeTree(go.GetComponent<Transform>(), ProcessNode);
        }

        public bool Write(string path, Format format)
        {
            return fbxeWrite(m_ctx, path, format);
        }


        #region impl
        void ProcessNode(Transform trans, Node node)
        {
            var mr = trans.GetComponent<MeshRenderer>();
            var smr = trans.GetComponent<SkinnedMeshRenderer>();
            var terrain = trans.GetComponent<Terrain>();

            if (terrain)
                AddTerrain(node, terrain);
            else if (smr)
                AddSkinnedMesh(node, smr);
            else if (mr)
                AddMesh(node, mr);
        }

        Node FindOrCreateNodeTree(Transform trans, Action<Transform, Node> act)
        {
            if (!trans) { return Node.Null; }

            if (m_nodes.ContainsKey(trans))
            {
                return m_nodes[trans];
            }
            else
            {
                var parent = !trans.parent ? fbxeGetRootNode(m_ctx) : FindOrCreateNodeTree(trans.parent, act);
                var node = fbxeCreateNode(m_ctx, parent, trans.name);
                fbxeSetTRS(m_ctx, node, trans.localPosition, trans.localRotation, trans.localScale);
                m_nodes.Add(trans, node);

                if (act != null) { act.Invoke(trans, node); }
                return node;
            }
        }

        bool AddMesh(Node node, Mesh mesh)
        {
            if (!mesh || mesh.vertexCount == 0) { return false; }
            if (!mesh.isReadable)
            {
                Debug.LogWarning("Mesh " + mesh.name + " is not readable and be ignored.");
                return false;
            }

            Topology topology = Topology.Triangles;

            var indices = mesh.triangles;
            var points = mesh.vertices;
            var normals = mesh.normals;     if (normals.Length == 0) normals = null;
            var tangents = mesh.tangents;   if (tangents.Length == 0) tangents = null;
            var uv = mesh.uv;               if (uv.Length == 0) uv = null;
            var colors = mesh.colors;       if (colors.Length == 0) colors = null;
            fbxeAddMesh(m_ctx, node, points.Length, points, normals, tangents, uv, colors);
            fbxeAddMeshSubmesh(m_ctx, node, topology, indices.Length, indices, -1);

            return true;
        }

        bool AddMesh(Node node, MeshRenderer mr)
        {
            var mf = mr.gameObject.GetComponent<MeshFilter>();
            if (!mf)
                return false;
            return AddMesh(node, mf.sharedMesh);
        }

        bool AddSkinnedMesh(Node node, SkinnedMeshRenderer smr)
        {
            var mesh = smr.sharedMesh;
            if (!AddMesh(node, mesh))
                return false;

            var bones = smr.bones;
            var bindposes = mesh.bindposes;
            var boneNodes = new Node[bones.Length];
            for (int bi = 0; bi < bones.Length; ++bi)
                boneNodes[bi] = FindOrCreateNodeTree(bones[bi], ProcessNode);

            fbxeAddMeshSkin(m_ctx, node, mesh.boneWeights, boneNodes.Length, boneNodes, bindposes);
            return true;
        }

        bool AddTerrain(Node node, Terrain terrain)
        {
            var tdata = terrain.terrainData;
            var w = tdata.heightmapWidth;
            var h = tdata.heightmapHeight;
            var heightmap = tdata.GetHeights(0, 0, w, h);

            int vertexCount = w * h;
            int indexCount = (w - 1) * (h - 1) * 2 * 3;
            var vertices = new Vector3[vertexCount];
            var normals = new Vector3[vertexCount];
            var uv = new Vector2[vertexCount];
            var indices = new int[indexCount];
            fbxeGenerateTerrainMesh(heightmap, w, h, tdata.size,
                vertices, normals, uv, indices);

            Topology topology = Topology.Triangles;
            fbxeAddMesh(m_ctx, node, vertices.Length, vertices, normals, null, uv, null);
            fbxeAddMeshSubmesh(m_ctx, node, topology, indices.Length, indices, -1);

            return true;
        }
        #endregion
    }

}
