using System.Collections.Generic;
using UnityEngine;

public class HeatSimulationController : MonoBehaviour
{
    [Header("Griglia di simulazione")]
    [Tooltip("Risoluzione della griglia 3D (numero di voxel per asse)")]
    public Vector3Int resolution = new Vector3Int(24, 24, 24);

    [Tooltip("Margine attorno al bounding box di tutti gli oggetti, in unità Unity (metri)")]
    public float domainMargin = 1.0f;

    [Header("Aria (mezzo di fondo)")]
    [Tooltip("Diffusività termica dell'aria [m²/s].")]
    public float airAlpha = 0.15f;
    public float initialAirTemperature = 20f;

    [Header("Ambiente esterno (condizione di Robin sul bordo del dominio)")]
    public float ambientTemperature = 20f;
    public float boundaryExchangeRate = 0.05f;

    [Header("Anello chiuso (PID)")]
    [Tooltip("Se true, la temperatura della sorgente è decisa dal PID in base all'errore rispetto al setpoint. Se false, si comporta come prima (gradino fisso su fixedSourceTemperature/stepTime).")]
    public bool useClosedLoop = false;

    [Tooltip("L'oggetto di cui vuoi controllare la temperatura (es. 'Target', NON la sorgente)")]
    public ThermalObjectSource controlTarget;

    [Tooltip("Temperatura che vuoi che l'oggetto controllato raggiunga [°C]")]
    public float setpointTemperature = 60f;

    public float pidKp = 132.9f;
    public float pidKi = 0.867f;
    public float pidKd = 0f;

    [Tooltip("Range fisico ammesso per la temperatura della sorgente [°C]. Il PID non potrà mai comandare un valore fuori da questo intervallo.")]
    public float sourceOutputMin = 20f;
    public float sourceOutputMax = 200f;

    [Header("Controllo Simulazione Accelerata")]
    [Tooltip("Quanti passi fisici calcolare in un singolo frame renderizzato")]
    public int stepsPerFrame = 200;
    
    [Tooltip("Il passo di integrazione temporale ")]
    public float simDeltaTime = 0.02f;

    [Header("Debug")]
    public bool logAverageTemperatures = false;

    public float ElapsedSimTime { get; private set; } = 0f;

    private HeatGridModel model;
    private float dx;
    private Bounds domainBounds;
    private List<ThermalObjectSource> thermalObjects;
    private Dictionary<ThermalObjectSource, List<int>> objectVoxels;
    private PIDController pid;

    private void Start(){
        thermalObjects = new List<ThermalObjectSource>(FindObjectsOfType<ThermalObjectSource>());
        if (thermalObjects.Count == 0){
            Debug.LogWarning("HeatSimulationController: nessun ThermalObjectSource trovato in scena.");
            enabled = false;
            return;
        }

        BuildDomainBounds();
        BuildGrid();
        VoxelizeObjects();

        if (useClosedLoop){
            pid = new PIDController(pidKp, pidKi, pidKd, sourceOutputMin, sourceOutputMax);
            pid.Reset();
        }
    }

    private void BuildDomainBounds(){
        domainBounds = thermalObjects[0].GetComponent<Renderer>().bounds;
        foreach (var obj in thermalObjects)
            domainBounds.Encapsulate(obj.GetComponent<Renderer>().bounds);

        domainBounds.Expand(domainMargin * 2f);
    }

    private void BuildGrid(){
        float maxSize = Mathf.Max(domainBounds.size.x, domainBounds.size.y, domainBounds.size.z);
        int maxRes = Mathf.Max(resolution.x, Mathf.Max(resolution.y, resolution.z));
        dx = maxSize / maxRes;

        model = new HeatGridModel(resolution.x, resolution.y, resolution.z, dx, initialAirTemperature);

        for (int i = 0; i < model.alpha.Length; i++)
            model.alpha[i] = airAlpha;

        model.ambientTemperature = ambientTemperature;
        model.boundaryExchangeRate = boundaryExchangeRate;
    }

    private void VoxelizeObjects(){
        objectVoxels = new Dictionary<ThermalObjectSource, List<int>>();
        foreach (var obj in thermalObjects)
            objectVoxels[obj] = new List<int>();

        Vector3 origin = domainBounds.min;
        Vector3 cellSize = new Vector3(dx, dx, dx);

        for (int k = 0; k < model.nz; k++)
        for (int j = 0; j < model.ny; j++)
        for (int i = 0; i < model.nx; i++){
            Vector3 cellCenter = origin + new Vector3((i + 0.5f) * dx, (j + 0.5f) * dx, (k + 0.5f) * dx);
            int idx = model.Index(i, j, k);

            Bounds voxelBounds = new Bounds(cellCenter, cellSize);
            ThermalObjectSource owner = FindOwner(voxelBounds);
            if (owner == null) continue;

            model.alpha[idx] = owner.Alpha;
            model.SetTemperature(i, j, k, owner.initialTemperature);

            if (owner.isHeatSource){
                model.isFixed[idx] = true;
                model.fixedTemperature[idx] = owner.fixedSourceTemperature;
            }

            objectVoxels[owner].Add(idx);
        }
    }

    private ThermalObjectSource FindOwner(Bounds voxelBounds)
{
        foreach (var obj in thermalObjects){
            if (obj.GetComponent<Renderer>().bounds.Intersects(voxelBounds))
                return obj;
        }
        return null;
    }

    private void Update(){
        if (model == null) return;

        // Esegue il calcolo fisico N volte per ogni frame visivo
        for (int i = 0; i < stepsPerFrame; i++){
            EseguiStepFisico();
        }

        // Aggiorna i colori dello schermo una sola volta alla fine del blocco
        UpdateVisualization();
    }

    private void EseguiStepFisico(){
        ElapsedSimTime += simDeltaTime;

        if (useClosedLoop)
            UpdateSourceTemperaturesClosedLoop(simDeltaTime);
        else
            UpdateSourceTemperaturesOpenLoop();

        float dtMax = model.ComputeMaxStableDt();
        int substeps = Mathf.Max(1, Mathf.CeilToInt(simDeltaTime / dtMax));
        float dt = simDeltaTime / substeps;

        for (int s = 0; s < substeps; s++)
            model.Step(dt);
    }

    private void UpdateSourceTemperaturesOpenLoop(){
        foreach (var obj in thermalObjects){
            if (!obj.isHeatSource) continue;

            float target = (ElapsedSimTime >= obj.stepTime)
                ? obj.fixedSourceTemperature
                : obj.initialTemperature;

            foreach (int idx in objectVoxels[obj])
                model.fixedTemperature[idx] = target;
        }
    }

    private void UpdateSourceTemperaturesClosedLoop(float dt){
        if (controlTarget == null || pid == null) return;

        float measurement = controlTarget.CurrentAverageTemperature;
        float sourceCommand = pid.Compute(dt, setpointTemperature, measurement);

        foreach (var obj in thermalObjects){
            if (!obj.isHeatSource) continue;

            foreach (int idx in objectVoxels[obj])
                model.fixedTemperature[idx] = sourceCommand;
        }
    }

    private void UpdateVisualization(){
        foreach (var obj in thermalObjects){
            var voxels = objectVoxels[obj];
            if (voxels.Count == 0) continue;

            float sum = 0f;
            foreach (int idx in voxels)
                sum += model.GetTemperature(idx);

            float avg = sum / voxels.Count;
            obj.UpdateVisual(avg);

            if (logAverageTemperatures)
                Debug.Log($"{obj.name}: T media = {avg:F2} °C");
        }
    }
}