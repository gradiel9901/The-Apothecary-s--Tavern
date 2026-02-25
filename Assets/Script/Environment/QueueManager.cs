using System.Collections.Generic;
using UnityEngine;

namespace Script.Environment
{
    public class QueueManager : MonoBehaviour
    {
        public static QueueManager Instance { get; private set; }

        [Header("Queue Settings")]
        [Tooltip("The starting point of the queue (usually the counter waypoint from the Spawner).")]
        [SerializeField] private Transform queueStartPoint;

        [Tooltip("How far apart each NPC stands in line (local space relative to start point).")]
        [SerializeField] private Vector3 queueSpacing = new Vector3(0, 0, -1.5f);

        [Tooltip("Maximum queue capacity. Spawners should respect this so the line doesn't go out the door forever.")]
        [SerializeField] private int maxQueueSize = 5;

        // Using Component as the base class so both NPC.cs and NPCController.cs can use it easily if they share a common interface or just by relying on SendMessage.
        // To be typesafe, we'll store the object and an interface.
        public interface IQueueable
        {
            void MoveUpLine(int newIndex, Vector3 newPosition);
        }

        private List<IQueueable> _queue = new List<IQueueable>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (queueStartPoint == null)
            {
                queueStartPoint = this.transform; // Fallback to itself if not explicitly set
            }
        }

        /// <summary>
        /// Adds an NPC to the end of the queue and returns their designated world position.
        /// </summary>
        public Vector3 JoinQueue(IQueueable npc)
        {
            if (!_queue.Contains(npc))
            {
                _queue.Add(npc);
            }
            int index = _queue.IndexOf(npc);
            return GetPositionForIndex(index);
        }

        /// <summary>
        /// Removes an NPC from the queue and tells everyone behind them to move up.
        /// </summary>
        public void LeaveQueue(IQueueable npc)
        {
            if (_queue.Contains(npc))
            {
                _queue.Remove(npc);
                UpdateQueuePositions();
            }
        }

        public bool IsQueueFull()
        {
            return _queue.Count >= maxQueueSize;
        }

        public int GetQueueSize()
        {
            return _queue.Count;
        }

        public bool IsFrontOfLine(IQueueable npc)
        {
            return _queue.Count > 0 && _queue[0] == npc;
        }

        private void UpdateQueuePositions()
        {
            for (int i = 0; i < _queue.Count; i++)
            {
                Vector3 newPos = GetPositionForIndex(i);
                _queue[i].MoveUpLine(i, newPos);
            }
        }

        private Vector3 GetPositionForIndex(int index)
        {
            if (queueStartPoint == null) return Vector3.zero;

            // Calculate position offsetting backwards from the start point
            // Forward is the direction the NPC is facing when at the counter (usually the counter's forward or backward depending on how it's set up)
            Vector3 offset = queueStartPoint.right * queueSpacing.x + 
                             queueStartPoint.up * queueSpacing.y + 
                             queueStartPoint.forward * queueSpacing.z;

            return queueStartPoint.position + (offset * index);
        }

        private void OnDrawGizmosSelected()
        {
            if (queueStartPoint == null) return;

            Gizmos.color = Color.yellow;
            // Draw gizmos for the first few positions to help the designer visualize the line
            for (int i = 0; i < maxQueueSize; i++)
            {
                Vector3 pos = GetPositionForIndex(i);
                Gizmos.DrawWireSphere(pos, 0.3f);
                if (i > 0)
                {
                    Gizmos.DrawLine(GetPositionForIndex(i - 1), pos);
                }
            }
        }
    }
}
