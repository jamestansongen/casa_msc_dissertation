using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TruckDroneDeployment_Simulation : MonoBehaviour
{
    public GameObject dronePrefab; // prefab of the drone to deploy
    public List<Vector3> assignedBoxes = new List<Vector3>(); // list of delivery points for the drones
    public float deploymentDelay = 15f; // delay between each drone deployment
    public SimulationManager simulationManager; // reference to SimulationManager

    void Start()
    {
        StartCoroutine(DeployDrones());
    }

    IEnumerator DeployDrones()
    {
        SortAssignedBoxesByDistance(); // sort the assigned boxes list from furthest to nearest

        foreach (Vector3 boxPosition in assignedBoxes)
        {
            GameObject drone = Instantiate(dronePrefab, transform.position, Quaternion.identity);
            simulationManager.AddSpawnedDrone(drone); // track each drone
            DroneMovement_Simulation droneMovement = drone.GetComponent<DroneMovement_Simulation>();
            if (droneMovement != null)
            {
                droneMovement.SetTargetPosition(boxPosition, FindBoxAtPosition(boxPosition));
            }
            yield return new WaitForSeconds(deploymentDelay);
        }
    }

    void SortAssignedBoxesByDistance()
    {
        assignedBoxes.Sort((a, b) =>
        {
            float distanceA = Vector3.Distance(transform.position, a);
            float distanceB = Vector3.Distance(transform.position, b);
            return distanceB.CompareTo(distanceA); // sort from furthest to nearest
        });
    }

    // used to find the position of the box
    GameObject FindBoxAtPosition(Vector3 position)
    {
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
