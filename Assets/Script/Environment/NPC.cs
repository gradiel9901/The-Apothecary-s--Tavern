using System.Collections;
using System.Collections.Generic;
using System.Linq; // Added for Linq grouping
using Script.Player;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;
using TMPro;

namespace Script.Environment
{
    /// <summary>
    /// New, robust NPC Controller.
    /// Handles NavMesh navigation, Door state logic, precise UI mapping,
    /// potion ordering, and automatic animation state switching.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(Animator))]
    public class NPC : MonoBehaviour, IInteractable, QueueManager.IQueueable
    {
        [Header("NPC Data")]
        [SerializeField] private string npcName = "Traveler";
        [Tooltip("The NPC will randomly pick one recipe from this list when spawned.")]
        [SerializeField] private List<PotionRecipe> availableOrders;

        [Header("Navigation Waypoints")]
        [Tooltip("Name of the scene GameObject where the NPC stands to order.")]
        [SerializeField] private string counterWaypointName = "Counter Waypoint";
        [Tooltip("Name of the scene GameObject where the NPC leaves to.")]
        [SerializeField] private string exitWaypointName = "Exit Waypoint";
        [SerializeField] private float stoppingDistance = 1.5f;

        [Header("UI Names (Must match scene exactly)")]
        [SerializeField] private string dialoguePanelName = "Dialogue Box Panel";
        [SerializeField] private string npcNameTextName = "NPC name";
        [SerializeField] private string dialogueTextName = "Dialogue Text";
        [SerializeField] private string acceptButtonName = "Accept Button";
        [SerializeField] private string declineButtonName = "No Ingredients Button";
        
        [Header("Quest UI Names (Optional)")]
        [SerializeField] private string questTitleName = "Quest Title";
        [SerializeField] private string questDescriptionName = "Quest Description";

        [Header("Animations")]
        [SerializeField] private string idleAnim = "Idle";
        [SerializeField] private string walkAnim = "Walking";
        [SerializeField] private string talkAnim = "Talking";
        [SerializeField] private float animTransitionTime = 0.15f;

        // --- Resolved References ---
        private Transform _counterWaypoint;
        private Transform _exitWaypoint;
        private GameObject _dialoguePanel;
        private TMP_Text _npcNameText;
        private TMP_Text _dialogueText;
        private Button _acceptButton;
        private Button _declineButton;
        
        private TMP_Text _questTitleText;
        private TMP_Text _questDescText;

        // --- State ---
        private NavMeshAgent _agent;
        private Animator _animator;
        private PotionRecipe _currentOrder;
        private string _currentAnimState;
        private PlayerInteraction _interactingPlayer;

        // Queue Info
        private int _queueIndex = -1;

        private enum NPCState { Spawning, WalkingToCounter, WaitingInLine, WaitingForOrder, WaitingForPotion, Satisfied, Declined, Leaving }
        private NPCState _state = NPCState.Spawning;

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _animator = GetComponent<Animator>();

            // If the model is a child, try finding Animator there
            if (_animator == null) _animator = GetComponentInChildren<Animator>();

            ResolveSceneReferences();
            HideUI();
        }

        private void Start()
        {
            // Pick an order
            if (availableOrders != null && availableOrders.Count > 0)
            {
                _currentOrder = availableOrders[Random.Range(0, availableOrders.Count)];
            }

            PlayAnim(idleAnim);
            StartCoroutine(NavigateToCounterRoutine());
        }

        private void OnEnable()
        {
            DoorAnimation.OnDoorOpened += HandleDoorOpened;
        }

        private void OnDisable()
        {
            DoorAnimation.OnDoorOpened -= HandleDoorOpened;
            RemoveButtonListeners(); // Clean up listeners if destroyed
        }

        // -------------------------------------------------------------------
        // Initialization & Reference Binding
        // -------------------------------------------------------------------

