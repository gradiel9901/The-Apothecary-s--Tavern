using System.Collections;
using System.Collections.Generic;
using Script.Player;
using Script.Systems;
using UnityEngine;
using UnityEngine.UI;

namespace Script.Environment
{
    public class Cauldron : MonoBehaviour, IInteractable
    {
        [Header("Crafting Settings")]
        [SerializeField] private List<PotionRecipe> recipes;
        [SerializeField] private GameObject failureEffect; // Optional
        
        [Header("Minigame Settings")]
        [SerializeField] private Slider progressBar;
        [SerializeField] private Light mixingLight;
        [SerializeField] private float lightToggleInterval = 1.0f;
        [SerializeField] private Color mixColor = Color.cyan;

        private List<ItemType> _currentIngredients = new List<ItemType>();
        
        // Minigame State
        private bool _isMixing = false;
        private PotionRecipe _currentRecipe;
        private float _currentProgress = 0f;
        private float _lightTimer = 0f;
        private bool _isLightOn = false;
        private int _failuresAtZero = 0;

        private void Start()
        {
            if (progressBar != null) progressBar.gameObject.SetActive(false);
            if (mixingLight != null) mixingLight.intensity = 0;
        }

        private void Update()
        {
            if (_isMixing)
            {
                HandleMinigameLoop();
            }
        }

        public void Interact(PlayerInteraction player)
        {
            Script.Systems.TutorialManager.NotifyStep(Script.Systems.TutorialManager.TutorialEvent.CauldronStarted);
            if (_isMixing)
            {
                Debug.Log("Cauldron is mixing! Use F to mix.");
            }
            else
            {
                HandleIngredientInput(player);
            }
        }

        public void Mix(PlayerInteraction player)
        {
            if (_isMixing)
            {
                HandleMixingInput(player);
            }
        }

        #region Ingredient Handling
        private void HandleIngredientInput(PlayerInteraction player)
        {
            if (player.HasItem())
            {
                ItemType heldItem = player.GetHeldItem();
                
                // Optional: Check if item is an Ingredient vs Potion if needed
                
                Debug.Log($"Added {heldItem} to Cauldron.");
                _currentIngredients.Add(heldItem);
                player.DropItem();

                CheckRecipes(player);
            }
            else
            {
                Debug.Log($"Cauldron contains: {string.Join(", ", _currentIngredients)}");
            }
        }

        private void CheckRecipes(PlayerInteraction player)
        {
            bool matchFound = false;
            bool potentialMatchFound = false;

            foreach (var recipe in recipes)
            {
                if (recipe.Matches(_currentIngredients))
                {
                    StartMixing(recipe);
                    matchFound = true;
                    return;
                }

                if (recipe.IsSubset(_currentIngredients))
                {
                    potentialMatchFound = true;
                }
            }
            
            if (!matchFound && !potentialMatchFound)
            {
                if (AudioManager.Instance != null) AudioManager.Instance.PlayMixMiss();
                TriggerFailure("Invalid mixture! Ingredients wasted.");
            }
        }
        #endregion

        #region Minigame Logic
        private void StartMixing(PotionRecipe recipe)
        {
            _isMixing = true;
            _currentRecipe = recipe;
            _currentProgress = 0f;
            _visualProgress = 0f;
            _failuresAtZero = 0;
            _lightTimer = 0f;

            if (progressBar != null)
            {
                progressBar.gameObject.SetActive(true);
                progressBar.value = 0;
            }
            
            if (AudioManager.Instance != null) AudioManager.Instance.PlayBubbling();
            
            Debug.Log("Mixing Started! Watch the light.");
        }

        [SerializeField] private float lightTransitionSpeed = 5.0f;
        [SerializeField] private float progressTransitionSpeed = 5.0f;
        
        private float _visualProgress = 0f;

