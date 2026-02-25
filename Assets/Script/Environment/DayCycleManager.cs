using TMPro;
using UnityEngine;
using Script.Systems;

namespace Script.Environment
{
    public class DayCycleManager : MonoBehaviour
    {
        public static DayCycleManager Instance { get; private set; }
        [Header("Time Implementation")]
        [Tooltip("How long the shop stays open in real life MINUTES (e.g., 5 = 5 minutes)")]
        [SerializeField] private float workingDurationMinutes = 5f;
        
        [Header("UI References")]
        [SerializeField] private TMP_Text dayText;
        [SerializeField] private TMP_Text timeRemainingText;

        public int CurrentDay { get; private set; } = 1;
        public bool IsWorkingHours { get; private set; } = false;
        
        [Tooltip("Current Visual Time (0 - 24)")]
        [Range(0, 24)]
        [SerializeField] private float timeOfDay;

        private float _workingTimeRemaining;
        private float _totalWorkingSeconds;

        [Header("Skybox Settings")]
        [SerializeField] private Material skyboxSunrise;
        [SerializeField] private Material skyboxMorning;
        [SerializeField] private Material skyboxAfternoon;
        [SerializeField] private Material skyboxNight;

        [Header("Ambient Lighting")]
        [SerializeField] private Gradient ambientColor;
        [SerializeField] private Gradient fogColor;

        [Header("Settings")]
        [SerializeField] private float startHour = 6f; // 6 AM
        [SerializeField] private float closeHour = 18f; // 6 PM

        [Header("Celestial Bodies")]
        [SerializeField] private Light sunLight;
        [SerializeField] private Gradient sunColor;
        [SerializeField] private Light moonLight;
        [SerializeField] private Gradient moonColor;
        [SerializeField] private float maxSunIntensity = 1f;
        [SerializeField] private float maxMoonIntensity = 0.5f;
        [SerializeField] private LightShadows shadowType = LightShadows.Soft;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            _totalWorkingSeconds = workingDurationMinutes * 60f;
            timeOfDay = startHour;
            RenderSettings.fog = true; // Ensure fog is enabled for the gradient to work
            
            UpdateSkybox();
            UpdateSunMoon();
            UpdateUI();
        }

        private void Update()
        {
            // Handle gameplay timer and sync visual day cycle
            if (IsWorkingHours)
            {
                _workingTimeRemaining -= Time.deltaTime;
                
                // Map the remaining time directly to the visual time of day
                // When remaining == total, time = startHour
                // When remaining == 0, time = closeHour
                float progress = 1f - (_workingTimeRemaining / _totalWorkingSeconds);
                timeOfDay = Mathf.Lerp(startHour, closeHour, progress);

                if (_workingTimeRemaining <= 0)
                {
                    _workingTimeRemaining = 0;
                    timeOfDay = closeHour;
                    IsWorkingHours = false;
                    Debug.Log("[DayCycleManager] Working hours have ended! Player can now close the door.");
                }
                
                UpdateSkybox();
                UpdateSunMoon();
                UpdateUI();
            }
        }

        public void StartWorkingHours()
        {
            if (IsWorkingHours) return;

            // Update total seconds in case they changed it in the inspector mid-game
            _totalWorkingSeconds = workingDurationMinutes * 60f;
            
            IsWorkingHours = true;
            _workingTimeRemaining = _totalWorkingSeconds;
            
            Debug.Log("[DayCycleManager] Shop is now OPEN!");
            UpdateUI();
        }

        public void EndDay()
        {
            CurrentDay++;
            timeOfDay = startHour; // Reset visual time to morning
            IsWorkingHours = false;
            
            Debug.Log($"[DayCycleManager] Day ended! Welcome to Day {CurrentDay}.");

            if (EventManager.Instance != null)
            {
                EventManager.Instance.EndDayTick();
            }

            UpdateUI();
        }

        private void UpdateUI()
        {
            if (dayText != null)
                dayText.text = $"Day {CurrentDay}";

            if (timeRemainingText != null)
            {
                if (IsWorkingHours)
                {
                    int minutes = Mathf.FloorToInt(_workingTimeRemaining / 60F);
                    int seconds = Mathf.FloorToInt(_workingTimeRemaining - minutes * 60);
                    timeRemainingText.text = string.Format("{0:00}:{1:00}", minutes, seconds);
                }
                else
                {
                    // Shop is closed or timer hit 0
                    timeRemainingText.text = "00:00";
                }
            }
        }

