using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HyperInfinite
{
    /// <summary>
    /// The screen that comes up when the player goes down: the view reddens in a few steps while it
    /// falls, then the words and the Respawn button appear. The button wakes up when PlayerRespawn says
    /// respawning is allowed. Click it, or press Enter or the gamepad's confirm button.
    /// PlayerHud makes one, shows it, hides it and ticks it.
    /// </summary>
    public class DeathScreen
    {
        const float WordsAfter = 0.45f;   // seconds after going down before the words appear

        readonly PlayerRespawn respawn;
        readonly GameObject root, words;
        readonly Image tint;
        readonly Button button;
        readonly Text hint;
        int shownStep = -1;
        bool shownReady;

        public bool Showing => root.activeSelf;

        public DeathScreen(Transform canvas, PlayerRespawn respawn, UnityAction onRespawn)
        {
            this.respawn = respawn;
            var middle = new Vector2(0.5f, 0.5f);
            RectTransform cover = UiKit.Cover("Death Screen", canvas);
            root = cover.gameObject;
            tint = root.AddComponent<Image>();
            tint.color = Color.clear;
            tint.raycastTarget = true;   // so clicks beside the button don't reach anything else

            RectTransform content = UiKit.Cover("Words", cover);
            words = content.gameObject;
            UiKit.Label("Title", content, middle, middle, new Vector2(0f, 132f), new Vector2(1200f, 96f), "YOU WENT DOWN", 64,
                        TextAnchor.MiddleCenter, Palette.Red);
            UiKit.Label("Note", content, middle, middle, new Vector2(0f, 60f), new Vector2(1200f, 36f),
                        "GET BACK UP RIGHT WHERE YOU FELL. THE FIGHT CARRIES ON.", 24, TextAnchor.MiddleCenter, Palette.White);
            button = UiKit.NewButton("Respawn Button", content, new Vector2(0f, -36f), new Vector2(360f, 78f), "RESPAWN", Palette.Cyan, onRespawn);
            hint = UiKit.Label("Hint", content, middle, middle, new Vector2(0f, -108f), new Vector2(1200f, 30f), "", 16,
                               TextAnchor.MiddleCenter, Palette.Dim);
            root.SetActive(false);
        }

        public void Show()
        {
            root.SetActive(true);
            words.SetActive(false);
            shownStep = -1;
            shownReady = false;
            button.interactable = false;
            hint.text = "THRUSTERS CHARGING...";
        }

        public void Hide()
        {
            if (Showing) UiKit.Select(null);
            root.SetActive(false);
        }

        public void Tick()
        {
            if (!Showing) return;
            // The red comes in over three steps while the view falls, then the words appear.
            float down = respawn.DownFor;
            int step = Mathf.Min(3, 1 + (int)(down / WordsAfter * 2f));
            if (step != shownStep)
            {
                shownStep = step;
                tint.color = new Color(0.22f, 0f, 0.03f, 0.2f * step);
                words.SetActive(step >= 3);
            }
            bool ready = respawn.CanRespawn && step >= 3;
            if (ready != shownReady)
            {
                shownReady = ready;
                button.interactable = ready;
                hint.text = ready ? "CLICK, OR PRESS ENTER" : "THRUSTERS CHARGING...";
            }
            // Keep the highlight on the button, so Enter and the gamepad work at once. A click on empty
            // space, or closing the pause menu, would otherwise leave nothing selected.
            EventSystem events = EventSystem.current;
            if (ready && events && !events.currentSelectedGameObject) UiKit.Select(button);
        }
    }
}
