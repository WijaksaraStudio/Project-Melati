using UnityEngine;

// This script should be attached to the Player's main GameObject.
public class PlayerSanity : MonoBehaviour
{
    [Header("Sanity Settings")]
    [Tooltip("The player's current sanity level (0.0 to 100.0).")]
    [Range(0f, 100f)]
    public float currentSanity = 100f;
    
    [Tooltip("The maximum sanity level.")]
    public const float MAX_SANITY = 100f;
    
    [Tooltip("The minimum sanity level.")]
    public const float MIN_SANITY = 0f;

    [Header("Sanity Regeneration")]
    [Tooltip("Amount of sanity to restore per second when safe.")]
    public float passiveSanityRegenRate = 0f;

    // A reference to the player's main camera to check what the player is looking at.
    private Camera playerCamera;

    void Start()
    {
        // Attempt to find the main camera. Assumes the camera is tagged "MainCamera".
        playerCamera = Camera.main;
        if (playerCamera == null)
        {
            Debug.LogError("PlayerSanity: Main Camera not found. Please ensure your camera is tagged 'MainCamera'.");
        }
    }

    void Update()
    {
        // Passive Sanity Regeneration
        if (currentSanity < MAX_SANITY)
        {
            currentSanity += passiveSanityRegenRate * Time.deltaTime;
            currentSanity = Mathf.Clamp(currentSanity, MIN_SANITY, MAX_SANITY);
        }

        // Example: Check for game over condition
        if (currentSanity <= MIN_SANITY)
        {
            Debug.Log("Sanity depleted! The player has gone insane.");
            // TODO: Implement game over/lose condition here.
        }
    }

    /// <summary>
    /// Reduces the player's sanity by a specified amount.
    /// </summary>
    /// <param name="amount">The amount to reduce sanity by (e.g., 5.0f).</param>
    public void DepleteSanity(float amount)
    {
        currentSanity -= amount;
        currentSanity = Mathf.Clamp(currentSanity, MIN_SANITY, MAX_SANITY);
        Debug.Log($"Sanity depleted by {amount}. Current Sanity: {currentSanity:F2}");
    }
}
