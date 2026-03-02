using Script.Environment;
using Script.Systems;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Script.Player
{
    [RequireComponent(typeof(PlayerMovement))]
    public class PlayerInteraction : MonoBehaviour
    {
        [Header("Interaction Settings")]
        [SerializeField] private float interactionRange = 2.0f;
        [SerializeField] private string interactableTag = "Interactable";
        [SerializeField] private string multiInteractTag = "Doors";
        [SerializeField] private string npcTag = "NPC";
        [SerializeField] private string sackTag = "Sack";
        [SerializeField] private Transform holdPoint;

        private InputSystem_Actions _inputActions;
        private PlayerMovement _playerMovement;
        
        // Single Items
        private ItemType? _heldItem;
        private GameObject _heldObjectInstance;
        
        // Sacks
        private ItemSack _heldSack;

        private void Awake()
        {
            _inputActions = new InputSystem_Actions();
            _playerMovement = GetComponent<PlayerMovement>();
        }

        private void OnEnable()
        {
            _inputActions.Player.Enable();
            _inputActions.Player.Interact.performed += OnInteract;
        }

        private void OnDisable()
        {
            _inputActions.Player.Interact.performed -= OnInteract;
            _inputActions.Player.Disable();
        }

        private void OnInteract(InputAction.CallbackContext context)
        {
            Collider[] colliders = Physics.OverlapSphere(transform.position, interactionRange);
            bool interactedWithMulti = false;

            // First, check for things that allow simultaneous interaction (like Doors)
            foreach (var collider in colliders)
            {
                // Check if the collider itself or its parent has the multiInteractTag
                bool hasMultiTag = collider.CompareTag(multiInteractTag) || 
                                   (collider.transform.parent != null && collider.transform.parent.CompareTag(multiInteractTag));

                if (hasMultiTag)
                {
                    // Try to get IInteractable on this object or its parents
                    IInteractable interactable = collider.GetComponentInParent<IInteractable>();
                    if (interactable != null)
                    {
                        interactable.Interact(this);
                        interactedWithMulti = true;
                    }
                }
            }

            // If we already interacted with doors, skip everything else
            if (interactedWithMulti) return;

            // ── NPC interaction: checked before item pick-up so talking always wins ──
            IInteractable closestNpc = null;
            float closestNpcDist = float.MaxValue;

            foreach (var collider in colliders)
            {
                if (!collider.CompareTag(npcTag)) continue;

                IInteractable npcInteractable = collider.GetComponentInParent<IInteractable>();
                if (npcInteractable == null)
                {
                    Debug.LogWarning($"[PlayerInteraction] Found object tagged '{npcTag}' ({collider.name}), but no IInteractable component was found on it or its parents.");
                    continue;
                }

                float dist = Vector3.Distance(transform.position, collider.transform.position);
                if (dist < closestNpcDist)
                {
                    closestNpcDist    = dist;
                    closestNpc        = npcInteractable;
                }
            }

            if (closestNpc != null)
            {
                Debug.Log($"[PlayerInteraction] Interacting with NPC: {closestNpc.GetType().Name} at distance {closestNpcDist:F2}");
                closestNpc.Interact(this);
                return; // Don't also pick up an item in the same frame
            }
            else
            {
                // Un-comment to spam log every click if needed
                // Debug.Log($"[PlayerInteraction] No NPC found within {interactionRange} units. Checking items instead.");
            }

            // Standard closest-single-item interaction
            IInteractable closestInteractable = null;
            float closestDistance = float.MaxValue;

            foreach (var collider in colliders)
            {
                // Accept either "Interactable" (normal items) or "Sack" (ingredient sacks)
                if (!collider.CompareTag(interactableTag) && !collider.CompareTag(sackTag)) continue;

                // Use GetComponentInParent just in case the collider is on a child object
                IInteractable interactable = collider.GetComponentInParent<IInteractable>();
                if (interactable != null)
                {
                    float distance = Vector3.Distance(transform.position, collider.transform.position);
                    if (distance < closestDistance)
                    {
                        closestDistance = distance;
                        closestInteractable = interactable;
                    }
                }
            }

            if (closestInteractable != null)
            {
                closestInteractable.Interact(this);
            }
        }

        private void Update()
        {
            // Handle Dropping Sacks using Q (bypassing input map for ease)
            if (Keyboard.current.qKey.wasPressedThisFrame)
            {
                if (_heldSack != null)
                {
                    DropSack();
                }
            }
            
            // Handle Replenishing via manual F key check
            if (Keyboard.current.fKey.wasPressedThisFrame)
            {
                if (_heldSack != null)
                {
                    TryReplenishShelf();
                }
                else
                {
                    TryMixCauldron();
                }
            }
        }
        
        private void TryMixCauldron()
        {
            Collider[] colliders = Physics.OverlapSphere(transform.position, interactionRange);
            Cauldron closestCauldron = null;
            float closestDistance = float.MaxValue;

            foreach (var collider in colliders)
            {
                if (collider.TryGetComponent(out Cauldron cauldron))
                {
                    float distance = Vector3.Distance(transform.position, collider.transform.position);
                    if (distance < closestDistance)
                    {
                        closestDistance = distance;
                        closestCauldron = cauldron;
                    }
                }
            }

            if (closestCauldron != null)
            {
                closestCauldron.Mix(this);
            }
        }
        
        private void TryReplenishShelf()
        {
            Collider[] colliders = Physics.OverlapSphere(transform.position, interactionRange);
            Shelf closestShelf = null;
            float closestDistance = float.MaxValue;

            foreach (var collider in colliders)
            {
                if (collider.TryGetComponent(out Shelf shelf))
                {
                    float distance = Vector3.Distance(transform.position, collider.transform.position);
                    if (distance < closestDistance)
                    {
                        closestDistance = distance;
                        closestShelf = shelf;
                    }
                }
            }

            if (closestShelf != null)
            {
                closestShelf.InteractSack(this, _heldSack);
            }
        }

        public ItemType GetHeldItem()
        {
            return _heldItem.GetValueOrDefault();
        }

        public void PickUpItem(ItemType item, GameObject prefab)
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlayItemPickup();
            
            _heldItem = item;
            Debug.Log($"Player is now holding: {_heldItem}");

            // Visuals
            if (_heldObjectInstance != null)
            {
                Destroy(_heldObjectInstance);
            }

            if (prefab != null && holdPoint != null)
            {
                _heldObjectInstance = Instantiate(prefab, holdPoint);
                _heldObjectInstance.transform.localPosition = Vector3.zero;
                _heldObjectInstance.transform.localRotation = Quaternion.identity;
            }
            
            // Trigger animation state
            if (_playerMovement != null)
            {
                _playerMovement.SetCarrying(true);
            }
        }
        
        // ===================================
        // SACK SYSTEM
        // ===================================

        public void PickUpSack(ItemSack sack)
        {
            // Drop current held item if holding one
            if (HasItem()) DropItem();
            // Drop current held sack if holding one
            if (_heldSack != null) DropSack();

            if (AudioManager.Instance != null) AudioManager.Instance.PlayItemPickup();

            _heldSack = sack;
            Debug.Log($"Player is now holding SACK: {_heldSack.itemType} x{_heldSack.amount}");

            _heldSack.transform.SetParent(holdPoint);
            _heldSack.transform.localPosition = Vector3.zero;
            _heldSack.transform.localRotation = Quaternion.identity;
            
            // Turn off physics / colliders so it doesn't bump into the player or block interaction raycasts
            if (_heldSack.TryGetComponent(out Rigidbody rb)) rb.isKinematic = true;
            if (_heldSack.TryGetComponent(out Collider col)) col.enabled = false;

            if (_playerMovement != null)
            {
                _playerMovement.SetCarrying(true);
            }
        }

        public void DropSack()
        {
            if (_heldSack == null) return;

            if (AudioManager.Instance != null) AudioManager.Instance.PlayItemDrop();
            
            Debug.Log("Player dropped SACK.");
            
            _heldSack.transform.SetParent(null);

            // Re-enable physics
            if (_heldSack.TryGetComponent(out Rigidbody rb)) rb.isKinematic = false;
            if (_heldSack.TryGetComponent(out Collider col)) col.enabled = true;

            _heldSack = null;

            if (_playerMovement != null)
            {
                _playerMovement.SetCarrying(false);
            }
        }

        public void ConsumeSack()
        {
            if (_heldSack != null)
            {
                Destroy(_heldSack.gameObject);
                _heldSack = null;
                if (_playerMovement != null)
                {
                    _playerMovement.SetCarrying(false);
                }
            }
        }

        public bool HasItem()
        {
            return _heldItem.HasValue;
        }

        public void DropItem()
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlayItemDrop();
            
            _heldItem = null;
            Debug.Log("Player dropped item.");

            if (_heldObjectInstance != null)
            {
                Destroy(_heldObjectInstance);
            }

            if (_playerMovement != null)
            {
                _playerMovement.SetCarrying(false);
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, interactionRange);
        }

        public float GetInteractionRange() => interactionRange;
        public bool IsHoldingSack() => _heldSack != null;

        public string GetInteractableTag() => interactableTag;
        public string GetMultiInteractTag() => multiInteractTag;
        public string GetNpcTag() => npcTag;
        public string GetSackTag() => sackTag;
    }
}
