using UnityEngine;
using System.Collections.Generic;

public class DemandSpawn_Simulation : MonoBehaviour
{
    public GameObject redBoxPrefab;
    public float spawnRadius = 100f;
    public LayerMask buildingLayerMask;
    public LayerMask groundLayerMask;
    public int numberOfBoxes;
    public SimulationManager simulationManager; // reference to SimulationManager

    private List<Transform> buildings = new List<Transform>();

    // find number of buildings that can serve as potential spawn site
    void Start()
    {
        FindBuildings();
        if (buildings.Count == 0)
        {
            Debug.LogError("No buildings found!");
            return;
        }

        Debug.Log("Number of buildings found: " + buildings.Count);
        SpawnRedBoxes();
    }

    // find all child objects with the tag "Building" and add to list
    void FindBuildings()
    {
        Transform[] allChildren = GetComponentsInChildren<Transform>();
        foreach (Transform child in allChildren)
        {
            if (child.CompareTag("Building"))
            {
                buildings.Add(child);
            }
        }
    }

    // selects buildings and spawn it based on valid position while ensuring no infinite loop if one is not found
    void SpawnRedBoxes()
    {
        int spawnedBoxes = 0;
        int attempts = 0;

        while (spawnedBoxes < numberOfBoxes && attempts < numberOfBoxes * 10)
        {
            Transform building = buildings[Random.Range(0, buildings.Count)];
            Vector3 spawnPosition = GetRandomPositionNearBuilding(building);

            if (IsValidSpawnPosition(spawnPosition))
            {
                GameObject redBox = Instantiate(redBoxPrefab, spawnPosition, Quaternion.identity);
                simulationManager.AddSpawnedCube(redBox); // track each red box
                spawnedBoxes++;
            }

            attempts++;
        }

        if (attempts >= numberOfBoxes * 10)
        {
            Debug.LogWarning("Could not find enough valid spawn positions for the red boxes.");
        }
    }

    // returns a valid position or the last attempted position
    Vector3 GetRandomPositionNearBuilding(Transform building)
    {
        Vector3 randomPosition;
        int safetyCounter = 0;

        do
        {
            Vector3 randomDirection = Random.insideUnitSphere * spawnRadius;
            randomDirection.y = 0;
            randomPosition = building.position + randomDirection;
            safetyCounter++;

            if (safetyCounter > 100)
            {
                Debug.LogWarning("Could not find a valid position near the building after 100 attempts.");
                break;
            }
        }
        while (!IsPositionOutsideBuildingBounds(randomPosition) || !IsOnGround(randomPosition));

        return randomPosition;
    }

    // check box is outside of building bounds
    bool IsPositionOutsideBuildingBounds(Vector3 position)
    {
        Collider[] colliders = Physics.OverlapSphere(position, 1f, buildingLayerMask);
        return colliders.Length == 0;
    }

    // check box is on ground layer
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

    // check if valid spawn position by its on ground and outside building bounds
    bool IsValidSpawnPosition(Vector3 position)
    {
        return IsOnGround(position) && IsPositionOutsideBuildingBounds(position);
    }
}
