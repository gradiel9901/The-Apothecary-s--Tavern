using UnityEngine;
using UnityEngine.InputSystem;
using Script.Environment;

namespace Script.Systems
{
    public class DeveloperManager : MonoBehaviour
    {
        private void Update()
        {
            if (Keyboard.current == null) return;

            if (Keyboard.current.f1Key.wasPressedThisFrame)
            {
                // End day immediately
                if (DayCycleManager.Instance != null)
                {
                    Debug.Log("[Developer] F1 pressed: Ending day immediately");
                    DayCycleManager.Instance.EndDay();
                }
            }

            if (Keyboard.current.f2Key.wasPressedThisFrame)
            {
                // Add gold
                if (ShopManager.Instance != null)
                {
                    Debug.Log("[Developer] F2 pressed: Adding 1000 Gold");
                    ShopManager.Instance.AddCurrency(1000);
                }
            }

            if (Keyboard.current.f3Key.wasPressedThisFrame)
            {
                // Increase duration of each day
                if (DayCycleManager.Instance != null)
                {
                    Debug.Log("[Developer] F3 pressed: Increasing day duration by 1 minute");
                    DayCycleManager.Instance.AdjustWorkingDuration(1f);
                }
            }

            if (Keyboard.current.f4Key.wasPressedThisFrame)
            {
                // Decrease duration of each day
                if (DayCycleManager.Instance != null)
                {
                    Debug.Log("[Developer] F4 pressed: Decreasing day duration by 1 minute");
                    DayCycleManager.Instance.AdjustWorkingDuration(-1f);
                }
            }
        }
    }
}
