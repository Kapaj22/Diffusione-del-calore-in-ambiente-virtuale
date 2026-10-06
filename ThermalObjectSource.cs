using UnityEngine;

[RequireComponent(typeof(Renderer))]
public class ThermalObjectSource : MonoBehaviour
{
    [Header("Proprietà del materiale")]
    [Tooltip("Conducibilità termica k [W/(m·K)]")]
    public float thermalConductivity = 200f;

    [Tooltip("Densità ρ [kg/m³]")]
    public float density = 2700f;

    [Tooltip("Calore specifico c [J/(kg·K)]")]
    public float specificHeat = 900f;

    [Tooltip("Temperatura iniziale dell'oggetto [°C]")]
    public float initialTemperature = 20f;

    [Header("Sorgente di calore (anello aperto)")]
    [Tooltip("Se true, questo oggetto è la sorgente: la sua temperatura resta fissata (Dirichlet), indipendente dallo scambio termico.")]
    public bool isHeatSource = false;

    [Tooltip("Temperatura imposta alla sorgente [°C], usata solo se isHeatSource = true")]
    public float fixedSourceTemperature = 150f;

    [Tooltip("Istante (in secondi dall'avvio della simulazione) in cui la sorgente passa da initialTemperature a fixedSourceTemperature. " +
             "0 = sorgente già accesa dall'inizio. Usalo per generare uno scalino netto e riproducibile per l'identificazione del modello FOPDT.")]
    public float stepTime = 0f;

    [Header("Visualizzazione")]
    [Tooltip("Temperatura corrispondente all'estremo 'freddo' del gradiente")]
    public float visualMinTemperature = 20f;
    [Tooltip("Temperatura corrispondente all'estremo 'caldo' del gradiente")]
    public float visualMaxTemperature = 150f;
    public Gradient temperatureGradient = DefaultGradient();

    public float Alpha => thermalConductivity / (density * specificHeat);

    public float CurrentAverageTemperature { get; private set; }

    private Renderer rend;
    private MaterialPropertyBlock mpb;
    private static readonly int ColorPropertyId = Shader.PropertyToID("_Color");
    private static readonly int BaseColorPropertyId = Shader.PropertyToID("_BaseColor");

    private void Awake()
    {
        rend = GetComponent<Renderer>();
        mpb = new MaterialPropertyBlock();
    }

    public void UpdateVisual(float averageTemperature)
    {
        CurrentAverageTemperature = averageTemperature;

        float t = Mathf.InverseLerp(visualMinTemperature, visualMaxTemperature, averageTemperature);
        Color c = temperatureGradient.Evaluate(t);

        rend.GetPropertyBlock(mpb);
        if (rend.sharedMaterial != null && rend.sharedMaterial.HasProperty(BaseColorPropertyId))
            mpb.SetColor(BaseColorPropertyId, c);
        else
            mpb.SetColor(ColorPropertyId, c);
        rend.SetPropertyBlock(mpb);
    }

    private static Gradient DefaultGradient()
    {
        var g = new Gradient();
        g.SetKeys(
            new[]
            {
                new GradientColorKey(Color.blue, 0f),
                new GradientColorKey(Color.green, 0.4f),
                new GradientColorKey(Color.yellow, 0.7f),
                new GradientColorKey(Color.red, 1f),
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(1f, 1f),
            }
        );
        return g;
    }
}