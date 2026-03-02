using UnityEngine;

namespace Script.Systems
{
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Header("Audio Sources")]
        [Tooltip("Source dedicated strictly to footsteps/movement looping.")]
        [SerializeField] private AudioSource movementSource;
        [Tooltip("Source dedicated to one-shot sound effects (doors, mixing, items).")]
        [SerializeField] private AudioSource sfxSource;
        [Tooltip("Source dedicated to Background Music looping.")]
        [SerializeField] private AudioSource bgmSource;
        [Tooltip("Source dedicated to Cauldron bubbling looping.")]
        [SerializeField] private AudioSource bubblingSource;

        [Header("Background Music")]
        public AudioClip bgmClip;

        [Header("Player Movement Sounds")]
        public AudioClip walkClip;
        public AudioClip runClip;

        [Header("NPC Sounds")]
        public AudioClip npcTalkClip;

        [Header("Cauldron Sounds")]
        public AudioClip mixHitClip;
        public AudioClip mixCritClip;
        public AudioClip mixMissClip;
        [Tooltip("Looping bubbling sound played during mixing.")]
        public AudioClip bubblingClip;

        [Header("Door Sounds")]
        public AudioClip doorOpenClip;
        public AudioClip doorCloseClip;

        [Header("Item Sounds")]
        public AudioClip itemPickupClip;
        public AudioClip itemDropClip;

        [Header("UI Sounds")]
        public AudioClip buttonClickClip;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // Ensure AudioSources exist if not assigned manually
            if (movementSource == null)
            {
                movementSource = gameObject.AddComponent<AudioSource>();
                movementSource.loop = true;
                movementSource.playOnAwake = false;
            }

            if (sfxSource == null)
            {
                sfxSource = gameObject.AddComponent<AudioSource>();
                sfxSource.loop = false;
                sfxSource.playOnAwake = false;
            }

            if (bgmSource == null)
            {
                bgmSource = gameObject.AddComponent<AudioSource>();
                bgmSource.loop = true;
                bgmSource.playOnAwake = true;
                bgmSource.volume = 0.5f; // Reasonable default
            }

            if (bgmSource != null && bgmClip != null)
            {
                bgmSource.clip = bgmClip;
                bgmSource.Play();
            }

            if (bubblingSource == null)
            {
                bubblingSource = gameObject.AddComponent<AudioSource>();
                bubblingSource.loop = true;
                bubblingSource.playOnAwake = false;
                bubblingSource.spatialBlend = 0f; // 2D Audio
                bubblingSource.volume = 0.6f;
            }
        }

        // ==========================================
        // MOVEMENT
        // ==========================================

        public void PlayMovement(bool isRunning)
        {
            if (movementSource == null) return;

            AudioClip desiredClip = isRunning ? runClip : walkClip;

            // If we are already playing the correct clip, don't interrupt it.
            if (movementSource.isPlaying && movementSource.clip == desiredClip)
            {
                return;
            }

            // Otherwise, switch clip and play
            if (desiredClip != null)
            {
                movementSource.clip = desiredClip;
                movementSource.Play();
            }
        }

        public void StopMovement()
        {
            if (movementSource != null && movementSource.isPlaying)
            {
                movementSource.Stop();
            }
        }

        // ==========================================
        // ONE-SHOT SOUND EFFECTS
        // ==========================================

        private void PlaySFX(AudioClip clip)
        {
            if (clip != null && sfxSource != null)
            {
                sfxSource.PlayOneShot(clip);
            }
        }

        public void PlayNPCTalk() => PlaySFX(npcTalkClip);

        public void PlayMixHit() => PlaySFX(mixHitClip);
        public void PlayMixCrit() => PlaySFX(mixCritClip);
        public void PlayMixMiss() => PlaySFX(mixMissClip);

        public void PlayDoorOpen() => PlaySFX(doorOpenClip);
        public void PlayDoorClose() => PlaySFX(doorCloseClip);

        public void PlayItemPickup() => PlaySFX(itemPickupClip);
        public void PlayItemDrop() => PlaySFX(itemDropClip);

        public void PlayButtonClick() => PlaySFX(buttonClickClip);

        public void PlayBubbling()
        {
            if (bubblingSource == null || bubblingClip == null) return;
            if (!bubblingSource.isPlaying)
            {
                bubblingSource.clip = bubblingClip;
                bubblingSource.Play();
            }
        }

        public void StopBubbling()
        {
            if (bubblingSource != null && bubblingSource.isPlaying)
            {
                bubblingSource.Stop();
            }
        }
    }
}