        private void ResolveSceneReferences()
        {
            // Waypoints
            GameObject counterObj = GameObject.Find(counterWaypointName);
            if (counterObj != null) _counterWaypoint = counterObj.transform;

            GameObject exitObj = GameObject.Find(exitWaypointName);
            if (exitObj != null) _exitWaypoint = exitObj.transform;

            // UI
            _dialoguePanel = GameObject.Find(dialoguePanelName);

            // If not found (because it is properly disabled in the editor), search through Canvases
            if (_dialoguePanel == null)
            {
                // Modern Unity API to find objects, including disabled ones, avoiding sort overhead
                Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (Canvas c in canvases)
                {
                    Transform[] children = c.GetComponentsInChildren<Transform>(true);
                    foreach (Transform t in children)
                    {
                        if (t.name == dialoguePanelName)
                        {
                            _dialoguePanel = t.gameObject;
                            break;
                        }
                    }
                    if (_dialoguePanel != null) break;
                }
            }

            if (_dialoguePanel != null)
            {
                // Find children by name
                _npcNameText = FindChildComponent<TMP_Text>(_dialoguePanel.transform, npcNameTextName);
                _dialogueText = FindChildComponent<TMP_Text>(_dialoguePanel.transform, dialogueTextName);
                _acceptButton = FindChildComponent<Button>(_dialoguePanel.transform, acceptButtonName);
                _declineButton = FindChildComponent<Button>(_dialoguePanel.transform, declineButtonName);
            }
            else
            {
                Debug.LogWarning($"[NPC] Could not find Canvas Panel named '{dialoguePanelName}' in the scene.");
            }

            // Optional Quest UI (Search all texts globally to allow them outside the dialogue panel)
            TMP_Text[] allTexts = FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (TMP_Text t in allTexts)
            {
                if (t.name == questTitleName) _questTitleText = t;
                if (t.name == questDescriptionName) _questDescText = t;
            }
        }

        private T FindChildComponent<T>(Transform parent, string childName) where T : Component
        {
            Transform[] children = parent.GetComponentsInChildren<Transform>(true);
            foreach (Transform t in children)
            {
                if (t.name == childName)
                {
                    return t.GetComponent<T>();
                }
            }
            return null;
        }

        // -------------------------------------------------------------------
        // Navigation Logic
        // -------------------------------------------------------------------

        private IEnumerator NavigateToCounterRoutine()
        {
            yield return null; // Wait 1 frame for NavMesh to settle

            if (_counterWaypoint == null)
            {
                Debug.LogError($"[NPC] Counter Waypoint '{counterWaypointName}' not found in scene!");
                yield break;
            }

            _state = NPCState.WalkingToCounter;
            PlayAnim(walkAnim);

            Vector3 targetPosition = _counterWaypoint.position;
            if (QueueManager.Instance != null && QueueManager.Instance.isActiveAndEnabled)
            {
                targetPosition = QueueManager.Instance.JoinQueue(this);
            }

            // Poll path validity (handles closed doors)
            while (true)
            {
                NavMeshPath path = new NavMeshPath();
                if (_agent.CalculatePath(targetPosition, path) && path.status == NavMeshPathStatus.PathComplete)
                {
                    _agent.stoppingDistance = stoppingDistance;
                    _agent.SetPath(path);
                    break;
                }
                yield return new WaitForSeconds(0.5f);
            }
        }

        private void Update()
        {
            // Check arrival at counter or spot in line
            if (_state == NPCState.WalkingToCounter || _state == NPCState.WaitingInLine)
            {
                if (!_agent.pathPending && _agent.remainingDistance <= stoppingDistance)
                {
                    _agent.ResetPath(); // Stop moving
                    
                    bool isFront = QueueManager.Instance == null || !QueueManager.Instance.isActiveAndEnabled || QueueManager.Instance.IsFrontOfLine(this);
                    if (isFront)
                    {
                        // Front of the line, facing the counter
                        transform.rotation = _counterWaypoint.rotation; 
                        _state = NPCState.WaitingForOrder;
                    }
                    else
                    {
                        // Still in line somewhere
                        _state = NPCState.WaitingInLine;
                        // Face forward towards the counter
                        Vector3 lookAt = _counterWaypoint.position;
                        lookAt.y = transform.position.y;
                        transform.LookAt(lookAt);
                    }

                    PlayAnim(idleAnim);
                }
            }
            // Check arrival at exit
            else if (_state == NPCState.Leaving)
            {
                if (!_agent.pathPending && _agent.remainingDistance <= 0.5f)
                {
                    Destroy(gameObject); // Despawn
                }
            }
        }

        private void HandleDoorOpened()
        {
            // If the door opens while we are trying to walk, recalculate immediately
            if ((_state == NPCState.WalkingToCounter || _state == NPCState.WaitingInLine))
            {
                Vector3 target = _counterWaypoint != null ? _counterWaypoint.position : transform.position;
                if (QueueManager.Instance != null && QueueManager.Instance.isActiveAndEnabled)
                {
                    // Assuming GetPositionForIndex would be nice here, but since JoinQueue already set us,
                    // we'll rely on the normal path polling or MoveUpLine event.
                }
                else
                {
                     _agent.SetDestination(target);
                }
            }
        }

        // -------------------------------------------------------------------
        // Interaction & UI Logic
        // -------------------------------------------------------------------

