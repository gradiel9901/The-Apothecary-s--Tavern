using System.Collections.Generic;
using UnityEngine;

namespace Script.Environment
{
    [CreateAssetMenu(fileName = "NewPotionRecipe", menuName = "Crafting/Potion Recipe")]
    public class PotionRecipe : ScriptableObject
    {
        [Header("Recipe")]
        public List<ItemType> ingredients;
        
        [Header("Result")]
        public ItemType resultPotion;
        public GameObject resultPrefab;

        [Header("Economy")]
        [Tooltip("Base gold earned when serving this potion.")]
        public int basePrice = 50;
        
        [Tooltip("Base glory earned on a successful order.")]
        public float gloryReward = 20f;
        
        [Tooltip("Glory lost when the wrong potion is served (or order declined).")]
        public float gloryPenalty = 40f;

        public bool Matches(List<ItemType> currentIngredients)
        {
            if (currentIngredients.Count != ingredients.Count) return false;

            // Simple check: create temp lists to compare content regardless of order
            List<ItemType> remaining = new List<ItemType>(ingredients);
            
            foreach (var item in currentIngredients)
            {
                if (remaining.Contains(item))
                {
                    remaining.Remove(item);
                }
                else
                {
                    return false;
                }
            }
            return remaining.Count == 0;
        }

        public bool IsSubset(List<ItemType> currentIngredients)
        {
             List<ItemType> remaining = new List<ItemType>(ingredients);
            
            foreach (var item in currentIngredients)
            {
                if (remaining.Contains(item))
                {
                    remaining.Remove(item);
                }
                else
                {
                    // Item in pot is NOT in this recipe (or we have too many of it)
                    return false;
                }
            }
            // If we get here, all items in pot were accounted for in the recipe
            return true;
        }
    }
}
