using Script.Player;
using UnityEngine;
using Script.UI;

namespace Script.Environment
{
    public class AlchemistTable : MonoBehaviour, IInteractable
    {
        [Header("Shop UI Object")]
        [Tooltip("Drag the parent Canvas/Panel of the Alchemist Shop here.")]
        [SerializeField] private GameObject shopUIPanel;

        private void Start()
        {
            if (shopUIPanel != null)
            {
                shopUIPanel.SetActive(false);
            }
        }

        public void Interact(PlayerInteraction player)
        {
            if (DayCycleManager.Instance != null && DayCycleManager.Instance.IsWorkingHours)
            {
                Debug.Log("The shop is only open after working hours!");
                return;
            }

            if (shopUIPanel != null)
            {
                Debug.Log("Opening Alchemist Shop...");
                shopUIPanel.SetActive(true);
                
                // If the user placed the AlchemistShopUI script on a child panel inside the Canvas,
                // we must explicitly re-enable that child because CloseShop() turns it off!
                AlchemistShopUI shopScript = shopUIPanel.GetComponentInChildren<AlchemistShopUI>(true);
                if (shopScript != null)
                {
                    shopScript.gameObject.SetActive(true);
                }
                
                // Try and unlock the cursor
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;

                // Stop the player movement when UI opens
                if (player.TryGetComponent(out PlayerMovement movement))
                {
                    movement.TogglePlayerInput(false);
                }
            }
        }
    }
}
