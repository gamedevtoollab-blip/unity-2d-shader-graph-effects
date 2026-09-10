using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace ShaderCapture.Editor
{
    static class ShaderCaptureValidationReceipt
    {
        [Serializable]
        sealed class Receipt
        {
            public string generatedUtc;
            public int consoleErrors;
            public int consoleWarnings;
            public int consoleLogs;
            public string unityVersion;
        }

        [MenuItem("Tools/Shader Capture/Write Validation Receipt")]
        static void WriteReceipt()
        {
            var logEntries = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.LogEntries");
            var getCounts = logEntries?.GetMethod("GetCountsByType", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (getCounts == null) throw new MissingMethodException("UnityEditor.LogEntries", "GetCountsByType");
            var arguments = new object[] { 0, 0, 0 };
            getCounts.Invoke(null, arguments);
            var receipt = new Receipt
            {
                generatedUtc = DateTime.UtcNow.ToString("O"),
                consoleErrors = (int)arguments[0],
                consoleWarnings = (int)arguments[1],
                consoleLogs = (int)arguments[2],
                unityVersion = Application.unityVersion
            };
            var root = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "ReviewBundles", "validation"));
            Directory.CreateDirectory(root);
            var path = Path.Combine(root, "console-receipt.json");
            File.WriteAllText(path, JsonUtility.ToJson(receipt, true) + Environment.NewLine);
            Debug.Log("[ShaderCapture] Validation receipt: " + path);
        }
    }
}
