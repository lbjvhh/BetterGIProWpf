using System.Collections.Generic;

namespace BetterGIProWpf.Services.Games;

public static class InputMapping
{
    public static readonly Dictionary<string, string> Genshin = new()
    {
        ["move_forward"] = "W", ["move_back"] = "S", ["move_left"] = "A", ["move_right"] = "D",
        ["jump"] = "Space", ["dash"] = "Shift", ["attack"] = "MouseLeft",
        ["skill"] = "E", ["burst"] = "Q", ["switch_1"] = "1", ["switch_2"] = "2", ["switch_3"] = "3", ["switch_4"] = "4",
        ["interact"] = "F", ["map"] = "M", ["inventory"] = "B", ["pause"] = "Esc",
    };

    public static readonly Dictionary<string, string> Wuthering = new()
    {
        ["move_forward"] = "W", ["move_back"] = "S", ["move_left"] = "A", ["move_right"] = "D",
        ["jump"] = "Space", ["dash"] = "Shift", ["attack"] = "MouseLeft",
        ["skill"] = "E", ["burst"] = "Q", ["switch_1"] = "1", ["switch_2"] = "2", ["switch_3"] = "3",
        ["interact"] = "F", ["map"] = "M", ["inventory"] = "I", ["pause"] = "Esc",
    };

    public static readonly Dictionary<string, string> Zenless = new()
    {
        ["move_forward"] = "W", ["move_back"] = "S", ["move_left"] = "A", ["move_right"] = "D",
        ["jump"] = "Space", ["dash"] = "Shift", ["attack"] = "MouseLeft",
        ["skill"] = "E", ["burst"] = "Q", ["switch_1"] = "1", ["switch_2"] = "2",
        ["interact"] = "F", ["map"] = "M", ["inventory"] = "I", ["pause"] = "Esc",
    };

    public static Dictionary<string, string> Get(string gameId) => gameId.ToLowerInvariant() switch
    {
        "genshin" => Genshin,
        "wuthering" => Wuthering,
        "zzz" or "zenless" => Zenless,
        _ => Genshin,
    };
}
