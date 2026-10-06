using UnityEngine;


public class HeatGridModel
{
    public readonly int nx, ny, nz;
    public readonly float dx;
    private float[] current;
    private float[] next;
    public readonly float[] alpha;
    public readonly bool[] isFixed;
    public readonly float[] fixedTemperature;

    public float ambientTemperature = 20f;
    public float boundaryExchangeRate = 0.05f;

    public HeatGridModel(int nx, int ny, int nz, float dx, float initialTemperature)
    {
        this.nx = nx; this.ny = ny; this.nz = nz; this.dx = dx;
        int n = nx * ny * nz;
        current = new float[n];
        next = new float[n];
        alpha = new float[n];
        isFixed = new bool[n];
        fixedTemperature = new float[n];

        for (int i = 0; i < n; i++) current[i] = initialTemperature;
    }

    public int Index(int i, int j, int k) => i + nx * (j + ny * k);

    public float GetTemperature(int i, int j, int k) => current[Index(i, j, k)];
    public float GetTemperature(int idx) => current[idx];

    public void SetTemperature(int i, int j, int k, float t) => current[Index(i, j, k)] = t;

    public float ComputeMaxStableDt(float safetyMargin = 0.9f)
    {
        float maxAlpha = 0f;
        for (int i = 0; i < alpha.Length; i++)
            if (alpha[i] > maxAlpha) maxAlpha = alpha[i];

        if (maxAlpha <= 0f) return float.MaxValue;
        return safetyMargin * (dx * dx) / (6f * maxAlpha);
    }

    public void Step(float dt)
    {
        for (int k = 0; k < nz; k++)
        for (int j = 0; j < ny; j++)
        for (int i = 0; i < nx; i++)
        {
            int idx = Index(i, j, k);

            if (isFixed[idx])
            {
                next[idx] = fixedTemperature[idx];
                continue;
            }

            float T = current[idx];
            float a = alpha[idx];

            float lap = 0f;
            lap += Neighbor(i - 1, j, k, T) - T;
            lap += Neighbor(i + 1, j, k, T) - T;
            lap += Neighbor(i, j - 1, k, T) - T;
            lap += Neighbor(i, j + 1, k, T) - T;
            lap += Neighbor(i, j, k - 1, T) - T;
            lap += Neighbor(i, j, k + 1, T) - T;
            lap /= (dx * dx);

            next[idx] = T + dt * a * lap;
        }

        ApplyRobinBoundary(dt);

        var tmp = current;
        current = next;
        next = tmp;
    }

    private float Neighbor(int i, int j, int k, float fallback)
    {
        if (i < 0 || i >= nx || j < 0 || j >= ny || k < 0 || k >= nz)
            return fallback;
        return current[Index(i, j, k)];
    }

    private void ApplyRobinBoundary(float dt)
    {
        if (boundaryExchangeRate <= 0f) return;

        for (int k = 0; k < nz; k++)
        for (int j = 0; j < ny; j++)
        for (int i = 0; i < nx; i++)
        {
            bool onBoundary = (i == 0 || i == nx - 1 || j == 0 || j == ny - 1 || k == 0 || k == nz - 1);
            if (!onBoundary) continue;

            int idx = Index(i, j, k);
            if (isFixed[idx]) continue;

            float T = next[idx];
            next[idx] = T + boundaryExchangeRate * (ambientTemperature - T) * dt;
        }
    }
}
