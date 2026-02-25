using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Script.Systems;

namespace Script.Environment
{
    public enum GameEvent
    {
        None,
        WhiteCalamity,
        NinthSun,
        WeepingHeavens
    }

    public class EventManager : MonoBehaviour
    {
        public static EventManager Instance { get; private set; }

        [Header("Event Logic Settings")]
        [Tooltip("How many days should pass before a new event rolls?")]
        [SerializeField] private int daysBetweenEvents = 5;
        [Tooltip("How many days does an event last once started?")]
        [SerializeField] private int eventDurationDays = 3;
        [Tooltip("How often (in real seconds) to rot items on shelves during Working Hours?")]
        [SerializeField] private float rotCheckInterval = 10f;

        [Header("Event Database (Names & Descriptions)")]
        [TextArea(2, 4)] public string descWhiteCalamity = "A merciless winter descends without warning. Snow devours the roads, frost grips the harvest, and the world slows beneath a shroud of white.";
        [TextArea(2, 4)] public string effectsWhiteCalamity = "Reduced NPC Spawn Rate\nCold-Sensitive Ingredients Spoil\nPlayer Movement Decreases";
        public Sprite imgWhiteCalamity;

        [TextArea(2, 4)] public string descNinthSun = "The heavens blaze with relentless fury. Rivers shrink, soil cracks, and the air itself burns against the skin. Fever and exhaustion spread through the land.";
        [TextArea(2, 4)] public string effectsNinthSun = "Increased NPC Spawn Rate\nDragon Heart Price Skyrockets";
        public Sprite imgNinthSun;

        [TextArea(2, 4)] public string descWeepingHeavens = "Thunder rolls like a royal decree from the skies above. Rain falls in unending sheets, swallowing roads and flooding homes.";
        [TextArea(2, 4)] public string effectsWeepingHeavens = "NPC Flood (Burst Spawns)\nMandrake Rot";
        public Sprite imgWeepingHeavens;

        [Header("UI Reference (Attached Panel)")]
        public GameObject eventUIPanel;
        public TMP_Text eventTitleText;
        public TMP_Text eventDescriptionText;
        public Image eventImageUI;

        // Current Stat Tracking
        public GameEvent CurrentEvent { get; private set; } = GameEvent.None;
        private int _daysSinceLastEvent = 0;
        private int _eventDaysRemaining = 0;
        
        // Rot tracking
        private float _rotTimer = 0f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (eventUIPanel != null)
                eventUIPanel.SetActive(false);
        }

        private void Update()
        {
            // Only rot items while the shop is open and time is actively ticking
            if (DayCycleManager.Instance != null && DayCycleManager.Instance.IsWorkingHours)
            {
                if (CurrentEvent == GameEvent.WhiteCalamity || CurrentEvent == GameEvent.WeepingHeavens)
                {
                    _rotTimer += Time.deltaTime;
                    if (_rotTimer >= rotCheckInterval)
                    {
                        _rotTimer = 0f;
                        PerformRotTick();
                    }
                }
            }
        }

        /// <summary>
        /// Called mechanically by DayCycleManager.EndDay() to check if we change states
        /// </summary>
        public void EndDayTick()
        {
            if (CurrentEvent != GameEvent.None)
            {
                // We are inside an event. Tick it down.
                _eventDaysRemaining--;
                Debug.Log($"[EventManager] Event {CurrentEvent} has {_eventDaysRemaining} days left.");

                if (_eventDaysRemaining <= 0)
                {
                    Debug.Log($"[EventManager] Event {CurrentEvent} has ENDED!");
                    CurrentEvent = GameEvent.None;
                    _daysSinceLastEvent = 0; // reset counter towards next
                }
            }
            else
            {
                // No event right now. Tick up towards the next one.
                _daysSinceLastEvent++;
                Debug.Log($"[EventManager] {_daysSinceLastEvent} days since last event.");

                if (_daysSinceLastEvent >= daysBetweenEvents)
                {
                    TriggerRandomEvent();
                }
            }
        }

        private void TriggerRandomEvent()
        {
            // Pick a random event that isn't None
            CurrentEvent = (GameEvent)Random.Range(1, System.Enum.GetValues(typeof(GameEvent)).Length);
            _eventDaysRemaining = eventDurationDays;
            
            Debug.Log($"[EventManager] A new event has started: {CurrentEvent}!");

            UpdateEventUI();
        }

