using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DroneMovement_Simulation : MonoBehaviour
{
    // drone variables (e.g. speed, flight height, avoidance radius, etc.)
    public float flightHeight = 60f; // height at which drone flies at, set at 60 given that Manna drones fly at 50-65m
    public float hoverHeight = 15f; // height at which drone descends to drop package, set at 15 given that Manna drones descend to this to avoid risk to people and animals
    public float speed = 15f; // speed at which drone moves at, set at 15 given that Manna drones move at 15m/s
    public float deliveryTime = 10f; // time for drones to complete delivery, set at 10s but can be adjusted accordingly
    public float avoidanceRadius = 30f; // radius at which artificial potential fields come into effect, set to twice of speed
    public float droneAvoidanceStrength = 30.0f; // avoidance strength of artificial potential fields to other drones, set equal to avoidance radius
    public float buildingAvoidanceStrength = 10.0f; // avoidance strength of artificial potential fields to buildings, set 1/3 to avoidance radius
    public float maxAvoidanceForce = 30.0f; // avoidance strength of artificial potential fields will never exceed this value
    public float proximityRadius = 60f; // proximity radius in a sphere surrounding the drone
    public LayerMask droneLayerMask; // mask to identify other drones in that layer
    public LayerMask buildingLayerMask; // mask to identify buildings in that layer
    public float deliveryDistanceThreshold = 5.0f; // buffer for drone to descend and destroy red box

    // event declarations which are triggered on certain actions occur
    public static event System.Action<DroneMovement_Simulation> OnSuccessfulDelivery; // provides details of successful delivery
    public static event System.Action<DroneMovement_Simulation> OnFailedDelivery; // provides details of failed delivery
    public static event System.Action OnDroneEncounter; // when drone encounters other drones 
    public static event System.Action OnAvoidanceManeuver; // when drone avoids other drones
    public static event System.Action<float> OnFlightTimeUpdate; // update flight time metrics
    public static event System.Action<float> OnFlightTimeInProximityUpdate; // update flight time in proximity metrics

    private Vector3 _targetPosition; // coordinates of red box or delivery point to travel to
    private Vector3 _truckPosition; // coordinates of truck to return to
    private GameObject _redBox; // red box object to destroy upon delivery
    private bool _isMoving = false; // ensure drone is mooving only when it needs to

    private Rigidbody _rigidbody; // ensure physics can be applied to this object 
    private Collider _collider; // define shape of drone collider
    private enum DroneState { Ascend, MoveToTarget, DescendToHover, Deliver, AscendBack, MoveToTruck, DescendBack, Completed } // states which the drone transits from
    private DroneState _state = DroneState.Ascend; // starting state of drone
    private bool _actionStarted = false; // flag to mark if drone has started moving
    
    // bottleneck situations
    private float stuckTime = 0f; // time which drone is stuck for
    private Vector3 lastPosition; // check drone last position if it moved or is still stuck (only when it is supposed to be moving and not during delivery)
    private const float stuckTimeThreshold = 3f; // time which drone uses small random pertubations and repulsion force decay to escape
    private const float minDroneAvoidanceStrength = 10.0f; // minimum force required for drones to still avoid buildings and other drone
    private float initialDroneAvoidanceStrength; // initital strength to reset after escaping situation
    private float positionCheckInterval = 1f; // time interval of checking bottlenecks
    private float positionCheckTimer = 0f; // time since last position check

    // metrics to record (total flight time, total flight time in proximity to other drones, unique ID)
    private float flightTime = 0f; 
    private float flightTimeInProximity = 0f;
    private HashSet<int> encounteredDrones = new HashSet<int>();

    // metrics for split tracking of flight time and flight time in proximity to horizontal and vertical states
    private float flightTimeHorizontal = 0f;
    private float flightTimeHorizontalProximity = 0f;
    private float flightTimeVertical = 0f;
    private float flightTimeVerticalProximity = 0f;

    // number of destroyed drones
    private static int destroyedDroneCount = 0;

    // properties to track if drone has completed deliveries or not and bottleneck or not
    private bool isDelivered = false; 

    // public properties for SimulationManager to read and write
    public int UniqueEncounters => encounteredDrones.Count;
    public int AvoidanceManeuvers { get; private set; } = 0;
    public float FlightTime => flightTime;
    public float FlightTimeInProximity => flightTimeInProximity;
    public int EncounteredDronesCount => encounteredDrones.Count;
    public float FlightTimeHorizontal => flightTimeHorizontal;
    public float FlightTimeHorizontalProximity => flightTimeHorizontalProximity;
    public float FlightTimeVertical => flightTimeVertical;
    public float FlightTimeVerticalProximity => flightTimeVerticalProximity;
    public bool IsDelivered => isDelivered;
    public bool IsBottlenecked { get; private set; } = false;

    void Start()
    {
        // check if rigidbody or collider components are present
        _rigidbody = GetComponent<Rigidbody>();
        _collider = GetComponent<Collider>();
        if (_rigidbody == null || _collider == null)
        {
            //Debug.LogError("Rigidbody or Collider component is missing from the drone.");
            return;
        }

        _isMoving = true; // change state to isMoving
        _truckPosition = transform.position; // record truck position to return to
        initialDroneAvoidanceStrength = droneAvoidanceStrength; // record initialDroneAvoidanceStrength
        lastPosition = transform.position; // record lastPosition to consistently check if drone is struck or not

        // initial categorisation of the drone to unsuccessfulNoBottlenecks category
        //Debug.Log("Drone added to unsuccessfulNoBottlenecks category initially.");
        SimulationManager.Instance.AddDroneToCategory(this, "unsuccessfulNoBottlenecks");
    }

    // if drone is moving to ApplyUpwardForce (to counteract gravity), HandleMovement (to handle different states) and TrackProximity (to record proximity to other drones)
    void FixedUpdate()
    {
        if (_isMoving) 
        {
            ApplyUpwardForce();
            HandleMovement();
            TrackProximity();
        }
    }

    // move from current position to delivery point
    public void SetTargetPosition(Vector3 target, GameObject box)
    {
        _targetPosition = target;
        _redBox = box;
    }

    // function to ascend vertically, move horizontally to target, descend vertically to target, delivery, repeat same path back and complete delivery
    void HandleMovement()
    {
        switch (_state)
        {
            case DroneState.Ascend:
                if (!_actionStarted)
                {
                    //Debug.Log("Ascending...");
                    _actionStarted = true;
                }
                MoveToPosition(new Vector3(transform.position.x, flightHeight, transform.position.z), DroneState.MoveToTarget);
                break;
            case DroneState.MoveToTarget:
                if (!_actionStarted)
                {
                    //Debug.Log("Moving to target...");
                    _actionStarted = true;
                }
                MoveToPosition(new Vector3(_targetPosition.x, flightHeight, _targetPosition.z), DroneState.DescendToHover);
                break;
            case DroneState.DescendToHover:
                if (!_actionStarted)
                {
                    //Debug.Log("Descending to hover...");
                    _actionStarted = true;
                }
                MoveToPosition(new Vector3(_targetPosition.x, _targetPosition.y + hoverHeight, _targetPosition.z), DroneState.Deliver);
                break;
            case DroneState.Deliver:
                if (!_actionStarted)
                {
                    //Debug.Log("Starting delivery...");
                    _actionStarted = true;
                    StartCoroutine(DeliverAndAscend());
                }
                break;
            case DroneState.AscendBack:
                if (!_actionStarted)
                {
                    //Debug.Log("Ascending back...");
                    _actionStarted = true;
                }
                MoveToPosition(new Vector3(_targetPosition.x, flightHeight, _targetPosition.z), DroneState.MoveToTruck);
                break;
            case DroneState.MoveToTruck:
                if (!_actionStarted)
                {
                    //Debug.Log("Moving to truck...");
                    _actionStarted = true;
                }
                MoveToPosition(new Vector3(_truckPosition.x, flightHeight, _truckPosition.z), DroneState.DescendBack);
                break;
            case DroneState.DescendBack:
                if (!_actionStarted)
                {
                    //Debug.Log("Descending back...");
                    _actionStarted = true;
                }
                MoveToPosition(new Vector3(_truckPosition.x, _truckPosition.y, _truckPosition.z), DroneState.Completed);
                break;
            case DroneState.Completed:
                if (!_actionStarted)
                {
                    //Debug.Log("Delivery completed.");
                    _actionStarted = true;
                    isDelivered = true; // mark the delivery as successful here
                    //Debug.Log("Drone moved to appropriate category based on isDelivered and IsBottlenecked flags.");
                    SimulationManager.Instance.UpdateDroneCategory(this); // update drone to succesfulNoBottlenecks or successfulBottlenecks category
                    StartCoroutine(DestroyNextFrame());
                }
                break;
        }
    }

    // time to wait for drone delivery
    IEnumerator DeliverAndAscend()
    {
        yield return new WaitForSeconds(deliveryTime);
        _state = DroneState.AscendBack;
        _actionStarted = false; // reset the flag here, after the coroutine finishes
    }

    // destroy drones upon returning to truck
    IEnumerator DestroyNextFrame()
    {
        yield return null; // wait for the next frame
        destroyedDroneCount++;
        if (isDelivered)
        {
            //Debug.Log("Successfully returned to truck after delivery.");
            OnSuccessfulDelivery?.Invoke(this);
        }
        else
        {
            //Debug.Log("Unsuccessfully returned to truck. Delivery failed.");
            OnFailedDelivery?.Invoke(this);
        }
        Debug.Log("Returned drones: " + destroyedDroneCount);
        Destroy(gameObject);
    }

    // movement logic
    void MoveToPosition(Vector3 target, DroneState nextState)
    {
        if (Vector3.Distance(transform.position, target) > deliveryDistanceThreshold)
        {
            Vector3 direction = (target - transform.position).normalized;
            Vector3 avoidance = CalculateAvoidance();
            Vector3 newDirection = (direction + avoidance).normalized;

            // ensure the drone doesn't go above flightHeight or below ground level
            if (newDirection.y > 0 && transform.position.y + newDirection.y * speed * Time.deltaTime > flightHeight)
            {
                newDirection.y = 0;
            }
            if (newDirection.y < 0 && transform.position.y + newDirection.y * speed * Time.deltaTime < 0)
            {
                newDirection.y = 0;
            }

            // apply a small random perturbation to help escape local minima, only if stuck
            if (stuckTime > stuckTimeThreshold)
            {
                newDirection += new Vector3(Random.Range(-1f, 1f), 0, Random.Range(-1f, 1f));
            }

            _rigidbody.MovePosition(transform.position + newDirection * speed * Time.deltaTime);

            // update stuck detection logic
            positionCheckTimer += Time.deltaTime;
            if (positionCheckTimer >= positionCheckInterval)
            {
                if (_state != DroneState.Deliver && Vector3.Distance(transform.position, lastPosition) < 0.5f)
                {
                    stuckTime += positionCheckTimer;
                    if (stuckTime > stuckTimeThreshold)
                    {
                        Debug.LogWarning($"Drone {gameObject.GetInstanceID()} is stuck, applying decay to repulsion force.");
                        IsBottlenecked = true; // mark the drone as bottlenecked
                        SimulationManager.Instance.UpdateDroneCategory(this); // update category to unsuccessfulBottlenecks
                        droneAvoidanceStrength = Mathf.Max(droneAvoidanceStrength * 0.5f, minDroneAvoidanceStrength); // decay the repulsion force more aggressively but maintain minimum strength
                    }
                }
                else
                {
                    stuckTime = 0f; // reset stuck time if the drone is moving
                    droneAvoidanceStrength = Mathf.Min(droneAvoidanceStrength / 0.5f, initialDroneAvoidanceStrength); // gradually restore the avoidance strength if the drone is not stuck
                }

                lastPosition = transform.position; // update the last known position
                positionCheckTimer = 0f; // reset the position check timer
            }
        }
        else
        {
            _state = nextState;
            _actionStarted = false; // reset action started flag when state changes
            if (_state == DroneState.Deliver && _redBox != null)
            {
                Destroy(_redBox);
                Debug.Log("Red box destroyed at position: " + _targetPosition);
            }
        }
    }

    // calculate the avoidance from other drones and buildings using inverse square of distance and force
    Vector3 CalculateAvoidance()
    {
        Vector3 avoidance = Vector3.zero;

        // avoidance from other drones
        Collider[] drones = Physics.OverlapSphere(transform.position, avoidanceRadius, droneLayerMask);
        foreach (Collider drone in drones)
        {
            if (drone.gameObject != this.gameObject)
            {
                Vector3 directionToDrone = transform.position - drone.transform.position;
                float distance = directionToDrone.magnitude;

                float forceMagnitude = Mathf.Min(droneAvoidanceStrength / Mathf.Pow(distance, 2), maxAvoidanceForce);
                avoidance += directionToDrone.normalized * forceMagnitude;

                AvoidanceManeuvers++;
                OnAvoidanceManeuver?.Invoke();
                //Debug.Log("Avoiding drone at distance: " + distance + " with force: " + forceMagnitude);
            }
        }

        // avoidance from buildings
        Collider[] buildings = Physics.OverlapSphere(transform.position, avoidanceRadius, buildingLayerMask);
        foreach (Collider building in buildings)
        {
            Vector3 directionToBuilding = transform.position - building.transform.position;
            float distance = directionToBuilding.magnitude;

            float forceMagnitude = Mathf.Min(buildingAvoidanceStrength / Mathf.Pow(distance, 2), maxAvoidanceForce);
            avoidance += directionToBuilding.normalized * forceMagnitude;
        }

        return avoidance;
    }

    // function to counteract gravity
    void ApplyUpwardForce()
    {
        _rigidbody.AddForce(Vector3.up * (_rigidbody.mass * -Physics.gravity.y));
    }

    // function to track unique drone encounters and horizontal+vertical proximity
    void TrackProximity()
    {
        Collider[] drones = Physics.OverlapSphere(transform.position, proximityRadius, droneLayerMask);
        bool isInProximity = drones.Length > 1;

        bool isHorizontalState = _state == DroneState.MoveToTarget || _state == DroneState.MoveToTruck;
        bool isVerticalState = _state == DroneState.Ascend || _state == DroneState.DescendToHover ||
                               _state == DroneState.AscendBack || _state == DroneState.DescendBack;

        foreach (Collider drone in drones)
        {
            if (drone.gameObject != this.gameObject)
            {
                encounteredDrones.Add(drone.GetInstanceID());
                OnDroneEncounter?.Invoke();
            }
        }

        if (isHorizontalState)
        {
            flightTimeHorizontal += Time.deltaTime;
            if (isInProximity) flightTimeHorizontalProximity += Time.deltaTime;
        }
        else if (isVerticalState)
        {
            flightTimeVertical += Time.deltaTime;
            if (isInProximity) flightTimeVerticalProximity += Time.deltaTime;
        }

        flightTime += Time.deltaTime;
        if (isInProximity) flightTimeInProximity += Time.deltaTime;

        OnFlightTimeUpdate?.Invoke(flightTime);
        if (isInProximity) OnFlightTimeInProximityUpdate?.Invoke(flightTimeInProximity);
    }
}
