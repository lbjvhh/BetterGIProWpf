using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using BetterGIProWpf.Services;
using BetterGIProWpf.Services.BetterGI;
using BetterGIProWpf.Services.Community;
using BetterGIProWpf.Services.LocalScripts;
using BetterGIProWpf.Services.Recognition;

namespace BetterGIProWpf;

/// <summary>
/// 脚本工坊（独立窗口）：把 5 类 BetterGI 脚本制作集中到一个 App——
/// JS 脚本 / 战斗策略 / 路径追踪 / 键鼠脚本 / 调度器配置组。
/// 内置模板 + 表格编辑 → 生成 → 测试执行（bridge 注入）→ 保存到 User → 上传云端。
/// </summary>
public partial class ScriptMakerWindow : Window
{
    public sealed record Row(string C1 = "", string C2 = "", string C3 = "", string C4 = "")
    {
        public string C1 { get; set; } = C1;
        public string C2 { get; set; } = C2;
        public string C3 { get; set; } = C3;
        public string C4 { get; set; } = C4;
    }

    private readonly ObservableCollection<Row> _rows = new();
    private readonly ScriptRepoClient _repo = new();
    private string _lastGenerated = "";
    private string _lastDir = "";

    public ScriptMakerWindow()
    {
        InitializeComponent();
        StepGrid.ItemsSource = _rows;
        TypeCombo.SelectedIndex = 0;
    }

    private string TypeTag =>
        (TypeCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "js";

    private static string UserRootOrDefault(string root)
    {
        if (Directory.Exists(root)) return root;
        return Path.Combine(AppContext.BaseDirectory, "User");
    }

    private void Log(string m) => LogBox.AppendText(m + "\n");

    private void TypeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var heads = TypeTag switch
        {
            "fight" => new[] { "指令", "时长(s)", "参数", "说明" },
            "pathing" => new[] { "X", "Y", "动作", "描述" },
            "keymouse" => new[] { "按键", "按住(ms)", "间隔(ms)", "描述" },
            "group" => new[] { "脚本类型", "脚本名", "路径", "参数" },
            _ => new[] { "任务类型", "目标", "地点", "动作" },
        };
        GridTitle.Text = $"编辑步骤（{heads[0]}/{heads[1]}/{heads[2]}/{heads[3]}）";
        StepGrid.Columns.Clear();
        foreach (var (h, i) in heads.Select((h, i) => (h, i + 1)))
            StepGrid.Columns.Add(new DataGridTextColumn
            {
                Header = h,
                Binding = new Binding($"C{i}") { UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged },
                Width = new DataGridLength(1, DataGridLengthUnitType.Star),
            });
        LoadTemplates();
        TemplateCombo.SelectedIndex = 0;
    }

    private void LoadTemplates()
    {
        TemplateCombo.Items.Clear();
        var tpl = TypeTag switch
        {
            "fight" => new[] { "元素反应连招", "闪避突进", "清怪循环" },
            "pathing" => new[] { "风起地采集", "蒙德跑图", "锚点传送" },
            "keymouse" => new[] { "连点交互", "打开地图往返", "冲刺跳跃" },
            "group" => new[] { "战斗+拾取", "采集+传送", "日常一条龙" },
            _ => new[] { "采集循环", "剧情对话", "传送跑图" },
        };
        foreach (var t in tpl) TemplateCombo.Items.Add(t);
    }

    private void LoadTemplate_Click(object sender, RoutedEventArgs e)
    {
        if (TemplateCombo.SelectedItem is not string t) return;
        LoadTemplateByName(t);
    }