        public void Interact(PlayerInteraction player)
        {
            Debug.Log($"[NPC] Interact called by Player. Current State: {_state}. Distance: {Vector3.Distance(transform.position, player.transform.position):F2}");

            if (_state == NPCState.WalkingToCounter || _state == NPCState.WaitingForOrder)
            {
                _interactingPlayer = player;
                OpenOrderDialogue();
            }
            else if (_state == NPCState.WaitingForPotion)
            {
                _interactingPlayer = player;
                CheckPotionDelivery(player);
            }
            else
            {
                Debug.Log($"[NPC] Interaction ignored. State {_state} does not allow interaction right now.");
            }
        }

        private void OpenOrderDialogue()
        {
            if (_currentOrder == null)
            {
                Debug.LogWarning("[NPC] Cannot open dialogue: _currentOrder is NULL. Did you assign PotionRecipes to the 'Available Orders' list on the NPC prefab?");
                return;
            }
            
            // Pause navigation if walking
            if (_state == NPCState.WalkingToCounter)
            {
                _agent.isStopped = true;
            }

            // Lock player controls and free cursor
            if (_interactingPlayer != null)
            {
                _interactingPlayer.GetComponent<PlayerMovement>()?.TogglePlayerInput(false);
            }

            PlayAnim(talkAnim);
            string potionName = FormatItemName(_currentOrder.resultPotion.ToString());
            
            Debug.Log($"[NPC] Opening dialogue for order: {potionName}");
            ShowUI(npcName, $"Hello! I am looking for a {potionName}. Can you brew one for me?", showButtons: true);
            
            // Setup buttons
            RemoveButtonListeners();
            if (_acceptButton != null) _acceptButton.onClick.AddListener(OnAcceptOrder);
            else Debug.LogWarning("[NPC] Accept button is null!");

            if (_declineButton != null) _declineButton.onClick.AddListener(OnDeclineOrder);
            else Debug.LogWarning("[NPC] Decline button is null!");
        }

        private void OnAcceptOrder()
        {
            RemoveButtonListeners();

            if (_questTitleText != null && _currentOrder != null)
            {
                _questTitleText.gameObject.SetActive(true);
                _questTitleText.text = $"Create a {FormatItemName(_currentOrder.resultPotion.ToString())}";
            }

            if (_questDescText != null && _currentOrder != null)
            {
                _questDescText.gameObject.SetActive(true);
                string desc = "Ingredients Needed:\n";
                
                // Group the ingredients to display like "3x Mushroom" instead of listing it 3 times
                var groupedIngredients = _currentOrder.ingredients
                    .GroupBy(i => i)
                    .Select(g => new { Ingredient = g.Key, Count = g.Count() });

                foreach (var item in groupedIngredients)
                {
                    desc += $"- {item.Count}x {FormatItemName(item.Ingredient.ToString())}\n";
                }
                
                _questDescText.text = desc;
            }

            if (_state == NPCState.WalkingToCounter)
            {
                // Resume walking to the counter and switch to WaitingForPotion once arrived
                _agent.isStopped = false;
                PlayAnim(walkAnim);
                _state = NPCState.WalkingToCounter; // Will transition to WaitingForPotion in Update()
                
                ShowUI(npcName, "Excellent! I'll head to the counter and wait.", showButtons: false);
                StartCoroutine(CloseUIAfterDelay(2.5f));
            }
            else
            {
                // Already at counter
                _state = NPCState.WaitingForPotion;
                ShowUI(npcName, "Excellent! I'll wait right here for it.", showButtons: false);
                StartCoroutine(CloseUIAfterDelay(2.5f));
            }
        }

        private void OnDeclineOrder()
        {
            _state = NPCState.Declined;
            if (_agent != null && _agent.isOnNavMesh) _agent.isStopped = false; // Resume moving (towards exit)

            if (_currentOrder != null && ShopManager.Instance != null)
            {
                ShopManager.Instance.RemoveGlory(_currentOrder.gloryPenalty * 0.5f); // Half penalty for declining early
            }

            RemoveButtonListeners();
            ShowUI(npcName, "Oh, I see. What a shame. I'll take my business elsewhere.", showButtons: false);
            StartCoroutine(LeaveRoutine(2.5f));
        }

