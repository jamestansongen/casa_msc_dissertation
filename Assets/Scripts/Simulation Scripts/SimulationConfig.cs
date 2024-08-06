using UnityEngine;

// this script is currently not in use

public class SimulationConfig : MonoBehaviour
{
    public static SimulationConfig Instance { get; private set; }

    public int NumberOfBoxesToSpawn { get; set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
}
