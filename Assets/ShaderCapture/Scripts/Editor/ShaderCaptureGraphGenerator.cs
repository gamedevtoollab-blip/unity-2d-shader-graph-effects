using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Graphing;
using UnityEditor.ShaderGraph;
using UnityEditor.ShaderGraph.Drawing;
using UnityEditor.ShaderGraph.Internal;
using UnityEngine;

namespace ShaderCapture.Editor
{
    static class ShaderCaptureGraphGenerator
    {
        const string GraphFolder = "Assets/ShaderCapture/Shaders/Graphs";
        const string IncludePath = "Assets/ShaderCapture/Shaders/Includes/ShaderCaptureEffects.hlsl";
        const string UnlitTemplate = "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/GraphTemplates/2D/1_2D Sprite Unlit.shadergraph";
        const string LitTemplate = "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/GraphTemplates/2D/0_2D Sprite Lit.shadergraph";
        const string GenerationMarker = "Assets/ShaderCapture/.round4-generation";
        const string GenerationVersion = "round-05-v1";

        static readonly string[] GraphNames =
        {
            "Effect01_HitFlashInvincible",
            "Effect02_PaletteSwap",
            "Effect03_Dissolve",
            "Effect04_OutlineInnerRim",
            "Effect05_WindVertexSquash",
            "Effect06_GlitchRgbSplit",
            "Effect07_PixelPosterizeDither",
            "Effect08_HologramShine",
            "Effect09_NormalMap2DLight",
            "Effect10_MaskMapLighting",
            "Effect11_WaterReflection",
            "Effect12_WorldScanReveal"
        };

