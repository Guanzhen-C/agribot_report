using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Agribot.VehicleConfiguration
{
    public sealed class VehicleManipulatorPreview : MonoBehaviour
    {
        [Serializable] public sealed class JointData
        {
            public string name, parent, child, type;
            public double[] xyz, rpy, axis;
            public double lower, upper;
        }
        [Serializable] public sealed class ModelData
        {
            public string[] links;
            public JointData[] joints;
        }

        private static readonly Dictionary<string, Mesh> Meshes = new Dictionary<string, Mesh>();
        private Transform[] joints;
        private Quaternion[] rotations;
        private Vector3[] positions;
        private JointData[] entries;
        private bool demo = true;
        private float started;
        private Material material, metal, gripper;

        public bool DemoEnabled => demo;

        public static Transform Attach(Transform hand)
        {
            Transform previous = hand.Find("ROS_manipulator_preview");
            if (previous != null) return previous;
            TextAsset json = Resources.Load<TextAsset>("Manipulators/Lite6/model");
            if (json == null) throw new InvalidOperationException("机械臂模型资源缺失。");
            ModelData model = JsonUtility.FromJson<ModelData>(json.text);
            // Authored FBX uses Blender's Z-up mesh basis with a Unity X reflection.
            // The measured CAD bottom/base-axis intersection is the arm mounting datum.
            MeshFilter datum = Array.Find(hand.GetComponentsInChildren<MeshFilter>(true),
                filter => filter.name == "JOINT_I_1_1");
            if (datum == null) throw new InvalidOperationException("机械臂缺少厂家底座网格 JOINT_I_1_1。");
            var root = new GameObject("ROS_manipulator_preview");
            root.transform.SetParent(hand, false);
            root.transform.position = datum.transform.TransformPoint(new Vector3(-0.018733693f, 0.008142131f, -0.064984436f));
            root.transform.rotation = datum.transform.rotation * Quaternion.LookRotation(Vector3.up, Vector3.forward);
            var preview = root.AddComponent<VehicleManipulatorPreview>();
            preview.started = Time.time;
            var links = new Dictionary<string, Transform>();
            links.Add("link_base", NewLink("link_base", root.transform, Vector3.zero, Vector3.zero));
            preview.joints = new Transform[model.joints.Length];
            preview.rotations = new Quaternion[model.joints.Length];
            preview.positions = new Vector3[model.joints.Length];
            preview.entries = model.joints;
            for (int i = 0; i < model.joints.Length; i++)
            {
                JointData entry = model.joints[i];
                Transform link = NewLink(entry.child, links[entry.parent], ToVector(entry.xyz), ToVector(entry.rpy));
                links.Add(entry.child, link);
                preview.joints[i] = link;
                preview.rotations[i] = link.localRotation;
                preview.positions[i] = link.localPosition;
            }
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            preview.material = new Material(shader) { color = new Color(0.85f, 0.88f, 0.90f) };
            preview.metal = new Material(shader) { color = new Color(0.60f, 0.64f, 0.68f) };
            preview.metal.SetFloat("_Metallic", 0.8f);
            preview.metal.SetFloat("_Smoothness", 0.65f);
            preview.gripper = new Material(shader) { color = new Color(0.20f, 0.22f, 0.24f) };
            foreach (string name in model.links)
            {
                var visual = new GameObject(name + "_visual", typeof(MeshFilter), typeof(MeshRenderer));
                visual.transform.SetParent(links[name], false);
                if (name == "link2")
                    visual.transform.localPosition = RosFluConverter.RosToUnity(new Vector3(0, 0, -(float)model.joints[1].xyz[1]));
                visual.GetComponent<MeshFilter>().sharedMesh = LoadMesh(name);
                visual.GetComponent<MeshRenderer>().sharedMaterial = name.StartsWith("finger")
                    ? preview.gripper : name == "link6" ? preview.metal : preview.material;
            }
            foreach (MeshRenderer renderer in hand.GetComponentsInChildren<MeshRenderer>(true))
                if (!renderer.transform.IsChildOf(root.transform)) renderer.enabled = false;
            return root.transform;
        }

        private static Transform NewLink(string name, Transform parent, Vector3 xyz, Vector3 rpy)
        {
            var link = new GameObject("manipulator_" + name).transform;
            link.SetParent(parent, false);
            RosFluConverter.SetLocalPoseFromRos(link, xyz, rpy);
            return link;
        }

        private static Vector3 ToVector(double[] value) => new Vector3((float)value[0], (float)value[1], (float)value[2]);

        private static Mesh LoadMesh(string name)
        {
            if (Meshes.TryGetValue(name, out Mesh cached) && cached != null) return cached;
            TextAsset asset = Resources.Load<TextAsset>("Manipulators/Lite6/" + name);
            if (asset == null) throw new InvalidOperationException("机械臂网格缺失：" + name);
            using var reader = new BinaryReader(new MemoryStream(asset.bytes));
            reader.ReadBytes(80);
            int count = checked((int)reader.ReadUInt32());
            if (asset.bytes.Length != 84 + count * 50) throw new InvalidOperationException("STL网格格式错误。");
            var vertices = new Vector3[count * 3];
            var triangles = new int[count * 3];
            for (int triangle = 0; triangle < count; triangle++)
            {
                reader.ReadBytes(12);
                for (int corner = 0; corner < 3; corner++)
                    vertices[triangle * 3 + corner] = RosFluConverter.RosToUnity(
                        new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()));
                reader.ReadUInt16();
                triangles[triangle * 3] = triangle * 3;
                triangles[triangle * 3 + 1] = triangle * 3 + 2;
                triangles[triangle * 3 + 2] = triangle * 3 + 1;
            }
            var mesh = new Mesh { name = "Lite6_" + name };
            if (vertices.Length > 65535) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            Meshes[name] = mesh;
            return mesh;
        }

        private void Update()
        {
            if (joints == null) return;
            float phase = demo ? (Time.time - started) * 2f * Mathf.PI / 16f : 0f;
            float wave = Mathf.Sin(phase), lift = (1f - Mathf.Cos(phase)) * 0.5f;
            float[] positions = { 0.55f * wave, -0.25f * lift, 0.65f * lift,
                                  0.25f * wave, 0.3f * lift, -0.45f * wave };
            for (int i = 0; i < joints.Length; i++)
            {
                if (entries[i].type == "fixed") continue;
                if (entries[i].type == "prismatic")
                {
                    joints[i].localPosition = this.positions[i] + rotations[i] *
                        RosFluConverter.RosToUnity(ToVector(entries[i].axis)) * (0.0089f * lift);
                    continue;
                }
                // Reflection from ROS FLU to Unity reverses positive rotations.
                joints[i].localRotation = rotations[i] * Quaternion.AngleAxis(
                    -positions[i] * Mathf.Rad2Deg, Vector3.up);
            }
        }

        private void OnDestroy()
        {
            foreach (Material item in new[] { material, metal, gripper })
            {
                if (item == null) continue;
                if (Application.isPlaying) Destroy(item);
                else DestroyImmediate(item);
            }
        }
    }
}
