using Script.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Script.UI
{
    [DefaultExecutionOrder(100)]
    public class PauseManager : MonoBehaviour
    {
        [Header("UI References")]
        [Tooltip("Drag the Pause Menu Canvas/Panel here.")]
        public GameObject pauseMenuPanel;
        [Tooltip("Drag the Save Menu Canvas/Panel here.")]
        public GameObject saveMenuPanel;
        [Tooltip("Drag the Load Menu Canvas/Panel here.")]
        public GameObject loadMenuPanel;
        [Tooltip("Drag the Settings Menu Canvas/Panel here.")]
        public GameObject settingsMenuPanel;

        [Header("Player Reference")]
        [Tooltip("Needed to unlock cursor and stop movement. Will be found automatically if left blank.")]
        public PlayerMovement playerMovement;

        private bool _isPaused = false;

        private void Start()
        {
            HideAllMenus();

            if (playerMovement == null)
            {
                playerMovement = FindFirstObjectByType<PlayerMovement>();
            }
        }

        private void Update()
        {
            // Check for Escape key press
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                // To prevent the Pause Menu from opening when the player is closing 
                // another UI (like the Shop or Event panel), we must check if any 
                // other script just handled the Escape key this frame.
                if (ConsumedEscapeThisFrame)
                {
                    return; // Skip pausing, another UI just closed
                }

                TogglePause();
            }

            // Reset the flag manually at the end of the frame in LateUpdate or wait
            if (ConsumedEscapeThisFrame && Keyboard.current != null && !Keyboard.current.escapeKey.isPressed)
            {
               ConsumedEscapeThisFrame = false;
            }
        }

        // A static flag that other scripts can set to true when they close via ESC
        public static bool ConsumedEscapeThisFrame = false;

        private void LateUpdate()
        {
            if (ConsumedEscapeThisFrame && Keyboard.current != null && !Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                ConsumedEscapeThisFrame = false;
            }
        }

        public void TogglePause()
        {
            _isPaused = !_isPaused;

            if (_isPaused)
            {
                PauseGame();
            }
            else
            {
                ResumeGame();
            }
        }

        private void PauseGame()
        {
            // Freeze everything that relies on Time.deltaTime
            Time.timeScale = 0f;

            // Show UI
            if (pauseMenuPanel != null)
            {
                pauseMenuPanel.SetActive(true);
            }

            // Halt player input and unlock cursor for UI interaction
            if (playerMovement != null)
            {
                playerMovement.TogglePlayerInput(false);
            }

            Debug.Log("[PauseManager] Game Paused");
        }

        public void ResumeGame()
        {
            // Unfreeze time
            Time.timeScale = 1f;

            // Hide UI
            if (pauseMenuPanel != null)
            {
                pauseMenuPanel.SetActive(false);
            }

            // Restore player input and lock cursor
            if (playerMovement != null)
            {
                playerMovement.TogglePlayerInput(true);
            }

            // Update state just in case this was called from a UI Button instead of ESC
            _isPaused = false;

            // Ensure no sub-menus are left open
            HideAllMenus();

            Debug.Log("[PauseManager] Game Resumed");
        }

        // ==========================================
        // UI NAVIGATION HOOKS
        // ==========================================

        private void HideAllMenus()
        {
            if (pauseMenuPanel != null) pauseMenuPanel.SetActive(false);
            if (saveMenuPanel != null) saveMenuPanel.SetActive(false);
            if (loadMenuPanel != null) loadMenuPanel.SetActive(false);
            if (settingsMenuPanel != null) settingsMenuPanel.SetActive(false);
        }

        public void ShowPauseMenu()
        {
            HideAllMenus();
            if (pauseMenuPanel != null) pauseMenuPanel.SetActive(true);
        }

        public void ShowSaveMenu()
        {
            HideAllMenus();
            if (saveMenuPanel != null) saveMenuPanel.SetActive(true);
        }

        public void ShowLoadMenu()
        {
            HideAllMenus();
            if (loadMenuPanel != null) loadMenuPanel.SetActive(true);
        }

        public void ShowSettingsMenu()
        {
            HideAllMenus();
            if (settingsMenuPanel != null) settingsMenuPanel.SetActive(true);
        }

        public void QuitToMainMenu()
        {
            // Restore time scale before leaving the scene
            Time.timeScale = 1f;
            Debug.Log("[PauseManager] Quitting to Main Menu...");
            SceneManager.LoadScene("MainMenu");
        }
    }
}
