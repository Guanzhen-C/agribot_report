using System;
using System.Collections;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Agribot.VehicleConfiguration.Tests
{
    public sealed class LocalLoginScreenTests
    {
        private static T Find<T>(string name) where T : Component => GameObject.Find(name).GetComponent<T>();

        [UnityTest]
        public IEnumerator MissingAndInvalidCredentialsKeepGarageClosed()
        {
            Assert.That(SceneUtility.GetScenePathByBuildIndex(0), Is.EqualTo("Assets/Scenes/Login.unity"));
            yield return SceneManager.LoadSceneAsync("Login");
            var account = Find<TMP_InputField>("LoginAccount");
            var password = Find<TMP_InputField>("LoginPassword");
            var button = Find<Button>("LoginSubmit");
            var status = Find<Text>("LoginStatus");
            Assert.That(Find<Image>("StartupVehicle").sprite.texture.name, Is.EqualTo("Car4"));
            Assert.That(button.IsInteractable(), Is.True, "The startup animation must not block login.");
            Assert.That(account.text, Is.Empty);
            Assert.That(password.inputType, Is.EqualTo(TMP_InputField.InputType.Password));
            button.onClick.Invoke();
            Assert.That(status.text, Is.EqualTo("请输入账号和密码。"));

            account.text = "admin";
            password.text = "incorrect";
            Assert.That(password.textComponent.text, Does.Not.Contain("incorrect"));
            button.onClick.Invoke();
            yield return null;
            Assert.That(status.text, Is.EqualTo("账号或密码错误。"));
            Assert.That(password.text, Is.Empty);
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Login"));
            Assert.That(GameObject.Find("Car0_Img"), Is.Null);
        }

        [UnityTest]
        public IEnumerator CorrectCredentialsOpenGarageButAreNotRemembered()
        {
            string testPassword = Environment.GetEnvironmentVariable("AGRIBOT_TEST_LOGIN_PASSWORD");
            if (string.IsNullOrEmpty(testPassword)) Assert.Ignore("Set AGRIBOT_TEST_LOGIN_PASSWORD to test successful login.");
            yield return SceneManager.LoadSceneAsync("Login");
            var account = Find<TMP_InputField>("LoginAccount");
            var password = Find<TMP_InputField>("LoginPassword");

            account.text = "visitor";
            password.text = testPassword;
            Find<Button>("LoginSubmit").onClick.Invoke();
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Login"));
            Assert.That(Find<Text>("LoginStatus").text, Is.EqualTo("账号或密码错误。"));

            account.text = "admin";
            password.text = testPassword;
            password.onSubmit.Invoke(testPassword);
            Assert.That(password.text, Is.Empty);
            float deadline = Time.realtimeSinceStartup + 20;
            while (SceneManager.GetActiveScene().name == "Login" && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("scene1"));
            Assert.That(GameObject.Find("Car0_Img"), Is.Not.Null);
            Assert.That(GameObject.Find("LoginCanvas"), Is.Null);

            yield return SceneManager.LoadSceneAsync("Login");
            Assert.That(Find<TMP_InputField>("LoginAccount").text, Is.Empty);
            Assert.That(Find<TMP_InputField>("LoginPassword").text, Is.Empty);
        }
    }
}
