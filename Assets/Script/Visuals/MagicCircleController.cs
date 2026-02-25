using UnityEngine;

namespace Script.Visuals
{
    [RequireComponent(typeof(MeshRenderer))]
    [RequireComponent(typeof(MeshFilter))]
    public class MagicCircleController : MonoBehaviour
    {
        [Header("Shader Settings")]
        [SerializeField] private Shader magicCircleShader;
        [ColorUsage(true, true)]
        [SerializeField] private Color circleColor = new Color(0, 1, 1, 1);
        [SerializeField] private float rotationSpeed = 1.0f;
        [SerializeField] private float radius = 2.0f; // World scale

        [Header("Particle Settings")]
        [SerializeField] private Shader particleShader;
        [SerializeField] private int maxParticles = 50;
        
        private Material _materialInstance;
        private Material _particleMaterialInstance;
        private MeshRenderer _meshRenderer;
        private MeshFilter _meshFilter;
        private ParticleSystem _particleSystem;
        private ParticleSystemRenderer _particleRenderer;

        private void Awake()
        {
            _meshRenderer = GetComponent<MeshRenderer>();
            _meshFilter = GetComponent<MeshFilter>();
            _particleSystem = GetComponent<ParticleSystem>();
            if (_particleSystem == null) _particleSystem = gameObject.AddComponent<ParticleSystem>();
            _particleRenderer = GetComponent<ParticleSystemRenderer>();

            SetupMesh();
            SetupMaterial();
            SetupParticles();
        }

        private void SetupMesh()
        {
            // If no mesh is assigned, create a simple Quad/Plane
            if (_meshFilter.sharedMesh == null)
            {
                GameObject primitive = GameObject.CreatePrimitive(PrimitiveType.Quad);
                _meshFilter.sharedMesh = primitive.GetComponent<MeshFilter>().sharedMesh;
                Destroy(primitive); // Cleanup the temp object
            }
            
            // Apply scale
            transform.localScale = new Vector3(radius, radius, 1);
            transform.localRotation = Quaternion.Euler(90, 0, 0); 
        }

        private void SetupMaterial()
        {
            if (magicCircleShader == null)
            {
                magicCircleShader = Shader.Find("Custom/MagicCircle");
            }

            if (magicCircleShader != null)
            {
                _materialInstance = new Material(magicCircleShader);
                _materialInstance.SetColor("_Color", circleColor);
                _materialInstance.SetFloat("_Speed", rotationSpeed);
                _meshRenderer.material = _materialInstance;
            }
        }

        private void SetupParticles()
        {
            if (particleShader == null) particleShader = Shader.Find("Custom/MagicParticle");
            
            if (_particleMaterialInstance == null && particleShader != null)
            {
                _particleMaterialInstance = new Material(particleShader);
                _particleMaterialInstance.SetColor("_Color", circleColor);
            }

            if (_particleRenderer != null && _particleMaterialInstance != null)
            {
                _particleRenderer.material = _particleMaterialInstance;
                _particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            }

            var main = _particleSystem.main;
            main.startColor = circleColor;
            main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.3f);
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.0f); // Shorter lifetime
            main.maxParticles = maxParticles;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World; // Use World space for predictable "Up"

            var emission = _particleSystem.emission;
            emission.rateOverTime = 20;

            var shape = _particleSystem.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.4f;
            shape.rotation = new Vector3(90, 0, 0); // Rotate shape to match correct emission plane if needed, but World velocity overrides direction

            var velocity = _particleSystem.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World; // Ensure velocity is in World space
            velocity.y = new ParticleSystem.MinMaxCurve(1.0f, 2.0f); // Upwards in World Y
            velocity.x = 0;
            velocity.z = 0; 
            
            var sizeOverLifetime = _particleSystem.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            AnimationCurve curve = new AnimationCurve();
            curve.AddKey(0.0f, 0.0f);
            curve.AddKey(0.2f, 1.0f);
            curve.AddKey(1.0f, 0.0f);
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1.0f, curve);

            if (!_particleSystem.isPlaying) _particleSystem.Play();
        }

        private void Update()
        {
            if (_materialInstance != null)
            {
                _materialInstance.SetColor("_Color", circleColor);
                _materialInstance.SetFloat("_Speed", rotationSpeed);
            }

            // Sync particle color
            if (_particleMaterialInstance != null)
            {
                 _particleMaterialInstance.SetColor("_Color", circleColor);
            }

            // Sync scale
            transform.localScale = new Vector3(radius, radius, 1);
        }

        private void OnDestroy()
        {
            if (_materialInstance != null) Destroy(_materialInstance);
            if (_particleMaterialInstance != null) Destroy(_particleMaterialInstance);
        }
    }
}
