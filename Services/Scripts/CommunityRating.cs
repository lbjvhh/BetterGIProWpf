namespace BetterGIProWpf.Services.Scripts;

/// <summary>模块19: 社区脚本评分。</summary>
public class CommunityRating
{
    public record ScriptRating(string ScriptName, int Score, string Comment, string User);

    public List<ScriptRating> ParseFromIssues(List<(string Title, string Body, string User)> issues)
    {
        var ratings = new List<ScriptRating>();
        foreach (var (title, body, user) in issues)
        {
            if (title.Contains("评分", StringComparison.OrdinalIgnoreCase) && title.Contains("-"))
            {
                var parts = title.Split('-');
                if (parts.Length >= 2)
                {
                    var name = parts[0].Replace("评分:", "").Trim();
                    var scoreStr = parts[1].Replace("星", "").Trim();
                    if (int.TryParse(scoreStr, out var score) && score >= 1 && score <= 5)
                    {
                        ratings.Add(new ScriptRating(name, score, body, user));
                    }
                }
            }
        }
        return ratings;
    }

    public double AverageScore(List<ScriptRating> ratings, string scriptName)
    {
        var matching = ratings.Where(r => r.ScriptName.Equals(scriptName, StringComparison.OrdinalIgnoreCase)).ToList();
        if (matching.Count == 0) return 0;
        return matching.Average(r => r.Score);
    }
}
