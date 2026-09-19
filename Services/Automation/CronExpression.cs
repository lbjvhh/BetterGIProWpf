namespace BetterGIProWpf.Services.Automation;

/// <summary>轻量 5 字段 cron 解析器（分 时 日 月 周），支持 * / , - 与数字。</summary>
public class CronExpression
{
    private readonly int[] _min = new int[60];
    private readonly int[] _hour = new int[24];
    private readonly int[] _day = new int[32];
    private readonly int[] _month = new int[13];
    private readonly int[] _dow = new int[7];

    public string Raw { get; }

    public CronExpression(string expr)
    {
        Raw = expr;
        var fields = expr.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (fields.Length != 5)
            throw new FormatException($"cron 表达式需 5 段（分 时 日 月 周），实际 {fields.Length} 段: {expr}");
        Parse(fields[0], _min, 0, 59, "分");
        Parse(fields[1], _hour, 0, 23, "时");
        Parse(fields[2], _day, 1, 31, "日");
        Parse(fields[3], _month, 1, 12, "月");
        Parse(fields[4], _dow, 0, 6, "周");
    }

    private static void Parse(string field, int[] bucket, int min, int max, string name)
    {
        foreach (var seg in field.Split(','))
        {
            var seg_ = seg.Trim();
            var slash = seg_.Split('/');
            var step = slash.Length > 1 ? int.Parse(slash[1]) : 1;
            if (step <= 0) throw new FormatException($"{name} 步长必须 &gt; 0: {field}");
            var range = slash[0] == "*" ? $"{min}-{max}" : slash[0];
            var dash = range.Split('-');
            var from = int.Parse(dash[0]);
            var to = dash.Length > 1 ? int.Parse(dash[1]) : from;
            if (from < min || to > max || from > to) throw new FormatException($"{name} 越界: {field}");
            for (var v = from; v <= to; v += step) bucket[v] = 1;
        }
    }

    /// <summary>计算 from 之后的下一触发时间（精确到分钟）。</summary>
    public DateTime Next(DateTime from)
    {
        var t = new DateTime(from.Year, from.Month, from.Day, from.Hour, from.Minute, 0).AddMinutes(1);
        // 上限 6 年（分钟级遍历），足够覆盖任意 5 段 cron
        var maxIterations = 366 * 6 * 24 * 60;
        for (var i = 0; i < maxIterations; i++)
        {
            if (_month[t.Month] == 1 && _day[t.Day] == 1 &&
                _dow[(int)t.DayOfWeek] == 1 && _hour[t.Hour] == 1 && _min[t.Minute] == 1)
                return t;
            t = t.AddMinutes(1);
        }
        throw new InvalidOperationException("6 年内找不到匹配时间: " + Raw);
    }
}
