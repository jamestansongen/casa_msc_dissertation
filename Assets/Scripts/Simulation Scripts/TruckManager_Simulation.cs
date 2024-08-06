using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class TruckManager_Simulation : MonoBehaviour
{
    // method for truck placement. KMeans is for a single company scenario while Random is for multiple companies scenario.
    public enum PositioningMethod { KMeans, Random }
    public PositioningMethod positioningMethod = PositioningMethod.KMeans;

    // variables to set
    public int numberOfTrucks; // number of trucks or launch point
    public GameObject truckPrefab; // truck prefab to use
    public LayerMask groundLayerMask; // ground layer
    public float searchRadius = 500f; // search radius for possible spawn points
    public int maxAttempts = 10; // maximum attempts to search for valid positions
    public float minDistanceBetweenTrucks = 15f; // minimum distance between trucks to prevent bottlenecks

    private List<Vector3> redBoxPositions = new List<Vector3>(); // list of red box positions
    private Dictionary<GameObject, List<Vector3>> truckAssignments = new Dictionary<GameObject, List<Vector3>>(); // dictionary to store which boxes belong to which trucks
    private List<Vector3> truckSpawnPositions = new List<Vector3>(); // list of positions where trucks are spawned
    private int spawnedTruckCount = 0;

    public SimulationManager simulationManager; // reference to SimulationManager

    // find red boxes and trucks
    void Start()
    {
        StartCoroutine(FindRedBoxesAndPositionTrucks());
    }

    // wait a short while before finding red boxes
    IEnumerator FindRedBoxesAndPositionTrucks()
    {
        yield return new WaitForSeconds(0.5f);

        foreach (GameObject box in GameObject.FindGameObjectsWithTag("RedBox"))
        {
            redBoxPositions.Add(box.transform.position);
        }

        Debug.Log("Number of red box positions found: " + redBoxPositions.Count);

        PositionTrucks();
    }

    // positions trucks based on chosen method (KMeans or Random), check if valid spawn and instantiate them
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

        spawnedTruckCount = 0;

        for (int i = 0; i < truckPositions.Count; i++)
        {
            Vector3 truckPosition = truckPositions[i];
            Vector3 lastCheckedPosition = truckPosition;
            int attempts = 0;

            while ((!IsValidSpawnPosition(truckPosition) || IsTooCloseToOtherTrucks(truckPosition)) && attempts < maxAttempts)
            {
                truckPosition = lastCheckedPosition + new Vector3(Random.Range(-100f, 100f), 0, Random.Range(-100f, 100f));
                lastCheckedPosition = truckPosition;
                attempts++;
            }

            if ((!IsValidSpawnPosition(truckPosition) || IsTooCloseToOtherTrucks(truckPosition)) && attempts >= maxAttempts)
            {
                truckPosition = FindNearestGroundPosition(lastCheckedPosition);
                Debug.LogWarning("Could not find a valid position for truck " + i + " within max attempts. Truck spawned at nearest ground position: " + truckPosition);
            }

            if (!IsTooCloseToOtherTrucks(truckPosition))
            {
                GameObject truck = Instantiate(truckPrefab, truckPosition, Quaternion.identity);
                simulationManager.AddInstantiatedObject(truck); // Track each truck
                truckAssignments[truck] = boxAssignments[i];
                truckSpawnPositions.Add(truckPosition);

                TruckDroneDeployment_Simulation truckScript = truck.GetComponent<TruckDroneDeployment_Simulation>();
                if (truckScript != null)
                {
                    truckScript.assignedBoxes = boxAssignments[i];
                    truckScript.simulationManager = simulationManager; // set reference to SimulationManager
                }

                Debug.Log("Truck spawned at position: " + truckPosition + ", Attempts: " + attempts);
                spawnedTruckCount++;
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
            Vector3 groundPosition = collider.ClosestPoint(startPosition);
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

        int pointsPerTruck = points.Count / k;

        // initial equal distribution of points
        for (int i = 0; i < k; i++)
        {
            List<Vector3> assignedBoxes = new List<Vector3>();
            for (int j = 0; j < pointsPerTruck; j++)
            {
                assignedBoxes.Add(shuffledPoints[i * pointsPerTruck + j]);
            }
            boxAssignments.Add(assignedBoxes);
        }

        // distribute remaining boxes randomly
        int remainingBoxesStartIndex = pointsPerTruck * k;
        for (int i = remainingBoxesStartIndex; i < shuffledPoints.Count; i++)
        {
            int randomTruckIndex = Random.Range(0, k);
            boxAssignments[randomTruckIndex].Add(shuffledPoints[i]);
        }

        // calculate truck positions based on assigned boxes
        foreach (var assignedBoxes in boxAssignments)
        {
            Vector3 averagePosition = Vector3.zero;
            foreach (var box in assignedBoxes)
            {
                averagePosition += box;
            }
            averagePosition /= assignedBoxes.Count;
            positions.Add(averagePosition);
        }

        return (positions, boxAssignments);
    }

    // use Fisher-Yates shuffle algorithm to randomise order of elements to randomly give to truck
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

    // ensure truck is on a valid spawn position
    bool IsValidSpawnPosition(Vector3 position)
    {
        if (!IsOnGround(position))
        {
            Debug.LogWarning("Position not on ground: " + position);
            return false;
        }

        return true;
    }

    // ensure trucks is on the ground layer
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

    // ensure trucks are not too close each other else drones will instantly repel each other
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
