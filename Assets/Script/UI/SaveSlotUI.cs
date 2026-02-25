using System.IO;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Script.UI
{
    public class SaveSlotUI : MonoBehaviour
    {
        [Header("Slot Configuration")]
        [Tooltip("Which slot is this? (1, 2, or 3)")]
        public int slotId = 1;

        [Header("UI References")]
        public RawImage screenshotDisplay;
        public TMP_Text saveDateText;
        public TMP_Text gloryInfoText;
        public TMP_Text dayInfoText;

        [Header("Buttons")]
        [Tooltip("The parent button for the slot, or a specific Save/Load button")]
        public Button slotButton;

        private void OnEnable()
        {
            RefreshSlotInfo();
        }

        public void RefreshSlotInfo()
        {
            if (Systems.SaveManager.Instance == null) return;

            bool hasSave = Systems.SaveManager.Instance.DoesSaveExist(slotId);

            if (hasSave)
            {
                // Load Screenshot
                Texture2D tex = Systems.SaveManager.Instance.LoadScreenshot(slotId);
                if (tex != null && screenshotDisplay != null)
                {
                    screenshotDisplay.texture = tex;
                    screenshotDisplay.color = Color.white; // Ensure it's not clear or greyed out
                }

                // Load Metadata for text
                Systems.GameSaveData data = Systems.SaveManager.Instance.GetSaveMetadata(slotId);
                if (data != null)
                {
                    if (saveDateText != null) saveDateText.text = data.saveDate;
                    if (gloryInfoText != null) gloryInfoText.text = $"Glory: {data.glory} (x{data.currentMultiplier})";
                    if (dayInfoText != null) dayInfoText.text = $"Day {data.currentDay}";
                }
            }
            else
            {
                // Slot is empty
                if (screenshotDisplay != null)
                {
                    screenshotDisplay.texture = null;
                    screenshotDisplay.color = new Color(0.2f, 0.2f, 0.2f, 1f); // Darken to show empty
                }

                if (saveDateText != null) saveDateText.text = "Empty Slot";
                if (gloryInfoText != null) gloryInfoText.text = "";
                if (dayInfoText != null) dayInfoText.text = "";
            }
        }

        // Hook these up to your UI Buttons!
        
        public void OnSaveClicked()
        {
            if (Systems.SaveManager.Instance != null)
            {
                Systems.SaveManager.Instance.SaveGame(slotId);
                
                // Refresh visuals after a tiny delay so the screenshot can finish saving
                Invoke(nameof(RefreshSlotInfo), 0.5f);
            }
        }

        public void OnLoadClicked()
        {
            if (Systems.SaveManager.Instance != null)
            {
                Systems.SaveManager.Instance.LoadGame(slotId);
            }
        }
    }
}
