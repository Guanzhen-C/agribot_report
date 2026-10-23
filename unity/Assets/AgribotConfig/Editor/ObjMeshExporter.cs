using System.Collections.Generic;
using UnityEngine;

namespace Agribot.VehicleConfiguration.Editor
{
    internal static class ObjMeshExporter
    {
        public static int Export(
            GameObject visualRoot,
            Transform baseLink,
            IReadOnlyList<SensorAnchorDefinition> sensors,
            string outputPath)
        {
            return VehicleMeshExporter.Export(
                visualRoot,
                baseLink,
                sensors,
                outputPath);
        }
    }
}