        private void UpdateSunMoon()
        {
            float sunRotation = (timeOfDay - 6f) * 15f; // 0 at 6 AM, 90 at 12 PM, 180 at 6 PM
            float moonRotation = (timeOfDay - 18f) * 15f; // Opposite to sun

            float timePercent = timeOfDay / 24f;

            if (sunLight != null)
            {
                sunLight.transform.rotation = Quaternion.Euler(sunRotation, -30f, 0f);
                sunLight.color = sunColor.Evaluate(timePercent);
                sunLight.shadows = shadowType; // Enforce shadows
                
                // Fade logic (Simple based on time or angle)
                float intensity = 0;
                if (timeOfDay >= 5 && timeOfDay <= 19)
                {
                    intensity = Mathf.Clamp01(1 - Mathf.Abs(12 - timeOfDay) / 7f); 
                }
                sunLight.intensity = intensity * maxSunIntensity;

                if (sunLight.intensity > 0 && RenderSettings.sun != sunLight)
                {
                    RenderSettings.sun = sunLight;
                }
            }

            if (moonLight != null)
            {
                moonLight.transform.rotation = Quaternion.Euler(moonRotation, -30f, 0f);
                moonLight.color = moonColor.Evaluate(timePercent);
                moonLight.shadows = shadowType; // Enforce shadows

                float intensity = 0;
                if (timeOfDay <= 6 || timeOfDay >= 18)
                {
                    float t = timeOfDay < 12 ? timeOfDay + 24 : timeOfDay; 
                    intensity = Mathf.Clamp01(1 - Mathf.Abs(24 - t) / 6f);
                }
                moonLight.intensity = intensity * maxMoonIntensity;

                if (moonLight.intensity > 0 && sunLight.intensity <= 0 && RenderSettings.sun != moonLight)
                {
                    RenderSettings.sun = moonLight;
                }
            }
        }

        private void UpdateSkybox()
        {
            Material currentSkybox = RenderSettings.skybox;
            
            // Update Ambient and Fog from Gradient
            float timePercent = timeOfDay / 24f;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = ambientColor.Evaluate(timePercent);
            RenderSettings.fogColor = fogColor.Evaluate(timePercent);

            if (timeOfDay >= 5 && timeOfDay < 7) // Sunrise
            {
                if (currentSkybox != skyboxSunrise) 
                {
                    RenderSettings.skybox = skyboxSunrise;
                    DynamicGI.UpdateEnvironment();
                }
            }
            else if (timeOfDay >= 7 && timeOfDay < 12) // Morning
            {
                if (currentSkybox != skyboxMorning) 
                {
                    RenderSettings.skybox = skyboxMorning;
                    DynamicGI.UpdateEnvironment();
                }
            }
            else if (timeOfDay >= 12 && timeOfDay < 18) // Afternoon
            {
                if (currentSkybox != skyboxAfternoon) 
                {
                    RenderSettings.skybox = skyboxAfternoon;
                    DynamicGI.UpdateEnvironment();
                }
            }
            else // Night
            {
                if (currentSkybox != skyboxNight) 
                {
                    RenderSettings.skybox = skyboxNight;
                    DynamicGI.UpdateEnvironment();
                }
            }
        }

        // Removed the old EndDay() that just fast-forwarded the visual time, 
        // as we replaced it with the new gameplay EndDay() method above.
        // ==========================================
        // DATA ACCESS & SAVING
        // ==========================================
        
        public float GetTimeOfDay() => timeOfDay;
        public float GetWorkingTimeRemaining() => _workingTimeRemaining;

        public void LoadFromSave(GameSaveData data)
        {
            CurrentDay = data.currentDay;
            timeOfDay = data.timeOfDay;
            IsWorkingHours = data.isWorkingHours;
            _workingTimeRemaining = data.workingTimeRemaining;

            _totalWorkingSeconds = workingDurationMinutes * 60f;

            UpdateSkybox();
            UpdateSunMoon();
            UpdateUI();
        }
    }
}
