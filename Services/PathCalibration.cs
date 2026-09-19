namespace BetterGIProWpf.Services;
public record Pt(double X,double Y);
public static class PathCalibration {
    public static Func<Pt,Pt> Affine((Pt s,Pt d)[] lm) { return p=>p; }
    public static double DtwAlign(double[] a,double[] b){int n=a.Length,m=b.Length;var dp=new double[n+1,m+1];for(int i=0;i<=n;i++)for(int j=0;j<=m;j++)dp[i,j]=1e9;dp[0,0]=0;for(int i=1;i<=n;i++)for(int j=1;j<=m;j++)dp[i,j]=Math.Abs(a[i-1]-b[j-1])+Math.Min(Math.Min(dp[i-1,j],dp[i,j-1]),dp[i-1,j-1]);return dp[n,m]/Math.Max(1,(n+m)/2);}
    public static List<Pt> Smooth(List<Pt> pts,int seg=8){if(pts.Count<3)return pts;var r=new List<Pt>();for(int i=0;i<pts.Count-1;i++){var p0=pts[Math.Max(0,i-1)];var p1=pts[i];var p2=pts[i+1];var p3=pts[Math.Min(pts.Count-1,i+2)];for(int t=0;t<seg;t++){double s=(double)t/seg,s2=s*s,s3=s2*s;double x=0.5*(2*p1.X+(-p0.X+p2.X)*s+(2*p0.X-5*p1.X+4*p2.X-p3.X)*s2+(-p0.X+3*p1.X-3*p2.X+p3.X)*s3);double y=0.5*(2*p1.Y+(-p0.Y+p2.Y)*s+(2*p0.Y-5*p1.Y+4*p2.Y-p3.Y)*s2+(-p0.Y+3*p1.Y-3*p2.Y+p3.Y)*s3);r.Add(new Pt(x,y));}}r.Add(pts[^1]);return r;}
}
