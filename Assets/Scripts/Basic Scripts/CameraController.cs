using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraController : MonoBehaviour
{
    public float moveSpeed = 50.0f; // forward (W), backward (S), left (A), right (D)
    public float rotateSpeed = 90.0f; //rotate speed
    public float verticalMoveSpeed = 50.0f; // up (E), down (Q)

    public float minVerticalAngle = -90f; // maximum downwards angle
    public float maxVerticalAngle = 90f;  // maximum upwards angle

    private float verticalRotation = 0f;  // default vertical rotation

    void Update()
    {
        // horizontal movement
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");
        Vector3 movement = new Vector3(horizontal, 0f, vertical) * moveSpeed * Time.deltaTime;
        transform.Translate(movement, Space.Self);

        // vertical movement
        float upDown = 0f;
        if (Input.GetKey(KeyCode.E)) // E key to move up
        {
            upDown = 1f;
        }
        else if (Input.GetKey(KeyCode.Q)) // Q key to move down
        {
            upDown = -1f;
        }

        transform.Translate(Vector3.up * upDown * verticalMoveSpeed * Time.deltaTime, Space.World); // apply vertical movement

        // rotation
        if (Input.GetMouseButton(1)) // right mouse button for rotation
        {
            float xRotation = Input.GetAxis("Mouse X") * rotateSpeed * Time.deltaTime;
            float yRotation = Input.GetAxis("Mouse Y") * rotateSpeed * Time.deltaTime;

            verticalRotation -= yRotation; // invert Y axis for intuitive rotation
            verticalRotation = Mathf.Clamp(verticalRotation, minVerticalAngle, maxVerticalAngle);

            transform.localRotation = Quaternion.Euler(verticalRotation, transform.localEulerAngles.y + xRotation, 0f); // apply rotations
        }
    }
}