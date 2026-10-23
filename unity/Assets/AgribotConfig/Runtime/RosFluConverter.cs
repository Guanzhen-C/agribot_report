using System;
using UnityEngine;

namespace Agribot.VehicleConfiguration
{
    public static class RosFluConverter
    {
        // Unity: +X right, +Y up, +Z forward. ROS FLU: +X forward, +Y left, +Z up.
        public static Vector3 UnityToRos(Vector3 value)
        {
            return new Vector3(value.z, -value.x, value.y);
        }

        public static Vector3 RosToUnity(Vector3 value)
        {
            return new Vector3(-value.y, value.z, value.x);
        }

        public static PoseData RelativePose(Transform baseLink, Transform target)
        {
            if (baseLink == null)
                throw new ArgumentNullException(nameof(baseLink));
            if (target == null)
                throw new ArgumentNullException(nameof(target));

            Matrix4x4 relative = baseLink.worldToLocalMatrix * target.localToWorldMatrix;
            Vector3 position = UnityToRos(relative.GetColumn(3));
            Matrix4x4 rosRotation = UnityRotationToRos(relative);
            Quaternion rotation = QuaternionFromRotationMatrix(rosRotation);
            Vector3 rpy = RpyFromRotationMatrix(rosRotation);
            return CreatePose(position, rotation, rpy);
        }

        public static void SetLocalPoseFromRos(Transform target, Vector3 xyz, Vector3 rpy)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));
            target.localPosition = RosToUnity(xyz);
            target.localRotation = RosRotationToUnityQuaternion(RotationFromRpy(rpy));
            target.localScale = Vector3.one;
        }

        public static PoseData CreatePose(Vector3 xyz, Vector3 rpy)
        {
            Matrix4x4 rotation = RotationFromRpy(rpy);
            return CreatePose(xyz, QuaternionFromRotationMatrix(rotation), rpy);
        }

        private static PoseData CreatePose(Vector3 xyz, Quaternion quaternion, Vector3 rpy)
        {
            quaternion.Normalize();
            return new PoseData
            {
                xyz = ToDoubleArray(xyz),
                quaternion =
                    new[]
                    {
                        (double)quaternion.x,
                        (double)quaternion.y,
                        (double)quaternion.z,
                        (double)quaternion.w
                    },
                rpy = ToDoubleArray(rpy)
            };
        }

        public static double[] ToDoubleArray(Vector3 value)
        {
            return new[] { (double)value.x, (double)value.y, (double)value.z };
        }

        private static Matrix4x4 UnityRotationToRos(Matrix4x4 unityRotation)
        {
            Matrix4x4 basis = UnityToRosBasis();
            return MultiplyRotation(MultiplyRotation(basis, unityRotation), basis.transpose);
        }

        private static Quaternion RosRotationToUnityQuaternion(Matrix4x4 rosRotation)
        {
            Matrix4x4 basis = UnityToRosBasis();
            Matrix4x4 unityRotation = MultiplyRotation(
                MultiplyRotation(basis.transpose, rosRotation),
                basis);
            return QuaternionFromRotationMatrix(unityRotation);
        }

        private static Matrix4x4 UnityToRosBasis()
        {
            Matrix4x4 basis = Matrix4x4.identity;
            basis.m00 = 0f;
            basis.m01 = 0f;
            basis.m02 = 1f;
            basis.m10 = -1f;
            basis.m11 = 0f;
            basis.m12 = 0f;
            basis.m20 = 0f;
            basis.m21 = 1f;
            basis.m22 = 0f;
            return basis;
        }

        private static Matrix4x4 MultiplyRotation(Matrix4x4 left, Matrix4x4 right)
        {
            Matrix4x4 result = Matrix4x4.identity;
            for (int row = 0; row < 3; row++)
            {
                for (int column = 0; column < 3; column++)
                {
                    float value = 0f;
                    for (int index = 0; index < 3; index++)
                        value += left[row, index] * right[index, column];
                    result[row, column] = value;
                }
            }
            return result;
        }

        private static Matrix4x4 RotationFromRpy(Vector3 rpy)
        {
            float cr = Mathf.Cos(rpy.x);
            float sr = Mathf.Sin(rpy.x);
            float cp = Mathf.Cos(rpy.y);
            float sp = Mathf.Sin(rpy.y);
            float cy = Mathf.Cos(rpy.z);
            float sy = Mathf.Sin(rpy.z);
            Matrix4x4 result = Matrix4x4.identity;
            result.m00 = cy * cp;
            result.m01 = cy * sp * sr - sy * cr;
            result.m02 = cy * sp * cr + sy * sr;
            result.m10 = sy * cp;
            result.m11 = sy * sp * sr + cy * cr;
            result.m12 = sy * sp * cr - cy * sr;
            result.m20 = -sp;
            result.m21 = cp * sr;
            result.m22 = cp * cr;
            return result;
        }

        private static Vector3 RpyFromRotationMatrix(Matrix4x4 rotation)
        {
            float pitch = Mathf.Asin(Mathf.Clamp(-rotation.m20, -1f, 1f));
            float roll;
            float yaw;
            if (Mathf.Abs(Mathf.Cos(pitch)) > 1e-6f)
            {
                roll = Mathf.Atan2(rotation.m21, rotation.m22);
                yaw = Mathf.Atan2(rotation.m10, rotation.m00);
            }
            else
            {
                roll = Mathf.Atan2(-rotation.m12, rotation.m11);
                yaw = 0f;
            }
            return new Vector3(roll, pitch, yaw);
        }

        private static Quaternion QuaternionFromRotationMatrix(Matrix4x4 matrix)
        {
            float trace = matrix.m00 + matrix.m11 + matrix.m22;
            Quaternion result;
            if (trace > 0f)
            {
                float scale = Mathf.Sqrt(trace + 1f) * 2f;
                result = new Quaternion(
                    (matrix.m21 - matrix.m12) / scale,
                    (matrix.m02 - matrix.m20) / scale,
                    (matrix.m10 - matrix.m01) / scale,
                    0.25f * scale);
            }
            else if (matrix.m00 > matrix.m11 && matrix.m00 > matrix.m22)
            {
                float scale = Mathf.Sqrt(1f + matrix.m00 - matrix.m11 - matrix.m22) * 2f;
                result = new Quaternion(
                    0.25f * scale,
                    (matrix.m01 + matrix.m10) / scale,
                    (matrix.m02 + matrix.m20) / scale,
                    (matrix.m21 - matrix.m12) / scale);
            }
            else if (matrix.m11 > matrix.m22)
            {
                float scale = Mathf.Sqrt(1f + matrix.m11 - matrix.m00 - matrix.m22) * 2f;
                result = new Quaternion(
                    (matrix.m01 + matrix.m10) / scale,
                    0.25f * scale,
                    (matrix.m12 + matrix.m21) / scale,
                    (matrix.m02 - matrix.m20) / scale);
            }
            else
            {
                float scale = Mathf.Sqrt(1f + matrix.m22 - matrix.m00 - matrix.m11) * 2f;
                result = new Quaternion(
                    (matrix.m02 + matrix.m20) / scale,
                    (matrix.m12 + matrix.m21) / scale,
                    0.25f * scale,
                    (matrix.m10 - matrix.m01) / scale);
            }
            result.Normalize();
            return result;
        }
    }
}
