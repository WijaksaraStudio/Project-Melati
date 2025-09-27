using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class A1_Monster : MonoBehaviour
{
    #region Spawn Configuration
    [Header("Spawn Configuration")]
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private float stayDuration = 10f;
    [SerializeField] private Transform playerTransform; // Assign the player's transform
    #endregion
    
    #region Proximity & Safety Rules
    [Header("Proximity & Safety Rules")]
    [SerializeField] private float proximityRadius = 6f;
    [SerializeField] private float safeRadius = 3f;
    [SerializeField] private float minTeleportDistance = 2f; // Minimum distance from player for teleportation
    [SerializeField] private float maxTeleportDistance = 15f; // Maximum distance from player for teleportation
    [Tooltip("Higher values make closer spawn points more likely")]
    [SerializeField] private float proximityWeight = 2.0f;
    [Tooltip("Angle in degrees in front of player to avoid spawning")]
    [SerializeField] private float avoidPlayerViewAngle = 60f;
    [Tooltip("Radius around spawn point to test for camera frustum visibility")]
    [SerializeField] private float spawnPointTestRadius = 1f;
    #endregion
    
    #region Debug Settings
    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;
    [SerializeField] private bool showDebugGizmos = true;
    #endregion
    
    #region Private Variables
    private Camera playerCamera;
    private float currentTimer;
    private bool isTimerActive;
    private bool isVisible;
    private bool isPlayerNearby;
    private Renderer monsterRenderer;
    private int currentSpawnIndex = -1;
    #endregion
    
    #region Events
    public System.Action OnSpawned;
    public System.Action OnTimerPaused;
    public System.Action OnTimerResumed;
    public System.Action OnTeleport;
    public System.Action OnProximityBlock; // New event for when teleport is blocked by proximity
    #endregion
    
    #region Unity Lifecycle
    void Start()
    {
        InitializeMonster();
        SpawnAtRandomLocation();
    }
    
    void Update()
    {
        if (isTimerActive)
        {
            UpdateProximityCheck();
            UpdateTimer();
            CheckVisibility();
        }
    }
    #endregion
    
    #region Initialization
    private void InitializeMonster()
    {
        // Get the main camera
        playerCamera = Camera.main;
        if (playerCamera == null)
        {
            Debug.LogError("A1_Monster: No main camera found! Please tag your camera as 'MainCamera'.");
            enabled = false;
            return;
        }
        
        // Auto-find player if not assigned
        if (playerTransform == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                playerTransform = player.transform;
            }
            else
            {
                Debug.LogWarning("A1_Monster: No player transform assigned and no GameObject with 'Player' tag found. Using camera transform instead.");
                playerTransform = playerCamera.transform;
            }
        }
        
        // Get renderer component for visibility checks
        monsterRenderer = GetComponent<Renderer>();
        if (monsterRenderer == null)
        {
            Debug.LogError("A1_Monster: No Renderer component found! Please add a Renderer to this GameObject.");
            enabled = false;
            return;
        }
        
        // Validate spawn points
        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogError("A1_Monster: No spawn points assigned! Please assign spawn points in the inspector.");
            enabled = false;
            return;
        }
        
        DebugLog("A1 Monster initialized successfully.");
    }
    #endregion
    
    #region Spawning Logic
    private void SpawnAtRandomLocation()
    {
        if (spawnPoints.Length == 0) return;
        
        // Get valid spawn points based on safety and proximity rules
        List<SpawnPointCandidate> validCandidates = GetValidSpawnPoints();
        
        if (validCandidates.Count == 0)
        {
            DebugLog("No valid spawn points available! Using fallback spawn point.");
            SpawnAtFallbackLocation();
            return;
        }
        
        // Choose spawn point using weighted randomness
        int newSpawnIndex = SelectSpawnPointWeighted(validCandidates);
        
        currentSpawnIndex = newSpawnIndex;
        transform.position = spawnPoints[currentSpawnIndex].position;
        transform.rotation = spawnPoints[currentSpawnIndex].rotation;
        
        // Reset timer and start countdown
        currentTimer = stayDuration;
        isTimerActive = true;
        isVisible = false;
        isPlayerNearby = false;
        
        DebugLog($"A1 spawned at spawn point {currentSpawnIndex} ({spawnPoints[currentSpawnIndex].name}) - Distance to player: {Vector3.Distance(transform.position, playerTransform.position):F1}m");
        OnSpawned?.Invoke();
    }
    
    private List<SpawnPointCandidate> GetValidSpawnPoints()
    {
        List<SpawnPointCandidate> candidates = new List<SpawnPointCandidate>();
        Vector3 playerPos = playerTransform.position;
        Vector3 playerForward = playerTransform.forward;
        
        // Get camera frustum planes for visibility testing
        Plane[] frustumPlanes = GeometryUtility.CalculateFrustumPlanes(playerCamera);
        
        for (int i = 0; i < spawnPoints.Length; i++)
        {
            if (spawnPoints[i] == null) continue;
            if (i == currentSpawnIndex) continue; // Don't stay at current position
            
            Vector3 spawnPos = spawnPoints[i].position;
            float distanceToPlayer = Vector3.Distance(spawnPos, playerPos);
            
            // Check safe radius (too close to player)
            if (distanceToPlayer < safeRadius)
            {
                DebugLog($"Spawn point {i} rejected: Too close to player ({distanceToPlayer:F1}m < {safeRadius}m)");
                continue;
            }
            
            // Check minimum/maximum teleport distance
            if (distanceToPlayer < minTeleportDistance || distanceToPlayer > maxTeleportDistance)
            {
                DebugLog($"Spawn point {i} rejected: Outside teleport range ({distanceToPlayer:F1}m)");
                continue;
            }
            
            // Check if spawn point is directly in front of player (angle-based check)
            Vector3 directionToSpawn = (spawnPos - playerPos).normalized;
            float angleToSpawn = Vector3.Angle(playerForward, directionToSpawn);
            
            if (angleToSpawn < avoidPlayerViewAngle / 2f)
            {
                DebugLog($"Spawn point {i} rejected: In player's view cone ({angleToSpawn:F1}° < {avoidPlayerViewAngle / 2f}°)");
                continue;
            }
            
            // Check if spawn point is visible in camera frustum
            if (IsSpawnPointVisibleInFrustum(spawnPos, frustumPlanes))
            {
                DebugLog($"Spawn point {i} rejected: Visible in camera frustum");
                continue;
            }
            
            // Calculate weight based on distance (closer = higher weight)
            float weight = Mathf.Pow(1f / distanceToPlayer, proximityWeight);
            
            candidates.Add(new SpawnPointCandidate
            {
                index = i,
                distance = distanceToPlayer,
                weight = weight
            });
            
            DebugLog($"Spawn point {i} valid: Distance={distanceToPlayer:F1}m, Angle={angleToSpawn:F1}°, Weight={weight:F3}");
        }
        
        return candidates;
    }
    
    private int SelectSpawnPointWeighted(List<SpawnPointCandidate> candidates)
    {
        // Calculate total weight
        float totalWeight = candidates.Sum(c => c.weight);
        float randomValue = Random.Range(0f, totalWeight);
        float currentWeight = 0f;
        
        // Select based on weighted probability
        foreach (var candidate in candidates)
        {
            currentWeight += candidate.weight;
            if (randomValue <= currentWeight)
            {
                return candidate.index;
            }
        }
        
        // Fallback to first candidate (shouldn't happen)
        return candidates[0].index;
    }
    
    private void SpawnAtFallbackLocation()
    {
        // Use the farthest spawn point as fallback
        int fallbackIndex = 0;
        float maxDistance = 0f;
        Vector3 playerPos = playerTransform.position;
        
        for (int i = 0; i < spawnPoints.Length; i++)
        {
            if (spawnPoints[i] == null || i == currentSpawnIndex) continue;
            
            float distance = Vector3.Distance(spawnPoints[i].position, playerPos);
            if (distance > maxDistance)
            {
                maxDistance = distance;
                fallbackIndex = i;
            }
        }
        
        currentSpawnIndex = fallbackIndex;
        transform.position = spawnPoints[currentSpawnIndex].position;
        transform.rotation = spawnPoints[currentSpawnIndex].rotation;
        
        currentTimer = stayDuration;
        isTimerActive = true;
        isVisible = false;
        isPlayerNearby = false;
        
        DebugLog($"A1 spawned at fallback location {currentSpawnIndex}");
        OnSpawned?.Invoke();
    }
    #endregion
    
    #region Timer & Visibility Logic
    private void UpdateTimer()
    {
        // Only count down if monster is not visible AND player is not too close
        if (!isVisible && !isPlayerNearby)
        {
            currentTimer -= Time.deltaTime;
            
            if (currentTimer <= 0f)
            {
                TeleportToNewLocation();
            }
        }
    }
    
    private void UpdateProximityCheck()
    {
        bool wasPlayerNearby = isPlayerNearby;
        float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);
        isPlayerNearby = distanceToPlayer <= proximityRadius;
        
        // Handle proximity state changes
        if (isPlayerNearby && !wasPlayerNearby)
        {
            DebugLog($"Player entered proximity radius ({distanceToPlayer:F1}m <= {proximityRadius}m) - timer blocked");
            OnProximityBlock?.Invoke();
        }
        else if (!isPlayerNearby && wasPlayerNearby)
        {
            DebugLog($"Player left proximity radius ({distanceToPlayer:F1}m > {proximityRadius}m) - timer can resume");
        }
    }
    
    private void CheckVisibility()
    {
        bool wasVisible = isVisible;
        isVisible = IsVisibleToCamera();
        
        // Handle visibility state changes
        if (isVisible && !wasVisible)
        {
            DebugLog("A1 is now visible - timer paused");
            OnTimerPaused?.Invoke();
        }
        else if (!isVisible && wasVisible)
        {
            DebugLog($"A1 is no longer visible - timer can resume ({currentTimer:F1}s remaining)");
            OnTimerResumed?.Invoke();
        }
    }
    
    private bool IsVisibleToCamera()
    {
        if (playerCamera == null || monsterRenderer == null) return false;
        
        // Check if the object is within the camera's frustum
        Bounds bounds = monsterRenderer.bounds;
        Plane[] planes = GeometryUtility.CalculateFrustumPlanes(playerCamera);
        
        return GeometryUtility.TestPlanesAABB(planes, bounds);
    }
    #endregion
    
    #region Teleportation
    private void TeleportToNewLocation()
    {
        isTimerActive = false;
        DebugLog("A1 timer completed - teleporting to new location");
        OnTeleport?.Invoke();
        
        // Small delay before spawning to allow for potential effects
        StartCoroutine(DelayedSpawn());
    }
    
    private IEnumerator DelayedSpawn()
    {
        yield return new WaitForSeconds(0.1f);
        SpawnAtRandomLocation();
    }
    #endregion
    
    #region Utility Methods
    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[A1_Monster] {message}");
        }
    }
    
    /// <summary>
    /// Checks if a spawn point is visible within the camera's frustum
    /// </summary>
    /// <param name="spawnPosition">World position of the spawn point</param>
    /// <param name="frustumPlanes">Camera frustum planes</param>
    /// <returns>True if the spawn point is visible in the camera frustum</returns>
    private bool IsSpawnPointVisibleInFrustum(Vector3 spawnPosition, Plane[] frustumPlanes)
    {
        // Create a small bounding box around the spawn point to test visibility
        Bounds testBounds = new Bounds(spawnPosition, Vector3.one * (spawnPointTestRadius * 2f));
        
        // Test if the bounding box intersects with the camera frustum
        bool isVisible = GeometryUtility.TestPlanesAABB(frustumPlanes, testBounds);
        
        return isVisible;
    }
    #endregion
    
    #region Public Interface
    public void ForceSpawn()
    {
        SpawnAtRandomLocation();
    }
    
    public void ForceTeleport()
    {
        TeleportToNewLocation();
    }
    
    public float GetRemainingTime()
    {
        return Mathf.Max(0f, currentTimer);
    }
    
    public bool IsCurrentlyVisible()
    {
        return isVisible;
    }
    
    public bool IsTimerRunning()
    {
        return isTimerActive && !isVisible && !isPlayerNearby;
    }
    
    public bool IsPlayerInProximity()
    {
        return isPlayerNearby;
    }
    
    public float GetDistanceToPlayer()
    {
        return Vector3.Distance(transform.position, playerTransform.position);
    }
    
    public bool IsSpawnPointVisibleToCamera(int spawnPointIndex)
    {
        if (spawnPointIndex < 0 || spawnPointIndex >= spawnPoints.Length || spawnPoints[spawnPointIndex] == null)
            return false;
            
        Plane[] frustumPlanes = GeometryUtility.CalculateFrustumPlanes(playerCamera);
        return IsSpawnPointVisibleInFrustum(spawnPoints[spawnPointIndex].position, frustumPlanes);
    }
    
    public List<int> GetValidSpawnPointIndices()
    {
        return GetValidSpawnPoints().Select(c => c.index).ToList();
    }
    
    public List<int> GetVisibleSpawnPointIndices()
    {
        List<int> visibleIndices = new List<int>();
        Plane[] frustumPlanes = GeometryUtility.CalculateFrustumPlanes(playerCamera);
        
        for (int i = 0; i < spawnPoints.Length; i++)
        {
            if (spawnPoints[i] != null && IsSpawnPointVisibleInFrustum(spawnPoints[i].position, frustumPlanes))
            {
                visibleIndices.Add(i);
            }
        }
        
        return visibleIndices;
    }
    #endregion
    
    #region Debug Gizmos
    void OnDrawGizmosSelected()
    {
        if (!showDebugGizmos) return;
        
        // Get camera frustum planes for visibility testing (only in play mode)
        Plane[] frustumPlanes = null;
        if (Application.isPlaying && playerCamera != null)
        {
            frustumPlanes = GeometryUtility.CalculateFrustumPlanes(playerCamera);
        }
        
        // Draw spawn points
        if (spawnPoints != null)
        {
            for (int i = 0; i < spawnPoints.Length; i++)
            {
                if (spawnPoints[i] == null) continue;
                
                Vector3 spawnPos = spawnPoints[i].position;
                float distanceToPlayer = playerTransform != null ? Vector3.Distance(spawnPos, playerTransform.position) : 0f;
                
                // Check if spawn point is visible in camera frustum
                bool isVisibleInFrustum = false;
                if (Application.isPlaying && frustumPlanes != null)
                {
                    isVisibleInFrustum = IsSpawnPointVisibleInFrustum(spawnPos, frustumPlanes);
                }
                
                // Color code spawn points based on validity
                if (i == currentSpawnIndex)
                {
                    Gizmos.color = Color.green; // Current spawn point
                }
                else if (isVisibleInFrustum)
                {
                    Gizmos.color = Color.magenta; // Visible in camera frustum
                }
                else if (playerTransform != null && distanceToPlayer < safeRadius)
                {
                    Gizmos.color = Color.red; // Too close (unsafe)
                }
                else if (playerTransform != null && (distanceToPlayer < minTeleportDistance || distanceToPlayer > maxTeleportDistance))
                {
                    Gizmos.color = Color.yellow; // Outside teleport range
                }
                else
                {
                    Gizmos.color = Color.cyan; // Valid spawn point
                }
                
                Gizmos.DrawWireSphere(spawnPos, 0.5f);
                Gizmos.DrawRay(spawnPos, spawnPoints[i].forward * 2f);
                
                // Draw test sphere for frustum visibility
                if (Application.isPlaying)
                {
                    Gizmos.color = new Color(Gizmos.color.r, Gizmos.color.g, Gizmos.color.b, 0.3f);
                    Gizmos.DrawSphere(spawnPos, spawnPointTestRadius);
                }
            }
        }
        
        // Draw proximity and safe radius around monster
        if (Application.isPlaying)
        {
            Vector3 monsterPos = transform.position;
            
            // Proximity radius (red)
            Gizmos.color = new Color(1f, 0f, 0f, 0.2f);
            Gizmos.DrawSphere(monsterPos, proximityRadius);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(monsterPos, proximityRadius);
            
            // Current position
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(monsterPos, 1f);
        }
        
        // Draw safe radius around player
        if (playerTransform != null)
        {
            Vector3 playerPos = playerTransform.position;
            
            // Safe radius (blue)
            Gizmos.color = new Color(0f, 0f, 1f, 0.2f);
            Gizmos.DrawSphere(playerPos, safeRadius);
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(playerPos, safeRadius);
            
            // Min/Max teleport distance
            Gizmos.color = new Color(1f, 1f, 0f, 0.1f);
            Gizmos.DrawSphere(playerPos, minTeleportDistance);
            Gizmos.color = new Color(0f, 1f, 0f, 0.1f);
            Gizmos.DrawSphere(playerPos, maxTeleportDistance);
            
            // Player view cone
            if (Application.isPlaying)
            {
                Vector3 forward = playerTransform.forward;
                float halfAngle = avoidPlayerViewAngle / 2f;
                Vector3 leftBound = Quaternion.Euler(0, -halfAngle, 0) * forward * 5f;
                Vector3 rightBound = Quaternion.Euler(0, halfAngle, 0) * forward * 5f;
                
                Gizmos.color = new Color(1f, 0f, 1f, 0.3f);
                Gizmos.DrawRay(playerPos, leftBound);
                Gizmos.DrawRay(playerPos, rightBound);
                Gizmos.DrawRay(playerPos, forward * 5f);
            }
        }
    }
    #endregion
    
    #region Helper Classes
    private class SpawnPointCandidate
    {
        public int index;
        public float distance;
        public float weight;
    }
    #endregion
}

