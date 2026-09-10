using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D;
using UnityEditor.U2D.Sprites;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Encoder;
using UnityEditor.Recorder.Input;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering;
using UnityEngine.U2D;

namespace ShaderCapture.Editor
{
    static class ShaderCaptureProjectGenerator
    {
        const string Root = "Assets/ShaderCapture";
        const string Art = Root + "/Art/Generated";
        const string Materials = Root + "/Materials";
        const string Prefabs = Root + "/Prefabs";
        const string RecorderSettings = Root + "/Settings/Recorder";
        const string SettingsRoot = Root + "/Settings";
        const string Scenes = Root + "/Scenes";
        const string Graphs = Root + "/Shaders/Graphs";
        const string GeneratedMarker = Root + "/.demo-generated";
        const string MaskMetalSortingLayerName = "MaskMetal";
        const int MaskMetalSortingLayerId = 1780948211;

        static readonly string[] SceneNames =
        {
            "00_CaptureHub", "01_HitFlashInvincible", "02_PaletteSwap", "03_Dissolve",
            "04_OutlineInnerRim", "05_WindVertexSquash", "06_GlitchRgbSplit",
            "07_PixelPosterizeDither", "08_HologramShine", "09_NormalMap2DLight",
            "10_MaskMapLighting", "11_WaterReflection", "12_WorldScanReveal"
        };

        static readonly string[] Titles =
        {
            "Capture Hub", "Hit Flash + Invincible Blink", "Palette Swap", "Dissolve",
            "Outline + Inner Rim", "Wind Vertex + Squash", "Glitch + RGB Split",
            "Pixelate + Posterize + Dither", "Hologram + Shine", "2D Normal Map Lighting",
            "Mask Map Lighting", "Water Reflection", "World Scan + Reveal"
        };

        [InitializeOnLoadMethod]
        static void GenerateOnFirstImport()
        {
            EditorApplication.delayCall += () =>
            {
                if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode) return;
                var currentMaterial = AssetDatabase.LoadAssetAtPath<Material>(Materials + "/Effect01.mat");
                if (File.Exists(GeneratedMarker) && File.Exists(Materials + "/BaseSprite.mat") &&
                    File.Exists(Art + "/DemoCharacter_QuadMesh.asset") && File.Exists(Art + "/DemoCharacter_SubdividedMesh.asset") &&
                    File.Exists(Art + "/DemoCharacter_Quad.png") && File.Exists(Art + "/DemoCharacter_Subdivided.png") && File.Exists(Prefabs + "/CaptureStage.prefab") &&
                    currentMaterial != null && currentMaterial.GetTexture("_MainTex") == null) return;
                if (!File.Exists(Graphs + "/Effect12_WorldScanReveal.shadergraph")) return;
                GenerateAll();
            };
        }

        [MenuItem("Tools/Shader Capture/Generate Demo Project")]
        public static void GenerateAll()
        {
            try
            {
                Directory.CreateDirectory(Art);
                Directory.CreateDirectory(Materials);
                Directory.CreateDirectory(Prefabs);
                Directory.CreateDirectory(RecorderSettings);
                Directory.CreateDirectory(Scenes);
                GenerateTextures();
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                ConfigureTextureImporters();
                AttachSecondaryTextures();
                GenerateSpriteAtlas();
                GenerateSubdividedMesh();
                GenerateComparisonSprites();
                GenerateMaterials();
                GenerateVolumeProfile();
                GenerateCaptureStagePrefab();
                GenerateRecorderPresets();
                EnsureMaskMetalSortingLayer();
                GenerateScenes();
                ConfigureBuildSettings();
                File.WriteAllText(GeneratedMarker, DateTime.UtcNow.ToString("O") + Environment.NewLine);
                AssetDatabase.Refresh();
                Debug.Log("[ShaderCapture] Generated placeholder art, materials, 13 scenes, Sprite Atlas, and build settings.");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                throw;
            }
        }

        static void GenerateTextures()
        {
            WritePng("DemoCharacter.png", 256, 256, CharacterPixel);
            WritePng("DemoCharacter_Mesh.png", 256, 256, CharacterPixel);
            WritePng("DemoCharacter_Quad.png", 256, 256, CharacterPixel);
            WritePng("DemoCharacter_Subdivided.png", 256, 256, CharacterPixel);
            WritePng("DemoCharacter_Flat.png", 256, 256, CharacterPixel);
            WritePng("DemoCharacter_Tight.png", 256, 256, CharacterPixel);
            WritePng("DemoCharacter_Index.png", 256, 256, IndexPixel);
            WritePng("DemoCharacter_Normal.png", 256, 256, NormalPixel);
            WritePng("FlatNormal.png", 16, 16, (_, _) => new Color(.5f, .5f, 1f, 1f));
            WritePng("DemoCharacter_Mask.png", 256, 256, MaskPixel);
            WritePng("Noise.png", 256, 256, NoisePixel);
            WritePng("Palette.png", 4, 4, PalettePixel);
            WritePng("Panel.png", 32, 32, (_, _) => Color.white);
        }

