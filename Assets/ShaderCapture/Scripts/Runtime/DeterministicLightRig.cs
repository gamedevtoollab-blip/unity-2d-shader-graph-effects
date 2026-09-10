using UnityEngine;

namespace ShaderCapture
{
    [ExecuteAlways]
    public sealed class DeterministicLightRig : MonoBehaviour
    {
        static readonly int DemoTimeId = Shader.PropertyToID("_SC_DemoTime");

        [SerializeField] Transform[] lights;
        [SerializeField] Transform[] centerRoots;
        [SerializeField] Vector3[] centers;
        [SerializeField] Vector2 orbitRadius = new(1.25f, 1.05f);
        [SerializeField] float angularSpeed = 1.35f;

        void Update() => ApplyTime(Shader.GetGlobalFloat(DemoTimeId));

        public void ApplyTime(float demoTime)
        {
            if (lights == null || centers == null) return;
            var count = Mathf.Min(lights.Length, centers.Length);
            var angle = Mathf.Max(0f, demoTime) * angularSpeed;
            var offset = new Vector3(Mathf.Cos(angle) * orbitRadius.x, Mathf.Sin(angle) * orbitRadius.y, 0f);
            for (var index = 0; index < count; index++)
                if (lights[index] != null)
                {
                    var center = centerRoots != null && index < centerRoots.Length && centerRoots[index] != null
                        ? centerRoots[index].TransformPoint(centers[index])
                        : centers[index];
                    lights[index].position = center + offset;
                }
        }
    }
}
