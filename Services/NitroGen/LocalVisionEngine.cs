namespace BetterGIProWpf.Services.NitroGen;
public sealed class LocalVisionEngine {
    public const int Tw=32,Th=18;
    private float[]? _last; private float _lx,_ly,_rx,_ry,_atk,_phase;
    public sealed record Ev(bool Move,bool Atk,bool Dash,bool Jump,bool Inter,float Warm,float Mot,float Sal);
    public sealed record Res(ActionBlock A,float Conf,string Mode,Ev? E);
    public Res Infer(RgbFrame[] frames) {
        if (frames.Length<2) return new Res(new ActionBlock(),0,"insufficient",null);
        var last=frames[^1]; var thumb=ToGrayThumb(last); var mv=_last==null?(0f,0f,0f):MV(_last,thumb); _last=thumb;
        float warm=Warm(last); float sal=Sal(last);
        bool moving=mv.Mag>0.05f; bool atk=warm>0.09f&&sal>0.05f&&mv.Mag>0.06f; _atk=atk?1f:Math.Max(0f,_atk-0.35f);
        bool dash=mv.Mag>0.45f; bool jump=moving&&mv.Mag>0.3f; bool inter=!moving&&sal>0.10f&&warm<0.06f;
        _lx=S(_lx,Math.Clamp(mv.X*2.2f,-1,1),0.35f); _ly=S(_ly,Math.Clamp(mv.Y*2.2f,-1,1),0.35f);
        var a=new ActionBlock();
        for(int s=0;s<NitroGenConst.BlockSteps;s++){int o=s*NitroGenConst.StepDim;a.Data[o]=(_lx+1)/2;a.Data[o+1]=(_ly+1)/2;a.Data[o+2]=(_rx+1)/2;a.Data[o+3]=(_ry+1)/2;if(_atk>0.5f)a.Data[o+4+NitroGenConst.Btn.A]=1;if(dash)a.Data[o+4+NitroGenConst.Btn.RT]=1;if(jump&&s%4==0)a.Data[o+4+NitroGenConst.Btn.B]=1;if(inter&&s==0)a.Data[o+4+NitroGenConst.Btn.X]=1;}
        float conf=Math.Clamp(0.35f+mv.Mag*0.3f+(atk?0.2f:0f),0.05f,0.95f);
        return new Res(a,conf,"local-vision",new Ev(moving,atk,dash,jump,inter,warm,mv.Mag,sal));
    }
    public static float[] ToGrayThumb(RgbFrame f){var o=new float[Tw*Th];for(int y=0;y<Th;y++){int sy=(int)Math.Floor(y/(double)Th*f.H);for(int x=0;x<Tw;x++){int sx=(int)Math.Floor(x/(double)Tw*f.W);int i=(sy*f.W+sx)*4;o[y*Tw+x]=0.299f*f.Data[i]+0.587f*f.Data[i+1]+0.114f*f.Data[i+2];}}return o;}
    public static (float X,float Y,float Mag) MV(float[] a,float[] b){double dx=0,dy=0,n=0;for(int y=1;y<Th-1;y++)for(int x=1;x<Tw-1;x++){int i=y*Tw+x;float d=b[i]-a[i];float mx=b[i+1]-b[i-1];float my=b[i+Tw]-b[i-Tw];if(Math.Abs(d)>4f){double g=Math.Sqrt(mx*mx+my*my)+1e-6;dx+=(mx/g)*Math.Abs(d);dy+=(my/g)*Math.Abs(d);n+=Math.Abs(d);}}if(n<1e-3)return(0,0,0);return((float)(dx/n),(float)(dy/n),(float)Math.Min(1,n/(Tw*Th*12.0)));}
    public static float Warm(RgbFrame f){int w=0,t=0;int step=Math.Max(1,(f.W*f.H)/(160*90));for(int i=0;i<f.Data.Length;i+=4*step){int r=f.Data[i],g=f.Data[i+1],b=f.Data[i+2];t++;if(r>120&&r>g*1.35&&r>b*1.35)w++;}return t==0?0f:(float)w/t;}
    public static float Sal(RgbFrame f){int cx0=(int)(f.W*0.3),cy0=(int)(f.H*0.25),cx1=(int)(f.W*0.7),cy1=(int)(f.H*0.75);double c=0,e=0;int cn=0,en=0;for(int y=cy0;y<cy1;y+=4)for(int x=cx0;x<cx1;x+=4){int i=(y*f.W+x)*4;c+=(f.Data[i]+f.Data[i+1]+f.Data[i+2])/3.0;cn++;}for(int y=0;y<f.H;y+=4)for(int x=0;x<f.W;x+=4){if(x>=cx0&&x<cx1&&y>=cy0&&y<cy1)continue;int i=(y*f.W+x)*4;e+=(f.Data[i]+f.Data[i+1]+f.Data[i+2])/3.0;en++;}if(cn==0||en==0)return 0f;return (float)Math.Max(0,Math.Min(1,Math.Abs(e/en-c/cn)/64.0));}
    private static float S(float c,float t,float k){float e=k*k*(3-2*k);return c+(t-c)*e;}
    public void Reset(){_last=null;_lx=_ly=_rx=_ry=_atk=_phase=0;}
}
