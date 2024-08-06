using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class TruckManagerWithEnergyConsumption : MonoBehaviour
{
    public enum PositioningMethod { KMeans, Random } // Different types of methods to position trucks
    public PositioningMethod positioningMethod = PositioningMethod.KMeans;

    public int numberOfTrucks = 50; // Number of trucks to spawn
    public GameObject truckPrefab; // Prefab of the truck to spawn
    public LayerMask groundLayerMask; // Layer mask to identify ground
    public float searchRadius = 500f; // Radius to search for the nearest ground position
    public int maxAttempts = 10; // Maximum attempts to find a valid position
    public float minDistanceBetweenTrucks = 10f; // Minimum distance between trucks

    private List<Vector3> redBoxPositions = new List<Vector3>();
    private Dictionary<GameObject, List<Vector3>> truckAssignments = new Dictionary<GameObject, List<Vector3>>();
    private List<Vector3> truckSpawnPositions = new List<Vector3>(); // List of spawned truck positions
    private int spawnedTruckCount = 0; // Counter for spawned trucks

    void Start()
    {
        // Start the coroutine to wait and then find the red boxes
        StartCoroutine(FindRedBoxesAndPositionTrucks());
    }

    IEnumerator FindRedBoxesAndPositionTrucks()
    {
        // Wait for a short time to ensure all red boxes are instantiated and tagged
        yield return new WaitForSeconds(0.5f);

        // Get the positions of all red boxes spawned by DemandSpawn
        foreach (GameObject box in GameObject.FindGameObjectsWithTag("RedBox"))
        {
            redBoxPositions.Add(box.transform.position);
        }

        Debug.Log("Number of red box positions found: " + redBoxPositions.Count);

        PositionTrucks();
    }

    void PositionTrucks()
    {
        List<Vector3> truckPositions;
        List<List<Vector3>> boxAssignments;

        switch (positioningMethod)
        {
            case PositioningMethod.KMeans:
                (truckPositions, boxAssignments) = KMeansClustering(redBoxPositions, numberOfTrucks);
                break;
            case PositioningMethod.Random:
                (truckPositions, boxAssignments) = RandomPositioning(redBoxPositions, numberOfTrucks);
                break;
            default:
                truckPositions = new List<Vector3>();
                boxAssignments = new List<List<Vector3>>();
                break;
        }

        if (truckPositions.Count != boxAssignments.Count)
        {
            Debug.LogError("Mismatch between truck positions and box assignments counts.");
            return;
        }

        spawnedTruckCount = 0; // Reset counter before placing trucks

        for (int i = 0; i < truckPositions.Count; i++)
        {
            Vector3 truckPosition = truckPositions[i];
            Vector3 lastCheckedPosition = truckPosition;
            int attempts = 0;

            while ((!IsValidSpawnPosition(truckPosition) || IsTooCloseToOtherTrucks(truckPosition)) && attempts < maxAttempts)
            {
                // Apply a larger random offset and retry
                truckPosition = lastCheckedPosition + new Vector3(Random.Range(-100f, 100f), 0, Random.Range(-100f, 100f));
                lastCheckedPosition = truckPosition; // Store the last checked position
                attempts++;
            }

            // Use the last checked position if maxAttempts is reached
            if ((!IsValidSpawnPosition(truckPosition) || IsTooCloseToOtherTrucks(truckPosition)) && attempts >= maxAttempts)
            {
                truckPosition = FindNearestGroundPosition(lastCheckedPosition);
                Debug.LogWarning("Could not find a valid position for truck " + i + " within max attempts. Truck spawned at nearest ground position: " + truckPosition);
            }

            if (!IsTooCloseToOtherTrucks(truckPosition))
            {
                GameObject truck = Instantiate(truckPrefab, truckPosition, Quaternion.identity);
                truckAssignments[truck] = boxAssignments[i];
                truckSpawnPositions.Add(truckPosition); // Add the position to the list of spawned truck positions
                TruckDroneDeploymentWithEnergyConsumption truckScript = truck.GetComponent<TruckDroneDeploymentWithEnergyConsumption>(); // Ensure TruckDroneDeploymentWithEnergyConsumption component is attached
                if (truckScript != null)
                {
                    truckScript.assignedBoxes = boxAssignments[i]; // Assign the boxes to the truck
                }

                Debug.Log("Truck spawned at position: " + truckPosition + ", Attempts: " + attempts);
                spawnedTruckCount++; // Increment the counter for each successfully spawned truck
            }
            else
            {
                Debug.LogWarning("Truck position too close to another truck: " + truckPosition);
            }
        }

        Debug.Log("Total trucks successfully spawned: " + spawnedTruckCount);
    }

    Vector3 FindNearestGroundPosition(Vector3 startPosition)
    {
        Collider[] colliders = Physics.OverlapSphere(startPosition, searchRadius, groundLayerMask);
        Vector3 nearestPosition = startPosition;
        float nearestDistance = float.MaxValue;

        foreach (Collider collider in colliders)
        {
            Vector3 groundPosition = collider.ClosestPoint(startPosition); // Get the closest point on the collider
            float distance = Vector3.Distance(startPosition, groundPosition);

            if (distance < nearestDistance)
            {
                nearestPosition = groundPosition;
                nearestDistance = distance;
            }
        }

        return nearestPosition;
    }

    (List<Vector3>, List<List<Vector3>>) KMeansClustering(List<Vector3> points, int k)
    {
        // Initialize centroids
        List<Vector3> centroids = new List<Vector3>();
        for (int i = 0; i < k; i++)
        {
            centroids.Add(points[Random.Range(0, points.Count)]);
        }

        bool hasChanged = true;
        List<Vector3> newCentroids = new List<Vector3>(centroids);
        List<List<Vector3>> clusters = new List<List<Vector3>>();

        while (hasChanged)
        {
            clusters.Clear();
            for (int i = 0; i < k; i++)
            {
                clusters.Add(new List<Vector3>());
            }

            // Assign points to nearest centroid
            foreach (Vector3 point in points)
            {
                int nearest = 0;
                float nearestDistance = Vector3.Distance(point, centroids[0]);
                for (int i = 1; i < k; i++)
                {
                    float distance = Vector3.Distance(point, centroids[i]);
                    if (distance < nearestDistance)
                    {
                        nearest = i;
                        nearestDistance = distance;
                    }
                }
                clusters[nearest].Add(point);
            }

            // Update centroids
            hasChanged = false;
            for (int i = 0; i < k; i++)
            {
                if (clusters[i].Count == 0) continue;

                Vector3 newCentroid = Vector3.zero;
                foreach (Vector3 point in clusters[i])
                {
                    newCentroid += point;
                }
                newCentroid /= clusters[i].Count;

                if (Vector3.Distance(newCentroid, centroids[i]) > 0.1f)
                {
                    hasChanged = true;
                    newCentroids[i] = newCentroid;
                }
            }

            centroids = new List<Vector3>(newCentroids);
        }

        return (centroids, clusters);
    }

    (List<Vector3>, List<List<Vector3>>) RandomPositioning(List<Vector3> points, int k)
    {
        List<Vector3> positions = new List<Vector3>();
        List<List<Vector3>> boxAssignments = new List<List<Vector3>>();
        List<Vector3> shuffledPoints = new List<Vector3>(points);
        ShuffleList(shuffledPoints);

        int pointsPerTruck = Mathf.Max(1, points.Count / k);
        for (int i = 0; i < k; i++)
        {
            int startIdx = i * pointsPerTruck;
            int endIdx = Mathf.Min(startIdx + pointsPerTruck, shuffledPoints.Count);

            if (startIdx >= shuffledPoints.Count)
                break;

            Vector3 averagePosition = Vector3.zero;
            List<Vector3> assignedBoxes = new List<Vector3>();
            for (int j = startIdx; j < endIdx; j++)
            {
                averagePosition += shuffledPoints[j];
                assignedBoxes.Add(shuffledPoints[j]);
            }
            averagePosition /= (endIdx - startIdx);

            positions.Add(averagePosition); // Add position regardless of validity
            boxAssignments.Add(new List<Vector3>(assignedBoxes)); // Create a new list to avoid reference issues
        }

        return (positions, boxAssignments);
    }

    void ShuffleList(List<Vector3> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            Vector3 temp = list[i];
            int randomIndex = Random.Range(i, list.Count);
            list[i] = list[randomIndex];
            list[randomIndex] = temp;
        }
    }

    bool IsValidSpawnPosition(Vector3 position)
    {
        if (!IsOnGround(position))
        {
            Debug.LogWarning("Position not on ground: " + position);
            return false;
        }

        return true;
    }

    // Ensure that Trucks are spawned on "Ground" layer
    bool IsOnGround(Vector3 position)
    {
        RaycastHit hit;
        if (Physics.Raycast(position + Vector3.up * 100, Vector3.down, out hit, Mathf.Infinity, groundLayerMask))
        {
            position.y = hit.point.y;
            return true;
        }
        return false;
    }

    bool IsTooCloseToOtherTrucks(Vector3 position)
    {
        foreach (Vector3 truckPosition in truckSpawnPositions)
        {
            if (Vector3.Distance(position, truckPosition) < minDistanceBetweenTrucks)
            {
                return true;
            }
        }
        return false;
    }
}

