using System.Collections.Generic;
using UnityEngine;
using NAV.Gameplay.Building;
using NAV.Gameplay.Crafting;

namespace NAV.Presentation.Building
{
    /// <summary>
    /// While build mode is on, draws a dashed circle on the ground around every placed
    /// workbench, at its build radius - the area where pieces that need a workbench can go
    /// (Valheim-style). One closed LineRenderer per workbench; its points are dropped onto
    /// whatever is below them (terrain, floors), so the line follows slopes instead of hanging
    /// in the air. The dashes come from a tiny generated texture repeated along the line
    /// (alpha-clipped URP Unlit). Rebuilt when workbenches appear/disappear and once a second
    /// (ground under the line can change as pieces are built). Presentation only.
    /// </summary>
    public class WorkbenchAreaIndicator : MonoBehaviour
    {
        [SerializeField] private PlayerBuilding _building;
        [SerializeField] private LayerMask _groundLayers = ~0;
        [Tooltip("Universal Render Pipeline/Unlit - referenced here so it's included in builds.")]
        [SerializeField] private Shader _unlitShader;

        [Header("Look")]
        [SerializeField] private Color _color = new(1f, 0.85f, 0.35f, 1f);
        [SerializeField, Min(0.01f)] private float _width = 0.1f;
        [Tooltip("Length of one dash plus its gap, in metres.")]
        [SerializeField, Min(0.1f)] private float _dashPeriod = 1.2f;
        [Tooltip("Height above the ground, so the line doesn't flicker into it.")]
        [SerializeField, Min(0f)] private float _groundOffset = 0.12f;
        [SerializeField, Range(16, 256)] private int _segments = 128;

        private const float RefreshInterval = 1f;
        private const float RayHeight = 40f;

        private readonly List<LineRenderer> _lines = new();
        private readonly RaycastHit[] _hits = new RaycastHit[8];
        private Material _material;
        private float _nextRefresh;
        private int _shownCount = -1;

        private void Awake()
        {
            if (_building == null)
            {
                Debug.LogError($"{nameof(WorkbenchAreaIndicator)} on '{name}' is missing its PlayerBuilding reference.", this);
                enabled = false;
                return;
            }

            _material = CreateDashMaterial();
        }

        private void OnDestroy()
        {
            if (_material != null)
            {
                Destroy(_material.mainTexture);
                Destroy(_material);
            }
        }

        private void LateUpdate()
        {
            if (!_building.IsBuildModeActive)
            {
                if (_shownCount != 0)
                {
                    SetLineCount(0);
                    _shownCount = 0;
                }

                return;
            }

            List<Workbench> benches = Workbench.AllWorkbenches;
            if (benches.Count == _shownCount && Time.time < _nextRefresh)
            {
                return;
            }

            _nextRefresh = Time.time + RefreshInterval;
            _shownCount = benches.Count;
            SetLineCount(benches.Count);
            for (int i = 0; i < benches.Count; i++)
            {
                DrawCircle(_lines[i], benches[i].transform.position, benches[i].BuildRadius);
            }
        }

        private void DrawCircle(LineRenderer line, Vector3 center, float radius)
        {
            line.positionCount = _segments;
            for (int i = 0; i < _segments; i++)
            {
                float angle = i * Mathf.PI * 2f / _segments;
                Vector3 point = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                line.SetPosition(i, OnGround(point, center.y));
            }

            // Repeat the dash texture once per dash period along the circumference.
            line.textureScale = new Vector2(1f / _dashPeriod, 1f);
        }

        private Vector3 OnGround(Vector3 point, float fallbackHeight)
        {
            int count = Physics.RaycastNonAlloc(point + Vector3.up * RayHeight, Vector3.down, _hits, RayHeight * 2f,
                _groundLayers, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            float height = fallbackHeight;
            for (int i = 0; i < count; i++)
            {
                // Ignore characters (the player, creatures) - the line belongs on the ground.
                if (_hits[i].collider is CharacterController || _hits[i].distance >= best)
                {
                    continue;
                }

                best = _hits[i].distance;
                height = _hits[i].point.y;
            }

            return new Vector3(point.x, height + _groundOffset, point.z);
        }

        private void SetLineCount(int count)
        {
            while (_lines.Count < count)
            {
                var go = new GameObject("WorkbenchArea");
                go.transform.SetParent(transform, false);
                var line = go.AddComponent<LineRenderer>();
                line.loop = true;
                line.useWorldSpace = true;
                line.widthMultiplier = _width;
                line.textureMode = LineTextureMode.Tile;
                line.alignment = LineAlignment.View;
                line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                line.receiveShadows = false;
                line.sharedMaterial = _material;
                line.startColor = line.endColor = _color;
                _lines.Add(line);
            }

            for (int i = 0; i < _lines.Count; i++)
            {
                _lines[i].gameObject.SetActive(i < count);
            }
        }

        // URP Unlit, alpha-clipped, with a 2-texel "dash | gap" texture repeated along the line.
        private Material CreateDashMaterial()
        {
            var texture = new Texture2D(2, 1, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Repeat,
                name = "WorkbenchAreaDash",
            };
            texture.SetPixels(new[] { Color.white, new Color(1f, 1f, 1f, 0f) });
            texture.Apply();

            Shader shader = _unlitShader != null ? _unlitShader : Shader.Find("Universal Render Pipeline/Unlit");
            var material = new Material(shader) { name = "WorkbenchAreaDash" };
            material.SetTexture("_BaseMap", texture);
            material.mainTexture = texture;
            material.SetColor("_BaseColor", _color);
            material.SetFloat("_AlphaClip", 1f);
            material.SetFloat("_Cutoff", 0.5f);
            material.EnableKeyword("_ALPHATEST_ON");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            return material;
        }
    }
}