        private void HandleMinigameLoop()
        {
            _lightTimer += Time.deltaTime;
            if (_lightTimer >= lightToggleInterval)
            {
                _lightTimer = 0f;
                ToggleLight();
            }

            if (mixingLight != null)
            {
                float targetIntensity = _isLightOn ? 5.0f : 0f;
                mixingLight.intensity = Mathf.MoveTowards(mixingLight.intensity, targetIntensity, Time.deltaTime * lightTransitionSpeed);
                
                // Color can toggle instantly or also smooth if desired, kept instant for clarity of "ON/OFF" state
                mixingLight.color = _isLightOn ? mixColor : Color.black; 
            }

            if (progressBar != null)
            {
                // Smoothly interpolate visual progress towards actual progress
                _visualProgress = Mathf.Lerp(_visualProgress, _currentProgress, Time.deltaTime * progressTransitionSpeed);
                progressBar.value = _visualProgress / 100f;
            }
        }

        private void ToggleLight()
        {
            _isLightOn = !_isLightOn; 
        }

        [SerializeField] private float minProgress = 5.0f;
        [SerializeField] private float maxProgress = 20.0f;
        [SerializeField] private float instantCompleteChance = 0.05f; // 5% chance

        private void HandleMixingInput(PlayerInteraction player)
        {
            if (_isLightOn)
            {
            Script.Systems.TutorialManager.NotifyStep(Script.Systems.TutorialManager.TutorialEvent.CauldronLightHit);
                // Correct Input
                float progressGain = Random.Range(minProgress, maxProgress);
                
                // Critical Success Check
                if (Random.value < instantCompleteChance)
                {
                    if (AudioManager.Instance != null) AudioManager.Instance.PlayMixCrit();
                    progressGain = 100f;
                    Debug.Log("CRITICAL MIX! Instant Success!");
                }
                else
                {
                    if (AudioManager.Instance != null) AudioManager.Instance.PlayMixHit();
                }

                _currentProgress += progressGain;
                Debug.Log($"Good Mix! Gained {progressGain:F1}. Progress: {_currentProgress:F1}");
                _failuresAtZero = 0; 
                
                if (_currentProgress >= 100f)
                {
                    CompleteMixing(player);
                }
            }
            else
            {
                // Wrong Input
                Debug.Log("Wrong Timing!");
                if (AudioManager.Instance != null) AudioManager.Instance.PlayMixMiss();

                if (_currentProgress > 0)
                {
                    _currentProgress -= 10f;
                    if (_currentProgress < 0) _currentProgress = 0;
                }
                else
                {
                    // Already at 0
                    _failuresAtZero++;
                    Debug.Log($"Failure Strike: {_failuresAtZero}/3");
                    if (_failuresAtZero >= 3)
                    {
                        TriggerFailure("Mixing Failed too many times!");
                    }
                }
            }
        }

        private void CompleteMixing(PlayerInteraction player)
        {
            CraftPotion(_currentRecipe, player);
            ResetCauldron();
        }
        #endregion

        private void TriggerFailure(string message)
        {
            Debug.Log(message);
            if (failureEffect != null)
            {
                Instantiate(failureEffect, transform.position + Vector3.up, Quaternion.identity);
            }
            ResetCauldron();
        }

        private void ResetCauldron()
        {
            _currentIngredients.Clear();
            _isMixing = false;
            _currentRecipe = null;
            _currentProgress = 0f;
            _failuresAtZero = 0;
            
            if (AudioManager.Instance != null) AudioManager.Instance.StopBubbling();
            
            if (progressBar != null) progressBar.gameObject.SetActive(false);
            if (mixingLight != null) mixingLight.intensity = 0;
        }

        private void CraftPotion(PotionRecipe recipe, PlayerInteraction player)
        {
            Debug.Log($"Crafted {recipe.resultPotion}!");
            
            if (recipe.resultPrefab != null)
            {
                player.PickUpItem(recipe.resultPotion, recipe.resultPrefab);
            }
        }
    }
}


