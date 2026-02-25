using System.Collections;
using Script.Player;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;
using TMPro;
using System.Text.RegularExpressions;
using System.Collections.Generic;

namespace Script.Environment
{
    /// <summary>
    /// NPC that walks to a tavern counter via NavMesh, orders a potion from the player,
    /// and smartly detects whether the correct potion is handed over.
    /// 
    /// Setup:
    ///   1. Add this component to the NPC GameObject.
    ///   2. Tag the NPC GameObject as "NPC".
    ///   3. Assign the Counter Waypoint (position + rotation the NPC stands at).
    ///   4. Assign the Exit Waypoint (where NPC walks to before being destroyed).
    ///   5. Assign NavMeshSurface so the path can be rebuilt when the door is open.
    ///   6. Wire up the dialogue UI references (Panel, Texts, Buttons).
    ///   7. Add at least one PotionRecipe to "Available Orders".
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(Animator))]
    public class NPCController : MonoBehaviour, IInteractable, QueueManager.IQueueable
    {
        // ─── NPC Info ─────────────────────────────────────────────────────────────
        [Header("NPC Info")]
        [SerializeField] private string npcDisplayName = "Mysterious Stranger";

        [Tooltip("The NPC will randomly pick one recipe from this list to order.")]
        [SerializeField] private List<PotionRecipe> availableOrders;

        // ─── Navigation ───────────────────────────────────────────────────────────
        [Header("Navigation")]
        [Tooltip("Set automatically by NPCSpawner. You can also assign here if the NPC is placed directly in the scene.")]
        [SerializeField] private GameObject counterWaypoint;

        [Tooltip("Set automatically by NPCSpawner. You can also assign here if the NPC is placed directly in the scene.")]
        [SerializeField] private GameObject exitWaypoint;

        [SerializeField] private float stoppingDistance = 1.5f;

        [Tooltip("Seconds to wait before the NPC starts trying to walk. Spawned NPCs should use 0.")]
        [SerializeField] private float startDelay = 0f;

        // ─── UI References ────────────────────────────────────────────────────────
        [Header("UI References (Auto-finds by name if empty)")]
        [Tooltip("Name of the scene GameObject. If empty, the script will look for an object named exactly 'Dialogue Box Panel'")]
        [SerializeField] private GameObject dialoguePanel;
        [SerializeField] private TMP_Text npcNameText;
        [SerializeField] private TMP_Text dialogueText;
        [SerializeField] private Button acceptButton;
        [SerializeField] private Button noIngredientsButton;

        [Header("Auto-Find Names (if fields above are empty)")]
        [SerializeField] private string counterWaypointName = "Counter Waypoint";
        [SerializeField] private string exitWaypointName    = "Exit Waypoint";
        [SerializeField] private string dialoguePanelName   = "Dialogue Box Panel";
        [SerializeField] private string npcNameTextName     = "NPC name";
        [SerializeField] private string dialogueTextName    = "Dialogue Text";
        [SerializeField] private string acceptButtonName    = "Accept Button";
        [SerializeField] private string declineButtonName   = "No Ingredients Button";

        // ─── Dialogue Lines ───────────────────────────────────────────────────────
        [Header("Dialogue Lines")]
        [SerializeField] private string orderLine      = "I would like to order a {0}, please!";
        [SerializeField] private string waitingLine    = "I'll wait right here. Don't forget my {0}!";
        [SerializeField] private string wrongLine      = "Hmm, that's not what I ordered. I want a {0}!";
        [SerializeField] private string correctLine    = "Wonderful! This is exactly what I needed. Thank you!";
        [SerializeField] private string noStockLine    = "Oh... I understand. I'll find it somewhere else.";
        [SerializeField] private string remindLine     = "Still waiting for my {0}... no rush.";

        // ─── Animation ────────────────────────────────────────────────────────────
        [Header("Animation")]
        [Tooltip("Animator is added automatically. Assign a controller with these state names.")]
        [SerializeField] private string idleAnimName    = "Idle";
        [SerializeField] private string walkAnimName    = "Walking";
        [SerializeField] private string talkAnimName    = "Talking";
        [SerializeField] private float  animTransition  = 0.15f;

        // ─── State Machine ────────────────────────────────────────────────────────
        private enum State
        {
            Idle,
            RebuildingNavMesh,
            WalkingToCounter,
            AtCounter,
            WaitingForPotion,
            Satisfied,
            Declined,
            Leaving
        }

        private State        _state = State.Idle;
        private NavMeshAgent  _agent;
        private Animator      _animator;
        private PotionRecipe  _order;
        private string        _currentAnim;
        private float         _pathRetryTimer   = 0f;
        private const float   PathRetryInterval = 1.5f;

        // Queue Info
        private int _queueIndex = -1;

        // ─────────────────────────────────────────────────────────────────────────
        // Unity Lifecycle
        // ─────────────────────────────────────────────────────────────────────────
        private void Awake()
        {
            _agent    = GetComponent<NavMeshAgent>();
            _animator = GetComponent<Animator>();
            if (_animator == null)
                _animator = GetComponentInChildren<Animator>();

            // Auto-find missing scene references (since Prefabs can't store them)
            if (counterWaypoint == null) counterWaypoint = GameObject.Find(counterWaypointName);
            if (exitWaypoint == null)    exitWaypoint    = GameObject.Find(exitWaypointName);

            if (dialoguePanel == null)   dialoguePanel   = GameObject.Find(dialoguePanelName);

            if (npcNameText == null)     npcNameText     = FindComponentByName<TMP_Text>(npcNameTextName);
            if (dialogueText == null)    dialogueText    = FindComponentByName<TMP_Text>(dialogueTextName);
            if (acceptButton == null)    acceptButton    = FindComponentByName<Button>(acceptButtonName);
            if (noIngredientsButton == null) noIngredientsButton = FindComponentByName<Button>(declineButtonName);

            HideDialogue();
            PlayAnimation(idleAnimName);
        }

        /// <summary>
        /// Called by NPCSpawner right after instantiating this prefab so it can
        /// receive scene object references that a prefab cannot hold itself.
        /// </summary>
        public void SetWaypoints(GameObject counter, GameObject exit)
        {
            counterWaypoint = counter;
            exitWaypoint    = exit;
        }

        private void OnEnable()
        {
            // Listen for any door finishing its open animation
            DoorAnimation.OnDoorOpened += OnDoorOpened;
        }

        private void OnDisable()
        {
            DoorAnimation.OnDoorOpened -= OnDoorOpened;
        }

        private void Start()
        {
            // Wire up buttons
            if (acceptButton      != null) acceptButton.onClick.AddListener(OnAccept);
            if (noIngredientsButton != null) noIngredientsButton.onClick.AddListener(OnNoIngredients);

            // Pick a random order
            if (availableOrders != null && availableOrders.Count > 0)
                _order = availableOrders[Random.Range(0, availableOrders.Count)];

            StartCoroutine(BeginRoutine());
        }

        private void Update()
        {
            switch (_state)
            {
                case State.WalkingToCounter:
                    if (!_agent.pathPending && _agent.remainingDistance <= stoppingDistance)
                    {
                        _agent.ResetPath();
                        OnArriveAtCounter();
                    }
                    else
                    {
                        // Path validity watchdog: if the agent has an invalid or
                        // partial path (e.g. door was still closed), retry periodically.
                        if (_agent.pathStatus == NavMeshPathStatus.PathInvalid ||
                            _agent.pathStatus == NavMeshPathStatus.PathPartial)
                        {
                            _pathRetryTimer += Time.deltaTime;
                            if (_pathRetryTimer >= PathRetryInterval)
                            {
                                _pathRetryTimer = 0f;
                                RetryPath();
                            }
                        }
                    }
                    break;

                case State.Leaving:
                    if (!_agent.pathPending && _agent.remainingDistance <= stoppingDistance)
                        Destroy(gameObject);
                    break;
            }
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Navigation
        // ─────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Waits, then does a FULL NavMesh rebuild and sets the destination.
        /// BuildNavMesh() is used (not UpdateNavMesh) so the initial closed-door
        /// geometry is completely discarded and the open doorway is baked fresh.
        /// </summary>
        private IEnumerator BeginRoutine()
        {
            // Wait one frame so all components (NavMeshAgent etc.) fully initialize
            yield return null;

            if (counterWaypoint == null || _agent == null)
            {
                Debug.LogWarning($"[NPCController] {npcDisplayName}: Missing Counter Waypoint or NavMeshAgent!", this);
                yield break;
            }

            if (startDelay > 0f)
                yield return new WaitForSeconds(startDelay);

            _state = State.WalkingToCounter;
            PlayAnimation(walkAnimName);

            const float retryInterval = 0.5f;
            const float timeout       = 15f;
            float elapsed = 0f;

            while (elapsed < timeout)
            {
                Vector3 targetPos = counterWaypoint.transform.position;
                if (QueueManager.Instance != null && QueueManager.Instance.isActiveAndEnabled)
                {
                    targetPos = QueueManager.Instance.JoinQueue(this);
                }

                NavMeshPath path = new NavMeshPath();
                bool found = _agent.CalculatePath(targetPos, path);

                if (found && path.status == NavMeshPathStatus.PathComplete)
                {
                    _agent.stoppingDistance = stoppingDistance;
                    _agent.SetPath(path);
                    _pathRetryTimer = 0f;
                    Debug.Log($"[NPCController] {npcDisplayName}: Valid path found after {elapsed:F1}s — walking to counter.");
                    yield break;
                }

                Debug.Log($"[NPCController] {npcDisplayName}: Path status = {path.status} — retrying in {retryInterval}s... ({elapsed:F1}/{timeout}s)");
                elapsed += retryInterval;
                yield return new WaitForSeconds(retryInterval);
            }

            Debug.LogWarning($"[NPCController] {npcDisplayName}: Could not find a complete path to the counter after {timeout}s. " +
                             "Make sure the NavMesh covers both inside and outside the tavern and is rebuilt with the door open.");
        }

        /// <summary>
        /// Re-issues the counter destination without a full rebuild.
        /// Used by the path-validity watchdog in Update.
        /// </summary>
        private void RetryPath()
        {
            if (counterWaypoint == null || _agent == null) return;
            Debug.Log($"[NPCController] {npcDisplayName}: Path invalid — retrying destination.");
            
            // Note: Does not currently call JoinQueue again, relies on earlier JoinQueue index 
            // unless MoveUpLine overrides it.
            _agent.SetDestination(counterWaypoint.transform.position);
        }

        /// <summary>
        /// Called by DoorAnimation.OnDoorOpened.
        /// The NavMeshObstacle on the door is now disabled, so the carved path is
        /// instantly restored — just re-issue the destination.
        /// </summary>
        private void OnDoorOpened()
        {
            if (_state != State.WalkingToCounter && _state != State.RebuildingNavMesh && _state != State.Idle) return;

            Debug.Log($"[NPCController] {npcDisplayName}: Door opened — setting destination.");

            // Push to WalkingToCounter in case NPC was still idle waiting
            _state = State.WalkingToCounter;
            PlayAnimation(walkAnimName);
            _agent.stoppingDistance = stoppingDistance;
            _agent.SetDestination(counterWaypoint.transform.position);
            _pathRetryTimer = 0f;
        }

        private void OnArriveAtCounter()
        {
            bool isFront = QueueManager.Instance == null || !QueueManager.Instance.isActiveAndEnabled || QueueManager.Instance.IsFrontOfLine(this);
            if (isFront)
            {
                _state = State.AtCounter;
                if (counterWaypoint != null)
                    transform.rotation = counterWaypoint.transform.rotation;
            }
            else
            {
                 // Still in line somewhere, waiting for our turn
                 // Wait in the generic WalkingToCounter state but stopped
                 if (counterWaypoint != null)
                 {
                    Vector3 lookAt = counterWaypoint.transform.position;
                    lookAt.y = transform.position.y;
                    transform.LookAt(lookAt);
                 }
            }

            PlayAnimation(idleAnimName);
            Debug.Log($"[NPCController] {npcDisplayName} arrived at destination.");
        }

        // ─────────────────────────────────────────────────────────────────────────
        // IInteractable — called by PlayerInteraction when player presses Interact
        // ─────────────────────────────────────────────────────────────────────────
        public void Interact(PlayerInteraction player)
        {
            switch (_state)
            {
                case State.WalkingToCounter:
                    bool isFront = QueueManager.Instance == null || !QueueManager.Instance.isActiveAndEnabled || QueueManager.Instance.IsFrontOfLine(this);
                    if (!isFront && QueueManager.Instance != null && QueueManager.Instance.isActiveAndEnabled)
                    {
                        Debug.Log("Leave me alone, I'm waiting in line!");
                    }
                    break;
                case State.AtCounter:
                    OpenOrderDialogue();
                    break;

                case State.WaitingForPotion:
                    HandlePotionDelivery(player);
                    break;

                case State.Satisfied:
                case State.Declined:
                case State.Leaving:
                    // NPC is on its way out — no response
                    break;

                default:
                    // NPC is still walking — could show a "not yet available" hint if desired
                    break;
            }
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Dialogue Interaction
        // ─────────────────────────────────────────────────────────────────────────

        private void OpenOrderDialogue()
        {
            if (_order == null)
            {
                Debug.LogWarning($"[NPCController] {npcDisplayName} has no order assigned!", this);
                return;
            }

            PlayAnimation(talkAnimName);
            ShowDialogue(string.Format(orderLine, GetPotionDisplayName()), showButtons: true);
        }

        private void HandlePotionDelivery(PlayerInteraction player)
        {
            PlayAnimation(talkAnimName);

            if (!player.HasItem())
            {
                // Player came back empty-handed — just remind them
                ShowDialogue(string.Format(remindLine, GetPotionDisplayName()), showButtons: false);
                StartCoroutine(AutoHideDialogue(2.5f));
                return;
            }

            ItemType held = player.GetHeldItem();

            if (_order != null && held == _order.resultPotion)
            {
                // ✅ Correct potion delivered
                player.DropItem();
                _state = State.Satisfied;
                PlayAnimation(talkAnimName);

                if (ShopManager.Instance != null)
                {
                    ShopManager.Instance.AddCurrency(_order.basePrice);
                    ShopManager.Instance.AddGlory(_order.gloryReward);
                }

                ShowDialogue(correctLine, showButtons: false);
                StartCoroutine(LeaveAfterDelay(2.5f));
            }
            else
            {
                // ❌ Wrong potion
                if (ShopManager.Instance != null && _order != null)
                {
                    ShopManager.Instance.RemoveGlory(_order.gloryPenalty);
                }

                ShowDialogue(string.Format(wrongLine, GetPotionDisplayName()), showButtons: false);
                StartCoroutine(AutoHideDialogueAndIdle(2.5f));
            }
        }

        // ─── Button Callbacks ─────────────────────────────────────────────────────

        private void OnAccept()
        {
            _state = State.WaitingForPotion;
            ShowDialogue(string.Format(waitingLine, GetPotionDisplayName()), showButtons: false);
            StartCoroutine(AutoHideDialogueAndIdle(2.5f));
            Debug.Log($"[NPCController] {npcDisplayName} is now waiting for: {GetPotionDisplayName()}");
        }

        private void OnNoIngredients()
        {
            _state = State.Declined;
            PlayAnimation(talkAnimName);

            if (ShopManager.Instance != null && _order != null)
            {
                ShopManager.Instance.RemoveGlory(_order.gloryPenalty * 0.5f); // Half penalty for declining early
            }

            ShowDialogue(noStockLine, showButtons: false);
            StartCoroutine(LeaveAfterDelay(2.5f));
        }

        // ─────────────────────────────────────────────────────────────────────────
        // UI Helpers
        // ─────────────────────────────────────────────────────────────────────────

        private void ShowDialogue(string text, bool showButtons)
        {
            if (dialoguePanel    != null) dialoguePanel.SetActive(true);
            if (npcNameText      != null) npcNameText.text = npcDisplayName;
            if (dialogueText     != null) dialogueText.text = text;
            if (acceptButton     != null) acceptButton.gameObject.SetActive(showButtons);
            if (noIngredientsButton != null) noIngredientsButton.gameObject.SetActive(showButtons);
        }

        private void HideDialogue()
        {
            if (dialoguePanel != null) dialoguePanel.SetActive(false);
        }

        private IEnumerator AutoHideDialogue(float delay)
        {
            yield return new WaitForSeconds(delay);
            HideDialogue();
        }

        /// <summary>Hides dialogue and returns to idle — used after a talking exchange ends without leaving.</summary>
        private IEnumerator AutoHideDialogueAndIdle(float delay)
        {
            yield return new WaitForSeconds(delay);
            HideDialogue();
            PlayAnimation(idleAnimName);
        }

        private IEnumerator LeaveAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            HideDialogue();

            _state = State.Leaving;

            if (QueueManager.Instance != null && QueueManager.Instance.isActiveAndEnabled)
            {
                QueueManager.Instance.LeaveQueue(this);
            }

            if (exitWaypoint != null && _agent != null)
            {
                _agent.stoppingDistance = 0.3f;
                _agent.SetDestination(exitWaypoint.transform.position);
                PlayAnimation(walkAnimName);
                // Update() will destroy the object when it arrives
            }
            else
            {
                Destroy(gameObject);
            }
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Animation
        // ─────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Plays an animation state by name using a crossfade.
        /// Guards against re-triggering the same state (avoids restarting mid-play).
        /// </summary>
        private void PlayAnimation(string animName)
        {
            if (_animator == null || _currentAnim == animName) return;
            _animator.CrossFadeInFixedTime(animName, animTransition);
            _currentAnim = animName;
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Utilities
        // ─────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Converts a PascalCase enum value like "HealthPotion" → "Health Potion".
        /// </summary>
        private string GetPotionDisplayName()
        {
            if (_order == null) return "potion";
            return Regex.Replace(_order.resultPotion.ToString(), "([a-z])([A-Z])", "$1 $2");
        }

        #region IQueueable

        public void MoveUpLine(int newIndex, Vector3 newPosition)
        {
            _queueIndex = newIndex;

            if (_state == State.WalkingToCounter || _state == State.AtCounter || _state == State.WaitingForPotion || _state == State.Idle)
            {
                // Push back to walking state if they were waiting in line
                if (_state == State.WalkingToCounter && _agent != null && _agent.isOnNavMesh)
                {
                    _agent.SetDestination(newPosition);
                    PlayAnimation(walkAnimName);
                }
            }
        }

        #endregion

        /// <summary>
        /// Helper to find a component on an object by its exact name in the scene.
        /// </summary>
        private T FindComponentByName<T>(string objName) where T : Component
        {
            GameObject obj = GameObject.Find(objName);
            if (obj != null) return obj.GetComponent<T>();
            return null;
        }

        // Draw the counter waypoint area in the editor for easy setup
        private void OnDrawGizmosSelected()
        {
            if (counterWaypoint != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(counterWaypoint.transform.position, stoppingDistance);
            }
            if (exitWaypoint != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(exitWaypoint.transform.position, 0.5f);
            }
        }
    }
}
