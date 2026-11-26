using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Overtime
{
    internal sealed class SupervisorHud : MonoBehaviour
    {
        [SerializeField] private SectorRoute _route;
        [SerializeField] private GameObject _panel;
        [SerializeField] private GameObject _roomPanel;
        [SerializeField] private Image _fill;
        [SerializeField] private TMP_Text _phase;
        [SerializeField] private TMP_Text _vitals;

        private int _previousHealth = -1;
        private SupervisorPhase _previousPhase = (SupervisorPhase)(-1);

        private void Awake()
        {
            var title = _panel.transform.Find("Name").GetComponent<TMP_Text>();
            title.fontStyle |= FontStyles.Italic;
            title.characterSpacing = 4f;
            title.fontSize = 24f;
            _phase.alignment = TextAlignmentOptions.Left;
            _phase.characterSpacing = 3f;
            _vitals.fontStyle |= FontStyles.Bold;
            foreach (Transform marker in _fill.transform.parent)
            {
                if (marker.name == "Phase Threshold")
                {
                    var divider = (RectTransform)marker;
                    divider.sizeDelta = new Vector2(4.5f, 15f);
                    divider.localRotation = Quaternion.Euler(0f, 0f, -22f);
                }
            }
        }

        private void LateUpdate()
        {
            var room = _route.Current;
            var boss = room.Boss;
            var visible = room.State == EncounterState.Active && boss != null;
            if (_panel.activeSelf != visible)
            {
                _panel.SetActive(visible);
                _roomPanel.SetActive(!visible);
                _previousHealth = -1;
                _previousPhase = (SupervisorPhase)(-1);
            }

            if (!visible)
            {
                return;
            }

            var health = boss.Health.Current;
            if (health != _previousHealth)
            {
                _previousHealth = health;
                _fill.rectTransform.anchorMax = new Vector2((float)health / boss.Health.Maximum, 1f);
                _vitals.SetText("{0:0} / {1:0}", health, boss.Health.Maximum);
            }

            if (boss.Phase != _previousPhase)
            {
                _previousPhase = boss.Phase;
                _phase.SetText(boss.Phase switch
                {
                    SupervisorPhase.SecurityAssessment => "SECURITY ASSESSMENT",
                    SupervisorPhase.CorrectiveAction => "CORRECTIVE ACTION",
                    _ => "EMERGENCY OVERRIDE"
                });
                _fill.color = boss.Phase switch
                {
                    SupervisorPhase.SecurityAssessment => new Color(0.22f, 0.84f, 0.90f),
                    SupervisorPhase.CorrectiveAction => new Color(1f, 0.58f, 0.21f),
                    _ => new Color(1f, 0.31f, 0.16f)
                };
            }
        }
    }
}
