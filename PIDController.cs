
public class PIDController{

    public float Kp;
    public float Ki;
    public float Kd;

    public float outputMin = 0f;
    public float outputMax = 200f;

    private float integral = 0f;
    private float previousError = 0f;
    private bool hasPreviousError = false;

    public PIDController(float kp, float ki, float kd, float outputMin, float outputMax){
        Kp = kp;
        Ki = ki;
        Kd = kd;
        this.outputMin = outputMin;
        this.outputMax = outputMax;
    }

    public void Reset(){
        integral = 0f;
        previousError = 0f;
        hasPreviousError = false;
    }

    public float Compute(float dt, float setpoint, float measurement){
        float error = setpoint - measurement;

        float pTerm = Kp * error;

        float candidateIntegral = integral + error * dt;
        float iTermCandidate = Ki * candidateIntegral;

        float dTerm = 0f;
        if (hasPreviousError && dt > 0f)
            dTerm = Kd * (error - previousError) / dt;

        float outputUnclamped = pTerm + iTermCandidate + dTerm;
        float output = UnityEngine.Mathf.Clamp(outputUnclamped, outputMin, outputMax);

        // Aggiorniamo l'integrale solo se l'uscita non e' in saturazione,
        // oppure se l'integrale "aiuterebbe" comunque a uscire dalla
        // saturazione (es. output saturato in alto ma errore negativo:
        // l'integrale sta gia' scendendo, va bene farlo scendere).
        bool saturatedHigh = outputUnclamped > outputMax;
        bool saturatedLow = outputUnclamped < outputMin;
        bool integralWouldHelp = (saturatedHigh && error < 0f) || (saturatedLow && error > 0f);

        if (!saturatedHigh && !saturatedLow || integralWouldHelp)
            integral = candidateIntegral;

        previousError = error;
        hasPreviousError = true;

        return output;
    }
}
