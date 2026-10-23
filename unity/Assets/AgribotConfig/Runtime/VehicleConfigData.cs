using System;

namespace Agribot.VehicleConfiguration
{
    [Serializable]
    public sealed class PoseData
    {
        public double[] xyz = new double[3];
        public double[] quaternion = { 0.0, 0.0, 0.0, 1.0 };
        public double[] rpy = new double[3];
    }

    [Serializable]
    public sealed class BaseFrameData
    {
        public string frameId = "base_link";
        public string reference = "rear_axle_midpoint";
    }

    [Serializable]
    public sealed class ExtentsData
    {
        public double frontM;
        public double rearM;
        public double leftM;
        public double rightM;
    }

    [Serializable]
    public sealed class WheelData
    {
        public string id;
        public double[] position = new double[3];
        public bool steering;
        public bool driven;
        public double radiusM;
        public double widthM;
    }

    [Serializable]
    public sealed class GeometryData
    {
        public ExtentsData bodyExtents = new ExtentsData();
        public double wheelbaseM;
        public double frontTrackM;
        public double rearTrackM;
        public double wheelRadiusM;
        public double wheelWidthM;
        public double maxSteeringAngleRad;
        public double minTurningRadiusM;
        public WheelData[] wheelCenters = Array.Empty<WheelData>();
    }

    [Serializable]
    public sealed class NavigationData
    {
        public ExtentsData clearance = new ExtentsData();
        public double maxLinearSpeedMps;
        public double maxAngularSpeedRadps;
        public bool allowReverse;
        public double inflationRadiusM;
        public double costScalingFactor;
    }

    [Serializable]
    public sealed class ChassisData
    {
        public double commandMaxLinearSpeedMps;
        public double commandMaxAngularSpeedRadps;
        public double minimumMotionSpeedMps;
    }

    [Serializable]
    public sealed class InertiaData
    {
        public double ixx;
        public double ixy;
        public double ixz;
        public double iyy;
        public double iyz;
        public double izz;
    }

    [Serializable]
    public sealed class DynamicsData
    {
        public double massKg;
        public double[] centerOfMass = new double[3];
        public InertiaData inertia = new InertiaData();
        public double maxAccelerationMps2;
        public double maxSteeringRateRadps;
    }

    [Serializable]
    public sealed class CollisionData
    {
        public double[] center = new double[3];
        public double[] size = new double[3];
    }

    [Serializable]
    public sealed class VisualData
    {
        public string name;
        public string meshUri;
        public PoseData pose = new PoseData();
        public double[] scale = { 1.0, 1.0, 1.0 };
    }

    [Serializable]
    public sealed class SensorData
    {
        public string id;
        public string sensorType;
        public string model;
        public string role;
        public string parentFrameId = "base_link";
        public string frameId;
        public PoseData pose = new PoseData();
        public string measurementFrameId;
        public PoseData measurementFramePose;
        public string topic;
        public double updateRateHz;
    }

    [Serializable]
    public sealed class MetadataData
    {
        public string authoringTool = "AgriculturalCar_Interior";
        public string profileVersion = "1.0.0";
        public string calibrationState = "nominal";
        public string notes = string.Empty;
    }

    [Serializable]
    public sealed class ManipulatorData
    {
        public string model = "lite6";
        public string parentFrameId = "base_link";
        public PoseData pose = new PoseData();
        public bool demoEnabled = true;
    }

    [Serializable]
    public sealed class VehicleConfigurationData
    {
        public string schemaVersion = "1.0";
        public string vehicleId;
        public string displayName;
        public string vehicleType = "ackermann";
        public string coordinateConvention = "ROS_FLU";
        public BaseFrameData baseFrame = new BaseFrameData();
        public GeometryData geometry = new GeometryData();
        public NavigationData navigation = new NavigationData();
        public ChassisData chassis = new ChassisData();
        public DynamicsData dynamics = new DynamicsData();
        public CollisionData collision = new CollisionData();
        public VisualData[] visuals = Array.Empty<VisualData>();
        public SensorData[] sensors = Array.Empty<SensorData>();
        public ManipulatorData[] manipulators = Array.Empty<ManipulatorData>();
        public MetadataData metadata = new MetadataData();
    }
}
