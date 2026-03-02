using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using Script.Environment;

namespace Script.Systems
{
    /// <summary>
    /// Drives a 10-step first-time tutorial using the existing Quest Title
    /// and Quest Description TMP fields. Steps auto-advance as the player
    /// completes each action. Permanently skipped after first completion.
    ///
    /// Attach to any persistent GameObject in the main scene (e.g. Managers).
    /// Steps 0-3 are detected via input polling.
    /// Steps 4-9 require other scripts to call TutorialManager.NotifyStep().
    /// </summary>
    public class TutorialManager : MonoBehaviour
    {
        public static TutorialManager Instance { get; private set; }

        // ─── Tutorial Event Types ──────────────────────────────────────────
        public enum TutorialEvent
        {
            ItemBought,       // AlchemistShopUI.BuyItem()
            ShelfFilled,      // Shelf.InteractSack()
            NPCOrderAccepted, // NPC.OnAcceptOrder()
            CauldronStarted,  // Cauldron.Interact()
            CauldronLightHit  // Cauldron interact while light is ON
        }

        // ─── Inspector Settings ────────────────────────────────────────────
        [Header("UI References (Drag & Drop here)")]
        [SerializeField] private TMP_Text questTitleText;
        [SerializeField] private TMP_Text questDescText;

        [Header("Settings")]
        [Tooltip("Total mouse movement (axis units) needed to complete step 0.")]
        [SerializeField] private float mouseLookThreshold   = 3f;

        // ─── Private State ─────────────────────────────────────────────────
        private int       _currentStep  = 0;
        private bool      _tutorialActive = false;
        private float     _totalMouseMove = 0f;

        // ─── Step Data ─────────────────────────────────────────────────────
        private static readonly string[] StepTitles = new string[]
        {
            "Camera Controls",
            "Movement",
            "Sprinting",
            "Interaction",
            "Stock Up",
            "Fill the Shelves",
            "Open for Business",
            "Serve a Customer",
            "Brew Time",
            "Mixing Minigame"
        };

        private static readonly string[] StepDescs = new string[]
        {
            "Move your <b>Mouse</b> to look around the tavern.",
            "Use <b>WASD</b> to walk around.",
            "Hold <b>Shift</b> while moving to Sprint.",
            "Press <b>E</b> to interact with objects.",
            "Open the <b>Alchemist Shop</b> at the table and buy <b>5 Sacks of Mushroom</b>.",
            "Pick up a <b>Sack</b> and bring it to the <b>Mushroom Shelves</b> to restock them.",
            "Open the <b>front door</b> to welcome your first customers!",
            "Walk up to an <b>NPC</b> and press <b>E</b> to hear their order.",
            "Bring the required ingredients to the <b>Cauldron</b> and press <b>E</b> to start mixing.",
            "Press <b>F</b> each time the <b>Cauldron glows</b> to successfully brew the potion!"
        };

        // ─── Unity Lifecycle ───────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            PlayerPrefs.DeleteKey("TutorialComplete"); // FORCE RESET FOR TESTING

            // Skip if already completed
            if (PlayerPrefs.GetInt("TutorialComplete", 0) == 1)
            {
                HideUI();
                return;
            }

            if (questTitleText == null || questDescText == null)
            {
                Debug.LogWarning("[Tutorial] UI Texts not assigned in the inspector!");
            }
            else
            {
                EnableParentChain(questTitleText);
                EnableParentChain(questDescText);
            }

            _tutorialActive = true;
            ShowStep(_currentStep);
        }

        /// <summary>
        /// Walks up the transform hierarchy and enables every inactive parent.
        /// This ensures nested TMPs (e.g. NPC Canvas, Dialogue Panel) become visible.
        /// </summary>
        private void EnableParentChain(Component target)
        {
            if (target == null) return;
            Transform current = target.transform;
            while (current != null)
            {
                if (!current.gameObject.activeSelf)
                    current.gameObject.SetActive(true);
                current = current.parent;
            }
        }

        private void OnEnable()
        {
            DoorAnimation.OnDoorOpened += HandleDoorOpened;
        }

        private void OnDisable()
        {
            DoorAnimation.OnDoorOpened -= HandleDoorOpened;
        }

