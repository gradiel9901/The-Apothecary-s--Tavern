using Script.Player;
using UnityEngine;

namespace Script.Environment
{
    public class ItemSack : MonoBehaviour, IInteractable
    {
        [Header("Sack Details")]
        public ItemType itemType;
        public int amount;

        public void Interact(PlayerInteraction player)
        {
            if (amount <= 0)
            {
                Debug.Log("This sack is empty!");
                Destroy(gameObject);
                return;
            }

            // Tell the player to pick this sack up
            // Since we don't want to destroy the sack immediately (unlike single items),
            // we will pass a reference to this actual instance to the player.
            player.PickUpSack(this);
        }
    }
}