        static void WritePng(string fileName, int width, int height, Func<int, int, Color> pixel)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false, false);
            var colors = new Color[width * height];
            for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
                colors[y * width + x] = pixel(x, y);
            texture.SetPixels(colors);
            texture.Apply(false, false);
            File.WriteAllBytes(Art + "/" + fileName, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
        }

        static bool InEllipse(float x, float y, float cx, float cy, float rx, float ry)
        {
            var dx = (x - cx) / rx;
            var dy = (y - cy) / ry;
            return dx * dx + dy * dy <= 1f;
        }

        static Color CharacterPixel(int x, int y)
        {
            var u = (x + .5f) / 256f;
            var v = (y + .5f) / 256f;
            var visible = InEllipse(u, v, .5f, .67f, .255f, .25f) ||
                          InEllipse(u, v, .5f, .34f, .32f, .31f) ||
                          InEllipse(u, v, .22f, .35f, .1f, .25f) ||
                          InEllipse(u, v, .78f, .35f, .1f, .25f) ||
                          InEllipse(u, v, .39f, .08f, .13f, .18f) ||
                          InEllipse(u, v, .61f, .08f, .13f, .18f);
            if (!visible) return Color.clear;
            var color = Color.Lerp(new Color(.08f, .22f, .42f), new Color(.12f, .86f, 1f), Mathf.Clamp01((u - .18f) / .64f));
            if (v > .53f && InEllipse(u, v, .5f, .68f, .205f, .16f)) color = new Color(.05f, .09f, .16f);
            if (InEllipse(u, v, .42f, .7f, .035f, .05f) || InEllipse(u, v, .58f, .7f, .035f, .05f)) color = new Color(1f, .26f, .47f);
            if (v < .27f && Mathf.Abs(u - .5f) < .055f) color = new Color(.04f, .08f, .14f);
            if (v > .84f && Mathf.Abs(u - .5f) < .04f) color = new Color(1f, .25f, .42f);
            var shine = Mathf.Clamp01(1f - Vector2.Distance(new Vector2(u, v), new Vector2(.36f, .58f)) * 3.2f);
            return Color.Lerp(color, Color.white, shine * .24f);
        }

        static Color IndexPixel(int x, int y)
        {
            if (CharacterPixel(x, y).a <= 0f) return Color.clear;
            var u = (x + .5f) / 256f;
            var v = (y + .5f) / 256f;
            var id = v > .53f ? (u < .5f ? 0f : .3333f) : (u < .5f ? .6667f : 1f);
            return new Color(id, Mathf.Clamp01(v), 0f, 1f);
        }

        static Color NormalPixel(int x, int y)
        {
            if (CharacterPixel(x, y).a <= 0f) return new Color(.5f, .5f, 1f, 0f);
            var u = (x + .5f) / 256f;
            var v = (y + .5f) / 256f;
            var nx = Mathf.Clamp((u - .5f) * 1.35f, -.72f, .72f);
            var ny = Mathf.Clamp((v - .48f) * 1.1f, -.72f, .72f);
            var nz = Mathf.Sqrt(Mathf.Max(.001f, 1f - nx * nx - ny * ny));
            return new Color(nx * .5f + .5f, ny * .5f + .5f, nz, 1f);
        }

        static Color MaskPixel(int x, int y)
        {
            if (CharacterPixel(x, y).a <= 0f) return Color.clear;
            var u = (x + .5f) / 256f;
            var v = (y + .5f) / 256f;
            var metallic = v > .52f ? .2f : .82f;
            var emission = u > .36f && u < .64f && v > .62f && v < .78f ? 1f : .05f;
            return new Color(metallic, emission, Mathf.Clamp01(Mathf.Abs(u - .5f) * 2f), 1f);
        }

        static Color NoisePixel(int x, int y)
        {
            var value = Mathf.Lerp(Hash(x, y), Mathf.PerlinNoise(x / 24f, y / 24f), .58f);
            return new Color(value, Hash(x + 71, y + 19), Hash(x + 13, y + 101), 1f);
        }

        static float Hash(int x, int y)
        {
            unchecked
            {
                var n = x * 374761393 + y * 668265263;
                n = (n ^ (n >> 13)) * 1274126177;
                return (n & 0x7fffffff) / 2147483647f;
            }
        }

        static Color PalettePixel(int x, int y)
        {
            var rows = new[,]
            {
                { new Color(.02f,.05f,.12f), new Color(.06f,.25f,.46f), new Color(.12f,.8f,1f), Color.white },
                { new Color(.1f,.02f,.16f), new Color(.42f,.07f,.55f), new Color(1f,.22f,.48f), new Color(1f,.82f,.36f) },
                { new Color(.03f,.1f,.06f), new Color(.08f,.42f,.24f), new Color(.36f,1f,.54f), new Color(.94f,1f,.72f) },
                { new Color(.08f,.08f,.08f), new Color(.32f,.25f,.2f), new Color(.92f,.62f,.22f), new Color(1f,.94f,.72f) }
            };
            return rows[y, x];
        }

        static void ConfigureTextureImporters()
        {
            Configure("DemoCharacter.png", true, false, true);
            Configure("DemoCharacter_Mesh.png", false, false, true, FilterMode.Bilinear, true);
            Configure("DemoCharacter_Quad.png", true, false, true);
            Configure("DemoCharacter_Subdivided.png", true, false, true);
            Configure("DemoCharacter_Flat.png", true, false, true);
            Configure("DemoCharacter_Tight.png", true, false, true);
            Configure("DemoCharacter_Index.png", false, false, false, FilterMode.Point);
            Configure("DemoCharacter_Normal.png", false, true, false);
            Configure("FlatNormal.png", false, true, false, FilterMode.Point);
            Configure("DemoCharacter_Mask.png", false, false, false);
            Configure("Noise.png", false, false, false);
            Configure("Palette.png", false, false, true, FilterMode.Point);
            Configure("Panel.png", true, false, true);
        }

        static void Configure(string name, bool sprite, bool normal, bool srgb, FilterMode filter = FilterMode.Bilinear, bool readable = false)
        {
            var path = Art + "/" + name;
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null) throw new InvalidOperationException("Missing generated texture importer: " + path);
            importer.textureType = normal ? TextureImporterType.NormalMap : sprite ? TextureImporterType.Sprite : TextureImporterType.Default;
            importer.spriteImportMode = sprite ? SpriteImportMode.Single : SpriteImportMode.None;
            importer.spritePixelsPerUnit = 100f;
            if (sprite)
            {
                var textureSettings = new TextureImporterSettings();
                importer.ReadTextureSettings(textureSettings);
                textureSettings.spriteMeshType = name.Contains("Tight", StringComparison.Ordinal) ? SpriteMeshType.Tight : SpriteMeshType.FullRect;
                importer.SetTextureSettings(textureSettings);
            }
            importer.alphaIsTransparency = !normal;
            importer.sRGBTexture = srgb;
            importer.mipmapEnabled = false;
            importer.filterMode = filter;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.isReadable = sprite || readable;
            importer.SaveAndReimport();
        }

        static void AttachSecondaryTextures()
        {
            AttachSecondaryTexturesTo("DemoCharacter.png", "DemoCharacter_Normal.png");
            AttachSecondaryTexturesTo("DemoCharacter_Flat.png", "FlatNormal.png");
            AttachSecondaryTexturesTo("DemoCharacter_Tight.png", "DemoCharacter_Normal.png");
        }

        static void AttachSecondaryTexturesTo(string spriteName, string normalName)
        {
            var importer = AssetImporter.GetAtPath(Art + "/" + spriteName) as TextureImporter;
            var factories = new SpriteDataProviderFactories();
            factories.Init();
            var provider = factories.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();
            var secondary = provider.GetDataProvider<ISecondaryTextureDataProvider>();
            if (secondary == null) throw new InvalidOperationException("Secondary Sprite Texture data provider is unavailable.");
            secondary.textures = new[]
            {
                new SecondarySpriteTexture { name = "_NormalMap", texture = AssetDatabase.LoadAssetAtPath<Texture2D>(Art + "/" + normalName) },
                new SecondarySpriteTexture { name = "_MaskTex", texture = AssetDatabase.LoadAssetAtPath<Texture2D>(Art + "/DemoCharacter_Mask.png") }
            };
            provider.Apply();
            importer.SaveAndReimport();
        }

        static void GenerateSpriteAtlas()
        {
            AssetDatabase.DeleteAsset(Art + "/ShaderCapture.spriteatlasv2");
            var path = Art + "/ShaderCapture.spriteatlas";
            AssetDatabase.DeleteAsset(path);
            var atlas = new SpriteAtlas();
            atlas.SetPackingSettings(new SpriteAtlasPackingSettings { enableRotation = false, enableTightPacking = false, padding = 8, blockOffset = 1 });
            atlas.SetTextureSettings(new SpriteAtlasTextureSettings { readable = false, generateMipMaps = false, sRGB = true, filterMode = FilterMode.Bilinear });
            atlas.Add(new UnityEngine.Object[]
            {
                AssetDatabase.LoadAssetAtPath<Texture2D>(Art + "/DemoCharacter.png"),
                AssetDatabase.LoadAssetAtPath<Texture2D>(Art + "/DemoCharacter_Tight.png")
            });
            AssetDatabase.CreateAsset(atlas, path);
        }

        static void GenerateSubdividedMesh()
        {
            AssetDatabase.DeleteAsset(Art + "/DemoCharacter_Subdivided.asset");
            GenerateGridMesh(Art + "/DemoCharacter_QuadMesh.asset", "DemoCharacter_QuadMesh", 1);
            GenerateGridMesh(Art + "/DemoCharacter_SubdividedMesh.asset", "DemoCharacter_SubdividedMesh", 12);
        }

        static void GenerateGridMesh(string path, string meshName, int segments)
        {
            AssetDatabase.DeleteAsset(path);
            var vertices = new Vector3[(segments + 1) * (segments + 1)];
            var uv = new Vector2[vertices.Length];
            for (var y = 0; y <= segments; y++)
            for (var x = 0; x <= segments; x++)
            {
                var i = y * (segments + 1) + x;
                uv[i] = new Vector2(x / (float)segments, y / (float)segments);
                vertices[i] = new Vector3(Mathf.Lerp(-1.28f, 1.28f, uv[i].x), Mathf.Lerp(-1.28f, 1.28f, uv[i].y), 0f);
            }
            var triangles = new int[segments * segments * 6];
            var index = 0;
            for (var y = 0; y < segments; y++)
            for (var x = 0; x < segments; x++)
            {
                var a = y * (segments + 1) + x;
                var b = a + 1;
                var c = a + segments + 1;
                var d = c + 1;
                triangles[index++] = a; triangles[index++] = d; triangles[index++] = c;
                triangles[index++] = a; triangles[index++] = b; triangles[index++] = d;
            }
            var mesh = new Mesh { name = meshName, vertices = vertices, uv = uv, triangles = triangles };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, path);
        }

        static void GenerateComparisonSprites()
        {
            ConfigureComparisonSprite("DemoCharacter_Quad.png", AssetDatabase.LoadAssetAtPath<Mesh>(Art + "/DemoCharacter_QuadMesh.asset"));
            ConfigureComparisonSprite("DemoCharacter_Subdivided.png", AssetDatabase.LoadAssetAtPath<Mesh>(Art + "/DemoCharacter_SubdividedMesh.asset"));
        }

        static void ConfigureComparisonSprite(string fileName, Mesh mesh)
        {
            if (mesh == null) throw new InvalidOperationException("Comparison mesh is missing: " + fileName);
            var importer = AssetImporter.GetAtPath(Art + "/" + fileName) as TextureImporter;
            var factories = new SpriteDataProviderFactories();
            factories.Init();
            var provider = factories.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();
            var spriteRect = provider.GetSpriteRects().Single();
            var meshProvider = provider.GetDataProvider<ISpriteMeshDataProvider>();
            if (meshProvider == null) throw new InvalidOperationException("Sprite mesh data provider is unavailable: " + fileName);
            var vertices = mesh.uv.Select(uv => new Vertex2DMetaData
            {
                position = new Vector2(spriteRect.rect.x + uv.x * spriteRect.rect.width, spriteRect.rect.y + uv.y * spriteRect.rect.height),
                boneWeight = new BoneWeight { boneIndex0 = 0, weight0 = 1f }
            }).ToArray();
            meshProvider.SetVertices(spriteRect.spriteID, vertices);
            meshProvider.SetIndices(spriteRect.spriteID, mesh.triangles);
            var boundaryEdges = mesh.triangles
                .SelectMany((value, index) => index % 3 == 0
                    ? new[]
                    {
                        new Vector2Int(mesh.triangles[index], mesh.triangles[index + 1]),
                        new Vector2Int(mesh.triangles[index + 1], mesh.triangles[index + 2]),
                        new Vector2Int(mesh.triangles[index + 2], mesh.triangles[index])
                    }
                    : Array.Empty<Vector2Int>())
                .GroupBy(edge => (min: Mathf.Min(edge.x, edge.y), max: Mathf.Max(edge.x, edge.y)))
                .Where(group => group.Count() == 1)
                .Select(group => group.First()).ToArray();
            meshProvider.SetEdges(spriteRect.spriteID, boundaryEdges);
            provider.Apply();
            importer.SaveAndReimport();
        }

        static void GenerateMaterials()
        {
            var baseMaterialPath = Materials + "/BaseSprite.mat";
            AssetDatabase.DeleteAsset(baseMaterialPath);
            var baseShader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (baseShader == null) throw new InvalidOperationException("URP 2D Sprite-Unlit-Default shader is unavailable.");
            AssetDatabase.CreateAsset(new Material(baseShader) { name = "BaseSprite" }, baseMaterialPath);

            for (var i = 1; i <= 12; i++)
            {
                var graphPath = Graphs + "/Effect" + i.ToString("00") + "_" + SceneNames[i].Substring(3) + ".shadergraph";
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(graphPath);
                if (shader == null) throw new InvalidOperationException("Shader Graph did not import as a Shader: " + graphPath);
                var materialPath = Materials + "/Effect" + i.ToString("00") + ".mat";
                AssetDatabase.DeleteAsset(materialPath);
                var material = new Material(shader) { name = "Effect" + i.ToString("00") };
                if (i == 5)
                    material.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(Art + "/DemoCharacter.png"));
                AssetDatabase.CreateAsset(material, materialPath);
            }
        }

        static void GenerateScenes()
        {
            GenerateHub();
            for (var i = 1; i <= 12; i++) GenerateEffectScene(i);
        }

        static void GenerateVolumeProfile()
        {
            var path = SettingsRoot + "/ShaderCaptureVolumeProfile.asset";
            AssetDatabase.DeleteAsset(path);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "ShaderCaptureVolumeProfile";
            var bloom = profile.Add<Bloom>();
            bloom.threshold.Override(.75f);
            bloom.intensity.Override(.65f);
            bloom.scatter.Override(.62f);
            AssetDatabase.CreateAsset(profile, path);
        }

        static void GenerateRecorderPresets()
        {
            CreateStillPreset();
            CreateMoviePreset(30);
            CreateMoviePreset(60);
        }

        static void CreateStillPreset()
        {
            var path = RecorderSettings + "/Still_1920x1080_PNG.asset";
            AssetDatabase.DeleteAsset(path);
            var controller = ScriptableObject.CreateInstance<RecorderControllerSettings>();
            controller.name = "Still 1920x1080 PNG";
            controller.SetRecordModeToManual();
            controller.FrameRate = 30f;
            AssetDatabase.CreateAsset(controller, path);
            var recorder = ScriptableObject.CreateInstance<ImageRecorderSettings>();
            recorder.name = "Still PNG";
            recorder.Enabled = true;
            recorder.OutputFormat = ImageRecorderSettings.ImageRecorderOutputFormat.PNG;
            recorder.CaptureAlpha = false;
            recorder.OutputFile = "Captures/Still_" + DefaultWildcard.Take + "." + DefaultWildcard.Frame;
            recorder.imageInputSettings = new GameViewInputSettings { OutputWidth = 1920, OutputHeight = 1080 };
            AssetDatabase.AddObjectToAsset(recorder, controller);
            controller.AddRecorderSettings(recorder);
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
        }

        static void CreateMoviePreset(int frameRate)
        {
            var path = RecorderSettings + "/Movie_1920x1080_" + frameRate + "fps_H264.asset";
            AssetDatabase.DeleteAsset(path);
            var controller = ScriptableObject.CreateInstance<RecorderControllerSettings>();
            controller.name = "Movie 1920x1080 " + frameRate + "fps H264";
            controller.SetRecordModeToManual();
            controller.FrameRate = frameRate;
            AssetDatabase.CreateAsset(controller, path);
            var recorder = ScriptableObject.CreateInstance<MovieRecorderSettings>();
            recorder.name = "Movie MP4";
            recorder.Enabled = true;
            recorder.CaptureAudio = false;
            recorder.CaptureAlpha = false;
            recorder.EncoderSettings = new CoreEncoderSettings
            {
                Codec = CoreEncoderSettings.OutputCodec.MP4,
                EncodingQuality = CoreEncoderSettings.VideoEncodingQuality.High
            };
            recorder.ImageInputSettings = new GameViewInputSettings { OutputWidth = 1920, OutputHeight = 1080 };
            recorder.OutputFile = "Captures/Movie_" + frameRate + "fps_" + DefaultWildcard.Take;
            AssetDatabase.AddObjectToAsset(recorder, controller);
            controller.AddRecorderSettings(recorder);
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
        }

        static void GenerateCaptureStagePrefab()
        {
            var root = new GameObject("Capture Stage");
            CreateCamera(new Color(.012f, .022f, .055f)).transform.SetParent(root.transform, true);
            var volume = new GameObject("Capture Post Processing", typeof(Volume));
            volume.transform.SetParent(root.transform, false);
            var volumeComponent = volume.GetComponent<Volume>();
            volumeComponent.isGlobal = true;
            volumeComponent.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(SettingsRoot + "/ShaderCaptureVolumeProfile.asset");
            CreateBackdrop(root.transform);
            AddText("Before Label", "BEFORE", new Vector3(-2.4f, 3.25f, 0f), 25, new Color(.55f, .72f, .86f)).transform.SetParent(root.transform, true);
            AddText("After Label", "SHADER OUTPUT", new Vector3(2.4f, 3.25f, 0f), 25, new Color(.35f, .94f, 1f)).transform.SetParent(root.transform, true);
            AddText("Scene Title", "SHADER EFFECT", new Vector3(0f, -3.92f, 0f), 30, Color.white).transform.SetParent(root.transform, true);
            PrefabUtility.SaveAsPrefabAsset(root, Prefabs + "/CaptureStage.prefab");
            UnityEngine.Object.DestroyImmediate(root);
        }

        static Camera CreateCamera(Color background)
        {
            var go = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            go.tag = "MainCamera";
            var camera = go.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5.1f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = background;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            go.transform.position = new Vector3(0f, 0f, -10f);
            return camera;
        }

        static void GenerateHub()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateCamera(new Color(.015f, .025f, .06f));
            new GameObject("Capture Hub", typeof(ShaderCapture.CaptureHubController));
            EditorSceneManager.SaveScene(scene, Scenes + "/" + SceneNames[0] + ".unity");
        }

        static void GenerateEffectScene(int index)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var stage = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "/CaptureStage.prefab"));
            stage.name = "Capture Stage";
            stage.transform.Find("Scene Title").GetComponent<TextMesh>().text = Titles[index];
            if (index == 5)
            {
                stage.transform.Find("Before Label").GetComponent<TextMesh>().text = "VERTEX DENSITY";
                stage.transform.Find("After Label").GetComponent<TextMesh>().text = "PATH ISOLATION";
            }
            else if (index == 7)
            {
                stage.transform.Find("After Label").GetComponent<TextMesh>().text = "LOCAL  /  SCREEN";
            }
            else if (index == 10)
            {
                stage.transform.Find("After Label").GetComponent<TextMesh>().text = "SECONDARY _MaskTex: R / G / B";
            }
            else if (index == 11)
            {
                stage.transform.Find("After Label").GetComponent<TextMesh>().text = "STABLE SUBJECT + REFLECTION";
            }
            else if (index == 1)
            {
                stage.transform.Find("After Label").GetComponent<TextMesh>().text = "HDR FLASH + OFFSET SEEDS";
            }
            else if (index == 4)
            {
                stage.transform.Find("After Label").GetComponent<TextMesh>().text = "4/8 DIRECTIONS  x  TIGHT/FULL";
            }

            var baseSprite = AssetDatabase.LoadAssetAtPath<Sprite>(Art + "/DemoCharacter.png");
            var flatSprite = index == 9 ? AssetDatabase.LoadAssetAtPath<Sprite>(Art + "/DemoCharacter_Flat.png") : null;
            var material = AssetDatabase.LoadAssetAtPath<Material>(Materials + "/Effect" + index.ToString("00") + ".mat");
            var before = index switch
            {
                5 => CreateWindDensityComparison(material),
                9 => CreateNormalSide("Before", flatSprite, material, AssetDatabase.LoadAssetAtPath<Texture2D>(Art + "/FlatNormal.png"), new Vector3(-2.4f, -.1f, 0f)),
                _ => CreateSprite("Before", baseSprite, null, new Vector3(-2.4f, -.1f, 0f), 2.15f)
            };
            if (index == 9)
            {
                stage.transform.Find("Before Label").GetComponent<TextMesh>().text = "SECONDARY FLAT NORMAL";
                stage.transform.Find("After Label").GetComponent<TextMesh>().text = "SECONDARY _NormalMap";
            }
            var after = index switch
            {
                5 => CreateWindPathComparison(material),
                1 => CreateHitFlashComparison(baseSprite, material),
                3 => CreateDissolveComparison(baseSprite, material),
                4 => CreateOutlineComparison(baseSprite, material),
                7 => CreateDitherComparison(baseSprite, material),
                9 => CreateNormalSide("After", baseSprite, material, AssetDatabase.LoadAssetAtPath<Texture2D>(Art + "/DemoCharacter_Normal.png"), new Vector3(2.4f, -.1f, 0f)),
                10 => CreateMaskComparison(baseSprite, material),
                11 => CreateWaterReflectionComparison(baseSprite, material),
                12 => CreateSingleAfterGroup(baseSprite, material),
                _ => CreateSprite("After", baseSprite, material, new Vector3(2.4f, -.1f, 0f), 2.15f)
            };

            if (index == 12) AddScanTargets(baseSprite, material, after.transform);
            if (index == 9 || index == 10) Add2DLights(index == 9, before, after);

            var controller = new GameObject("Effect Demo Controller", typeof(ShaderCapture.EffectDemoController)).GetComponent<ShaderCapture.EffectDemoController>();
            var serialized = new SerializedObject(controller);
            serialized.FindProperty("sceneIndex").intValue = index;
            serialized.FindProperty("effectTitle").stringValue = Titles[index];
            serialized.FindProperty("playbackSpeed").floatValue = index is 6 or 8 ? .35f : .22f;
            serialized.FindProperty("seed").floatValue = .173f + index * .031f;
            serialized.FindProperty("beforeRoot").objectReferenceValue = before;
            serialized.FindProperty("afterRoot").objectReferenceValue = after;
            serialized.FindProperty("noiseTexture").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>(Art + "/Noise.png");
            serialized.FindProperty("indexTexture").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>(Art + "/DemoCharacter_Index.png");
            serialized.FindProperty("paletteTexture").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>(Art + "/Palette.png");
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.SaveScene(scene, Scenes + "/" + SceneNames[index] + ".unity");
        }

        static void CreateBackdrop(Transform parent)
        {
            var panel = AssetDatabase.LoadAssetAtPath<Sprite>(Art + "/Panel.png");
            var left = CreateSprite("Before Panel", panel, null, new Vector3(-2.4f, -.15f, 2f), 1f);
            left.transform.localScale = new Vector3(13f, 20.625f, 1f);
            left.transform.SetParent(parent, true);
            left.GetComponent<SpriteRenderer>().color = new Color(.035f, .07f, .13f, 1f);
            var right = CreateSprite("After Panel", panel, null, new Vector3(2.4f, -.15f, 2f), 1f);
            right.transform.localScale = new Vector3(13f, 20.625f, 1f);
            right.transform.SetParent(parent, true);
            right.GetComponent<SpriteRenderer>().color = new Color(.025f, .055f, .11f, 1f);
        }

        static GameObject CreateSprite(string name, Sprite sprite, Material material, Vector3 position, float scale)
        {
            var go = new GameObject(name, typeof(SpriteRenderer));
            go.transform.position = position;
            go.transform.localScale = Vector3.one * scale;
            var renderer = go.GetComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sharedMaterial = material != null
                ? material
                : AssetDatabase.LoadAssetAtPath<Material>(Materials + "/BaseSprite.mat");
            renderer.sortingOrder = 10;
            return go;
        }

        static GameObject CreateGridMesh(string name, Material material, string meshPath, Vector3 position, float scale)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.position = position;
            go.transform.localScale = Vector3.one * scale;
            go.GetComponent<MeshFilter>().sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            var properties = go.AddComponent<ShaderCapture.ShaderCapturePropertyBlock>();
            properties.MainTextureOverride = AssetDatabase.LoadAssetAtPath<Texture2D>(Art + "/DemoCharacter.png");
            renderer.sortingOrder = 10;
            return go;
        }

        static GameObject CreateDitherComparison(Sprite sprite, Material material)
        {
            var root = new GameObject("After");
            root.transform.position = new Vector3(2.4f, -.1f, 0f);
            var local = CreateSprite("Local Dither", sprite, material, new Vector3(1.45f, -.1f, 0f), 1.25f);
            local.transform.SetParent(root.transform, true);
            var localProperties = local.AddComponent<ShaderCapture.ShaderCapturePropertyBlock>();
            localProperties.DitherSpace = 0f;
            var screen = CreateSprite("Screen Dither", sprite, material, new Vector3(3.35f, -.1f, 0f), 1.25f);
            screen.transform.SetParent(root.transform, true);
            var screenProperties = screen.AddComponent<ShaderCapture.ShaderCapturePropertyBlock>();
            screenProperties.DitherSpace = 1f;
            AddText("Local Label", "LOCAL", new Vector3(1.45f, -2.35f, 0f), 20, new Color(.55f, .82f, 1f)).transform.SetParent(root.transform, true);
            AddText("Screen Label", "SCREEN", new Vector3(3.35f, -2.35f, 0f), 20, new Color(.55f, 1f, .82f)).transform.SetParent(root.transform, true);
            return root;
        }

        static GameObject CreateNormalSide(string name, Sprite sprite, Material material, Texture2D normal, Vector3 position)
        {
            var root = new GameObject(name);
            root.transform.position = position;
            var subject = CreateSprite(name + " Subject", sprite, material, position, 2.15f);
            subject.transform.SetParent(root.transform, true);
            subject.AddComponent<ShaderCapture.ShaderCapturePropertyBlock>().NormalMapOverride = normal;
            return root;
        }

        static GameObject CreateDissolveComparison(Sprite sprite, Material material)
        {
            var root = new GameObject("After");
            root.transform.position = new Vector3(2.4f, -.1f, 0f);
            var positions = new[]
            {
                new Vector3(1.45f, .8f, 0f), new Vector3(3.35f, .8f, 0f),
                new Vector3(1.45f, -1.25f, 0f), new Vector3(3.35f, -1.25f, 0f)
            };
            var names = new[] { "Hard Dissolve", "Soft Dissolve", "Hard Reveal", "Soft Reveal" };
            for (var index = 0; index < positions.Length; index++)
            {
                var subject = CreateSprite(names[index], sprite, material, positions[index], .72f);
                subject.transform.SetParent(root.transform, true);
                var properties = subject.AddComponent<ShaderCapture.ShaderCapturePropertyBlock>();
                properties.DissolveSoftness = index % 2 == 0 ? 0f : .1f;
                properties.DissolveInvert = index < 2 ? 0f : 1f;
                AddText(names[index] + " Label", names[index].ToUpperInvariant(), positions[index] + new Vector3(0f, -1.05f, 0f), 14,
                    index % 2 == 0 ? new Color(1f, .58f, .68f) : new Color(.5f, .9f, 1f)).transform.SetParent(root.transform, true);
            }
            return root;
        }

        static GameObject CreateSingleAfterGroup(Sprite sprite, Material material)
        {
            var root = new GameObject("After");
            root.transform.position = new Vector3(2.4f, -.1f, 0f);
            var subject = CreateSprite("After Subject", sprite, material, new Vector3(2.4f, -.1f, 0f), 2.15f);
            subject.transform.SetParent(root.transform, true);
            return root;
        }

        static GameObject CreateHitFlashComparison(Sprite sprite, Material material)
        {
            var root = new GameObject("After");
            root.transform.position = new Vector3(2.4f, -.1f, 0f);
            var positions = new[] { 1.25f, 2.4f, 3.55f };
            var offsets = new[] { 0f, .33f, .66f };
            for (var index = 0; index < positions.Length; index++)
            {
                var subject = CreateSprite("Seed " + (index + 1), sprite, material, new Vector3(positions[index], -.1f, 0f), .88f);
                subject.transform.SetParent(root.transform, true);
                var properties = subject.AddComponent<ShaderCapture.ShaderCapturePropertyBlock>();
                properties.SeedOffset = offsets[index];
                AddText("Seed Label " + (index + 1), "SEED + " + offsets[index].ToString("0.00"), new Vector3(positions[index], -2.1f, 0f), 16, new Color(1f, .55f, .65f)).transform.SetParent(root.transform, true);
            }
            return root;
        }

        static GameObject CreateOutlineComparison(Sprite fullRectSprite, Material material)
        {
            var root = new GameObject("After");
            root.transform.position = new Vector3(2.4f, -.1f, 0f);
            var tightSprite = AssetDatabase.LoadAssetAtPath<Sprite>(Art + "/DemoCharacter_Tight.png");
            var positions = new[]
            {
                new Vector3(1.45f, .8f, 0f), new Vector3(3.35f, .8f, 0f),
                new Vector3(1.45f, -1.25f, 0f), new Vector3(3.35f, -1.25f, 0f)
            };
            var names = new[] { "4-WAY TIGHT", "4-WAY FULL", "8-WAY TIGHT", "8-WAY FULL" };
            for (var index = 0; index < positions.Length; index++)
            {
                var useTight = index % 2 == 0;
                var subject = CreateSprite(names[index], useTight ? tightSprite : fullRectSprite, material, positions[index], .72f);
                subject.transform.SetParent(root.transform, true);
                subject.AddComponent<ShaderCapture.ShaderCapturePropertyBlock>().OutlineDirections = index < 2 ? 4f : 8f;
                AddText(names[index] + " Label", names[index], positions[index] + new Vector3(0f, -1.05f, 0f), 14,
                    useTight ? new Color(1f, .55f, .72f) : new Color(.45f, .9f, 1f)).transform.SetParent(root.transform, true);
            }
            return root;
        }

        static GameObject CreateMaskComparison(Sprite sprite, Material material)
        {
            var root = new GameObject("After");
            root.transform.position = new Vector3(2.4f, -.1f, 0f);
            var positions = new[] { 1.25f, 2.4f, 3.55f };
            var labels = new[] { "R WET", "G METAL", "B WEAK" };
            for (var channel = 0; channel < 3; channel++)
            {
                var subject = CreateSprite(labels[channel] + " Channel", sprite, material, new Vector3(positions[channel], -.1f, 0f), .88f);
                subject.transform.SetParent(root.transform, true);
                if (channel == 1) subject.GetComponent<SpriteRenderer>().sortingLayerID = MaskMetalSortingLayerId;
                var properties = subject.AddComponent<ShaderCapture.ShaderCapturePropertyBlock>();
                properties.MaskChannel = channel;
                AddText(labels[channel] + " Label", labels[channel], new Vector3(positions[channel], -2.1f, 0f), 17,
                    channel == 0 ? new Color(.25f, .85f, 1f) : channel == 1 ? new Color(.85f, .95f, 1f) : new Color(1f, .35f, .62f)).transform.SetParent(root.transform, true);
            }
            return root;
        }

        static GameObject CreateWindDensityComparison(Material material)
        {
            var root = new GameObject("Before");
            root.transform.position = new Vector3(-2.4f, -.1f, 0f);
            var sprites = new[]
            {
                AssetDatabase.LoadAssetAtPath<Sprite>(Art + "/DemoCharacter_Quad.png"),
                AssetDatabase.LoadAssetAtPath<Sprite>(Art + "/DemoCharacter_Subdivided.png")
            };
            var labels = new[] { "4V VERTEX ONLY", "169V VERTEX ONLY" };
            var positions = new[] { -3.35f, -1.45f };
            for (var index = 0; index < sprites.Length; index++)
                CreateWindSubject(root.transform, labels[index], sprites[index], material,
                    new Vector3(positions[index], -.1f, 0f), 1.18f, 0f, 1f, 0f,
                    new Color(.55f, .82f, 1f));
            return root;
        }

        static GameObject CreateWindPathComparison(Material material)
        {
            var root = new GameObject("After");
            root.transform.position = new Vector3(2.4f, -.1f, 0f);
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Art + "/DemoCharacter_Subdivided.png");
            var labels = new[] { "UV ONLY", "SQUASH ONLY", "ALL PATHS" };
            var positions = new[] { 1.25f, 2.4f, 3.55f };
            var uv = new[] { 1f, 0f, 1f };
            var vertex = new[] { 0f, 0f, 1f };
            var squash = new[] { 0f, 1f, 1f };
            for (var index = 0; index < labels.Length; index++)
                CreateWindSubject(root.transform, labels[index], sprite, material,
                    new Vector3(positions[index], -.1f, 0f), .88f, uv[index], vertex[index], squash[index],
                    index == 0 ? new Color(.45f, 1f, .78f) : index == 1 ? new Color(1f, .72f, .42f) : new Color(1f, .5f, .72f));
            return root;
        }

        static void CreateWindSubject(Transform parent, string label, Sprite sprite, Material material,
            Vector3 position, float scale, float uv, float vertex, float squash, Color labelColor)
        {
            var subject = CreateSprite(label, sprite, material, position, scale);
            subject.transform.SetParent(parent, true);
            var properties = subject.AddComponent<ShaderCapture.ShaderCapturePropertyBlock>();
            properties.UvWiggleAmount = uv;
            properties.VertexWindAmount = vertex;
            properties.SquashAmount = squash;
            AddText(label + " Label", label, position + new Vector3(0f, -2.0f, 0f), 14, labelColor).transform.SetParent(parent, true);
        }

        static GameObject AddText(string name, string value, Vector3 position, int size, Color color)
        {
            var go = new GameObject(name, typeof(TextMesh));
            go.transform.position = position;
            var text = go.GetComponent<TextMesh>();
            text.text = value;
            text.fontSize = size;
            text.characterSize = .085f;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.color = color;
            text.GetComponent<MeshRenderer>().sortingOrder = 30;
            return go;
        }

        static GameObject CreateWaterReflectionComparison(Sprite sprite, Material reflectionMaterial)
        {
            var root = new GameObject("After");
            root.transform.position = new Vector3(2.4f, -.1f, 0f);
            var subject = CreateSprite("Stable Subject", sprite, null, new Vector3(2.4f, 1.15f, 0f), 1.32f);
            subject.transform.SetParent(root.transform, true);
            var reflection = CreateSprite("Reflection", sprite, reflectionMaterial, new Vector3(2.4f, -1.45f, 0f), 1.32f);
            reflection.name = "Reflection";
            reflection.transform.localScale = new Vector3(1.32f, -1.32f, 1.32f);
            reflection.GetComponent<SpriteRenderer>().color = new Color(.52f, .74f, 1f, .55f);
            reflection.transform.SetParent(root.transform, true);
            AddText("Waterline", "~ ~ ~ ~ ~ ~ ~ ~", new Vector3(2.4f, -.6f, -.1f), 28, new Color(.25f, .82f, 1f)).transform.SetParent(root.transform, true);
            return root;
        }

        static void AddScanTargets(Sprite sprite, Material material, Transform parent)
        {
            for (var i = 0; i < 5; i++)
            {
                var target = CreateSprite("Scan Target " + (i + 1), sprite, material, new Vector3(-3.6f + i * 1.8f, -2.3f + Mathf.Sin(i) * .18f, .1f), .46f);
                target.transform.SetParent(parent, true);
                target.GetComponent<SpriteRenderer>().color = Color.Lerp(new Color(.35f, .55f, 1f), Color.white, i / 4f);
            }
        }

        static Transform Add2DLights(bool normalDemo, GameObject beforeRoot, GameObject afterRoot)
        {
            var global = new GameObject("Global Light 2D", typeof(Light2D)).GetComponent<Light2D>();
            global.lightType = Light2D.LightType.Global;
            global.intensity = normalDemo ? .2f : .22f;
            global.targetSortingLayers = normalDemo ? new[] { 0 } : new[] { 0, MaskMetalSortingLayerId };
            if (normalDemo)
            {
                var flat = CreatePointLight("Flat Point Light 2D", new Vector3(-2.4f, 0f, -.5f), new Color(.72f, .92f, 1f), 8f, 2.6f);
                var mapped = CreatePointLight("Normal Point Light 2D", new Vector3(2.4f, 0f, -.5f), new Color(.72f, .92f, 1f), 8f, 2.6f);
                flat.SetParent(beforeRoot.transform, true);
                mapped.SetParent(afterRoot.transform, true);
                var rig = new GameObject("Deterministic Light Rig", typeof(ShaderCapture.DeterministicLightRig)).GetComponent<ShaderCapture.DeterministicLightRig>();
                ConfigureLightRig(rig, new[] { flat, mapped }, new[] { beforeRoot.transform, afterRoot.transform },
                    new[] { new Vector3(0f, .1f, -.5f), new Vector3(0f, .1f, -.5f) });
                rig.ApplyTime(0f);
                return rig.transform;
            }
            var metalLight = CreatePointLight("G Metal Blend Style Light 2D", new Vector3(2.4f, -.1f, -.5f), new Color(.72f, .92f, 1f), 2.6f, 2.7f, 3);
            metalLight.GetComponent<Light2D>().targetSortingLayers = new[] { MaskMetalSortingLayerId };
            metalLight.SetParent(afterRoot.transform, true);
            var metalRig = new GameObject("Deterministic Metal Light Rig", typeof(ShaderCapture.DeterministicLightRig)).GetComponent<ShaderCapture.DeterministicLightRig>();
            ConfigureLightRig(metalRig, new[] { metalLight }, new[] { afterRoot.transform }, new[] { new Vector3(0f, 0f, -.5f) });
            metalRig.ApplyTime(0f);
            return metalRig.transform;
        }

        static void ConfigureLightRig(ShaderCapture.DeterministicLightRig rig, Transform[] lightTransforms, Transform[] roots, Vector3[] localCenters)
        {
            var serialized = new SerializedObject(rig);
            var lights = serialized.FindProperty("lights");
            lights.arraySize = lightTransforms.Length;
            var centerRoots = serialized.FindProperty("centerRoots");
            centerRoots.arraySize = roots.Length;
            var centers = serialized.FindProperty("centers");
            centers.arraySize = localCenters.Length;
            for (var index = 0; index < lightTransforms.Length; index++)
            {
                lights.GetArrayElementAtIndex(index).objectReferenceValue = lightTransforms[index];
                centerRoots.GetArrayElementAtIndex(index).objectReferenceValue = roots[index];
                centers.GetArrayElementAtIndex(index).vector3Value = localCenters[index];
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static Transform CreatePointLight(string name, Vector3 position, Color color, float intensity, float radius, int blendStyleIndex = 0)
        {
            var pointObject = new GameObject(name, typeof(Light2D));
            pointObject.transform.position = position;
            var point = pointObject.GetComponent<Light2D>();
            point.lightType = Light2D.LightType.Point;
            point.intensity = intensity;
            point.pointLightOuterRadius = radius;
            point.color = color;
            var marker = new GameObject("Visible Moving Light Marker", typeof(SpriteRenderer));
            marker.transform.SetParent(pointObject.transform, false);
            marker.transform.localPosition = new Vector3(0f, 0f, -.1f);
            marker.transform.localScale = Vector3.one * .62f;
            var markerRenderer = marker.GetComponent<SpriteRenderer>();
            markerRenderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Art + "/Panel.png");
            markerRenderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(Materials + "/BaseSprite.mat");
            markerRenderer.color = new Color(1f, .86f, .3f, 1f);
            markerRenderer.sortingOrder = 50;
            var serialized = new SerializedObject(point);
            var blendStyle = serialized.FindProperty("m_BlendStyleIndex");
            if (blendStyle != null) blendStyle.intValue = blendStyleIndex;
            var quality = serialized.FindProperty("m_NormalMapQuality");
            if (quality != null) quality.intValue = (int)Light2D.NormalMapQuality.Accurate;
            var useNormalMap = serialized.FindProperty("m_UseNormalMap");
            if (useNormalMap != null) useNormalMap.boolValue = true;
            var normalMapDistance = serialized.FindProperty("m_NormalMapDistance");
            if (normalMapDistance != null) normalMapDistance.floatValue = .5f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return pointObject.transform;
        }

        static void EnsureMaskMetalSortingLayer()
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (assets.Length == 0) throw new InvalidOperationException("TagManager.asset could not be loaded.");
            var tagManager = new SerializedObject(assets[0]);
            var layers = tagManager.FindProperty("m_SortingLayers");
            if (layers == null) throw new InvalidOperationException("TagManager sorting layers could not be read.");
            for (var index = 0; index < layers.arraySize; index++)
                if (layers.GetArrayElementAtIndex(index).FindPropertyRelative("name").stringValue == MaskMetalSortingLayerName)
                    return;
            var newIndex = layers.arraySize;
            layers.InsertArrayElementAtIndex(newIndex);
            var layer = layers.GetArrayElementAtIndex(newIndex);
            layer.FindPropertyRelative("name").stringValue = MaskMetalSortingLayerName;
            layer.FindPropertyRelative("uniqueID").intValue = MaskMetalSortingLayerId;
            layer.FindPropertyRelative("locked").boolValue = false;
            tagManager.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
        }

        static void ConfigureBuildSettings()
        {
            EditorBuildSettings.scenes = SceneNames.Select(name => new EditorBuildSettingsScene(Scenes + "/" + name + ".unity", true)).ToArray();
        }
    }
}
