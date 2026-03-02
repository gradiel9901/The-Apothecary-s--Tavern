using System.Collections.Generic;
using Script.Player;
using UnityEngine;

namespace Script.Environment
{
    public class Shelf : MonoBehaviour, IInteractable
    {
        [Header("Shelf Settings")]
        [SerializeField] private ItemType itemType;
        [SerializeField] private GameObject itemPrefab;
        [Tooltip("The absolute maximum number of items the shelf can hold.")]
        [SerializeField] private int maxItems = 10;
        [Tooltip("The starting/current stock of the shelf. If you want an empty shelf, set this to 0.")]
        [SerializeField] private int currentStock = 10;

        [Header("Visual Settings")]
        [Tooltip("If checked, use Uniform Scale. If unchecked, use Vector3 Scale.")]
        [SerializeField] private bool constrainProportions = true;
        [SerializeField] private float uniformScale = 0.5f;
        [SerializeField] private Vector3 vectorScale = Vector3.one;

        [Tooltip("Base rotation applied to every item on the shelf.")]
        [SerializeField] private Vector3 baseRotation = Vector3.zero;

        [Tooltip("Additional rotation applied depending on the row index (useful if ingredients need to lean against the back differently per shelf).")]
        [SerializeField] private Vector3 perRowRotationOffset = Vector3.zero;

        [Tooltip("The local starting position for the first visual item (e.g. top-left corner of the top shelf).")]
        [SerializeField] private Vector3 startOffset = Vector3.zero;

        [Tooltip("If true, automatically calculates the spacing based on the prefab's physical width.")]
        [SerializeField] private bool autoCalculateSpacing = true;
        [SerializeField] private float padding = 0.05f;

        [Tooltip("Manual spacing added for each subsequent item (Overrides Auto Calculate if false).")]
        [SerializeField] private Vector3 manualSpacingOffset = new Vector3(0.5f, 0, 0);

        [Tooltip("How many items fit on one shelf level before moving down to the next?")]
        [SerializeField] private int itemsPerRow = 5;

        [Tooltip("The local offset applied when moving to a new row/shelf level (e.g. going down in Y).")]
        [SerializeField] private Vector3 newRowOffset = new Vector3(0, -0.6f, 0);

        private List<GameObject> _spawnedVisualItems = new List<GameObject>();

        public ItemType GetItemType() => itemType;
        public int GetCurrentItems() => currentStock;

        private void Awake()
        {
            currentStock = Mathf.Clamp(currentStock, 0, maxItems);
            SpawnVisualItems();
        }

