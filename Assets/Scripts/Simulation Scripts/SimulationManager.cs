using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;

public class SimulationManager : MonoBehaviour
{
    // variables to set prior to run
    public int[] boxCounts = { 100, 150, 200, 250, 300 }; // set values of demand points (i.e. maximum number of drones in the sky)
    public int[] truckCounts = { 20, 25, 30, 35, 40 }; // set values of trucks (i.e. number of launch points)
    public int numberOfRuns = 15; // set number of runs for each permutation
    public GameObject demandSpawnPrefab; // set object used for spawning demand points
    public GameObject truckManagerPrefab; // set trucks used for spawning drones
    public float maxSimulationTime = 600f; // set maximum simulation time before timeout and proceed to next simulation in seconds (e.g. 10 minutes)

    // references to demand spawn and truck manager
    private GameObject demandSpawn;
    private GameObject truckManager;

    // metrics to track current run
    private int currentRun = 0;
    private int currentBoxIndex = 0;
    private int currentTruckIndex = 0;
    private string currentMethod = "KMeans";
    private List<string> simulationResults = new List<string>();
    private List<GameObject> instantiatedObjects = new List<GameObject>();
    private List<GameObject> spawnedCubes = new List<GameObject>(); // to track spawned cubes
    private List<GameObject> spawnedDrones = new List<GameObject>(); // to track spawned drones
    private float simulationStartTime;

    // lists to categorise drones
    private List<DroneMovement_Simulation> successfulNoBottlenecks = new List<DroneMovement_Simulation>();
    private List<DroneMovement_Simulation> successfulBottlenecks = new List<DroneMovement_Simulation>();
    private List<DroneMovement_Simulation> unsuccessfulNoBottlenecks = new List<DroneMovement_Simulation>();
    private List<DroneMovement_Simulation> unsuccessfulBottlenecks = new List<DroneMovement_Simulation>();

    // ensure only one class is created throughout the run
    private static SimulationManager _instance;
    public static SimulationManager Instance => _instance;
    private bool isCompletingSimulation = false; // flag to track completion
    private Coroutine checkTimeoutCoroutine; // coroutine reference for timeout check

    // progress bar
    private int totalSimulations;
    private int currentSimulation;

