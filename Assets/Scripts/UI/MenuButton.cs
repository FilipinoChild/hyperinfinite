using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HyperInfinite
{
    /// <summary>
    /// Makes a menu button behave the way the title art shows it: there is one highlight, and it sits on
    /// one button at a time. Pointing at the button with the mouse selects it, exactly as the arrow keys
    /// or a gamepad would, so two buttons are never lit at once. While selected it can show a marker
    /// (the ">" beside the label) and brighten its label.
    ///
    /// Put it on the same object as the Button. UiKit.NewButton does that for buttons made in code, and
    /// TitleMenu adds it to the title screen's buttons. UiKit.NewSlider puts it on sliders too.
    /// </summary>
    [DisallowMultipleComponent]
    public class MenuButton : MonoBehaviour, IPointerEnterHandler, ISelectHandler, IDeselectHandler, IPointerClickHandler, ISubmitHandler
    {
        [Tooltip("Shown only while this button is selected. Optional.")]
        [SerializeField] Graphic marker;
        [Tooltip("Brightened while this button is selected. Optional.")]
        [SerializeField] Graphic label;
        [SerializeField] Color labelColor = new Color(0.667f, 0.647f, 0.643f);
        [SerializeField] Color selectedLabelColor = Color.white;

        Selectable selectable;
        float shownAt;

        /// <summary>Whether a click or Enter makes the select sound. Off for sliders, which tick as they move.</summary>
        public bool PressSound { get; set; } = true;

        /// <summary>For buttons made in code: what to show and brighten while selected.</summary>
        public void SetUp(Graphic marker, Graphic label, Color labelColor, Color selectedLabelColor)
        {
            this.marker = marker;
            this.label = label;
            this.labelColor = labelColor;
            this.selectedLabelColor = selectedLabelColor;
            Show(false);
        }

        void Awake()
        {
            selectable = GetComponent<Selectable>();
        }

        void OnEnable()
        {
            shownAt = Time.unscaledTime;
            EventSystem current = EventSystem.current;
            Show(current && current.currentSelectedGameObject == gameObject);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (selectable && !selectable.IsInteractable()) return;
            if (EventSystem.current) EventSystem.current.SetSelectedGameObject(gameObject);
        }

        public void OnSelect(BaseEventData eventData)
        {
            Show(true);
            // A blip as the highlight moves, but not for the button a menu starts on as it opens.
            if (Time.unscaledTime - shownAt > 0.1f) Sfx.Play(Sound.UiMove);
        }

        public void OnPointerClick(PointerEventData eventData) => Pressed();
        public void OnSubmit(BaseEventData eventData) => Pressed();

        void Pressed()
        {
            if (!PressSound || (selectable && !selectable.IsInteractable())) return;
            Sfx.Play(Sound.UiSelect);
        }

        public void OnDeselect(BaseEventData eventData)
        {
            Show(false);
        }

        void Show(bool selected)
        {
            if (marker) marker.enabled = selected;
            if (label) label.color = selected ? selectedLabelColor : labelColor;
        }
    }
}
