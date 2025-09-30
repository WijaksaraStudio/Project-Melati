using UnityEngine;
using System.Collections;

// This script should be attached to the Monster or Camera GameObject.
public class MonsterVision : MonoBehaviour
{
    [Header("Detection Settings")]
    // The player's transform that the monster will try to detect and track.
    public Transform playerTarget;
    // The maximum distance the monster can see.
    public float viewRange = 13f;
    // The angle (in degrees) of the monster's field of view (e.g., 90 means 45 degrees left and 45 degrees right).
    [Range(0, 360)]
    public float fieldOfViewAngle = 360f;

    [Header("Sanity Depletion Settings")]
    [Tooltip("The base rate (per second) at which sanity depletes when the vision intersection occurs.")]
    public float sanityDrainRate = 2f;
    [Tooltip("Maximum distance for sanity to drain. Should be less than or equal to viewRange.")]
    public float sanityDrainRange = 8f;
    [Tooltip("How fast the sanity drain rate increases per second while the player is staring.")]
    public float compoundRate = 1.02f;
    [Tooltip("The maximum multiplier for the sanity drain rate. (e.g., 3 means 3x the base rate).")]
    public float maxCompoundMultiplier = 3f;
    
    // Reference to the PlayerSanity script.
    private PlayerSanity playerSanity;
    // Reference to the player's main camera component.
    private Camera playerCamera;
    
    // Tracks the time the player has been staring at the monster while the monster is also looking at the player.
    private float stareTimer = 0f;


    [Header("Movement Settings")]
    // The speed at which the monster rotates to track the target.
    public float rotationSpeed = 4f;
    // The speed at which the monster rotates when it's not tracking the player (for patrol).
    public float patrolSpeed = 0f;

    // Internal state to check if the player is currently detected.
    private bool isPlayerDetected = false;
    
    void Start()
    {
        // Attempt to find the PlayerSanity component on the Player Target.
        if (playerTarget != null)
        {
            playerSanity = playerTarget.GetComponent<PlayerSanity>();
        }

        if (playerSanity == null)
        {
            Debug.LogError("MonsterVision: PlayerSanity script not found on the Player Target!");
        }
        
        // Find the player's main camera for the 'Player Looking at Monster' check.
        playerCamera = Camera.main;
        if (playerCamera == null)
        {
            Debug.LogError("MonsterVision: Main Camera not found. Sanity drain on sight will not work.");
        }
    }


    // --- Unity Life Cycle Methods ---

    void Update()
    {
        // Basic checks for component references
        if (playerTarget == null || playerSanity == null || playerCamera == null)
        {
            // If essential references are missing, skip the update logic to prevent errors.
            return;
        }

        // 1. Check for player visibility by the MONSTER every frame.
        isPlayerDetected = CheckForPlayer();

        // 2. Check for monster visibility by the PLAYER every frame.
        bool isMonsterSeenByPlayer = CheckIfSeenByPlayer();
        
        // 3. Apply Sanity Drain based on visibility intersection and compounding.
        HandleSanityDrain(isMonsterSeenByPlayer);

        // 4. Monster Movement Logic
        if (isPlayerDetected)
        {
            TrackPlayer();
        }
        else
        {
            PatrolRotation();
        }
    }

    // --- Core Sanity Logic ---

    /// <summary>
    /// Checks if the monster is visible within the player's camera view and applies sanity drain.
    /// </summary>
    /// <returns>True if the player's camera can see the monster.</returns>
    private bool CheckIfSeenByPlayer()
    {
        // 1. Frustum Check (Is the monster potentially visible?)
        // The monster must be in front of the camera (positive Z) and within the viewport (0-1 range).
        Vector3 viewportPoint = playerCamera.WorldToViewportPoint(transform.position);
        
        // Check if the monster is inside the viewport (0 to 1 on X and Y) and in front of the camera (Z > 0).
        bool inViewport = viewportPoint.z > 0 && 
                          viewportPoint.x > 0 && viewportPoint.x < 1 &&
                          viewportPoint.y > 0 && viewportPoint.y < 1;

        if (!inViewport)
        {
            return false;
        }

        // 2. Distance Check for Sanity Drain
        float distanceToPlayer = Vector3.Distance(transform.position, playerTarget.position);
        if (distanceToPlayer > sanityDrainRange)
        {
            return false;
        }
        
        // 3. Line of Sight Check (Is something blocking the view?)
        // Cast a ray from the camera position to the monster's position.
        Vector3 directionToMonster = (transform.position - playerCamera.transform.position).normalized;
        float rayDistance = distanceToPlayer; // Use the actual distance as the ray length.
        
        RaycastHit hit;
        // Layer mask to exclude the player itself from blocking the view, if needed (mask is optional).
        // For simplicity, we assume we only care if the Raycast hits the monster.
        if (Physics.Raycast(playerCamera.transform.position, directionToMonster, out hit, rayDistance))
        {
            // If the ray hits something, check if that 'something' is the monster itself.
            if (hit.collider.transform == transform)
            {
                // The monster is in the player's FOV and there's a clear line of sight.
                return true;
            }
        }
        
        // If the ray hits nothing or hits something other than the monster, line of sight is blocked/missed.
        return false;
    }
    