        [InitializeOnLoadMethod]
        static void ScheduleInitialGeneration()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
                if (!File.Exists(Path.GetFullPath(IncludePath))) return;
                if (File.Exists(GenerationMarker) && File.ReadAllText(GenerationMarker).Trim() == GenerationVersion) return;
                GenerateAllGraphs();
                ShaderCaptureProjectGenerator.GenerateAll();
                File.WriteAllText(GenerationMarker, GenerationVersion + Environment.NewLine);
                AssetDatabase.ImportAsset(GenerationMarker);
            };
        }

        [MenuItem("Tools/Shader Capture/Generate Shader Graphs")]
        public static void GenerateAllGraphs()
        {
            Directory.CreateDirectory(Path.GetFullPath(GraphFolder));
            AssetDatabase.ImportAsset(IncludePath, ImportAssetOptions.ForceSynchronousImport);
            var includeGuid = AssetDatabase.AssetPathToGUID(IncludePath);
            if (string.IsNullOrEmpty(includeGuid)) throw new InvalidOperationException($"Missing include GUID: {IncludePath}");

            for (var index = 0; index < GraphNames.Length; index++)
            {
                var effectNumber = index + 1;
                var useLit = effectNumber is 9 or 10;
                CreateGraph(effectNumber, GraphNames[index], useLit ? LitTemplate : UnlitTemplate, includeGuid);
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Debug.Log($"[ShaderCapture] Generated {GraphNames.Length} Shader Graph assets.");
        }

        static void CreateGraph(int effectNumber, string graphName, string templatePath, string includeGuid)
        {
            if (!FileUtilities.TryReadGraphDataFromDisk(templatePath, out var graph) || graph == null)
                throw new InvalidOperationException($"Could not read Shader Graph template: {templatePath}");

            graph.path = $"Shader Capture/{graphName}";
            if (effectNumber == 10)
            {
                var spriteMaskProperty = graph.properties.OfType<Texture2DShaderProperty>().FirstOrDefault(property => property.referenceName == "_MaskMap");
                if (spriteMaskProperty == null) throw new InvalidOperationException("Sprite Lit template Mask Map property was not found.");
                spriteMaskProperty.overrideReferenceName = "_MaskTex";
                spriteMaskProperty.displayName = "Secondary Sprite Mask (_MaskTex)";
                var defaultReference = typeof(AbstractShaderProperty).GetField("m_DefaultReferenceName", BindingFlags.Instance | BindingFlags.NonPublic);
                defaultReference?.SetValue(spriteMaskProperty, "_MaskTex");
            }
            foreach (var textureProperty in graph.properties.OfType<Texture2DShaderProperty>())
            {
                // SpriteRenderer supplies _MainTex per renderer. 2D SRP Batcher rejects
                // material-side _MainTex_ST and _MainTex_TexelSize declarations.
                textureProperty.useTilingAndOffset = false;
                textureProperty.useTexelSize = false;
                if (textureProperty.overrideReferenceName is "_NormalMap" or "_MaskTex")
                {
                    var perRendererData = typeof(AbstractShaderProperty).GetField("m_PerRendererData", BindingFlags.Instance | BindingFlags.NonPublic);
                    if (perRendererData == null) throw new MissingFieldException(typeof(AbstractShaderProperty).FullName, "m_PerRendererData");
                    perRendererData.SetValue(textureProperty, true);
                }
            }
            if (effectNumber == 5)
            {
                var universalTarget = graph.activeTargets.FirstOrDefault(target => target.GetType().Name == "UniversalTarget");
                var surfaceField = universalTarget?.GetType().GetField("m_SurfaceType", BindingFlags.Instance | BindingFlags.NonPublic);
                if (surfaceField == null) throw new MissingFieldException("UniversalTarget", "m_SurfaceType");
                surfaceField.SetValue(universalTarget, Enum.Parse(surfaceField.FieldType, "Transparent"));
            }
            var baseColorBlock = graph.GetNodes<BlockNode>().First(node => node.descriptor == BlockFields.SurfaceDescription.BaseColor);
            var alphaBlock = graph.GetNodes<BlockNode>().First(node => node.descriptor == BlockFields.SurfaceDescription.Alpha);

            var incomingBaseEdges = graph.GetEdges(baseColorBlock.GetSlotReference(0)).ToArray();
            if (incomingBaseEdges.Length != 1)
                throw new InvalidOperationException($"Template Base Color connection changed: {templatePath}");
            var baseSource = incomingBaseEdges[0].outputSlot;
            graph.RemoveEdges(incomingBaseEdges);
            graph.RemoveEdges(graph.GetEdges(alphaBlock.GetSlotReference(0)).ToArray());

            if (!ShaderCaptureNativeGraphBuilder.TryBuildFragment(graph, effectNumber, baseSource, baseColorBlock, alphaBlock))
            {
                var fragment = CreateFragmentNode(effectNumber, includeGuid);
                var fragmentUv = new UVNode { precision = Precision.Single };
                var worldPosition = new PositionNode { precision = Precision.Single };
                var screenPosition = new ScreenPositionNode { precision = Precision.Single };
                graph.AddNode(fragment);
                graph.AddNode(fragmentUv);
                graph.AddNode(worldPosition);
                graph.AddNode(screenPosition);
                graph.Connect(baseSource, fragment.GetSlotReference(0));
                graph.Connect(fragmentUv.GetSlotReference(UVNode.OutputSlotId), fragment.GetSlotReference(1));
                graph.Connect(worldPosition.GetSlotReference(0), fragment.GetSlotReference(2));
                graph.Connect(screenPosition.GetSlotReference(0), fragment.GetSlotReference(3));
                graph.Connect(fragment.GetSlotReference(4), baseColorBlock.GetSlotReference(0));
                graph.Connect(fragment.GetSlotReference(5), alphaBlock.GetSlotReference(0));
            }

            if (effectNumber == 9)
                AmplifyNormalMap(graph);

            if (effectNumber == 5)
            {
                var positionBlock = graph.GetNodes<BlockNode>().First(node => node.descriptor == BlockFields.VertexDescription.Position);
                graph.RemoveEdges(graph.GetEdges(positionBlock.GetSlotReference(0)).ToArray());
                if (!ShaderCaptureNativeGraphBuilder.TryBuildVertex(graph, effectNumber, positionBlock))
                {
                    var vertex = CreateVertexNode(includeGuid);
                    var objectPosition = new PositionNode { precision = Precision.Single };
                    SetCoordinateSpace(objectPosition, CoordinateSpace.Object);
                    var vertexUv = new UVNode { precision = Precision.Single };
                    graph.AddNode(vertex);
                    graph.AddNode(objectPosition);
                    graph.AddNode(vertexUv);
                    graph.Connect(objectPosition.GetSlotReference(0), vertex.GetSlotReference(0));
                    graph.Connect(vertexUv.GetSlotReference(UVNode.OutputSlotId), vertex.GetSlotReference(1));
                    graph.Connect(vertex.GetSlotReference(2), positionBlock.GetSlotReference(0));
                }
            }

            graph.ValidateGraph();
            var destination = $"{GraphFolder}/{graphName}.shadergraph";
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(destination) != null)
                AssetDatabase.DeleteAsset(destination);
            if (FileUtilities.WriteShaderGraphToDisk(destination, graph) == null)
                throw new IOException($"Failed to write Shader Graph: {destination}");
            AssetDatabase.ImportAsset(destination, ImportAssetOptions.ForceSynchronousImport);
        }

        static void AmplifyNormalMap(GraphData graph)
        {
            var normalBlock = graph.GetNodes<BlockNode>().First(node => node.descriptor == BlockFields.SurfaceDescription.NormalTS);
            var incoming = graph.GetEdges(normalBlock.GetSlotReference(0)).ToArray();
            if (incoming.Length != 1) throw new InvalidOperationException("Sprite Lit template Normal connection changed.");
            var nodeType = typeof(GraphData).Assembly.GetType("UnityEditor.ShaderGraph.NormalStrengthNode");
            if (nodeType == null) throw new TypeLoadException("Shader Graph NormalStrengthNode is unavailable.");
            var strength = (AbstractMaterialNode)Activator.CreateInstance(nodeType);
            strength.name = "Normal Strength (2.5x)";
            var drawState = strength.drawState;
            drawState.position = new Rect(-160f, 520f, 210f, 120f);
            strength.drawState = drawState;
            graph.AddNode(strength);
            strength.FindSlot<Vector1MaterialSlot>(1).value = 2.5f;
            graph.RemoveEdges(incoming);
            graph.Connect(incoming[0].outputSlot, strength.GetSlotReference(0));
            graph.Connect(strength.GetSlotReference(2), normalBlock.GetSlotReference(0));
        }

        static CustomFunctionNode CreateFragmentNode(int effectNumber, string includeGuid)
        {
            var node = new CustomFunctionNode
            {
                sourceType = HlslSourceType.File,
                functionName = $"Effect{effectNumber:00}Fragment",
                functionSource = includeGuid,
                functionSourceUsePragmas = false,
                precision = effectNumber == 6 ? Precision.Half : Precision.Single
            };
            node.AddSlot(new Vector4MaterialSlot(0, "Base RGBA", "BaseRGBA", SlotType.Input, Vector4.one));
            node.AddSlot(new Vector2MaterialSlot(1, "UV", "UV", SlotType.Input, Vector2.zero));
            node.AddSlot(new Vector3MaterialSlot(2, "World Position", "WorldPos", SlotType.Input, Vector3.zero));
            node.AddSlot(new Vector3MaterialSlot(3, "Screen Position", "ScreenPos", SlotType.Input, Vector3.zero));
            node.AddSlot(new Vector3MaterialSlot(4, "Out Color", "OutColor", SlotType.Output, Vector3.one));
            node.AddSlot(new Vector1MaterialSlot(5, "Out Alpha", "OutAlpha", SlotType.Output, 1f));
            return node;
        }

        static CustomFunctionNode CreateVertexNode(string includeGuid)
        {
            var node = new CustomFunctionNode
            {
                sourceType = HlslSourceType.File,
                functionName = "Effect05Vertex",
                functionSource = includeGuid,
                functionSourceUsePragmas = false,
                precision = Precision.Single
            };
            node.AddSlot(new Vector3MaterialSlot(0, "Position OS", "PositionOS", SlotType.Input, Vector3.zero));
            node.AddSlot(new Vector2MaterialSlot(1, "UV", "UV", SlotType.Input, Vector2.zero));
            node.AddSlot(new Vector3MaterialSlot(2, "Out Position", "OutPosition", SlotType.Output, Vector3.zero));
            return node;
        }

        static void SetCoordinateSpace(PositionNode node, CoordinateSpace coordinateSpace)
        {
            var field = typeof(GeometryNode).GetField("m_Space", BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null) throw new MissingFieldException(typeof(GeometryNode).FullName, "m_Space");
            field.SetValue(node, coordinateSpace);
            node.UpdateNodeAfterDeserialization();
        }
    }
}
