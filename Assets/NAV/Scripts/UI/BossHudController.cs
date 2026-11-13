using UnityEngine;
using UnityEngine.UIElements;
using NAV.Gameplay.Bosses;
using NAV.Gameplay.Player;
using NAV.World.Encounters;

namespace NAV.UI
{
    /// <summary>
    /// The boss part of the gameplay HUD (PlayerHud.uxml "boss-bar" and "omen-message"): the
    /// name and health of the nearest boss that is fighting within ShowDistance, red once it is
    /// enraged; and the forest's omens and the Leshy's arrival/death lines (ForestWrath.Message)
    /// faded in for MessageSeconds. A view only - BossActor/ForestWrath own the state.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class BossHudController : MonoBehaviour
    {
        [Tooltip("Optional - its messages (omens, arrival, death) are shown under the bar.")]
        [SerializeField] private ForestWrath _forestWrath;
        [Tooltip("Found by its PlayerMotor when empty.")]
        [SerializeField] private Transform _player;
        [SerializeField, Min(5f)] private float _showDistance = 70f;
        [SerializeField, Min(1f)] private float _messageSeconds = 5f;

        private VisualElement _bar;
        private Label _name;
        private VisualElement _fill;
        private Label _message;
        private float _messageUntil;
        private BossActor _shown;
        private bool _visible;

        private void OnEnable()
        {
            VisualElement root = GetComponent<UIDocument>().rootVisualElement;
            _bar = root.Q<VisualElement>("boss-bar");
            _name = root.Q<Label>("boss-bar-name");
            _fill = root.Q<VisualElement>("boss-bar-fill");
            _message = root.Q<Label>("omen-message");
            if (_bar == null || _name == null || _fill == null || _message == null)
            {
                Debug.LogError($"{nameof(BossHudController)} on '{name}': PlayerHud.uxml has no boss-bar / omen-message elements.", this);
                enabled = false;
                return;
            }

            if (_forestWrath != null)
            {
                _forestWrath.Message += ShowMessage;
            }
        }

        private void OnDisable()
        {
            if (_forestWrath != null)
            {
                _forestWrath.Message -= ShowMessage;
            }
        }

        private void ShowMessage(string text)
        {
            _message.text = NAV.Core.Localization.Loc.T(text);
            _message.AddToClassList("omen-message--shown");
            _messageUntil = Time.time + _messageSeconds;
        }

        private void Update()
        {
            if (_message.ClassListContains("omen-message--shown") && Time.time >= _messageUntil)
            {
                _message.RemoveFromClassList("omen-message--shown");
            }

            if (_player == null)
            {
                PlayerMotor motor = FindAnyObjectByType<PlayerMotor>();
                if (motor == null)
                {
                    return;
                }

                _player = motor.transform;
            }

            BossActor boss = BossActor.NearestFighting(_player.position, _showDistance);
            if (boss == null)
            {
                // A flag, not "_shown != null": a boss that was removed (went back into the
                // forest) is a destroyed object, which Unity reports as null - the bar stayed up.
                if (_visible)
                {
                    _bar.AddToClassList("boss-bar--hidden");
                    _visible = false;
                    _shown = null;
                }

                return;
            }

            if (!_visible || !ReferenceEquals(boss, _shown))
            {
                _shown = boss;
                _visible = true;
                _name.text = NAV.Core.Localization.Loc.T(boss.Title);
                _bar.RemoveFromClassList("boss-bar--hidden");
            }

            float fraction = boss.Creature.MaxHealth > 0f ? Mathf.Clamp01(boss.Creature.CurrentHealth / boss.Creature.MaxHealth) : 0f;
            _fill.style.width = Length.Percent(fraction * 100f);
            _fill.EnableInClassList("boss-bar__fill--enraged", boss.IsEnraged);
        }
    }
}