    /// <summary>
    /// Applies the sanity drain if the monster is visible to the player's camera AND the player is visible to the monster.
    /// Also increases the drain rate the longer the intersection occurs (compounding effect).
    /// </summary>
    private void HandleSanityDrain(bool isMonsterSeenByPlayer)
    {
        // STRICT CONDITION: Sanity drains only if the player sees the monster AND the monster sees the player.
        if (isMonsterSeenByPlayer && isPlayerDetected)
        {
            // 1. Increase stare timer (compound effect build-up)
            stareTimer += Time.deltaTime;
            
            // Calculate the current multiplier, ensuring it doesn't go below 1x and is capped.
            // Formula: Multiplier = 1 + (Time * CompoundRate)
            float currentMultiplier = 1f + (stareTimer * compoundRate);
            currentMultiplier = Mathf.Clamp(currentMultiplier, 1f, maxCompoundMultiplier);
            
            // 2. Apply compound drain
            float drainAmount = sanityDrainRate * currentMultiplier * Time.deltaTime;
            playerSanity.DepleteSanity(drainAmount);
        }
        else
        {
            // When the condition is not met (player or monster looks away), decrease the stare timer.
            // Decay the timer faster than it builds for quicker recovery.
            float decayRate = compoundRate * 2f; 
            stareTimer = Mathf.Max(0f, stareTimer - Time.deltaTime * decayRate);
        }
    }


    // --- Original Vision and Movement Logic ---

    /// <summary>
    /// Checks if the player is within range AND within the field of view cone of the monster.
    /// </summary>
    /// <returns>True if the player is visible to the monster, otherwise false.</returns>
    private bool CheckForPlayer()
    {
        // Calculate the vector pointing from the monster to the player.
        Vector3 directionToTarget = playerTarget.position - transform.position;

        // 1. Distance Check
        // If the player is too far away, return false immediately.
        if (directionToTarget.magnitude > viewRange)
        {
            return false;
        }

        // 2. Angle Check (Field of View - FOV)
        // Vector3.Angle returns the angle between the two vectors (Monster's forward direction and direction to player).
        float angleToTarget = Vector3.Angle(transform.forward, directionToTarget);

        // If the angle is less than half the FOV, the player is inside the vision cone.
        if (angleToTarget <= fieldOfViewAngle / 2f)
        {
            // 3. Raycast Check (Line-of-Sight)
            RaycastHit hit;
            // Does a raycast from the monster to the player, ignoring hits beyond the player.
            if (Physics.Raycast(transform.position, directionToTarget.normalized, out hit, viewRange))
            {
                // Check if the object hit is the player.
                if (hit.collider.transform == playerTarget)
                {
                    // Monster sees player, clear line of sight.
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Smoothly rotates the monster to face the player's current position.
    /// </summary>
    private void TrackPlayer()
    {
        // 1. Determine the target rotation needed to look at the player.
        Vector3 directionToTarget = playerTarget.position - transform.position;
        // Ignore the Y-axis (up/down) to ensure the monster only turns on the horizontal plane.
        directionToTarget.y = 0; 
        
        // Quaternion.LookRotation creates a rotation that looks along the direction vector.
        Quaternion targetRotation = Quaternion.LookRotation(directionToTarget);

        // 2. Smoothly rotate from the current rotation to the target rotation.
        // Quaternion.Slerp (Spherical Linear Interpolation) provides smooth rotation over time.
        transform.rotation = Quaternion.Slerp(
            transform.rotation, 
            targetRotation, 
            rotationSpeed * Time.deltaTime // Multiply by Time.deltaTime for frame-rate independence
        );
    }

    /// <summary>
    /// Rotates the monster slowly when it is not tracking the player.
    /// </summary>
    private void PatrolRotation()
    {
        // Rotate around the Y-axis (vertical axis) at a constant speed.
        transform.Rotate(Vector3.up * patrolSpeed * Time.deltaTime);
    }

    // --- Gizmos for Visual Debugging (Only visible in the Unity Editor Scene View) ---

    void OnDrawGizmosSelected()
    {
        // Only draw gizmos if we have the player target.
        if (playerTarget == null) return;
        
        // Set the color for the Gizmos (useful for debugging).
        Gizmos.color = isPlayerDetected ? Color.red : Color.yellow;

        // Draw the viewing range sphere.
        Gizmos.DrawWireSphere(transform.position, viewRange);

        // Draw the FOV cone lines.
        Vector3 fovLine1 = Quaternion.Euler(0, -fieldOfViewAngle / 2f, 0) * transform.forward * viewRange;
        Vector3 fovLine2 = Quaternion.Euler(0, fieldOfViewAngle / 2f, 0) * transform.forward * viewRange;

        Gizmos.DrawRay(transform.position, fovLine1);
        Gizmos.DrawRay(transform.position, fovLine2);
        
        // Draw the Sanity Drain Range
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, sanityDrainRange);

        // Draw a line indicating the monster's current facing direction.
        Gizmos.color = Color.blue;
        Gizmos.DrawRay(transform.position, transform.forward * 1f);
    }
}
