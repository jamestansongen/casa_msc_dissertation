using UnityEngine;
using System.Collections.Generic;

public class DemandSpawn : MonoBehaviour
{
    public int numberOfBoxes = 1000; // Number of boxes/demand points to spawn
    public GameObject redBoxPrefab; // Prefab of the red box to spawn
    public float spawnRadius = 100f; // Increased radius around the residential buildings where boxes can spawn.
    public LayerMask buildingLayerMask; // Layer mask to identify buildings
    public LayerMask groundLayerMask; // Layer mask to identify ground

    private List<Transform> residentialBuildings = new List<Transform>();

    void Start()
    {
        // Get all residential buildings
        foreach (Transform child in transform)
        {
            residentialBuildings.Add(child);
        }

        SpawnRedBoxes();
    }

    void SpawnRedBoxes()
    {
        int spawnedBoxes = 0;
        int attempts = 0; // To prevent infinite loops
        while (spawnedBoxes < numberOfBoxes && attempts < numberOfBoxes * 10)
        {
            // Randomly select a residential building
            Transform residentialBuilding = residentialBuildings[Random.Range(0, residentialBuildings.Count)];
            Vector3 spawnPosition = GetRandomPositionNearBuilding(residentialBuilding);

            // Check if the spawn position is valid
            if (IsValidSpawnPosition(spawnPosition))
            {
                Instantiate(redBoxPrefab, spawnPosition, Quaternion.identity);
                spawnedBoxes++;
            }
            attempts++;
        }

        if (attempts >= numberOfBoxes * 10)
        {
            Debug.LogWarning("Could not find enough valid spawn positions for the red boxes.");
        }
    }

    Vector3 GetRandomPositionNearBuilding(Transform building)
    {
        Vector3 randomPosition;
        int safetyCounter = 0;

        do
        {
            // Get a random position around the building within the spawn radius
            Vector3 randomDirection = Random.insideUnitSphere * spawnRadius;
            randomDirection.y = 0; // Keep the position on the ground level
            randomPosition = building.position + randomDirection;
            safetyCounter++;

            if (safetyCounter > 100) // Avoid infinite loops
            {
                Debug.LogWarning("Could not find a valid position near the building after 100 attempts.");
                break;
            }
        }
        while (!IsPositionOutsideBuildingBounds(randomPosition) || !IsOnGround(randomPosition));

        return randomPosition;
    }

    bool IsPositionOutsideBuildingBounds(Vector3 position)
    {
        // Check if the position is outside any building using Physics.OverlapSphere
        Collider[] colliders = Physics.OverlapSphere(position, 1f, buildingLayerMask); // Adjust the radius as needed
        if (colliders.Length > 0)
        {
            return false;
        }
        return true;
    }

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

    bool IsValidSpawnPosition(Vector3 position)
    {
        // Check if the position is on the ground and not inside any building
        return IsOnGround(position) && IsPositionOutsideBuildingBounds(position);
    }
}
