using UnityEngine;
using UnityEngine.UI;

namespace Script.Environment
{
    [RequireComponent(typeof(NPC))]
    public class NPCPatienceMeter : MonoBehaviour
    {
        [Header("Positioning")]
        [Tooltip("How high above the NPC the meter floats")]
        public float yOffset = 2.2f;
        [Tooltip("Scale of the world-space canvas")]
        public Vector3 canvasScale = new Vector3(0.01f, 0.01f, 0.01f);
        
        [Header("Meter Appearance")]
        public Vector2 barSize = new Vector2(100f, 15f);
        public Color backgroundColor = new Color(0, 0, 0, 0.5f);
        public Color fillColor = Color.green;
        public Color warningColor = Color.red;
        [Tooltip("At what percentage left should the color turn red?")]
        [Range(0f, 1f)] public float warningThreshold = 0.25f;

        private NPC _npc;
        private Canvas _canvas;
        private Image _fillImage;
        private Camera _mainCamera;

        private void Awake()
        {
            _npc = GetComponent<NPC>();
            _mainCamera = Camera.main;
            SetupCanvas();
        }

        private void SetupCanvas()
        {
            // 1. Create a World Space Canvas
            GameObject canvasObj = new GameObject("PatienceMeterCanvas");
            canvasObj.transform.SetParent(transform, false);
            canvasObj.transform.localPosition = new Vector3(0, yOffset, 0);
            
            _canvas = canvasObj.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;
            _canvas.sortingOrder = 998; // Draw slightly behind interaction tooltips (999)
            
            canvasObj.transform.localScale = canvasScale;

            // 2. Add Background Image
            GameObject bgObj = new GameObject("Background");
            bgObj.transform.SetParent(canvasObj.transform, false);
            Image bgImage = bgObj.AddComponent<Image>();
            bgImage.color = backgroundColor;
            bgImage.rectTransform.sizeDelta = barSize;

            // 3. Add Fill Image
            GameObject fillObj = new GameObject("Fill");
            fillObj.transform.SetParent(canvasObj.transform, false);
            _fillImage = fillObj.AddComponent<Image>();
            _fillImage.color = fillColor;
            
            // Set up Image to be a standard unfilled rectangle we can dynamically resize
            _fillImage.rectTransform.pivot = new Vector2(0, 0.5f); // Pivot left
            _fillImage.rectTransform.anchorMin = new Vector2(0, 0.5f);
            _fillImage.rectTransform.anchorMax = new Vector2(0, 0.5f);
            _fillImage.rectTransform.anchoredPosition = new Vector2(-barSize.x / 2f, 0);
            _fillImage.rectTransform.sizeDelta = barSize;

            // Start hidden
            _canvas.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (_mainCamera == null) _mainCamera = Camera.main;

            if (_npc.IsWaiting())
            {
                _canvas.gameObject.SetActive(true);

                // Billboard effect (Face the camera)
                if (_mainCamera != null)
                {
                    _canvas.transform.rotation = _mainCamera.transform.rotation;
                }

                // Retrieve precise time percentage
                float percentageLeft = _npc.GetWaitPercentage();

                // 1.0 = Full Time Left, 0.0 = Time completely run out.
                // The NPC counts UP, so the formula is: 1f - (timeElapsed / waitTime)
                
                // Adjust bar width based on percentage
                float currentWidth = barSize.x * percentageLeft;
                _fillImage.rectTransform.sizeDelta = new Vector2(currentWidth, barSize.y);

                // Change color if running low
                if (percentageLeft <= warningThreshold)
                {
                    _fillImage.color = warningColor;
                }
                else
                {
                    _fillImage.color = fillColor;
                }
            }
            else
            {
                _canvas.gameObject.SetActive(false);
            }
        }
    }
}
