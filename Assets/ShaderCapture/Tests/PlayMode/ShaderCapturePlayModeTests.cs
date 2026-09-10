using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;

namespace ShaderCapture.Tests
{
    public sealed class ShaderCapturePlayModeTests
    {
        [UnityTest]
        public IEnumerator HubLoadsAndExposesTwelveEntries()
        {
            SceneManager.LoadScene(0);
            yield return null;
            Assert.That(Object.FindAnyObjectByType<CaptureHubController>(), Is.Not.Null);
            // The Test Framework temporarily adds its bootstrap scene while PlayMode tests run.
            Assert.That(SceneManager.sceneCountInBuildSettings, Is.GreaterThanOrEqualTo(13));
        }

        [UnityTest]
        public IEnumerator EveryEffectSceneLoadsAndAnimates()
        {
            for (var index = 1; index <= 12; index++)
            {
                SceneManager.LoadScene(index);
                yield return null;
                var controller = Object.FindAnyObjectByType<EffectDemoController>();
                Assert.That(controller, Is.Not.Null, "Build scene " + index);
                Assert.That(controller.SceneIndex, Is.EqualTo(index));
                var initial = controller.Effect;
                yield return new WaitForSecondsRealtime(.12f);
                Assert.That(controller.Effect, Is.GreaterThan(initial), "Build scene " + index);
                Assert.That(Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include).All(renderer => renderer.sharedMaterial != null), Is.True, "Build scene " + index);
            }
        }

        [UnityTest]
        public IEnumerator PauseResetAndSoloModeAreDeterministic()
        {
            SceneManager.LoadScene(6);
            yield return null;
            var controller = Object.FindAnyObjectByType<EffectDemoController>();
            yield return new WaitForSecondsRealtime(.15f);
            controller.SetAutoPlay(false);
            var pausedEffect = controller.Effect;
            var pausedTime = controller.DemoTime;
            yield return new WaitForSecondsRealtime(1f);
            Assert.That(controller.Effect, Is.EqualTo(pausedEffect).Within(1e-6f));
            Assert.That(controller.DemoTime, Is.EqualTo(pausedTime).Within(1e-6f));

            controller.SetBeforeVisible(false);
            controller.SetHudVisible(false);
            controller.ResetDemo();
            Assert.That(controller.Effect, Is.EqualTo(0f));
            Assert.That(controller.DemoTime, Is.EqualTo(0f));
            Assert.That(controller.IsAutoPlaying, Is.False);
            Assert.That(controller.IsBeforeVisible, Is.True);
            Assert.That(controller.IsHudVisible, Is.True);
            Assert.That(GameObject.Find("After").transform.position.x, Is.EqualTo(2.4f).Within(1e-5f));

            controller.SetBeforeVisible(false);
            var after = GameObject.Find("After");
            Assert.That(after.transform.position.x, Is.EqualTo(0f).Within(1e-5f));
            controller.SetBeforeVisible(true);
            Assert.That(after.transform.position.x, Is.EqualTo(2.4f).Within(1e-5f));
        }

        [UnityTest]
        public IEnumerator MaskMetalLight2DChangesOnlyGSortingLayerPixels()
        {
            SceneManager.LoadScene(10);
            yield return null;
            var controller = Object.FindAnyObjectByType<EffectDemoController>();
            controller.SetManualEffect(1f);
            var camera = Camera.main;
            var metalLight = Object.FindObjectsByType<Light2D>()
                .Single(light => light.name == "G Metal Blend Style Light 2D");
            var sprites = Object.FindObjectsByType<SpriteRenderer>();
            var rWet = sprites.Single(renderer => renderer.name == "R WET Channel");
            var gMetal = sprites.Single(renderer => renderer.name == "G METAL Channel");
            var bWeak = sprites.Single(renderer => renderer.name == "B WEAK Channel");
            var originalStates = sprites.Select(renderer => renderer.enabled).ToArray();
            var originalLightState = metalLight.enabled;
            try
            {
                foreach (var renderer in sprites) renderer.enabled = false;
                foreach (var renderer in new[] { rWet, gMetal, bWeak })
                {
                    renderer.enabled = true;
                    metalLight.enabled = true;
                    yield return null;
                    yield return new WaitForEndOfFrame();
                    var lit = CaptureCamera(camera);
                    metalLight.enabled = false;
                    yield return null;
                    yield return new WaitForEndOfFrame();
                    var unlit = CaptureCamera(camera);
                    var changed = lit.Zip(unlit, PixelDelta).Count(delta => delta > 18);
                    if (renderer == gMetal)
                        Assert.That(changed, Is.GreaterThan(50), "G Metal did not respond to actual Blend Style 3 Light2D enable state");
                    else
                        Assert.That(changed, Is.LessThan(10), renderer.name + " changed from G-only Light2D");
                    renderer.enabled = false;
                }
            }
            finally
            {
                for (var index = 0; index < sprites.Length; index++) sprites[index].enabled = originalStates[index];
                metalLight.enabled = originalLightState;
            }
        }

        static int PixelDelta(Color32 left, Color32 right)
            => Mathf.Abs(left.r - right.r) + Mathf.Abs(left.g - right.g) + Mathf.Abs(left.b - right.b) + Mathf.Abs(left.a - right.a);

        static Color32[] CaptureCamera(Camera camera)
        {
            var target = RenderTexture.GetTemporary(640, 360, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            var image = new Texture2D(640, 360, TextureFormat.RGBA32, false, false);
            image.ReadPixels(new Rect(0, 0, 640, 360), 0, 0);
            image.Apply(false, false);
            var pixels = image.GetPixels32();
            Object.Destroy(image);
            camera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
            RenderTexture.ReleaseTemporary(target);
            return pixels;
        }
    }
}
