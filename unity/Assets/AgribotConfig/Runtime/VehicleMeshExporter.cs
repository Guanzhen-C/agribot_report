using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace Agribot.VehicleConfiguration
{
    public static class VehicleMeshExporter
    {
        public static int ExportSensorVisuals(IReadOnlyList<SensorAnchorDefinition> sensors, string directory)
        {
            int count = 0;
            var exportedRoots = new HashSet<Transform>();
            foreach (SensorAnchorDefinition sensor in sensors)
            {
                if (sensor.visualRoot == null || !sensor.visualRoot.gameObject.activeInHierarchy ||
                    !exportedRoots.Add(sensor.visualRoot)) continue;
                string path = Path.Combine(directory, "sensor_" + SafeName(sensor.id) + "_visual.obj");
                count += Export(sensor.visualRoot.gameObject, sensor.anchor, null, path);
            }
            return count;
        }

        public static int Export(
            GameObject visualRoot,
            Transform baseLink,
            IReadOnlyList<SensorAnchorDefinition> sensors,
            string outputPath,
            Transform[] wheelVisualRoots = null)
        {
            if (visualRoot == null)
                throw new ArgumentNullException(nameof(visualRoot));
            if (baseLink == null)
                throw new ArgumentNullException(nameof(baseLink));
            if (string.IsNullOrWhiteSpace(outputPath))
                throw new ArgumentException("输出路径不能为空。", nameof(outputPath));

            // Export exactly the configuration currently visible in the Game
            // view. Inactive variant parts and disabled renderers must not be
            // copied into the ROS simulation mesh.
            MeshFilter[] filters = visualRoot.GetComponentsInChildren<MeshFilter>(false);
            StringBuilder obj = new StringBuilder(1024 * 1024);
            StringBuilder mtl = new StringBuilder(4096);
            Dictionary<Material, string> materialNames = new Dictionary<Material, string>();
            string mtlPath = Path.ChangeExtension(outputPath, ".mtl");
            obj.AppendLine("# ROS FLU mesh exported by AgriculturalCar_Interior");
            obj.AppendLine($"mtllib {Path.GetFileName(mtlPath)}");
            int vertexOffset = 0;
            int uvOffset = 0;
            int normalOffset = 0;
            int exportedMeshes = 0;
            bool defaultMaterialWritten = false;

            foreach (MeshFilter filter in filters)
            {
                Mesh mesh = filter.sharedMesh;
                MeshRenderer renderer = filter.GetComponent<MeshRenderer>();
                if (mesh == null || renderer == null || !renderer.enabled ||
                    !filter.gameObject.activeInHierarchy ||
                    IsSensorGeometry(filter.transform, sensors) || IsManipulatorGeometry(filter.transform))
                    continue;
                if (!mesh.isReadable)
                {
                    throw new InvalidOperationException(
                        $"网格 {filter.name} 未启用 Read/Write，无法导出完整车模。" +
                        "请先运行“农机配置/启用车体网格运行时读取”。");
                }

                string objectName = SafeName(filter.name);
                if (wheelVisualRoots != null)
                    for (int i = 0; i < wheelVisualRoots.Length; i++)
                        if (wheelVisualRoots[i] != null && filter.transform.IsChildOf(wheelVisualRoots[i]))
                            objectName = new[] { "FLwheel", "FRwheel", "RLwheel", "RRwheel" }[i] + "__" + exportedMeshes;
                obj.AppendLine($"o {objectName}");
                Matrix4x4 toBase =
                    baseLink.worldToLocalMatrix * filter.transform.localToWorldMatrix;

                foreach (Vector3 vertex in mesh.vertices)
                {
                    Vector3 ros = RosFluConverter.UnityToRos(
                        toBase.MultiplyPoint3x4(vertex));
                    obj.AppendLine(string.Format(
                        CultureInfo.InvariantCulture,
                        "v {0:R} {1:R} {2:R}",
                        ros.x,
                        ros.y,
                        ros.z));
                }

                Vector2[] uvs = mesh.uv;
                foreach (Vector2 uv in uvs)
                {
                    obj.AppendLine(string.Format(
                        CultureInfo.InvariantCulture,
                        "vt {0:R} {1:R}",
                        uv.x,
                        uv.y));
                }

                Vector3[] normals = mesh.normals;
                foreach (Vector3 normal in normals)
                {
                    Vector3 baseNormal = baseLink.InverseTransformDirection(
                        filter.transform.TransformDirection(normal));
                    Vector3 ros = RosFluConverter.UnityToRos(baseNormal).normalized;
                    obj.AppendLine(string.Format(
                        CultureInfo.InvariantCulture,
                        "vn {0:R} {1:R} {2:R}",
                        ros.x,
                        ros.y,
                        ros.z));
                }

                for (int subMesh = 0; subMesh < mesh.subMeshCount; subMesh++)
                {
                    Material material =
                        renderer != null && subMesh < renderer.sharedMaterials.Length
                            ? renderer.sharedMaterials[subMesh]
                            : null;
                    if (material != null)
                    {
                        string materialName = RegisterMaterial(
                            material,
                            materialNames,
                            mtl);
                        obj.AppendLine($"usemtl {materialName}");
                    }
                    else
                    {
                        // Never inherit a previous red material into an unassigned submesh.
                        if (!defaultMaterialWritten)
                        {
                            mtl.AppendLine("newmtl unassigned\nKd 0.6 0.6 0.6\nd 1\nillum 2");
                            defaultMaterialWritten = true;
                        }
                        obj.AppendLine("usemtl unassigned");
                    }

                    int[] triangles = mesh.GetTriangles(subMesh, true);
                    // Runtime static batching can expose a GPU base vertex that
                    // addresses a shared buffer rather than mesh.vertices.
                    if (Array.Exists(
                            triangles,
                            triangle => (uint)triangle >= (uint)mesh.vertexCount))
                    {
                        triangles = mesh.GetTriangles(subMesh, false);
                    }
                    bool hasUv = uvs.Length == mesh.vertexCount;
                    bool hasNormals = normals.Length == mesh.vertexCount;
                    for (int index = 0; index + 2 < triangles.Length; index += 3)
                    {
                        int firstLocal = triangles[index];
                        int secondLocal = triangles[index + 2];
                        int thirdLocal = triangles[index + 1];
                        if ((uint)firstLocal >= (uint)mesh.vertexCount ||
                            (uint)secondLocal >= (uint)mesh.vertexCount ||
                            (uint)thirdLocal >= (uint)mesh.vertexCount)
                        {
                            throw new InvalidOperationException(
                                $"网格 {filter.name} 的三角形索引超出顶点范围。");
                        }

                        // Unity-to-ROS changes handedness, so reverse winding.
                        int first = firstLocal + vertexOffset + 1;
                        int second = secondLocal + vertexOffset + 1;
                        int third = thirdLocal + vertexOffset + 1;
                        obj.Append("f ");
                        AppendFaceVertex(
                            obj,
                            first,
                            firstLocal + uvOffset + 1,
                            firstLocal + normalOffset + 1,
                            hasUv,
                            hasNormals);
                        obj.Append(' ');
                        AppendFaceVertex(
                            obj,
                            second,
                            secondLocal + uvOffset + 1,
                            secondLocal + normalOffset + 1,
                            hasUv,
                            hasNormals);
                        obj.Append(' ');
                        AppendFaceVertex(
                            obj,
                            third,
                            thirdLocal + uvOffset + 1,
                            thirdLocal + normalOffset + 1,
                            hasUv,
                            hasNormals);
                        obj.AppendLine();
                    }
                }

                vertexOffset += mesh.vertexCount;
                uvOffset += uvs.Length;
                normalOffset += normals.Length;
                exportedMeshes++;
            }

            if (exportedMeshes == 0)
                throw new InvalidOperationException("车体可视根节点下没有可导出的 MeshFilter。");

            string outputDirectory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(outputDirectory))
                Directory.CreateDirectory(outputDirectory);
            File.WriteAllText(outputPath, obj.ToString(), new UTF8Encoding(false));
            File.WriteAllText(mtlPath, mtl.ToString(), new UTF8Encoding(false));
            return exportedMeshes;
        }

        private static bool IsManipulatorGeometry(Transform candidate)
        {
            for (Transform parent = candidate; parent != null; parent = parent.parent)
                if (parent.name == "Hand" || parent.name == "ROS_manipulator_preview") return true;
            return false;
        }

        private static bool IsSensorGeometry(
            Transform candidate,
            IReadOnlyList<SensorAnchorDefinition> sensors)
        {
            if (sensors == null)
                return false;
            foreach (SensorAnchorDefinition sensor in sensors)
            {
                if (sensor == null)
                    continue;
                if ((sensor.anchor != null && (candidate == sensor.anchor || candidate.IsChildOf(sensor.anchor))) ||
                    (sensor.visualRoot != null && (candidate == sensor.visualRoot || candidate.IsChildOf(sensor.visualRoot))))
                    return true;
            }
            return false;
        }

        private static void AppendFaceVertex(
            StringBuilder builder,
            int vertexIndex,
            int uvIndex,
            int normalIndex,
            bool hasUv,
            bool hasNormals)
        {
            builder.Append(vertexIndex);
            if (!hasUv && !hasNormals)
                return;
            builder.Append('/');
            if (hasUv)
                builder.Append(uvIndex);
            if (hasNormals)
            {
                builder.Append('/');
                builder.Append(normalIndex);
            }
        }

        private static string RegisterMaterial(
            Material material,
            IDictionary<Material, string> names,
            StringBuilder output)
        {
            if (names.TryGetValue(material, out string existing))
                return existing;

            string name = $"{SafeName(material.name)}_{names.Count}";
            names[material] = name;
            Color color = material.HasProperty("_BaseColor")
                ? material.GetColor("_BaseColor")
                : material.color;
            output.AppendLine($"newmtl {name}");
            output.AppendLine(string.Format(
                CultureInfo.InvariantCulture,
                "Kd {0:R} {1:R} {2:R}",
                color.r,
                color.g,
                color.b));
            output.AppendLine(string.Format(
                CultureInfo.InvariantCulture,
                "d {0:R}",
                color.a));
            output.AppendLine("illum 2");
            return name;
        }

        private static string SafeName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "unnamed";
            return value.Replace(' ', '_').Replace('/', '_').Replace('\\', '_');
        }
    }
}