    private void TemplateCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TemplateCombo.SelectedItem is string t) LoadTemplateByName(t);
    }

    private void LoadTemplateByName(string t)
    {
        _rows.Clear();
        switch (t)
        {
            case "采集循环":
                AddRow("combat", "采集水晶块", "层岩巨渊", "E 技能清杂");
                AddRow("collect", "拾取掉落", "", "F 交互");
                AddRow("move", "前往下一采集点", "矿区北侧", "W 前进");
                AddRow("teleport", "回锚点", "层岩巨渊锚点", "M 开地图");
                break;
            case "剧情对话":
                AddRow("dialogue", "与 NPC 对话", "蒙德城", "F 连续对话");
                AddRow("choice", "选择选项", "", "点击选项");
                AddRow("dialogue", "领取奖励", "", "F 交互");
                break;
            case "传送跑图":
                AddRow("teleport", "传送到锚点", "风起地", "M 开地图");
                AddRow("move", "跑向目标", "大树下", "W 前进");
                AddRow("collect", "开启宝箱", "", "F 交互");
                break;
            case "元素反应连招":
                AddRow("skill", "0.4", "e", "元素战技起手");
                AddRow("burst", "0.8", "q", "元素爆发");
                AddRow("attack", "2.0", "", "普攻循环");
                AddRow("wait", "0.5", "", "等 CD");
                AddRow("skill", "0.4", "e", "再挂元素");
                break;
            case "闪避突进":
                AddRow("dash", "0.5", "", "冲刺拉开");
                AddRow("jump", "0.3", "", "跳跃躲避");
                AddRow("attack", "1.0", "", "回身输出");
                break;
            case "清怪循环":
                AddRow("burst", "0.6", "q", "开局爆发");
                AddRow("skill", "0.4", "e", "元素战技");
                AddRow("attack", "1.5", "", "普攻补刀");
                AddRow("walk", "0.3", "", "调整站位");
                break;
            case "风起地采集":
                AddRow("500", "300", "interact", "七天神像旁矿点");
                AddRow("620", "410", "interact", "树下水晶块");
                AddRow("760", "520", "interact", "崖边宝箱");
                break;
            case "蒙德跑图":
                AddRow("300", "700", "move", "城门出发");
                AddRow("500", "450", "move", "广场");
                AddRow("780", "300", "interact", "凯瑟琳");
                break;
            case "锚点传送":
                AddRow("400", "200", "teleport", "风起地锚点");
                AddRow("600", "600", "move", "目标点");
                break;
            case "连点交互":
                AddRow("f", "80", "120", "连续交互");
                AddRow("f", "80", "120", "第二次交互");
                AddRow("f", "80", "400", "收尾交互");
                break;
            case "打开地图往返":
                AddRow("m", "100", "300", "打开地图");
                AddRow("m", "100", "500", "关闭地图");
                break;
            case "冲刺跳跃":
                AddRow("shift", "150", "200", "冲刺");
                AddRow("space", "80", "150", "跳跃");
                break;
            case "战斗+拾取":
                AddRow("fight", "清怪", "AutoFight/demo_fight.txt", "先战斗");
                AddRow("keymouse", "拾取", "KeyMouseScript/pick.json", "再拾取");
                break;
            case "采集+传送":
                AddRow("pathing", "采集", "AutoPathing/mine.json", "跑图采集");
                AddRow("keymouse", "回城", "KeyMouseScript/map.json", "传送回城");
                break;
            case "日常一条龙":
                AddRow("fight", "战斗", "AutoFight/domain.txt", "秘境清怪");
                AddRow("pathing", "采集", "AutoPathing/ore.json", "矿点采集");
                AddRow("js", "剧情", "JsScript/dialogue", "自动对话");
                break;
        }
        UpdateCount();
        Log($"已载入模板「{t}」（{_rows.Count} 行）");
    }

    private void AddRow(string c1, string c2, string c3 = "", string c4 = "") =>
        _rows.Add(new Row(c1, c2, c3, c4));

    private void UpdateCount() => CountLabel.Text = $"共 {_rows.Count} 行";

    private void AddRow_Click(object sender, RoutedEventArgs e) { AddRow("", "", "", ""); UpdateCount(); }

    private void DelRow_Click(object sender, RoutedEventArgs e)
    {
        if (StepGrid.SelectedItem is Row r) { _rows.Remove(r); UpdateCount(); }
    }

    private void ClearRows_Click(object sender, RoutedEventArgs e) { _rows.Clear(); UpdateCount(); }

    /// <summary>按类型生成脚本。返回主产物路径。</summary>
    private string Generate()
    {
        var name = string.IsNullOrWhiteSpace(NameBox.Text) ? $"script_{DateTime.Now:HHmmss}" : NameBox.Text.Trim();
        var root = UserRootOrDefault(UserRootBox.Text.Trim());
        var dir = Path.Combine(Path.GetTempPath(), "scriptmaker", name);
        Directory.CreateDirectory(dir);

        switch (TypeTag)
        {
            case "fight":
            {
                var cmds = _rows
                    .Where(r => !string.IsNullOrWhiteSpace(r.C1))
                    .Select(r => new BattleScriptGenerator.Command(
                        r.C1.Trim().ToLowerInvariant(),
                        double.TryParse(r.C2, out var d) ? Math.Max(0.1, d) : 0.3,
                        string.IsNullOrWhiteSpace(r.C3) ? null : r.C3.Trim()))
                    .ToList();
                var txt = BattleScriptGenerator.ToBattleScriptTxt(cmds);
                var path = Path.Combine(dir, name + ".txt");
                File.WriteAllText(path, txt, Encoding.UTF8);
                _lastDir = dir;
                return _lastGenerated = path;
            }
            case "pathing":
            {
                var waypoints = _rows.Where(r => double.TryParse(r.C1, out _) && double.TryParse(r.C2, out _))
                    .Select(r => new { x = double.Parse(r.C1), y = double.Parse(r.C2), action = r.C3, desc = r.C4 })
                    .ToList();
                var obj = new { name, version = VersionBox.Text, waypoints };
                var path = Path.Combine(dir, name + ".json");
                File.WriteAllText(path, JsonSerializer.Serialize(obj, new JsonSerializerOptions { WriteIndented = true }), Encoding.UTF8);
                _lastDir = dir;
                return _lastGenerated = path;
            }
            case "keymouse":
            {
                var events = _rows.Where(r => !string.IsNullOrWhiteSpace(r.C1))
                    .Select(r => new
                    {
                        key = r.C1.Trim().ToLowerInvariant(),
                        hold_ms = int.TryParse(r.C2, out var h) ? h : 80,
                        delay_ms = int.TryParse(r.C3, out var d) ? d : 150,
                        desc = r.C4,
                    })
                    .ToList();
                var obj = new { name, version = VersionBox.Text, events };
                var path = Path.Combine(dir, name + ".json");
                File.WriteAllText(path, JsonSerializer.Serialize(obj, new JsonSerializerOptions { WriteIndented = true }), Encoding.UTF8);
                _lastDir = dir;
                return _lastGenerated = path;
            }
            case "group":
            {
                var group = _rows.Where(r => !string.IsNullOrWhiteSpace(r.C2))
                    .Select(r => new { type = r.C1.Trim(), name = r.C2.Trim(), path = r.C3.Trim(), args = r.C4.Trim() })
                    .ToList();
                var obj = new { name, version = VersionBox.Text, group };
                var path = Path.Combine(dir, name + ".json");
                File.WriteAllText(path, JsonSerializer.Serialize(obj, new JsonSerializerOptions { WriteIndented = true }), Encoding.UTF8);
                _lastDir = dir;
                return _lastGenerated = path;
            }
            default: // js
            {
                var tasks = _rows.Where(r => !string.IsNullOrWhiteSpace(r.C2))
                    .Select(r => new BetterGIScriptGenerator.TaskItem(
                        string.IsNullOrWhiteSpace(r.C1) ? "move" : r.C1.Trim(),
                        r.C2.Trim(), r.C3, r.C4))
                    .ToList();
                if (tasks.Count == 0) tasks.Add(new BetterGIScriptGenerator.TaskItem("move", "默认任务", "", ""));
                var outDir = BetterGIScriptGenerator.Generate(dir, name, tasks,
                    string.IsNullOrWhiteSpace(VideoBox.Text) ? null : VideoBox.Text.Trim());
                _lastDir = outDir;
                return _lastGenerated = Path.Combine(outDir, "main.js");
            }
        }
    }

    private void Generate_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var path = Generate();
            Log($"✅ 已生成 [{TypeTag}] {NameBox.Text}\n   {path}");
            if (File.Exists(path)) Log($"   大小: {new FileInfo(path).Length} B");
            if (_lastDir != "" && Directory.Exists(_lastDir))
                foreach (var f in Directory.EnumerateFiles(_lastDir, "*.*", SearchOption.TopDirectoryOnly))
                    Log($"   ├ {Path.GetFileName(f)}");
        }
        catch (Exception ex)
        {
            Log($"❌ 生成失败: {ex.Message}");
        }
    }

    private async void TestRun_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (string.IsNullOrEmpty(_lastGenerated) || !File.Exists(_lastGenerated))
            {
                Log("尚未生成脚本，先生成。");
                return;
            }
            Log($"== 测试执行: {_lastGenerated} ==");
            var root = UserRootOrDefault(UserRootBox.Text.Trim());
            var mgr = new LocalScriptManager(root);
            var ok = await mgr.ExecuteViaBridgeAsync(_lastGenerated,
                m => Dispatcher.Invoke(() => Log("  " + m)));
            Log($"执行结果: {(ok ? "✅ 成功" : "❌ 失败/被安全停机拦截")}（需 stream_bridge 5005 运行、游戏窗口可见）");
        }
        catch (Exception ex)
        {
            Log($"❌ 测试执行异常: {ex.Message}");
        }
    }

    private void SaveToUser_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var path = string.IsNullOrEmpty(_lastGenerated) || !File.Exists(_lastGenerated) ? Generate() : _lastGenerated;
            var root = UserRootOrDefault(UserRootBox.Text.Trim());
            var sub = TypeTag switch
            {
                "fight" => "AutoFight",
                "pathing" => "AutoPathing",
                "keymouse" => "KeyMouseScript",
                "group" => "ScriptGroup",
                _ => "JsScript",
            };
            var destDir = Path.Combine(root, sub);
            Directory.CreateDirectory(destDir);
            // JS 脚本是目录形态：整个拷贝；其余拷贝主文件
            if (TypeTag == "js" && Directory.Exists(_lastDir))
            {
                var dest = Path.Combine(destDir, Path.GetFileName(_lastDir.TrimEnd('\\', '/')));
                if (Directory.Exists(dest)) Directory.Delete(dest, true);
                CopyDir(_lastDir, dest);
                Log($"✅ 已保存 JS 脚本目录 → {dest}");
            }
            else
            {
                var dest = Path.Combine(destDir, Path.GetFileName(path));
                File.Copy(path, dest, true);
                Log($"✅ 已保存 → {dest}");
            }
            // 同步 manifest（若有）
            var manifest = Path.Combine(Path.GetDirectoryName(path) ?? "", "manifest.json");
            if (File.Exists(manifest))
            {
                var dm = Path.Combine(destDir, "manifest.json");
                File.Copy(manifest, dm, true);
            }
        }
        catch (Exception ex)
        {
            Log($"❌ 保存失败: {ex.Message}");
        }
    }

    private static void CopyDir(string src, string dst)
    {
        Directory.CreateDirectory(dst);
        foreach (var f in Directory.EnumerateFiles(src))
            File.Copy(f, Path.Combine(dst, Path.GetFileName(f)), true);
        foreach (var d in Directory.EnumerateDirectories(src))
            CopyDir(d, Path.Combine(dst, Path.GetFileName(d)));
    }

    private async void Upload_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var pat = PatBox.Password.Trim();
            if (string.IsNullOrEmpty(pat)) { Log("[上传] 请先填写 GitHub PAT（repo 权限）"); return; }
            var path = string.IsNullOrEmpty(_lastGenerated) || !File.Exists(_lastGenerated) ? Generate() : _lastGenerated;
            var cat = (UploadCatCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "combat";
            var kind = TypeTag == "js" ? ScriptKind.JsScript : TypeTag switch
            {
                "fight" => ScriptKind.AutoFight,
                "pathing" => ScriptKind.AutoPathing,
                "keymouse" => ScriptKind.KeyMouseScript,
                _ => ScriptKind.ScriptGroup,
            };
            Log($"[上传] {Path.GetFileName(path)} → scripts/{cat}/ …");
            var (ok, msg) = await _repo.UploadAsync(path, cat, pat,
                log: m => Dispatcher.Invoke(() => Log("  " + m)));
            Log($"  {(ok ? "✅" : "❌")} {msg}");
        }
        catch (Exception ex)
        {
            Log($"❌ 上传异常: {ex.Message}");
        }
    }
}
