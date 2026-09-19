namespace BetterGIProWpf.Services.Recognition;
public static class Timing {
    public sealed record R(double Dist,List<(int A,int B)> Path);
    public static R Dtw(double[] a,double[] b){int n=a.Length,m=b.Length;if(n==0||m==0)return new R(0,new());var d=new double[n,m];d[0,0]=Math.Abs(a[0]-b[0]);for(int i=1;i<n;i++)d[i,0]=Math.Abs(a[i]-b[0])+d[i-1,0];for(int j=1;j<m;j++)d[0,j]=Math.Abs(a[0]-b[j])+d[0,j-1];for(int i=1;i<n;i++)for(int j=1;j<m;j++)d[i,j]=Math.Abs(a[i]-b[j])+Math.Min(d[i-1,j-1],Math.Min(d[i-1,j],d[i,j-1]));var p=new List<(int,int)>();int x=n-1,y=m-1;p.Add((x,y));while(x>0||y>0){double dg=x>0&&y>0?d[x-1,y-1]:1e9,up=x>0?d[x-1,y]:1e9,lf=y>0?d[x,y-1]:1e9;if(dg<=up&&dg<=lf){x--;y--;}else if(up<=lf)x--;else y--;p.Add((x,y));}p.Reverse();return new R(d[n-1,m-1],p);}
}
