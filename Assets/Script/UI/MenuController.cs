using UnityEngine;

namespace Script.UI
{
    public class MenuController : MonoBehaviour
    {
        [Header("Canvas References")]
        [Tooltip("The Canvases to turn OFF when this button is clicked")]
        public GameObject[] canvasesToHide;

        [Tooltip("The Canvases to turn ON when this button is clicked")]
        public GameObject[] canvasesToShow;

        /// <summary>
        /// Links to a UI Button's OnClick() event to switch between menus.
        /// </summary>
        public void SwitchMenu()
        {
            if (canvasesToHide != null)
            {
                foreach (GameObject canvas in canvasesToHide)
                {
                    if (canvas != null) canvas.SetActive(false);
                }
            }

            if (canvasesToShow != null)
            {
                foreach (GameObject canvas in canvasesToShow)
                {
                    if (canvas != null) canvas.SetActive(true);
                }
            }
        }
    }
}
