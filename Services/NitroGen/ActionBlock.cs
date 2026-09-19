using System;
using System.Linq;

namespace BetterGIProWpf.Services.NitroGen;

public static class NitroGenConst
{
    public const int BlockSteps = 16;
    public const int StepDim = 21;
    public const int TotalDim = BlockSteps * StepDim;
    public static class Btn
    {
        public const int A = 0, B = 1, X = 2, Y = 3, LB = 4, RB = 5, LT = 6, RT = 7, BACK = 8, START = 9, GUIDE = 10;
    }
}

public sealed class RgbFrame
{
    public byte[] Data { get; }
    public int Width { get; }
    public int Height { get; }
    public RgbFrame(byte[] data, int w, int h) { Data = data; Width = w; Height = h; }
}

public sealed class ActionBlock
{
    public float[] Data { get; } = new float[NitroGenConst.TotalDim];

    public float[] Step(int s)
    {
        var off = s * NitroGenConst.StepDim;
        var step = new float[NitroGenConst.StepDim];
        Array.Copy(Data, off, step, 0, NitroGenConst.StepDim);
        return step;
    }

    public float LeftX(int s) => (Data[s * NitroGenConst.StepDim] * 2f) - 1f;
    public float LeftY(int s) => (Data[s * NitroGenConst.StepDim + 1] * 2f) - 1f;
    public float RightX(int s) => (Data[s * NitroGenConst.StepDim + 2] * 2f) - 1f;
    public float RightY(int s) => (Data[s * NitroGenConst.StepDim + 3] * 2f) - 1f;
    public bool IsButton(int s, int btn) => Data[s * NitroGenConst.StepDim + 4 + btn] > 0.5f;

    public float Entropy()
    {
        double sum = 0; int n = 0;
        for (int i = 0; i < Data.Length; i++)
        {
            var p = Data[i];
            if (p <= 0.001f || p >= 0.999f) continue;
            sum -= p * Math.Log(p) + (1 - p) * Math.Log(1 - p);
            n++;
        }
        return n == 0 ? 0f : (float)(sum / n);
    }
}
