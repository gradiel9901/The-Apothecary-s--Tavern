using UnityEngine;
using UnityEngine.SceneManagement;

namespace Script.UI
{
    public class MainMenuController : MonoBehaviour
    {
        [Header("Menu Panels")]
        [Tooltip("The main panel with New Game, Load Game, etc.")]
        public GameObject mainMenuPanel;
        
        [Tooltip("The panel containing the SaveSlotUI elements for loading.")]
        public GameObject loadMenuPanel;

        [Header("Scene Settings")]
        [Tooltip("The name of the main gameplay scene to load.")]
        public string mainSceneName = "MainScene";

        private void Start()
        {
            // Ensure the correct panels are active on start
            ShowMainMenu();
        }

        public void OnNewGameClicked()
        {
            Debug.Log("[MainMenuController] Starting New Game...");
            // Load the main gameplay scene
            SceneManager.LoadScene(mainSceneName);
        }

        public void OnLoadGameClicked()
        {
            Debug.Log("[MainMenuController] Opening Load Menu...");
            // Hide main menu, show load menu
            if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
            if (loadMenuPanel != null) loadMenuPanel.SetActive(true);
        }

        public void ShowMainMenu()
        {
            // Show main menu, hide load menu
            if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
            if (loadMenuPanel != null) loadMenuPanel.SetActive(false);
        }

        public void OnQuitClicked()
        {
            Debug.Log("[MainMenuController] Quitting Application...");
            #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
            #else
            Application.Quit();
            #endif
        }
    }
}
