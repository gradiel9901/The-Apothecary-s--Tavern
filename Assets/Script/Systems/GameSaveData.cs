using System;
using UnityEngine;
using Script.Environment;

namespace Script.Systems
{
    [Serializable]
    public class GameSaveData
    {
        // -------------------------
        // ECONOMY & PROGRESSION
        // -------------------------
        public int currency;
        public float glory;
        public int currentMultiplier;

        // -------------------------
        // DAY CYCLE & TIME
        // -------------------------
        public int currentDay;
        public float timeOfDay;
        public bool isWorkingHours;
        public float workingTimeRemaining;

        // -------------------------
        // EVENT TRACKING
        // -------------------------
        public GameEvent currentEvent;
        public int daysSinceLastEvent;
        public int eventDaysRemaining;

        // -------------------------
        // PLAYER TRANSFORM
        // -------------------------
        public float playerPosX, playerPosY, playerPosZ;
        public float playerRotX, playerRotY, playerRotZ, playerRotW;

        // -------------------------
        // METADATA
        // -------------------------
        public string saveDate;

        public void SavePlayerPosition(Vector3 position, Quaternion rotation)
        {
            playerPosX = position.x;
            playerPosY = position.y;
            playerPosZ = position.z;

            playerRotX = rotation.x;
            playerRotY = rotation.y;
            playerRotZ = rotation.z;
            playerRotW = rotation.w;
        }

        public Vector3 GetPlayerPosition()
        {
            return new Vector3(playerPosX, playerPosY, playerPosZ);
        }

        public Quaternion GetPlayerRotation()
        {
            return new Quaternion(playerRotX, playerRotY, playerRotZ, playerRotW);
        }
    }
}
