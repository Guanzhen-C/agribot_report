using NUnit.Framework;
using UnityEngine;

namespace Agribot.VehicleConfiguration.Tests
{
    public sealed class RosFluConverterTests
    {
        [Test]
        public void ConvertsUnityAxesToRosFlu()
        {
            Assert.That(RosFluConverter.UnityToRos(Vector3.forward), Is.EqualTo(Vector3.right));
            Assert.That(RosFluConverter.UnityToRos(Vector3.left), Is.EqualTo(Vector3.up));
            Assert.That(RosFluConverter.UnityToRos(Vector3.up), Is.EqualTo(Vector3.forward));
        }

        [Test]
        public void RoundTripsRosPosition()
        {
            Vector3 expected = new Vector3(0.5755f, -0.03f, 0.154f);
            Vector3 actual = RosFluConverter.UnityToRos(RosFluConverter.RosToUnity(expected));
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(1e-6f));
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(1e-6f));
            Assert.That(actual.z, Is.EqualTo(expected.z).Within(1e-6f));
        }

        [Test]
        public void ProducesRosOpticalFrameRotation()
        {
            GameObject baseObject = new GameObject("base");
            GameObject cameraObject = new GameObject("camera");
            try
            {
                cameraObject.transform.SetParent(baseObject.transform, false);
                RosFluConverter.SetLocalPoseFromRos(
                    cameraObject.transform,
                    Vector3.zero,
                    new Vector3(-Mathf.PI * 0.5f, 0f, -Mathf.PI * 0.5f));
                PoseData pose = RosFluConverter.RelativePose(
                    baseObject.transform,
                    cameraObject.transform);
                Assert.That(pose.rpy[0], Is.EqualTo(-Mathf.PI * 0.5f).Within(1e-5));
                Assert.That(pose.rpy[1], Is.EqualTo(0.0).Within(1e-5));
                Assert.That(pose.rpy[2], Is.EqualTo(-Mathf.PI * 0.5f).Within(1e-5));
            }
            finally
            {
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(baseObject);
            }
        }
    }
}