        private void SpawnVisualItems()
        {
            if (itemPrefab == null) return;

            Vector3 currentSpacing = manualSpacingOffset;
            Vector3 finalScale = constrainProportions ? new Vector3(uniformScale, uniformScale, uniformScale) : vectorScale;

            for (int i = 0; i < currentStock; i++)
            {
                // Instantiate internal visual copy
                GameObject visualItem = Instantiate(itemPrefab, transform);
                visualItem.transform.localScale = finalScale;

                // Strip components immediately so they don't interfere with calculations
                Collider[] colliders = visualItem.GetComponentsInChildren<Collider>();
                foreach (Collider col in colliders) col.enabled = false;

                MonoBehaviour[] scripts = visualItem.GetComponentsInChildren<MonoBehaviour>();
                foreach (MonoBehaviour script in scripts) Destroy(script);

                // For the very first item, if auto calculating is on, figure out how wide it is
                if (i == 0 && autoCalculateSpacing)
                {
                    Renderer[] renderers = visualItem.GetComponentsInChildren<Renderer>();
                    if (renderers.Length > 0)
                    {
                        Bounds bounds = renderers[0].bounds;
                        for (int r = 1; r < renderers.Length; r++)
                        {
                            bounds.Encapsulate(renderers[r].bounds);
                        }
                        
                        // Roughly convert world width to local shelf space
                        float itemWidth = bounds.size.x / transform.lossyScale.x;
                        currentSpacing = new Vector3(itemWidth + padding, 0, 0);
                    }
                }

                // Calculate the row and column of this specific item
                int rowIndex = i / itemsPerRow;
                int colIndex = i % itemsPerRow;

                // Apply rotation
                Vector3 finalRotation = baseRotation + (perRowRotationOffset * rowIndex);
                visualItem.transform.localRotation = Quaternion.Euler(finalRotation);

                // Position the item locally based on its row and column
                Vector3 rowStartPosition = startOffset + (newRowOffset * rowIndex);
                visualItem.transform.localPosition = rowStartPosition + (currentSpacing * colIndex);

                _spawnedVisualItems.Add(visualItem);
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Only update live during Play Mode
            if (Application.isPlaying && _spawnedVisualItems != null && _spawnedVisualItems.Count > 0)
            {
                // Delay the visual update slightly to avoid Unity Editor warnings when destroying objects mid-validation
                UnityEditor.EditorApplication.delayCall += () =>
                {
                    if (this == null) return; // Ensure the object still exists

                    // Clear old visual items
                    foreach (var item in _spawnedVisualItems)
                    {
                        if (item != null) Destroy(item);
                    }
                    _spawnedVisualItems.Clear();

                    // Respawn with new settings
                    SpawnVisualItems();
                };
            }
        }
#endif

        public void Interact(PlayerInteraction player)
        {
            if (currentStock > 0)
            {
                Debug.Log($"Picked up {itemType} from Shelf. Items left: {currentStock - 1}");
                player.PickUpItem(itemType, itemPrefab);
                currentStock--;

                // Destroy the last visual item in the list
                if (_spawnedVisualItems.Count > 0)
                {
                    GameObject itemToDestroy = _spawnedVisualItems[_spawnedVisualItems.Count - 1];
                    _spawnedVisualItems.RemoveAt(_spawnedVisualItems.Count - 1);
                    Destroy(itemToDestroy);
                }
            }
            else
            {
                Debug.Log("Shelf is empty!");
            }
        }

        public void RemoveItems(int amount)
        {
            if (currentStock <= 0) return;

            int amountToRemove = Mathf.Min(amount, currentStock);
            currentStock -= amountToRemove;

            for (int i = 0; i < amountToRemove; i++)
            {
                if (_spawnedVisualItems.Count > 0)
                {
                    GameObject itemToDestroy = _spawnedVisualItems[_spawnedVisualItems.Count - 1];
                    _spawnedVisualItems.RemoveAt(_spawnedVisualItems.Count - 1);
                    Destroy(itemToDestroy);
                }
            }
        }

        public void InteractSack(PlayerInteraction player, ItemSack sack)
        {
            Script.Systems.TutorialManager.NotifyStep(Script.Systems.TutorialManager.TutorialEvent.ShelfFilled);
            if (sack.itemType != this.itemType)
            {
                Debug.Log($"[Shelf] Wrong item type! Shelf needs {itemType}, but Sack has {sack.itemType}.");
                return;
            }

            int spaceLeft = maxItems - currentStock;
            if (spaceLeft <= 0)
            {
                Debug.Log("[Shelf] Shelf is already full!");
                return;
            }

            // Figure out how much we can actually take from the sack
            int amountToTake = Mathf.Min(spaceLeft, sack.amount);
            
            currentStock += amountToTake;
            sack.amount -= amountToTake;

            Debug.Log($"[Shelf] Restocked {amountToTake} {itemType}. Shelf now at {currentStock}/{maxItems}. Sack has {sack.amount} left.");

            // Clear visually so we can rebuild it cleanly
            foreach (var item in _spawnedVisualItems)
            {
                if (item != null) Destroy(item);
            }
            _spawnedVisualItems.Clear();
            SpawnVisualItems();

            // Destroy sack if empty
            if (sack.amount <= 0)
            {
                player.ConsumeSack();
            }
        }
    }
}

