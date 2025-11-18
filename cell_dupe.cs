using UnityEngine;
using UnityEngine.InputSystem; // important!

public class StretchSphere : MonoBehaviour
{
    public float stretchSpeed = 2f; // Speed of stretching/shrinking
    private float minScale = 0.1f; // Minimum allowed scale

    void Update()
    {
        // Get input directly from the keyboard or gamepad
        float moveInputY = 0;

        // reads for Keyboard input (W/S or Up/Down arrows)
        if (Keyboard.current != null)
        {
            if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed)
            {
                moveInputY = 1;
                Debug.Log("Keyboard input detected: Stretching (W/UpArrow)");
            }
            else if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed)
            {
                moveInputY = -1;
                Debug.Log("Keyboard input detected: Shrinking (S/DownArrow)");
            }
        }


        // Log the current scale for debugging
        Debug.Log("Current Scale: " + transform.localScale);

        // Stretch the sphere when moving up
        if (moveInputY > 0)
        {
            transform.localScale += new Vector3(0, stretchSpeed * Time.deltaTime, 0);
            Debug.Log("Stretching: New Scale = " + transform.localScale);
        }

        // Shrink the sphere when moving down
        if (moveInputY < 0)
        {
            if (transform.localScale.y > minScale)
            {
                transform.localScale -= new Vector3(0, stretchSpeed * Time.deltaTime, 0);
                Debug.Log("Shrinking: New Scale = " + transform.localScale);
            }
            else
            {
                Debug.Log("Minimum scale reached. Cannot shrink further.");
            }
        }
        // Check if the sphere's scale exceeds the threshold for splitting
        int i = 1;
        if (transform.localScale.y > 6f)
        {
            //split in half only ONCE
            if (i == 1)
            {
                Vector3 newScale = new Vector3(transform.localScale.x, transform.localScale.y / 2, transform.localScale.z);
                transform.localScale = newScale;

                // Create a new sphere and set its scale
                GameObject newSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                newSphere.transform.position = transform.position + new Vector3(2, 0, 0); // Offset position to avoid overlap
                newSphere.transform.localScale = newScale;

                Debug.Log("Sphere split into two. New Scale: " + newScale);
                i++; // Increment to prevent further splits
            }
            


        }

        // Log if no input is detected
        if (moveInputY == 0)
        {
            Debug.Log("No input detected. Sphere remains unchanged.");
        }
    }
}