    // ensure only one class is created throughout the run
    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(this.gameObject);
        }
        else
        {
            _instance = this;
            DontDestroyOnLoad(this.gameObject);
        }

        totalSimulations = 2 * boxCounts.Length * truckCounts.Length * numberOfRuns;
        currentSimulation = 0;
    }

    // add relevant GameObjects
    void Start()
    {
        StartCoroutine(StartNextSimulation());
    }

    public void AddInstantiatedObject(GameObject obj)
    {
        instantiatedObjects.Add(obj);
    }

    public void AddSpawnedCube(GameObject cube)
    {
        spawnedCubes.Add(cube);
    }

    public void AddSpawnedDrone(GameObject drone)
    {
        spawnedDrones.Add(drone);
    }

    IEnumerator StartNextSimulation()
    {
        simulationStartTime = Time.time;

        // Update and display the progress
        currentSimulation++;
        Debug.Log($"Progress: {currentSimulation}/{totalSimulations} simulations completed.");

        // check if all combinations are complete
        if (currentRun >= numberOfRuns)
        {
            if (currentMethod == "KMeans")
            {
                currentMethod = "Random";
                currentRun = 0;
                currentBoxIndex = 0;
                currentTruckIndex = 0; // reset all indices when switching methods
            }
            else
            {
                ExportResultsToCSV();
                Debug.Log("All simulations completed.");
                ExitPlayMode();
                yield break;
            }
        }

        // if runs are completed, switch method or finish
        if (currentRun >= numberOfRuns)
        {
            if (currentMethod == "KMeans")
            {
                currentMethod = "Random";
                currentRun = 0;
            }
            else
            {
                ExportResultsToCSV();
                Debug.Log("All simulations completed.");
                ExitPlayMode();
                yield break;
            }
        }

        // reset lists and metrics for the new run
        successfulNoBottlenecks.Clear();
        successfulBottlenecks.Clear();
        unsuccessfulNoBottlenecks.Clear();
        unsuccessfulBottlenecks.Clear();

        // instantiate and configure the DemandSpawn script
        demandSpawn = Instantiate(demandSpawnPrefab);
        AddInstantiatedObject(demandSpawn);  // track the demandSpawn object directly
        DemandSpawn_Simulation demandSpawnScript = demandSpawn.GetComponent<DemandSpawn_Simulation>();
        demandSpawnScript.numberOfBoxes = boxCounts[currentBoxIndex];
        demandSpawnScript.simulationManager = this; // set the reference to the simulation manager

        yield return new WaitForSeconds(1f); // wait for DemandSpawn to complete

        // instantiate and configure the TruckManager
        truckManager = Instantiate(truckManagerPrefab);
        AddInstantiatedObject(truckManager); // track the truckManager object directly
        TruckManager_Simulation truckManagerScript = truckManager.GetComponent<TruckManager_Simulation>();
        truckManagerScript.numberOfTrucks = truckCounts[currentTruckIndex];
        truckManagerScript.positioningMethod = currentMethod == "KMeans" ? TruckManager_Simulation.PositioningMethod.KMeans : TruckManager_Simulation.PositioningMethod.Random;
        truckManagerScript.simulationManager = this; // set the reference to the simulation manager

        // subscribe to delivery events
        DroneMovement_Simulation.OnSuccessfulDelivery += OnDeliveryCompletion;
        DroneMovement_Simulation.OnFailedDelivery += OnDeliveryCompletion;

        // start a coroutine to check for simulation timeout
        if (checkTimeoutCoroutine != null)
        {
            StopCoroutine(checkTimeoutCoroutine);
        }
        checkTimeoutCoroutine = StartCoroutine(CheckSimulationTimeout());
}

    // check for simulation timeout, if exceeded to proceed to next run, used in case of bottlenecks
    IEnumerator CheckSimulationTimeout()
    {
        while (true)
        {
            if (Time.time - simulationStartTime > maxSimulationTime && !isCompletingSimulation) // check the flag
            {
                Debug.LogWarning("Simulation timeout reached. Moving to next simulation.");
                CompleteCurrentSimulation();
                yield break; // stop the coroutine after timeout
            }
            yield return new WaitForSeconds(1f); // check every second
        }
    }

    // add drones to the appropriate category at initialisation
    public void AddDroneToCategory(DroneMovement_Simulation drone, string category)
    {
        switch (category)
        {
            case "successfulNoBottlenecks":
                successfulNoBottlenecks.Add(drone);
                break;
            case "successfulBottlenecks":
                successfulBottlenecks.Add(drone);
                break;
            case "unsuccessfulNoBottlenecks":
                unsuccessfulNoBottlenecks.Add(drone);
                break;
            case "unsuccessfulBottlenecks":
                unsuccessfulBottlenecks.Add(drone);
                break;
        }
    }

    // update drones category as its state changes
    public void UpdateDroneCategory(DroneMovement_Simulation drone)
    {
        // remove the drone from its current category
        if (successfulNoBottlenecks.Remove(drone))
        {
            Debug.Log("Drone removed from successfulNoBottlenecks.");
        }
        else if (successfulBottlenecks.Remove(drone))
        {
            Debug.Log("Drone removed from successfulBottlenecks.");
        }
        else if (unsuccessfulNoBottlenecks.Remove(drone))
        {
            Debug.Log("Drone removed from unsuccessfulNoBottlenecks.");
        }
        else if (unsuccessfulBottlenecks.Remove(drone))
        {
            Debug.Log("Drone removed from unsuccessfulBottlenecks.");
        }

        // add the drone to its new category based on its flags
        if (drone.IsDelivered)
        {
            if (drone.IsBottlenecked)
            {
                successfulBottlenecks.Add(drone);
                Debug.Log("Drone added to successfulBottlenecks.");
            }
            else
            {
                successfulNoBottlenecks.Add(drone);
                Debug.Log("Drone added to successfulNoBottlenecks.");
            }
        }
        else
        {
            if (drone.IsBottlenecked)
            {
                unsuccessfulBottlenecks.Add(drone);
                Debug.Log("Drone added to unsuccessfulBottlenecks.");
            }
            else
            {
                unsuccessfulNoBottlenecks.Add(drone);
                Debug.Log("Drone added to unsuccessfulNoBottlenecks.");
            }
        }
    }

    // move to next simulation once dell deliveries are completed
    void OnDeliveryCompletion(DroneMovement_Simulation drone)
    {
        UpdateDroneCategory(drone);

        // check if all drones have completed their tasks
        if (AllDronesCompleted() && !isCompletingSimulation) // check the flag before proceeding
        {
            CompleteCurrentSimulation();
        }
    }

    // number of successful deliveries must equal spawnedDrones to move to next round (besides timeout)
    bool AllDronesCompleted()
    {
        return successfulNoBottlenecks.Count + successfulBottlenecks.Count >= spawnedDrones.Count;
    }
    
    // function upon completion of current simulation
    void CompleteCurrentSimulation()
    {

        isCompletingSimulation = true; // set the flag before starting completion

        // unsubscribe from delivery events for cleanup purposes and prevent memory leakage
        DroneMovement_Simulation.OnSuccessfulDelivery -= OnDeliveryCompletion;
        DroneMovement_Simulation.OnFailedDelivery -= OnDeliveryCompletion;

        // ensure indices are within bounds before accessing arrays (i.e. prevent out of bounds error)
        int boxIndex = Mathf.Clamp(currentBoxIndex, 0, boxCounts.Length - 1);
        int truckIndex = Mathf.Clamp(currentTruckIndex, 0, truckCounts.Length - 1);

        // collect and record metrics for all categories in one row
        RecordMetrics(boxIndex, truckIndex);

        // destroy all spawned cubes
        foreach (var cube in spawnedCubes)
        {
            Destroy(cube);
        }
        spawnedCubes.Clear();

        // destroy all spawned drones
        foreach (var drone in spawnedDrones)
        {
            Destroy(drone);
        }
        spawnedDrones.Clear();

        // destroy all instantiated objects (demand spawn, truck manager, etc.)
        foreach (var obj in instantiatedObjects)
        {
            Destroy(obj);
        }
        instantiatedObjects.Clear();

        // destroy demand spawn and truck manager objects directly
        if (demandSpawn != null)
        {
            Debug.Log("Destroying demand spawn");
            Destroy(demandSpawn);
            demandSpawn = null;
        }
        if (truckManager != null)
        {
            Debug.Log("Destroying truck manager");
            Destroy(truckManager);
            truckManager = null;
        }

        // increment indices in the correct order
        currentBoxIndex++;
        if (currentBoxIndex >= boxCounts.Length)
        {
            currentBoxIndex = 0;
            currentTruckIndex++;
            if (currentTruckIndex >= truckCounts.Length)
            {
                currentTruckIndex = 0;
                currentRun++;
            }
        }

        isCompletingSimulation = false; // reset the flag after completion

        // stop the timeout coroutine
        if (checkTimeoutCoroutine != null)
        {
            StopCoroutine(checkTimeoutCoroutine);
            checkTimeoutCoroutine = null;
        }

        StartCoroutine(DelayedNextSimulation());
    }

    void RecordMetrics(int boxIndex, int truckIndex)
    {
        // define variables to store the total values for each metric and each category. 4 is used becaused 4 different categories
        int[] categoryCounts = new int[4];
        int[] totalEncounters = new int[4];
        int[] uniqueDronesInProximity = new int[4];
        int[] totalAvoidanceManeuvers = new int[4];
        float[] totalFlightTime = new float[4];
        float[] totalFlightTimeInProximity = new float[4];
        float[] totalFlightTimeHorizontal = new float[4];
        float[] totalFlightTimeHorizontalProximity = new float[4];
        float[] totalFlightTimeVertical = new float[4];
        float[] totalFlightTimeVerticalProximity = new float[4];

        // function to accumulate drone metrics into the respective arrays
        void AccumulateMetrics(List<DroneMovement_Simulation> drones, int categoryIndex)
        {
            categoryCounts[categoryIndex] = drones.Count;
            foreach (var drone in drones)
            {
                totalEncounters[categoryIndex] += drone.UniqueEncounters;
                uniqueDronesInProximity[categoryIndex] += drone.EncounteredDronesCount;
                totalAvoidanceManeuvers[categoryIndex] += drone.AvoidanceManeuvers;
                totalFlightTime[categoryIndex] += drone.FlightTime;
                totalFlightTimeInProximity[categoryIndex] += drone.FlightTimeInProximity;
                totalFlightTimeHorizontal[categoryIndex] += drone.FlightTimeHorizontal;
                totalFlightTimeHorizontalProximity[categoryIndex] += drone.FlightTimeHorizontalProximity;
                totalFlightTimeVertical[categoryIndex] += drone.FlightTimeVertical;
                totalFlightTimeVerticalProximity[categoryIndex] += drone.FlightTimeVerticalProximity;
            }
        }

        // accumulate metrics for each category
        AccumulateMetrics(successfulNoBottlenecks, 0);
        AccumulateMetrics(successfulBottlenecks, 1);
        AccumulateMetrics(unsuccessfulNoBottlenecks, 2);
        AccumulateMetrics(unsuccessfulBottlenecks, 3);

        // log List Counts
        Debug.Log($"SuccessfulNoBottlenecks Count: {successfulNoBottlenecks.Count}");
        Debug.Log($"SuccessfulBottlenecks Count: {successfulBottlenecks.Count}");
        Debug.Log($"UnsuccessfulNoBottlenecks Count: {unsuccessfulNoBottlenecks.Count}");
        Debug.Log($"UnsuccessfulBottlenecks Count: {unsuccessfulBottlenecks.Count}");

        // log Accumulated Metrics
        for (int i = 0; i < 4; i++)
        {
            Debug.Log($"Category {i} - Count: {categoryCounts[i]}, TotalEncounters: {totalEncounters[i]}, UniqueDronesInProximity: {uniqueDronesInProximity[i]}, TotalAvoidanceManeuvers: {totalAvoidanceManeuvers[i]}, TotalFlightTime: {totalFlightTime[i]}, TotalFlightTimeInProximity: {totalFlightTimeInProximity[i]}, TotalFlightTimeHorizontal: {totalFlightTimeHorizontal[i]}, TotalFlightTimeHorizontalProximity: {totalFlightTimeHorizontalProximity[i]}, TotalFlightTimeVertical: {totalFlightTimeVertical[i]}, TotalFlightTimeVerticalProximity: {totalFlightTimeVerticalProximity[i]}");
        }

        // construct a single row string with all categories in the correct order
        string result = $"{boxCounts[boxIndex]},{truckCounts[truckIndex]},{currentMethod},";

        // use a loop to iterate over all category metrics in the correct order
        for (int i = 0; i < categoryCounts.Length; i++)
        {
            result += $"{categoryCounts[i]},{totalEncounters[i]},{uniqueDronesInProximity[i]},{totalAvoidanceManeuvers[i]}," +
                      $"{totalFlightTime[i]},{totalFlightTimeInProximity[i]},{totalFlightTimeHorizontal[i]},{totalFlightTimeHorizontalProximity[i]}," +
                      $"{totalFlightTimeVertical[i]},{totalFlightTimeVerticalProximity[i]},";
        }

        // remove the last trailing comma
        result = result.TrimEnd(',');

        simulationResults.Add(result);
    }

    IEnumerator DelayedNextSimulation()
    {
        yield return new WaitForSeconds(1.0f); // small delay for cleanup (adjust as needed)
        StartCoroutine(StartNextSimulation());
    }

    // write results when quit application
    void OnApplicationQuit()
    {
        ExportResultsToCSV();
    }

    // export results to CSV which is located in the assets folder
    void ExportResultsToCSV()
    {
        string timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string fileName = $"SimulationResults_{timestamp}.csv";
        string filePath = Path.Combine(Application.dataPath, fileName);
        using (StreamWriter writer = new StreamWriter(filePath))
        {
            writer.WriteLine("TotalDrones,TotalTrucks,Method," +
                             "SuccessfulNoBottlenecks_Count,SuccessfulNoBottlenecks_Encounters,SuccessfulNoBottlenecks_UniqueDrones,SuccessfulNoBottlenecks_AvoidanceManeuvers,SuccessfulNoBottlenecks_FlightTime,SuccessfulNoBottlenecks_FlightTimeInProximity,SuccessfulNoBottlenecks_FlightTimeHorizontal,SuccessfulNoBottlenecks_FlightTimeHorizontalProximity,SuccessfulNoBottlenecks_FlightTimeVertical,SuccessfulNoBottlenecks_FlightTimeVerticalProximity," +
                             "SuccessfulBottlenecks_Count,SuccessfulBottlenecks_Encounters,SuccessfulBottlenecks_UniqueDrones,SuccessfulBottlenecks_AvoidanceManeuvers,SuccessfulBottlenecks_FlightTime,SuccessfulBottlenecks_FlightTimeInProximity,SuccessfulBottlenecks_FlightTimeHorizontal,SuccessfulBottlenecks_FlightTimeHorizontalProximity,SuccessfulBottlenecks_FlightTimeVertical,SuccessfulBottlenecks_FlightTimeVerticalProximity," +
                             "UnsuccessfulNoBottlenecks_Count,UnsuccessfulNoBottlenecks_Encounters,UnsuccessfulNoBottlenecks_UniqueDrones,UnsuccessfulNoBottlenecks_AvoidanceManeuvers,UnsuccessfulNoBottlenecks_FlightTime,UnsuccessfulNoBottlenecks_FlightTimeInProximity,UnsuccessfulNoBottlenecks_FlightTimeHorizontal,UnsuccessfulNoBottlenecks_FlightTimeHorizontalProximity,UnsuccessfulNoBottlenecks_FlightTimeVertical,UnsuccessfulNoBottlenecks_FlightTimeVerticalProximity," +
                             "UnsuccessfulBottlenecks_Count,UnsuccessfulBottlenecks_Encounters,UnsuccessfulBottlenecks_UniqueDrones,UnsuccessfulBottlenecks_AvoidanceManeuvers,UnsuccessfulBottlenecks_FlightTime,UnsuccessfulBottlenecks_FlightTimeInProximity,UnsuccessfulBottlenecks_FlightTimeHorizontal,UnsuccessfulBottlenecks_FlightTimeHorizontalProximity,UnsuccessfulBottlenecks_FlightTimeVertical,UnsuccessfulBottlenecks_FlightTimeVerticalProximity");
            foreach (string result in simulationResults)
            {
                writer.WriteLine(result);
            }
        }
        Debug.Log("Results exported to " + filePath);
    }

    void ExitPlayMode()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
