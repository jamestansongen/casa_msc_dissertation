using System.Collections;
using UnityEngine;

public class DroneMovementWithEnergyConsumption : MonoBehaviour
{
    public float flightHeight = 60f;
    public float hoverHeight = 15f;
    public float speed = 15f;
    public float deliveryTime = 30f;
    public float avoidanceRadius = 20f;
    public float droneAvoidanceStrength = 100.0f;
    public float buildingAvoidanceStrength = 10.0f;
    public float maxAvoidanceForce = 10.0f;
    public LayerMask droneLayerMask;
    public LayerMask buildingLayerMask;
    public float deliveryDistanceThreshold = 5.0f;

    private Vector3 _targetPosition;
    private Vector3 _truckPosition;
    private GameObject _redBox;
    private bool _isMoving = false;

    private Rigidbody _rigidbody;
    private Collider _collider;
    private EnergyConsumption _energyConsumption;

    private enum DroneState { Ascend, MoveToTarget, DescendToHover, Deliver, AscendBack, MoveToTruck, DescendBack, Completed, EmergencyLanding }
    private DroneState _state = DroneState.Ascend;

    private static int successfulDeliveries = 0;
    private static int emergencyLandings = 0;

    void Start()
    {
        _rigidbody = GetComponent<Rigidbody>();
        _collider = GetComponent<Collider>();
        _energyConsumption = GetComponent<EnergyConsumption>();

        if (_rigidbody == null || _collider == null || _energyConsumption == null)
        {
            Debug.LogError("Rigidbody, Collider, or EnergyConsumption component is missing from the drone.");
            return;
        }

        _isMoving = true;
        _truckPosition = transform.position;
    }

    void FixedUpdate()
    {
        if (_isMoving)
        {
            ApplyUpwardForce();
            HandleMovement();
            CheckEnergy();
        }
    }

    public void SetTargetPosition(Vector3 target, GameObject box)
    {
        _targetPosition = target;
        _redBox = box;
        Debug.Log("Target position set to: " + target);
    }

    void HandleMovement()
    {
        Debug.Log("Current state: " + _state);
        switch (_state)
        {
            case DroneState.Ascend:
                MoveToPosition(new Vector3(transform.position.x, flightHeight, transform.position.z), DroneState.MoveToTarget);
                break;
            case DroneState.MoveToTarget:
                MoveToPosition(new Vector3(_targetPosition.x, flightHeight, _targetPosition.z), DroneState.DescendToHover);
                break;
            case DroneState.DescendToHover:
                MoveToPosition(new Vector3(_targetPosition.x, _targetPosition.y + hoverHeight, _targetPosition.z), DroneState.Deliver);
                break;
            case DroneState.Deliver:
                StartCoroutine(DeliverAndAscend());
                break;
            case DroneState.AscendBack:
                MoveToPosition(new Vector3(_targetPosition.x, flightHeight, _targetPosition.z), DroneState.MoveToTruck);
                break;
            case DroneState.MoveToTruck:
                MoveToPosition(new Vector3(_truckPosition.x, flightHeight, _truckPosition.z), DroneState.DescendBack);
                break;
            case DroneState.DescendBack:
                MoveToPosition(new Vector3(_truckPosition.x, _truckPosition.y, _truckPosition.z), DroneState.Completed);
                break;
            case DroneState.Completed:
                StartCoroutine(DestroyNextFrame());
                break;
            case DroneState.EmergencyLanding:
                StartCoroutine(EmergencyLanding());
                break;
        }
    }

    IEnumerator DestroyNextFrame()
    {
        yield return null; // Wait for the next frame
        successfulDeliveries++; // Increment the counter
        Debug.Log("Successfully returned to truck after delivery.");
        Debug.Log("Returned drones: " + successfulDeliveries); // Log the counter
        Destroy(gameObject);
    }

