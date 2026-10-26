using UnityEngine;

namespace Meowdieval.FoliageForge
{
    /// <summary>
    /// Pushes one shared wind setup to the global shader values the Foliage Forge shader reads.
    /// Place one in the scene; every tree sways together and nothing else needs touching.
    /// </summary>
    [ExecuteAlways, DisallowMultipleComponent]
    [AddComponentMenu("Meowdieval/Foliage Forge/Foliage Wind")]
    public sealed class FoliageWind : MonoBehaviour
    {
        static readonly int WindDirId = Shader.PropertyToID("_FF_WindDir");
        static readonly int WindParamsId = Shader.PropertyToID("_FF_WindParams");
        static readonly int GustColorId = Shader.PropertyToID("_FF_GustColor");

        [Tooltip("Wind heading in degrees around Y. 0 blows toward +Z, 90 toward +X.")]
        [SerializeField, Range(0f, 360f)] float _direction = 45f;
        [Tooltip("How far leaves and branches are pushed, in metres.")]
        [SerializeField, Range(0f, 1f)] float _strength = 0.25f;
        [Tooltip("How fast gusts travel across the scene.")]
        [SerializeField, Range(0f, 5f)] float _speed = 1.2f;
        [Tooltip("Gust waves per metre. Low values make slow, wide gusts.")]
        [SerializeField, Range(0.01f, 2f)] float _gustFrequency = 0.35f;
        [Tooltip("0 = constant push, 1 = strong gusts with calm gaps.")]
        [SerializeField, Range(0f, 1f)] float _gustContrast = 0.7f;
        [Tooltip("Random phase offset per spot, so neighbouring trees and grass do not sway in lockstep.")]
        [SerializeField, Range(0f, 10f)] float _entropy = 2.5f;

        [Tooltip("Tint the passing gusts leave on the leaves. Alpha = strength, 0 = off.")]
        [SerializeField] Color _gustColor = new Color(0.95f, 0.83f, 0.78f, 0.15f);

        /// <summary>Wind heading in degrees around Y. 0 blows toward +Z, 90 toward +X.</summary>
        public float Direction
        {
            get => _direction;
            set
            {
                _direction = value;
                Apply();
            }
        }

        /// <summary>Displacement scale in metres.</summary>
        public float Strength
        {
            get => _strength;
            set
            {
                _strength = value;
                Apply();
            }
        }

        /// <summary>Re-sends this component's values, for tools that temporarily override the globals.</summary>
        public void Reapply() => Apply();

        private void OnEnable() => Apply();

        private void OnValidate() => Apply();

        private void OnDisable()
        {
            SetGlobals(0f, 0f, 0f, 0f, 0f);
            Shader.SetGlobalColor(GustColorId, Color.clear);
        }

        private void Apply()
        {
            SetGlobals(_direction, _strength, _speed, _gustFrequency, _gustContrast, _entropy);
            Shader.SetGlobalColor(GustColorId, _gustColor);
        }

        /// <summary>Writes the global shader values. Also used by the editor preview.</summary>
        public static void SetGlobals(float direction, float strength, float speed, float gustFrequency, float gustContrast, float entropy = 2.5f)
        {
            float rad = direction * Mathf.Deg2Rad;
            Shader.SetGlobalVector(WindDirId, new Vector4(Mathf.Sin(rad), 0f, Mathf.Cos(rad), strength));
            Shader.SetGlobalVector(WindParamsId, new Vector4(speed, gustFrequency, gustContrast, entropy));
        }

        private void OnDrawGizmosSelected()
        {
            float rad = _direction * Mathf.Deg2Rad;
            var dir = new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad));
            Gizmos.color = new Color(0.55f, 0.85f, 0.95f);
            Gizmos.DrawRay(transform.position, dir * 3f);
            Gizmos.DrawWireSphere(transform.position + dir * 3f, 0.15f);
        }
    }
}
