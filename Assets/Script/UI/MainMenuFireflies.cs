using UnityEngine;

namespace Script.UI
{
    /// <summary>
    /// Attach this script to any empty GameObject in your Main Menu scene.
    /// It will automatically generate and configure a beautiful firefly particle system.
    /// </summary>
    [RequireComponent(typeof(ParticleSystem))]
    public class MainMenuFireflies : MonoBehaviour
    {
        [Header("Camera Attachment")]
        [Tooltip("If true, automatically snaps the particles in front of the Main Camera.")]
        public bool attachToCamera = true;
        
        [Tooltip("How far forward from the camera lens the fireflies spawn.")]
        public float distanceFromCamera = 10f;

        [Header("Firefly Settings")]
        [Tooltip("The color the fireflies will glow.")]
        public Color fireflyColor = new Color(1f, 0.8f, 0.2f, 1f); // Warm yellow/orange

        [Tooltip("How many fireflies are on screen roughly.")]
        public int maxAmount = 50;

        [Tooltip("How large of a box area the fireflies can spawn within.")]
        public Vector3 spawnAreaScale = new Vector3(30f, 20f, 10f); // 3D world space bounds

        [Tooltip("How fast the fireflies wander around.")]
        public float wanderSpeed = 1f;

        [Tooltip("The size of the individual firefly light.")]
        public float particleSize = 0.5f; // 3D world size, larger enough to see

        [Header("Build Material Fix")]
        [Tooltip("If your particles are pink squares in the final build, drag ANY default particle material here! Unity strips script-generated materials from builds.")]
        public Material overrideMaterial;

        private ParticleSystem _ps;

        private void Awake()
        {
            if (attachToCamera)
            {
                Camera cam = Camera.main;
                if (cam != null)
                {
                    // Force this GameObject to sit exactly in front of the camera lens!
                    transform.SetParent(cam.transform, false);
                    transform.localPosition = new Vector3(0, 0, distanceFromCamera);
                    transform.localRotation = Quaternion.identity;
                    transform.localScale = Vector3.one;
                }
            }

            _ps = GetComponent<ParticleSystem>();

            // If the user hasn't supplied a custom material, we'll try to use Unity's default particle material
            ParticleSystemRenderer renderer = GetComponent<ParticleSystemRenderer>();
            
            // Apply the user's override material if one was provided to bypass build stripping
            if (overrideMaterial != null)
            {
                renderer.sharedMaterial = overrideMaterial;
                renderer.sharedMaterial.mainTexture = CreateGlowTexture();
            }
            else if (renderer.sharedMaterial == null || renderer.sharedMaterial.name == "Default-Material" || renderer.sharedMaterial.name == "Default-Particle")
            {
                // Unity's default additive soft particle material (Prone to stripping in builds)
                Material defaultMat = new Material(Shader.Find("Particles/Standard Unlit"));
                defaultMat.SetInt("_BlendOp", 0); // Add
                defaultMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                defaultMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
                defaultMat.SetInt("_ZWrite", 0);
                defaultMat.DisableKeyword("_ALPHATEST_ON");
                defaultMat.EnableKeyword("_ALPHABLEND_ON");
                defaultMat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                defaultMat.renderQueue = 3000;
                
                // Assign a procedurally generated soft glow circle texture so it isn't a square
                defaultMat.mainTexture = CreateGlowTexture();
                
                renderer.sharedMaterial = defaultMat;
            }

            ConfigureParticleSystem();
        }

        private Texture2D CreateGlowTexture()
        {
            int size = 64;
            Texture2D tex = new Texture2D(size, size, TextureFormat.ARGB32, false);
            Vector2 center = new Vector2(size / 2f, size / 2f);
            float radius = size / 2f;
            
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    float alpha = Mathf.Clamp01(1f - (dist / radius));
                    
                    // Create a soft bell-curve glow
                    alpha = Mathf.Pow(alpha, 1.5f);
                    
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            tex.Apply();
            return tex;
        }

        private void ConfigureParticleSystem()
        {
            // === Main Module ===
            var main = _ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.duration = 5f;
            
            // Random lifetime between 3 to 7 seconds
            main.startLifetime = new ParticleSystem.MinMaxCurve(3f, 7f);
            
            // Very slow upward/wandering speed
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, wanderSpeed);
            
            // Size
            main.startSize = new ParticleSystem.MinMaxCurve(particleSize * 0.5f, particleSize * 1.5f);
            
            main.maxParticles = maxAmount;

            // === Emission ===
            var emission = _ps.emission;
            emission.enabled = true;
            emission.rateOverTime = maxAmount / 3f; // Stable spawn rate based on lifetime
            
            // === Shape ===
            var shape = _ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = spawnAreaScale;
            // Spawn inside the entire box volume
            shape.randomDirectionAmount = 1f;
            shape.sphericalDirectionAmount = 1f;

            // === Color Over Lifetime (Glow logic) ===
            var colorOverLife = _ps.colorOverLifetime;
            colorOverLife.enabled = true;

            // Create a gradient that fades in from 0 alpha, stays bright, then fades back to 0
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] { 
                    new GradientColorKey(fireflyColor, 0.0f), 
                    new GradientColorKey(fireflyColor, 1.0f) 
                },
                new GradientAlphaKey[] { 
                    new GradientAlphaKey(0.0f, 0.0f),  // Start invisible
                    new GradientAlphaKey(1.0f, 0.2f),  // Fade in
                    new GradientAlphaKey(1.0f, 0.8f),  // Stay visible 
                    new GradientAlphaKey(0.0f, 1.0f)   // Fade out dying
                }
            );
            colorOverLife.color = new ParticleSystem.MinMaxGradient(grad);

            // === Noise Module (Bug Wander logic) ===
            var noise = _ps.noise;
            noise.enabled = true;
            noise.strength = 0.5f;
            noise.frequency = 0.5f;
            noise.scrollSpeed = 0.5f;
            noise.damping = true;
            
            // Apply all changes
            if (!_ps.isPlaying)
            {
                _ps.Play();
            }
        }
    }
}