        private void UpdateEventUI()
        {
            if (eventUIPanel != null)
            {
                eventUIPanel.SetActive(true);
                
                // Unlock cursor so they can hit CLOSE
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;

                switch (CurrentEvent)
                {
                    case GameEvent.WhiteCalamity:
                        if (eventTitleText != null) eventTitleText.text = "The White Calamity";
                        if (eventDescriptionText != null) eventDescriptionText.text = descWhiteCalamity + "\n\n" + effectsWhiteCalamity;
                        if (eventImageUI != null && imgWhiteCalamity != null) eventImageUI.sprite = imgWhiteCalamity;
                        break;
                    case GameEvent.NinthSun:
                        if (eventTitleText != null) eventTitleText.text = "The Ninth Sun";
                        if (eventDescriptionText != null) eventDescriptionText.text = descNinthSun + "\n\n" + effectsNinthSun;
                        if (eventImageUI != null && imgNinthSun != null) eventImageUI.sprite = imgNinthSun;
                        break;
                    case GameEvent.WeepingHeavens:
                        if (eventTitleText != null) eventTitleText.text = "The Weeping Heavens";
                        if (eventDescriptionText != null) eventDescriptionText.text = descWeepingHeavens + "\n\n" + effectsWeepingHeavens;
                        if (eventImageUI != null && imgWeepingHeavens != null) eventImageUI.sprite = imgWeepingHeavens;
                        break;
                }
            }
        }

        public void UI_CloseEventPanel()
        {
            if (eventUIPanel != null)
                eventUIPanel.SetActive(false);
                
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        // ===================================
        // ROT LOGIC
        // ===================================
        private void PerformRotTick()
        {
            ItemType targetRotType = ItemType.Mushroom; // Default
            if (CurrentEvent == GameEvent.WhiteCalamity) targetRotType = ItemType.Mushroom;
            else if (CurrentEvent == GameEvent.WeepingHeavens) targetRotType = ItemType.Mandrake;
            else return; // Saftey catch

            // Find all shelves in the scene
            Shelf[] shelves = FindObjectsByType<Shelf>(FindObjectsSortMode.None);
            foreach(Shelf s in shelves)
            {
                if (s.GetItemType() == targetRotType)
                {
                    if (s.GetCurrentItems() > 0)
                    {
                        Debug.Log($"[EventManager] Rot destroyed 1 {targetRotType} from a Shelf!");
                        s.RemoveItems(1);
                    }
                }
            }
        }

        // ===================================
        // SYSTEM HOOKS (API)
        // ===================================

        // Used by PlayerMovement
        public float GetPlayerSpeedMultiplier()
        {
            if (CurrentEvent == GameEvent.WhiteCalamity) return 0.9f; // -10% speed
            return 1.0f;
        }

        // Used by NPCSpawner for interval math
        public float GetSpawnIntervalMultiplier()
        {
            if (CurrentEvent == GameEvent.WhiteCalamity) return 1.4f; // Slower spawn (+40% time)
            if (CurrentEvent == GameEvent.NinthSun) return 0.75f; // Faster spawn (-25% time)
            return 1.0f;
        }

        // Used by NPCSpawner for how many drop per spawn tick
        public int GetBurstSpawnAmount()
        {
            if (CurrentEvent == GameEvent.WeepingHeavens) return Random.Range(2, 4); // 2 to 3
            return 1; // Default
        }

        // Used by ShopManager / ShopUI
        public bool IsItemBuyable(ItemType itemType)
        {
            if (CurrentEvent == GameEvent.WhiteCalamity && itemType == ItemType.Mushroom) return false;
            if (CurrentEvent == GameEvent.WeepingHeavens && itemType == ItemType.Mandrake) return false;
            return true; 
        }

        // Used by ShopManager / ShopUI
        public int GetItemPrice(ItemType itemType, int baseCost)
        {
            if (CurrentEvent == GameEvent.NinthSun && itemType == ItemType.DragonHeart)
            {
                return baseCost * 2;
            }
            return baseCost;
        }

        // ==========================================
        // DATA ACCESS & SAVING
        // ==========================================

        public int GetDaysSinceLastEvent() => _daysSinceLastEvent;
        public int GetEventDaysRemaining() => _eventDaysRemaining;

        public void LoadFromSave(GameSaveData data)
        {
            CurrentEvent = data.currentEvent;
            _daysSinceLastEvent = data.daysSinceLastEvent;
            _eventDaysRemaining = data.eventDaysRemaining;

            // Optional: If an event is active on load, update the UI or ensure logic state
            if (CurrentEvent != GameEvent.None)
            {
                // We shouldn't show the UI popup again if they just loaded, 
                // but the active event variables are safely stored!
                Debug.Log($"[EventManager] Loaded Active Event: {CurrentEvent} with {_eventDaysRemaining} days remaining.");
            }
        }
    }
}
