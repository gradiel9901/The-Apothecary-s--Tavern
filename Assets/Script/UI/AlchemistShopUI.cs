using System;
using Script.Environment;
using UnityEngine;
using UnityEngine.UI;
using TMPro; // Add TextMeshPro
using Script.Player; // For cursor locking
using UnityEngine.InputSystem;

namespace Script.UI
{
    [Serializable]
    public class ShopItemData
    {
        public ItemType itemType;
        public int cost;
        public int amountPerSack;
        public GameObject sackPrefab;
        [Tooltip("The UI Button for buying this item. Needed to grey it out during events.")]
        public Button buyButton;
        [Tooltip("The UI Text showing the cost of this item. Needed to update dynamic prices during events.")]
        public TMP_Text priceText;
        [Tooltip("Optional button reference, but easier to just link the OnClick event in inspector to BuyItem(index)")]
        public string itemName; // Just for organization in the inspector
    }

    public class AlchemistShopUI : MonoBehaviour
    {
        [Header("Shop Settings")]
        public Transform sackSpawnPoint;
        public ShopItemData[] availableItems;

        [Header("Player Reference")]
        [Tooltip("Needed to re-enable movement when closing the shop. Optional if you prefer tagging.")]
        public PlayerMovement playerMovement;

        // Ensure we find the player if not linked
        private void OnEnable()
        {
            if (playerMovement == null)
            {
                playerMovement = FindFirstObjectByType<PlayerMovement>();
            }

            RefreshShopUI();
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                Script.UI.PauseManager.ConsumedEscapeThisFrame = true;
                CloseShop();
            }
        }

        private void RefreshShopUI()
        {
            if (EventManager.Instance == null) return;

            // Loop through all items and check if they are buyable
            for (int i = 0; i < availableItems.Length; i++)
            {
                ShopItemData data = availableItems[i];
                
                // Update Button State
                if (data.buyButton != null)
                {
                    bool isBuyable = EventManager.Instance.IsItemBuyable(data.itemType);
                    data.buyButton.interactable = isBuyable;
                }

                // Update Price Text
                if (data.priceText != null)
                {
                    int currentPrice = EventManager.Instance.GetItemPrice(data.itemType, data.cost);
                    data.priceText.text = currentPrice.ToString();
                    
                    // Optional: Highlight text in red if price went up (like Dragon Heart)
                    if (currentPrice > data.cost)
                    {
                        data.priceText.color = Color.red;
                    }
                    else
                    {
                        data.priceText.color = Color.white; // Or whatever your default color is
                    }
                }
            }
        }

        public void BuyItem(int index)
        {
            Script.Systems.TutorialManager.NotifyStep(Script.Systems.TutorialManager.TutorialEvent.ItemBought);
            if (index < 0 || index >= availableItems.Length)
            {
                Debug.LogError("Invalid Shop Item Index!");
                return;
            }

            ShopItemData data = availableItems[index];

            if (ShopManager.Instance == null)
            {
                Debug.LogError("No ShopManager found!");
                return;
            }

            // Check if the item is blocked by a current event
            if (EventManager.Instance != null && !EventManager.Instance.IsItemBuyable(data.itemType))
            {
                Debug.Log($"[Shop] You cannot buy {data.itemType} during the current event!");
                return; // You could optionally show a UI warning here.
            }

            // Calculate final cost taking event modifiers into account
            int finalCost = data.cost;
            if (EventManager.Instance != null)
            {
                finalCost = EventManager.Instance.GetItemPrice(data.itemType, data.cost);
            }

            if (ShopManager.Instance.HasCurrency(finalCost))
            {
                ShopManager.Instance.SpendCurrency(finalCost);
                
                // Spawn Sack
                if (data.sackPrefab != null && sackSpawnPoint != null)
                {
                    GameObject sackObj = Instantiate(data.sackPrefab, sackSpawnPoint.position, sackSpawnPoint.rotation);
                    if (sackObj.TryGetComponent(out ItemSack sackComponent))
                    {
                        sackComponent.itemType = data.itemType;
                        sackComponent.amount = data.amountPerSack;
                    }
                    Debug.Log($"[Shop] Bought {data.amountPerSack}x {data.itemType} for {finalCost} Gold.");
                }
                else
                {
                    Debug.LogError("[Shop] Missing Sack Prefab or Spawn Point!");
                }
            }
            else
            {
                Debug.Log("[Shop] Not enough Gold!");
                // Here you could trigger a UI animation or sound effect for "Failed Purchase"
            }
        }

        public void CloseShop()
        {
            // Re-lock cursor
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            // Re-enable player movement
            if (playerMovement != null)
            {
                playerMovement.TogglePlayerInput(true);
            }

            gameObject.SetActive(false);
        }
    }
}

