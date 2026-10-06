using System;
using System.IO;
using UnityEngine;

public class ThermalDataLogger : MonoBehaviour{
    [Header("Controller della Simulazione")]
    [Tooltip("Trascina qui l'oggetto che contiene l'HeatSimulationController")]
    public HeatSimulationController simController;

    [Header("Cosa loggare")]
    [Tooltip("L'oggetto di cui vuoi registrare la temperatura nel tempo (es. l'oggetto target, non la sorgente)")]
    public ThermalObjectSource targetObject;

    [Header("Impostazioni di logging")]
    [Tooltip("Ogni quanti secondi (di tempo di simulazione) scrivere una riga. 0 = scrivi in ogni step registrato")]
    public float logIntervalSeconds = 0f;

    [Tooltip("Se true, inizia a loggare automaticamente all'avvio della scena")]
    public bool startLoggingOnAwake = true;

    private StreamWriter writer;
    private float lastLogTime = 0f;
    private bool isLogging = false;
    private string filePath;

    private void Awake(){
        if (startLoggingOnAwake)
            StartLogging();
    }

    public void StartLogging(){
        if (targetObject == null){
            Debug.LogWarning("ThermalDataLogger: nessun targetObject assegnato, logging disabilitato.");
            enabled = false;
            return;
        }

        string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        string fileName = $"thermal_log_{targetObject.name}_{timestamp}.csv";

        string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        filePath = Path.Combine(desktopPath, fileName);

        writer = new StreamWriter(filePath, false);
        writer.WriteLine("time_s,temperature_C");

        lastLogTime = 0f;
        isLogging = true;

        Debug.Log($"ThermalDataLogger: registrazione avviata su {filePath}");
    }

    public void StopLogging(){
        if (!isLogging) return;

        isLogging = false;
        writer?.Flush();
        writer?.Close();
        writer = null;

        Debug.Log($"ThermalDataLogger: registrazione terminata. File salvato in: {filePath}");
    }

    private void Update(){
        if (!isLogging || writer == null || simController == null) return;

        // Se è passato il tempo di log rispetto all'orologio interno del simulatore
        if (simController.ElapsedSimTime - lastLogTime >= logIntervalSeconds){
            float temperature = targetObject.CurrentAverageTemperature;
            
            writer.WriteLine($"{simController.ElapsedSimTime.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)},{temperature.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)}");
            writer.Flush();
            
            lastLogTime = simController.ElapsedSimTime;
        }
    }

    private void OnApplicationQuit(){
        StopLogging();
    }

    private void OnDestroy(){
        StopLogging();
    }
}