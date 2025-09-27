using UnityEngine;
using UnityEngine.UI; // Required for UI components like Text or Slider
using TMPro; // Use TextMeshPro if available, as it's the modern standard

// This script should be attached to a UI Text or TextMeshPro element in your Canvas.
public class SanityDisplay : MonoBehaviour
{
    [Tooltip("Reference to the PlayerSanity script on the player GameObject.")]
    public PlayerSanity playerSanity;

    // Use TextMeshProUGUI if you're using TextMeshPro (highly recommended in modern Unity).
    // If you are using the older built-in Text, change this type to 'Text'.
    private TextMeshProUGUI sanityText; 

    void Start()
    {
        // Get the Text component attached to this GameObject
        sanityText = GetComponent<TextMeshProUGUI>();

        if (sanityText == null)
        {
            Debug.LogError("SanityDisplay requires a TextMeshProUGUI component attached to this GameObject!");
            return;
        }

        // --- Auto-Find PlayerSanity ---
        if (playerSanity == null)
        {
            // Look for the PlayerSanity script on the object tagged "Player" or "GameController"
            GameObject playerObject = GameObject.FindWithTag("Player"); 
            if (playerObject != null)
            {
                playerSanity = playerObject.GetComponent<PlayerSanity>();
            }

            if (playerSanity == null)
            {
                Debug.LogError("PlayerSanity script not found in the scene! Ensure the player has the script and is tagged 'Player'.");
            }
        }

        // Initial update
        UpdateSanityDisplay();
    }

    void Update()
    {
        // Update the display every frame to show the current value
        UpdateSanityDisplay();
    }

    private void UpdateSanityDisplay()
    {
        if (playerSanity != null && sanityText != null)
        {
            // Determine the color based on sanity level (e.g., green at max, red at min)
            Color displayColor = Color.Lerp(Color.red, Color.green, playerSanity.currentSanity / PlayerSanity.MAX_SANITY);

            // Set the text format to show the sanity level as a percentage or value
            sanityText.color = displayColor;
            sanityText.text = $"SANITY: {playerSanity.currentSanity:F0}%"; // Display as a whole number percentage
        }
    }
}
