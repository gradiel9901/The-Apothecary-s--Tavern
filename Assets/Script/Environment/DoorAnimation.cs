using System.Collections;
using Script.Player;
using Script.Systems;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;

namespace Script.Environment
{
    public class DoorAnimation : MonoBehaviour, IInteractable
    {
        /// <summary>
        /// Fired when any door finishes its open animation.
        /// NPCSpawner and NPCController subscribe to this.
        /// </summary>
        public static event System.Action OnDoorOpened;

        [Header("Animation Settings")]
        [Tooltip("If true, the door will move to the exact Absolute Open Position coordinates instead of adding the Offset.")]
        [SerializeField] private bool useAbsolutePosition = false;
        [SerializeField] private Vector3 absoluteOpenPosition;
        [SerializeField] private Vector3 openPositionOffset;
        [SerializeField] private Vector3 openRotationOffset = new Vector3(0, 90f, 0);
        [SerializeField] private float animationDuration = 1.0f;
        [SerializeField] private AnimationCurve animationCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("NavMesh Integration")]
        [Tooltip("Optional: Assign a NavMeshObstacle (Carve = ON) placed in the doorway. " +
                 "Enabled when the door is closed (blocks the path), disabled when open (restores it). " +
                 "Requires the NavMesh to be pre-baked with the door OPEN.")]
        [SerializeField] private NavMeshObstacle doorObstacle;

        [Tooltip("Optional: Assign the NavMeshSurface so the path can rebuild when the door opens. " +
                 "This fixes issues where the floor under the door wasn't baked initially.")]
        [SerializeField] private NavMeshSurface navMeshSurface;

        private Vector3    _closedPosition;
        private Quaternion _closedRotation;
        private Vector3    _openPosition;
        private Quaternion _openRotation;

        private bool      _isOpen = false;
        private Coroutine _animationCoroutine;

        private void Awake()
        {
            _closedPosition = transform.localPosition;
            _closedRotation = transform.localRotation;

            _openPosition = useAbsolutePosition ? absoluteOpenPosition : _closedPosition + openPositionOffset;
            _openRotation = _closedRotation * Quaternion.Euler(openRotationOffset);

            // Door starts closed — obstacle should be active
            if (doorObstacle != null) doorObstacle.enabled = true;
        }

        public void Interact(PlayerInteraction player)
        {
            ToggleDoor();
        }

        public void ToggleDoor()
        {
            // If the door is OPEN, and the shop is currently in Working Hours, we cannot close it.
            if (_isOpen && DayCycleManager.Instance != null && DayCycleManager.Instance.IsWorkingHours)
            {
                Debug.Log("Cannot close the door while the shop is open! Wait for working hours to end.");
                return;
            }

            _isOpen = !_isOpen;

            // Trigger Day Cycle and Audio logic
            if (_isOpen)
            {
                if (AudioManager.Instance != null) AudioManager.Instance.PlayDoorOpen();
                
                if (DayCycleManager.Instance != null)
                {
                    DayCycleManager.Instance.StartWorkingHours();
                }
            }
            else
            {
                if (AudioManager.Instance != null) AudioManager.Instance.PlayDoorClose();
                
                if (DayCycleManager.Instance != null)
                {
                    DayCycleManager.Instance.EndDay();
                }
            }

            if (_animationCoroutine != null)
                StopCoroutine(_animationCoroutine);

            Vector3    targetPosition = _isOpen ? _openPosition : _closedPosition;
            Quaternion targetRotation = _isOpen ? _openRotation : _closedRotation;

            _animationCoroutine = StartCoroutine(AnimateDoor(targetPosition, targetRotation));
        }

        private IEnumerator AnimateDoor(Vector3 targetPosition, Quaternion targetRotation)
        {
            Vector3    startPosition = transform.localPosition;
            Quaternion startRotation = transform.localRotation;
            float elapsedTime = 0f;

            while (elapsedTime < animationDuration)
            {
                elapsedTime += Time.deltaTime;
                float t          = elapsedTime / animationDuration;
                float curveValue = animationCurve.Evaluate(t);

                transform.localPosition = Vector3.LerpUnclamped(startPosition, targetPosition, curveValue);
                transform.localRotation = Quaternion.LerpUnclamped(startRotation, targetRotation, curveValue);

                yield return null;
            }

            // Snap to exact target
            transform.localPosition = targetPosition;
            transform.localRotation = targetRotation;
            _animationCoroutine = null;

            if (doorObstacle != null)
                doorObstacle.enabled = !_isOpen;

            if (_isOpen && navMeshSurface != null)
            {
                // Rebuild using PhysicsColliders to avoid mesh read/write access errors
                navMeshSurface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
                navMeshSurface.BuildNavMesh();
            }

            // Notify any listeners (NPCSpawner, NPCController) the door is fully open
            if (_isOpen)
                OnDoorOpened?.Invoke();
        }
    }
}
