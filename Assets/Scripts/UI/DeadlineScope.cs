using UnityEngine;
using UnityEngine.UI;

namespace Overtime
{
    internal sealed class DeadlineScope : MonoBehaviour
    {
        private static readonly int s_charge = Shader.PropertyToID("_Charge");
        private static readonly int s_extent = Shader.PropertyToID("_Extent");

        [SerializeField] private RawImage _glass;

        private Material _optics;
        private float _charge = -1f;
        private Vector2Int _resolution;
        private GameObject[] _coveredPanels;
        private bool[] _panelVisibility;
        private bool _coveringPanels;

        internal static float Blend(float aim)
        {
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.58f, 0.96f, aim));
        }

        internal static DeadlineScope Create(Transform parent)
        {
            var glass = Resources.Load<Material>("Deadline Scope");
            if (glass == null)
            {
                return null;
            }

            var root = new GameObject("Deadline Optical Sight", typeof(RectTransform), typeof(RawImage));
            var rect = (RectTransform)root.transform;
            rect.SetParent(parent, false);
            rect.SetAsFirstSibling();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var image = root.GetComponent<RawImage>();
            image.material = glass;
            image.raycastTarget = false;
            image.enabled = false;
            return root.AddComponent<DeadlineScope>();
        }

        private void Awake()
        {
            if (_glass == null)
            {
                _glass = GetComponent<RawImage>();
            }

            _optics = new Material(_glass.material);
            _glass.material = _optics;
            _glass.enabled = false;
            var hud = transform.parent;
            _coveredPanels = new[] { hud.Find("Sector Encounter")?.gameObject, hud.Find("Room Announcement")?.gameObject };
            _panelVisibility = new bool[_coveredPanels.Length];
        }

        private void OnDisable()
        {
            CoverPanels(false);
        }

        private void OnDestroy()
        {
            if (_optics != null)
            {
                Destroy(_optics);
            }
        }

        private void CoverPanels(bool covered)
        {
            if (covered == _coveringPanels || _coveredPanels == null)
            {
                return;
            }

            _coveringPanels = covered;
            for (var index = 0; index < _coveredPanels.Length; index++)
            {
                var panel = _coveredPanels[index];
                if (panel == null)
                {
                    continue;
                }

                if (covered)
                {
                    _panelVisibility[index] = panel.activeSelf;
                }

                panel.SetActive(!covered && _panelVisibility[index]);
            }
        }

        internal void Present(float blend, float charge)
        {
            CoverPanels(blend > 0.22f);
            _glass.enabled = blend > 0.001f;
            if (!_glass.enabled)
            {
                return;
            }

            _glass.color = new Color(1f, 1f, 1f, blend);
            var resolution = new Vector2Int(Screen.width, Screen.height);
            if (resolution != _resolution)
            {
                _resolution = resolution;
                var shortest = Mathf.Max(1, Mathf.Min(resolution.x, resolution.y));
                _optics.SetVector(s_extent, new Vector4((float)resolution.x / shortest, (float)resolution.y / shortest, 2f / shortest, 0f));
            }

            if (!Mathf.Approximately(_charge, charge))
            {
                _charge = charge;
                _optics.SetFloat(s_charge, charge);
            }
        }
    }
}
