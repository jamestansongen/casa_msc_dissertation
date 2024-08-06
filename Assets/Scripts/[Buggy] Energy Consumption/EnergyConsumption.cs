using UnityEngine;

public class EnergyConsumption : MonoBehaviour
{
    public float batteryCapacity_mAh = 32000f; // mAh
    public float batteryVoltage = 22.2f; // V
    public float droneWeight = 7.2f; // kg (converted from 7200g to kg)
    public float maxPayloadWeight = 5.0f; // kg (converted from 5000g to kg)

    private float remainingEnergy; // in Wh

    void Start()
    {
        float batteryCapacity_Wh = (batteryCapacity_mAh / 1000f) * batteryVoltage; // Convert mAh to Ah and then to Wh
        remainingEnergy = batteryCapacity_Wh; // Initialize remaining energy in Wh
    }

    public float CalculateCruisePower(float speed)
    {
        // Power consumption formula for cruising based on Liu et al.
        float weight = droneWeight + maxPayloadWeight; // Total weight in kg
        float k1 = 1.0f; // Coefficient for cruising power (example value)
        float power = k1 * weight * speed; // Power in W
        return power;
    }

    public float CalculateHoverPower()
    {
        // Power consumption formula for hovering based on Liu et al.
        float weight = droneWeight + maxPayloadWeight; // Total weight in kg
        float k2 = 0.1f; // Coefficient for hovering power (example value)
        float power = k2 * weight * Physics.gravity.magnitude; // Power in W
        return power;
    }

    public float GetRemainingEnergy()
    {
        return remainingEnergy;
    }

    public void ConsumeEnergy(float power, float duration)
    {
        remainingEnergy -= power * duration / 3600f; // Convert power consumption from Wh to energy used per second
        if (remainingEnergy < 0)
        {
            remainingEnergy = 0;
        }
    }

    public bool IsEnergyLow()
    {
        return remainingEnergy <= 0.1f * ((batteryCapacity_mAh / 1000f) * batteryVoltage); // 10% of initial energy in Wh
    }
}
