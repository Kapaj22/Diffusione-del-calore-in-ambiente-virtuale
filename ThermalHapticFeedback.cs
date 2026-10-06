using UnityEngine;
using WeArt.Core;
using WeArt.Components;

[RequireComponent(typeof(Collider))]
public class ThermalHapticFeedback : MonoBehaviour
{
    [Header("Impostazioni Sensazione Termica")]
    public float minTempThreshold = 30f;
    public float maxTempThreshold = 100f;

    [Header("Hardware WEART")]
    [Tooltip("Il componente WeArtHapticObject che controlla i ditali di questa mano")]
    public WeArtHapticObject weartHapticObject;

    private void OnTriggerStay(Collider other){
        ThermalObjectSource thermalObj = other.GetComponent<ThermalObjectSource>();
        
        if (thermalObj != null && weartHapticObject != null)
        {
            float currentTemp = thermalObj.CurrentAverageTemperature;

            if (currentTemp < minTempThreshold) {
                weartHapticObject.Temperature = Temperature.Default;
                return;
            }

            float intensity = Mathf.InverseLerp(minTempThreshold, maxTempThreshold, currentTemp);

            Temperature weartTemp = new Temperature();
            weartTemp.Active = true;
            weartTemp.Value = intensity;
            weartHapticObject.EnablingActuating(true); //per bypassare i controlli di sicurezza da reimpostare ogni volta
            weartHapticObject.Temperature = weartTemp;
        }
    }

    private void OnTriggerExit(Collider other){
        ThermalObjectSource thermalObj = other.GetComponent<ThermalObjectSource>();
        if (thermalObj != null && weartHapticObject != null)
        {
            weartHapticObject.Temperature = Temperature.Default;
        }
    }
}