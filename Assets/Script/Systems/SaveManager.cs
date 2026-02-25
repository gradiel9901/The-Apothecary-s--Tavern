using System.Collections;
using System.IO;
using UnityEngine;
using Script.Environment;
using Script.Player;

namespace Script.Systems
{
    public class SaveManager : MonoBehaviour
    {
        public static SaveManager Instance { get; private set; }

        private const string SAVE_FILE_NAME = "saveSlot_";
        private const string SCREENSHOT_NAME = "screenshot_";

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        // ==========================================
        // SAVING
        // ==========================================
        
        public void SaveGame(int slotId)
        {
            StartCoroutine(SaveGameRoutine(slotId));
        }

        private IEnumerator SaveGameRoutine(int slotId)
        {
            // Wait for end of frame to ensure UI changes (like hiding save menus) are rendered before screenshot
            yield return new WaitForEndOfFrame();

            // 1. Capture and Save Screenshot
            CaptureScreenshot(slotId);

            // 2. Gather Data from all Managers
            GameSaveData data = new GameSaveData();
            data.saveDate = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm");

            if (ShopManager.Instance != null)
            {
                data.currency = ShopManager.Instance.Currency;
                data.glory = ShopManager.Instance.CurrentGlory;
                data.currentMultiplier = ShopManager.Instance.CurrentMultiplier;
            }

            if (DayCycleManager.Instance != null)
            {
                data.currentDay = DayCycleManager.Instance.CurrentDay;
                data.timeOfDay = DayCycleManager.Instance.GetTimeOfDay();
                data.isWorkingHours = DayCycleManager.Instance.IsWorkingHours;
                data.workingTimeRemaining = DayCycleManager.Instance.GetWorkingTimeRemaining();
            }

            if (EventManager.Instance != null)
            {
                data.currentEvent = EventManager.Instance.CurrentEvent;
                data.daysSinceLastEvent = EventManager.Instance.GetDaysSinceLastEvent();
                data.eventDaysRemaining = EventManager.Instance.GetEventDaysRemaining();
            }

            PlayerMovement player = FindFirstObjectByType<PlayerMovement>();
            if (player != null)
            {
                data.SavePlayerPosition(player.transform.position, player.transform.rotation);
            }

            // 3. Serialize and Write to Disk
            string json = JsonUtility.ToJson(data, true);
            string savePath = GetSaveFilePath(slotId);

            File.WriteAllText(savePath, json);
            Debug.Log($"[SaveManager] Game Saved to Slot {slotId} at {savePath}");
        }

        private void CaptureScreenshot(int slotId)
        {
            int width = Screen.width;
            int height = Screen.height;
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGB24, false);

            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();

            byte[] bytes = tex.EncodeToPNG();
            Destroy(tex);

            string screenshotPath = GetScreenshotFilePath(slotId);
            File.WriteAllBytes(screenshotPath, bytes);
            Debug.Log($"[SaveManager] Screenshot Saved to Slot {slotId} at {screenshotPath}");
        }

        // ==========================================
        // LOADING
        // ==========================================

        public void LoadGame(int slotId)
        {
            string savePath = GetSaveFilePath(slotId);
            if (!File.Exists(savePath))
            {
                Debug.LogWarning($"[SaveManager] No save file found for Slot {slotId}");
                return;
            }

            string json = File.ReadAllText(savePath);
            GameSaveData data = JsonUtility.FromJson<GameSaveData>(json);

            Debug.Log($"[SaveManager] Loading Game from Slot {slotId}...");

            if (ShopManager.Instance != null) ShopManager.Instance.LoadFromSave(data);
            if (DayCycleManager.Instance != null) DayCycleManager.Instance.LoadFromSave(data);
            if (EventManager.Instance != null) EventManager.Instance.LoadFromSave(data);

            PlayerMovement player = FindFirstObjectByType<PlayerMovement>();
            if (player != null)
            {
                // Force CharacterController to teleport
                CharacterController cc = player.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;
                
                player.transform.position = data.GetPlayerPosition();
                player.transform.rotation = data.GetPlayerRotation();
                
                if (cc != null) cc.enabled = true;
            }

            Debug.Log($"[SaveManager] Load Complete!");
        }

        // ==========================================
        // UTILITIES
        // ==========================================

        public bool DoesSaveExist(int slotId)
        {
            return File.Exists(GetSaveFilePath(slotId));
        }

        public string GetSaveFilePath(int slotId)
        {
            return Path.Combine(Application.persistentDataPath, $"{SAVE_FILE_NAME}{slotId}.json");
        }

        public string GetScreenshotFilePath(int slotId)
        {
            return Path.Combine(Application.persistentDataPath, $"{SCREENSHOT_NAME}{slotId}.png");
        }

        public Texture2D LoadScreenshot(int slotId)
        {
            string path = GetScreenshotFilePath(slotId);
            if (!File.Exists(path)) return null;

            byte[] fileData = File.ReadAllBytes(path);
            Texture2D tex = new Texture2D(2, 2);
            tex.LoadImage(fileData); // This automatically resizes the texture to match the PNG
            return tex;
        }
        
        public GameSaveData GetSaveMetadata(int slotId)
        {
            string savePath = GetSaveFilePath(slotId);
            if (!File.Exists(savePath)) return null;
            
            string json = File.ReadAllText(savePath);
            return JsonUtility.FromJson<GameSaveData>(json); // Will be partially loaded just to read the date and glory text
        }
    }
}