        private void CheckPotionDelivery(PlayerInteraction player)
        {
            PlayAnim(talkAnim);

            // Lock player controls during delivery response
            player.GetComponent<PlayerMovement>()?.TogglePlayerInput(false);

            if (!player.HasItem())
            {
                string potionName = FormatItemName(_currentOrder.resultPotion.ToString());
                ShowUI(npcName, $"Still waiting for my {potionName}. Take your time!", showButtons: false);
                StartCoroutine(CloseUIAfterDelay(2.5f));
                return;
            }

            ItemType heldPotion = player.GetHeldItem();

            if (heldPotion == _currentOrder.resultPotion)
            {
                // Success!
                player.DropItem(); // Consume the item from player's hand
                _state = NPCState.Satisfied;
                
                if (ShopManager.Instance != null)
                {
                    ShopManager.Instance.AddCurrency(_currentOrder.basePrice);
                    ShopManager.Instance.AddGlory(_currentOrder.gloryReward);
                }

                ShowUI(npcName, "Perfect! This is exactly what I needed. Thank you!", showButtons: false);
                StartCoroutine(LeaveRoutine(2.5f));
            }
            else
            {
                // Wrong item
                if (ShopManager.Instance != null)
                {
                    ShopManager.Instance.RemoveGlory(_currentOrder.gloryPenalty);
                }

                string potionName = FormatItemName(_currentOrder.resultPotion.ToString());
                ShowUI(npcName, $"Hmm, this isn't right. I clearly asked for a {potionName}.", showButtons: false);
                StartCoroutine(CloseUIAfterDelay(2.5f));
            }
        }

        private IEnumerator CloseUIAfterDelay(float delay)
        {
            if (QueueManager.Instance != null && QueueManager.Instance.isActiveAndEnabled && _state != NPCState.Leaving)
            {
                QueueManager.Instance.LeaveQueue(this);
            }

            yield return new WaitForSeconds(delay);
            HideUI();
            PlayAnim(idleAnim); // Return to idle looking around

            if (_interactingPlayer != null)
            {
                _interactingPlayer.GetComponent<PlayerMovement>()?.TogglePlayerInput(true);
                _interactingPlayer = null;
            }
        }

        private IEnumerator LeaveRoutine(float delay)
        {
            if (QueueManager.Instance != null && QueueManager.Instance.isActiveAndEnabled)
            {
                QueueManager.Instance.LeaveQueue(this);
            }

            yield return new WaitForSeconds(delay);
            HideUI();
            
            if (_questTitleText != null) _questTitleText.gameObject.SetActive(false);
            if (_questDescText != null) _questDescText.gameObject.SetActive(false);

            if (_interactingPlayer != null)
            {
                _interactingPlayer.GetComponent<PlayerMovement>()?.TogglePlayerInput(true);
                _interactingPlayer = null;
            }
            
            _state = NPCState.Leaving;
            PlayAnim(walkAnim);

            if (_exitWaypoint != null)
            {
                _agent.stoppingDistance = 0f;
                _agent.SetDestination(_exitWaypoint.position);
            }
            else
            {
                Destroy(gameObject); // Backup despawn if no waypoint
            }
        }

        private void RemoveButtonListeners()
        {
            if (_acceptButton != null) _acceptButton.onClick.RemoveAllListeners();
            if (_declineButton != null) _declineButton.onClick.RemoveAllListeners();
        }

        // -------------------------------------------------------------------
        // UI Helpers
        // -------------------------------------------------------------------

        private void ShowUI(string nameText, string dialogue, bool showButtons)
        {
            if (_dialoguePanel != null) _dialoguePanel.SetActive(true);
            if (_npcNameText != null) _npcNameText.text = nameText;
            if (_dialogueText != null) _dialogueText.text = dialogue;
            
            if (_acceptButton != null) _acceptButton.gameObject.SetActive(showButtons);
            if (_declineButton != null) _declineButton.gameObject.SetActive(showButtons);
        }

        private void HideUI()
        {
            if (_dialoguePanel != null) _dialoguePanel.SetActive(false);
        }

        // -------------------------------------------------------------------
        // Utilities
        // -------------------------------------------------------------------

        private void PlayAnim(string stateName)
        {
            if (_animator == null || _currentAnimState == stateName) return;
            _animator.CrossFadeInFixedTime(stateName, animTransitionTime);
            _currentAnimState = stateName;
        }

        private string FormatItemName(string input)
        {
            // Converts "HealthPotion" -> "Health Potion"
            return System.Text.RegularExpressions.Regex.Replace(input, "([a-z])([A-Z])", "$1 $2");
        }

        #region IQueueable Implementation

        public void MoveUpLine(int newIndex, Vector3 newPosition)
        {
            _queueIndex = newIndex;

            if (_state != NPCState.Leaving && _state != NPCState.Satisfied && _state != NPCState.Declined)
            {
                // Tell the agent to start walking to the new spot in line
                _state = NPCState.WaitingInLine; 
                if (_agent != null && _agent.isOnNavMesh)
                {
                    _agent.isStopped = false;
                    _agent.SetDestination(newPosition);
                    PlayAnim(walkAnim);
                }
            }
        }

        #endregion
    }
}
