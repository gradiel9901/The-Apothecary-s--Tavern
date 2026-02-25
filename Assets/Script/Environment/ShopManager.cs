using System;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Script.Systems;

namespace Script.Environment
{
    public class ShopManager : MonoBehaviour
    {
        public static ShopManager Instance { get; private set; }

        [Header("Economy Settings")]
        [SerializeField] private int startingCurrency = 0;
        [SerializeField] private int maxMultiplier = 8;
        
        [Header("Glory Scaling")]
        [Tooltip("Base glory required to reach 2x multiplier.")]
        [SerializeField] private float baseGloryRequired = 100f;
        [Tooltip("How much the required glory multiplies for each subsequent level (e.g., 1.5x harder each time).")]
        [SerializeField] private float gloryRequirementScale = 1.5f;

        [Header("UI References (Optional)")]
        [SerializeField] private TMP_Text currencyText;
        [SerializeField] private TMP_Text multiplierText;
        [SerializeField] private Slider glorySlider;

        [Header("Glory Colors")]
        [Tooltip("Colors for the slider fill, one for each multiplier level (1x to 8x). If not set, color won't change.")]
        [SerializeField] private Color[] multiplierColors;
        [Tooltip("Optional: Explicit reference to the slider's fill image. If empty, it will auto-find the fill image from the Slider.")]
        [SerializeField] private Image gloryFillImage;

        // Current State
        public int Currency { get; private set; }
        public int CurrentMultiplier { get; private set; } = 1;
        public float CurrentGlory { get; private set; } = 0f;

        private float _gloryRequiredForNextLevel;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            
            Currency = startingCurrency;
            CalculateNextLevelRequirement();
            UpdateUI();
        }

        private void CalculateNextLevelRequirement()
        {
            // E.g., Level 1 -> 2 requires 100
            // Level 2 -> 3 requires 100 * 1.5 = 150
            // Level 3 -> 4 requires 150 * 1.5 = 225
            if (CurrentMultiplier >= maxMultiplier)
            {
                _gloryRequiredForNextLevel = CurrentGlory; // Maxed out
            }
            else
            {
                _gloryRequiredForNextLevel = baseGloryRequired * Mathf.Pow(gloryRequirementScale, CurrentMultiplier - 1);
            }
        }

        public void AddCurrency(int amount)
        {
            int totalEarned = amount * CurrentMultiplier;
            Currency += totalEarned;
            Debug.Log($"[ShopManager] Earned {amount} x {CurrentMultiplier} = {totalEarned} Gold! Total: {Currency}");
            UpdateUI();
        }

        public bool HasCurrency(int amount)
        {
            return Currency >= amount;
        }

        public void SpendCurrency(int amount)
        {
            if (HasCurrency(amount))
            {
                Currency -= amount;
                Debug.Log($"[ShopManager] Spent {amount} Gold. Remaining: {Currency}");
                UpdateUI();
            }
        }

        public void AddGlory(float amount)
        {
            if (CurrentMultiplier >= maxMultiplier) 
            {
                CurrentGlory = _gloryRequiredForNextLevel;
                UpdateUI();
                return;
            }

            CurrentGlory += amount;
            Debug.Log($"[ShopManager] Gained {amount} Glory. Total: {CurrentGlory}/{_gloryRequiredForNextLevel}");

            CheckLevelUp();
            UpdateUI();
        }

        public void RemoveGlory(float amount)
        {
            CurrentGlory -= amount;
            Debug.Log($"[ShopManager] Lost {amount} Glory! Total: {CurrentGlory}");

            CheckLevelDown();
            UpdateUI();
        }

        private void CheckLevelUp()
        {
            while (CurrentGlory >= _gloryRequiredForNextLevel && CurrentMultiplier < maxMultiplier)
            {
                CurrentGlory -= _gloryRequiredForNextLevel;
                CurrentMultiplier++;
                Debug.Log($"[ShopManager] LEVEL UP! Multiplier is now {CurrentMultiplier}x!");
                CalculateNextLevelRequirement();
            }

            if (CurrentMultiplier >= maxMultiplier)
            {
                CurrentGlory = _gloryRequiredForNextLevel; // Cap it
            }
        }

        private void CheckLevelDown()
        {
            while (CurrentGlory < 0)
            {
                if (CurrentMultiplier > 1)
                {
                    CurrentMultiplier--;
                    CalculateNextLevelRequirement();
                    CurrentGlory += _gloryRequiredForNextLevel; // Wrap around backwards
                    Debug.Log($"[ShopManager] LEVEL DOWN! Multiplier dropped to {CurrentMultiplier}x!");
                }
                else
                {
                    CurrentGlory = 0; // Never drop below 0 at 1x
                    break;
                }
            }
        }

        private void UpdateUI()
        {
            if (currencyText != null)
                currencyText.text = $"Gold: {Currency}";

            if (multiplierText != null)
                multiplierText.text = $"x{CurrentMultiplier}";

            UpdateGloryBar();
        }

        private void UpdateGloryBar()
        {
            if (glorySlider != null)
            {
                if (CurrentMultiplier >= maxMultiplier)
                {
                    glorySlider.value = 1f; // Maxed
                }
                else
                {
                    glorySlider.value = CurrentGlory / _gloryRequiredForNextLevel;
                }

                // Update color based on multiplier
                if (multiplierColors != null && multiplierColors.Length > 0)
                {
                    int colorIndex = Mathf.Clamp(CurrentMultiplier - 1, 0, multiplierColors.Length - 1);
                    
                    // Try to auto-find the fill image if not manually assigned
                    if (gloryFillImage == null && glorySlider.fillRect != null)
                    {
                        gloryFillImage = glorySlider.fillRect.GetComponent<Image>();
                    }

                    if (gloryFillImage != null)
                    {
                        gloryFillImage.color = multiplierColors[colorIndex];
                    }
                }
            }
        }

        // ==========================================
        // DATA ACCESS & SAVING
        // ==========================================

        public void LoadFromSave(GameSaveData data)
        {
            Currency = data.currency;
            CurrentGlory = data.glory;
            CurrentMultiplier = data.currentMultiplier;

            CalculateNextLevelRequirement();
            UpdateUI();
        }
    }
}
