using Script.Player;
using UnityEngine;

namespace Script.Environment
{
    public class Trash : MonoBehaviour, IInteractable
    {
        public void Interact(PlayerInteraction player)
        {
            if (player.HasItem())
            {
                Debug.Log("Disposing of item in Trash.");
                player.DropItem();
            }
            else
            {
                Debug.Log("Player has no item to trash.");
            }
        }
    }
}