    void MoveToPosition(Vector3 target, DroneState nextState)
    {
        float distance = Vector3.Distance(transform.position, target);
        if (distance > deliveryDistanceThreshold)
        {
            Vector3 direction = (target - transform.position).normalized;
            Vector3 avoidance = CalculateAvoidance();
            Vector3 newDirection = (direction + avoidance).normalized;

            // Ensure the drone doesn't go above flightHeight
            if (newDirection.y > 0 && transform.position.y + newDirection.y * speed * Time.deltaTime > flightHeight)
            {
                newDirection.y = 0;
            }

            _rigidbody.MovePosition(transform.position + newDirection * speed * Time.deltaTime);
            _energyConsumption.ConsumeEnergy(_energyConsumption.CalculateCruisePower(speed), Time.deltaTime);
        }
        else
        {
            Debug.Log("Reached position: " + target + " in state: " + _state);
            _state = nextState;
            if (_state == DroneState.Deliver && _redBox != null)
            {
                Destroy(_redBox);
                Debug.Log("Red box destroyed at position: " + _targetPosition);
            }
        }
    }

    IEnumerator DeliverAndAscend()
    {
        Debug.Log("Starting delivery...");
        yield return new WaitForSeconds(deliveryTime);
        Debug.Log("Delivery complete, ascending back");
        _state = DroneState.AscendBack;
    }

    IEnumerator DescendToTarget()
    {
        DisableBuildingCollider();
        while (Vector3.Distance(transform.position, _targetPosition) > deliveryDistanceThreshold)
        {
            Vector3 direction = (_targetPosition - transform.position).normalized;
            _rigidbody.MovePosition(transform.position + direction * speed * Time.deltaTime);
            _energyConsumption.ConsumeEnergy(_energyConsumption.CalculateHoverPower(), Time.deltaTime);
            yield return null;
        }
        _state = DroneState.Deliver;
        EnableBuildingCollider();
    }

    IEnumerator DescendToTruck()
    {
        DisableBuildingCollider();
        while (Vector3.Distance(transform.position, _truckPosition) > deliveryDistanceThreshold)
        {
            Vector3 direction = (_truckPosition - transform.position).normalized;
            _rigidbody.MovePosition(transform.position + direction * speed * Time.deltaTime);
            _energyConsumption.ConsumeEnergy(_energyConsumption.CalculateHoverPower(), Time.deltaTime);
            yield return null;
        }
        _state = DroneState.Completed;
        EnableBuildingCollider();
    }

    IEnumerator EmergencyLanding()
    {
        DisableBuildingCollider();
        while (transform.position.y > 0)
        {
            Vector3 direction = Vector3.down;
            _rigidbody.MovePosition(transform.position + direction * speed * Time.deltaTime);
            yield return null;
        }
        emergencyLandings++; // Increment the counter
        Debug.Log("Emergency landings: " + emergencyLandings); // Log the counter
        Destroy(gameObject);
    }

    Vector3 CalculateAvoidance()
    {
        Vector3 avoidance = Vector3.zero;

        // Avoidance from other drones
        Collider[] drones = Physics.OverlapSphere(transform.position, avoidanceRadius, droneLayerMask);
        foreach (Collider drone in drones)
        {
            if (drone.gameObject != this.gameObject)
            {
                Vector3 directionToDrone = transform.position - drone.transform.position;
                float distance = directionToDrone.magnitude;

                float forceMagnitude = Mathf.Min(droneAvoidanceStrength / Mathf.Pow(distance, 2), maxAvoidanceForce);
                avoidance += directionToDrone.normalized * forceMagnitude;

                Debug.Log("Avoiding drone at distance: " + distance + " with force: " + forceMagnitude);
            }
        }

        // Avoidance from buildings
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

    void ApplyUpwardForce()
    {
        _rigidbody.AddForce(Vector3.up * (_rigidbody.mass * -Physics.gravity.y));
    }

    void DisableBuildingCollider()
    {
        Physics.IgnoreLayerCollision(gameObject.layer, LayerMask.NameToLayer("Building"), true);
    }

    void EnableBuildingCollider()
    {
        Physics.IgnoreLayerCollision(gameObject.layer, LayerMask.NameToLayer("Building"), false);
    }

    void CheckEnergy()
    {
        if (_energyConsumption.IsEnergyLow())
        {
            _state = DroneState.EmergencyLanding;
        }
    }
}
