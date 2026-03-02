using UnityEngine;
using TMPro;
using Script.Environment;

namespace Script.Player
{
    [RequireComponent(typeof(PlayerInteraction))]
    public class InteractionTooltip : MonoBehaviour
    {
        [Header("Settings")]
        [Tooltip("How high above the player the text should float")]
        public float yOffset = 1.0f;
        [Tooltip("How far to the side (right) of the player the text should float")]
        public float xOffset = 0.8f;
        [Tooltip("Scale of the world-space canvas")]
        public Vector3 canvasScale = new Vector3(0.015f, 0.015f, 0.015f);
        
        [Header("Font Settings")]
        [Tooltip("Custom font asset for the text")]
        public TMP_FontAsset fontAsset;
        [Tooltip("Size of the text")]
        public float fontSize = 24f;
        public Color fontColor = Color.white;
        [Tooltip("Multiplies the color brightness (HDR effect) to fix dull text")]
        public float colorBrightness = 1.0f;
        
        [Header("Outline Settings")]
        [Tooltip("Outline thickness (requires a TMP material that supports it, or it will inject it dynamically)")]
        [Range(0f, 1f)] public float outlineWidth = 0.2f;
        public Color outlineColor = Color.black;

        private PlayerInteraction _playerInteraction;
        private Canvas _canvas;
        private TextMeshProUGUI _textMesh;
        private Camera _mainCamera;

        private void Awake()
        {
            _playerInteraction = GetComponent<PlayerInteraction>();
            _mainCamera = Camera.main;
            SetupTooltipCanvas();
        }

        private void SetupTooltipCanvas()
        {
            // 1. Create a World Space Canvas
            GameObject canvasObj = new GameObject("GeneratedTooltipCanvas");
            _canvas = canvasObj.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;
            _canvas.sortingOrder = 999; // Always draw on top of typical world objects
            
            // 2. Add TextMeshPro
            GameObject textObj = new GameObject("TooltipText");
            textObj.transform.SetParent(canvasObj.transform, false);
            
            _textMesh = textObj.AddComponent<TextMeshProUGUI>();
            _textMesh.alignment = TextAlignmentOptions.Center;
            _textMesh.fontSize = fontSize;
#pragma warning disable 0618 // suppress obsolete warning
            _textMesh.enableWordWrapping = false;
#pragma warning restore 0618
            
            // Apply Colors and outline (requires valid TMP font asset, usually defaults work)
            _textMesh.color = fontColor;
            _textMesh.outlineWidth = outlineWidth;
            _textMesh.outlineColor = outlineColor;
            
            // 3. Size it appropriately
            canvasObj.transform.localScale = canvasScale;
            _canvas.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (_mainCamera == null) _mainCamera = Camera.main;

            // Apply font settings dynamically in case they are changed in Inspector
            if (_textMesh != null)
            {
                if (fontAsset != null && _textMesh.font != fontAsset)
                {
                    _textMesh.font = fontAsset;
                }
                
                _textMesh.fontSize = fontSize;
                
                // Multiply the color by the brightness factor to give it an HDR pop
                Color finalColor = fontColor * colorBrightness;
                finalColor.a = fontColor.a; // preserve original alpha
                _textMesh.color = finalColor;

                // For outlines to work properly via script in TMP, the underlying material 
                // properties must be modified. We update the instantiated fontMaterial directly.
                if (_textMesh.fontMaterial != null)
                {
                    _textMesh.fontMaterial.SetFloat(ShaderUtilities.ID_OutlineWidth, outlineWidth);
                    _textMesh.fontMaterial.SetColor(ShaderUtilities.ID_OutlineColor, outlineColor);
                    _textMesh.UpdateMeshPadding(); // Crucial for outline bleeding boundaries
                }
            }

            float range = _playerInteraction.GetInteractionRange();
            Collider[] colliders = Physics.OverlapSphere(transform.position, range);

            GameObject closestObj = null;
            float closestDist = float.MaxValue;
            string promptText = "";

            // Evaluate in specific priority order to match the player's interaction logic

            string multiTag = _playerInteraction.GetMultiInteractTag();
            string npcTag = _playerInteraction.GetNpcTag();
            string interactableTag = _playerInteraction.GetInteractableTag();
            string sackTag = _playerInteraction.GetSackTag();

            // 1. Doors have absolute priority for Multi-Interact
            foreach (var col in colliders)
            {
                if (col.CompareTag(multiTag) || (col.transform.parent != null && col.transform.parent.CompareTag(multiTag)))
                {
                    float d = Vector3.Distance(transform.position, col.transform.position);
                    if (d < closestDist)
                    {
                        closestDist = d;
                        closestObj = col.gameObject;
                        promptText = "Hold E to Start the day";
                    }
                }
            }

            // 2. NPCs jump the queue if no doors are nearby
            if (closestObj == null)
            {
                foreach (var col in colliders)
                {
                    if (col.CompareTag(npcTag))
                    {
                        float d = Vector3.Distance(transform.position, col.transform.position);
                        if (d < closestDist)
                        {
                            closestDist = d;
                            closestObj = col.gameObject;
                            promptText = "Hold E to take order";
                        }
                    }
                }
            }

            // 3. All other Interactables / Sacks
            if (closestObj == null)
            {
                foreach (var col in colliders)
                {
                    if (col.CompareTag(interactableTag) || col.CompareTag(sackTag))
                    {
                        float d = Vector3.Distance(transform.position, col.transform.position);
                        if (d < closestDist)
                        {
                            closestDist = d;
                            closestObj = col.gameObject;
                            
                            // Determine logic via components
                            if (col.GetComponentInParent<Shelf>() != null)
                            {
                                if (_playerInteraction.IsHoldingSack())
                                    promptText = "Press F to replenish Shelf";
                                else
                                    promptText = "Hold E to get Ingredients";
                            }
                            else if (col.GetComponentInParent<Cauldron>() != null)
                            {
                                promptText = "Hold E to mix ingredients\nPress F during minigame";
                            }
                            else if (col.GetComponentInParent<AlchemistTable>() != null)
                            {
                                promptText = "Hold E to Open Shop";
                            }
                            else
                            {
                                promptText = "Hold E to interact";
                            }
                        }
                    }
                }
            }

            // 4. Update the UI Position and Text
            if (closestObj != null && !string.IsNullOrEmpty(promptText))
            {
                _canvas.gameObject.SetActive(true);
                _textMesh.text = promptText;

                // Position the tooltip at the right side of the Player (Genshin style)
                Vector3 targetPos = transform.position; // This script is on the Player
                
                // We want it to be to the right of the camera's view of the player,
                // or just to the right of the player's local right vector.
                // Using camera right vector keeps it readable regardless of player rotation.
                if (_mainCamera != null)
                {
                    targetPos += _mainCamera.transform.right * xOffset;
                }
                else
                {
                    targetPos += transform.right * xOffset;
                }
                
                targetPos.y += yOffset;
                
                _canvas.transform.position = targetPos;

                // Make the text face the camera perfectly (Billboard effect)
                if (_mainCamera != null)
                {
                    _canvas.transform.rotation = _mainCamera.transform.rotation;
                }
            }
            else
            {
                _canvas.gameObject.SetActive(false);
            }
        }
    }
}