        private void Update()
        {
            if (!_tutorialActive) return;

            switch (_currentStep)
            {
                case 0: // Mouse Look
                    if (Mouse.current != null)
                    {
                        Vector2 delta = Mouse.current.delta.ReadValue();
                        _totalMouseMove += Mathf.Abs(delta.x) + Mathf.Abs(delta.y);
                        if (_totalMouseMove >= mouseLookThreshold * 100f)
                            AdvanceStep();
                    }
                    break;

                case 1: // WASD movement
                    if (Keyboard.current != null &&
                       (Keyboard.current.wKey.isPressed || Keyboard.current.aKey.isPressed ||
                        Keyboard.current.sKey.isPressed || Keyboard.current.dKey.isPressed ||
                        Keyboard.current.upArrowKey.isPressed || Keyboard.current.downArrowKey.isPressed ||
                        Keyboard.current.leftArrowKey.isPressed || Keyboard.current.rightArrowKey.isPressed))
                    {
                        AdvanceStep();
                    }
                    break;

                case 2: // Sprint (Shift + movement)
                    if (Keyboard.current != null && Keyboard.current.shiftKey.isPressed &&
                       (Keyboard.current.wKey.isPressed || Keyboard.current.aKey.isPressed ||
                        Keyboard.current.sKey.isPressed || Keyboard.current.dKey.isPressed ||
                        Keyboard.current.upArrowKey.isPressed || Keyboard.current.downArrowKey.isPressed ||
                        Keyboard.current.leftArrowKey.isPressed || Keyboard.current.rightArrowKey.isPressed))
                    {
                        AdvanceStep();
                    }
                    break;

                case 3: // Interact key
                    if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
                    {
                        AdvanceStep();
                    }
                    break;
            }
        }

        // ─── Static Notification (called by other scripts) ─────────────────

        /// <summary>
        /// Call this from any relevant gameplay script to advance the tutorial.
        /// Silently ignored if the tutorial is inactive or on a different step.
        /// </summary>
        public static void NotifyStep(TutorialEvent evt)
        {
            if (Instance == null || !Instance._tutorialActive) return;

            int required = EventToStep(evt);
            if (Instance._currentStep == required)
                Instance.AdvanceStep();
        }

        private static int EventToStep(TutorialEvent evt)
        {
            switch (evt)
            {
                case TutorialEvent.ItemBought:       return 4;
                case TutorialEvent.ShelfFilled:      return 5;
                case TutorialEvent.NPCOrderAccepted: return 7;
                case TutorialEvent.CauldronStarted:  return 8;
                case TutorialEvent.CauldronLightHit: return 9;
                default:                             return -1;
            }
        }

        // ─── Event Handlers ────────────────────────────────────────────────

        private void HandleDoorOpened()
        {
            if (_tutorialActive && _currentStep == 6)
                AdvanceStep();
        }

        // ─── Step Control ──────────────────────────────────────────────────

        private void AdvanceStep()
        {
            _currentStep++;

            if (_currentStep >= StepTitles.Length)
            {
                CompleteTutorial();
                return;
            }

            ShowStep(_currentStep);
        }

        private void ShowStep(int index)
        {
            if (index < 0 || index >= StepTitles.Length) return;

            if (questTitleText != null)
            {
                questTitleText.gameObject.SetActive(true);
                questTitleText.text = "Tutorial: " + StepTitles[index];
            }

            if (questDescText != null)
            {
                questDescText.gameObject.SetActive(true);
                questDescText.text = StepDescs[index];
            }
        }

        private void CompleteTutorial()
        {
            _tutorialActive = false;
            PlayerPrefs.SetInt("TutorialComplete", 1);
            PlayerPrefs.Save();
            StartCoroutine(HideUIWithDelay());
        }

        private IEnumerator HideUIWithDelay()
        {
            if (questTitleText != null) questTitleText.text = "Tutorial Complete!";
            if (questDescText  != null) questDescText.text  = "You are ready to run the tavern. Good luck!";
            yield return new WaitForSeconds(3f);
            HideUI();
        }

        private void HideUI()
        {
            if (questTitleText != null) questTitleText.gameObject.SetActive(false);
            if (questDescText  != null) questDescText.gameObject.SetActive(false);
        }

        // ─── Editor Utility ────────────────────────────────────────────────

        /// <summary>Resets tutorial completion for testing in the Editor.</summary>
        [ContextMenu("Reset Tutorial")]
        public void ResetTutorial()
        {
            PlayerPrefs.DeleteKey("TutorialComplete");
            PlayerPrefs.Save();
            Debug.Log("[TutorialManager] Tutorial reset. Re-enter Play Mode to restart.");
        }
    }
}
