using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TruckDroneDeploymentWithEnergyConsumption : MonoBehaviour
{
    public GameObject dronePrefab; // Prefab of the drone to deploy
    public List<Vector3> assignedBoxes = new List<Vector3>(); // List of delivery points for the drones
    public float deploymentDelay = 15f; // Delay between each drone deployment

    void Start()
    {
        StartCoroutine(DeployDrones());
    }

    IEnumerator DeployDrones()
    {
        foreach (Vector3 boxPosition in assignedBoxes)
        {
            Debug.Log("Deploying drone to box position: " + boxPosition);
            GameObject drone = Instantiate(dronePrefab, transform.position, Quaternion.identity);
            DroneMovementWithEnergyConsumption droneMovement = drone.GetComponent<DroneMovementWithEnergyConsumption>();
            if (droneMovement != null)
            {
                GameObject box = FindBoxAtPosition(boxPosition);
                droneMovement.SetTargetPosition(boxPosition, box);
                Debug.Log("Drone deployed with target position: " + boxPosition);
            }
            else
            {
                Debug.LogError("DroneMovementWithEnergyConsumption component not found on drone prefab.");
            }
            yield return new WaitForSeconds(deploymentDelay);
        }
    }

    GameObject FindBoxAtPosition(Vector3 position)
    {
        // Find the red box GameObject at the given position
        foreach (GameObject box in GameObject.FindGameObjectsWithTag("RedBox"))
        {
            if (Vector3.Distance(box.transform.position, position) < 0.1f)
            {
                return box;
            }
        }
        return null;
    }
}
