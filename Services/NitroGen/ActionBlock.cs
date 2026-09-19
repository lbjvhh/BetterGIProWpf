namespace BetterGIProWpf.Services.NitroGen;
public static class NitroGenConst { public const int StepDim = 21; public const int BlockSteps = 16; public static class Btn { public const int A=0,B=1,X=2,Y=3,LB=4,RB=5,LT=6,RT=7,Up=8,Down=9,Left=10,Right=11,Back=12,Start=13,L3=14,R3=15,Home=16; } }
public sealed class ActionBlock {
    public float[] Data { get; }
    public ActionBlock(float[]? d=null) { Data = d ?? new float[NitroGenConst.StepDim*NitroGenConst.BlockSteps]; if (Data.Length != NitroGenConst.StepDim*NitroGenConst.BlockSteps) throw new ArgumentException(); }
    public float LX(int s) => Data[s*NitroGenConst.StepDim]; public float LY(int s) => Data[s*NitroGenConst.StepDim+1]; public float RX(int s) => Data[s*NitroGenConst.StepDim+2]; public float RY(int s) => Data[s*NitroGenConst.StepDim+3];
    public bool IsBtn(int s, int b) => Data[s*NitroGenConst.StepDim+4+b] > 0.5f;
    public float Entropy() { var c = new int[17]; int tot=0; for(int s=0;s<NitroGenConst.BlockSteps;s++) for(int b=0;b<17;b++) if(IsBtn(s,b)){c[b]++;tot++;} if(tot==0) return 1f; double e=0; foreach(var x in c){if(x==0)continue;double p=(double)x/tot;e-=p*Math.Log2(p);} return (float)Math.Min(1,e/Math.Log2(17)); }
    public float[] Step(int s) { var off=s*NitroGenConst.StepDim; var a=new float[NitroGenConst.StepDim]; Array.Copy(Data,off,a,0,NitroGenConst.StepDim); return a; }
}
public sealed class RgbFrame { public byte[] Data { get; } public int W { get; } public int H { get; } public RgbFrame(byte[] d,int w,int h){if(d.Length<w*h*4)throw new ArgumentException();Data=d;W=w;H=h;} }
