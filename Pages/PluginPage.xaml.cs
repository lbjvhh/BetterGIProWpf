    private void Disable_Click(object sender, RoutedEventArgs e)
    {
        if (PluginList.SelectedItem is IGamePlugin p)
        {
            DetailText.Text = _manager.Disable(p.Name);
            Refresh();
        }
        else DetailText.Text = "请先选择插件";
    }

    private void ScanUserScripts_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var userRoot = Path.Combine(AppContext.BaseDirectory, "User");
            var lines = new List<string> { "== User 目录脚本扫描 ==" };
            string[] subDirs = { "AutoFight", "ScriptGroup", "AutoPathing", "KeyMouseScript" };
            foreach (var sub in subDirs)
            {
                var dir = Path.Combine(userRoot, sub);
                if (!Directory.Exists(dir)) { lines.Add($"  [{sub}] 目录不存在"); continue; }
                var files = Directory.GetFiles(dir, "*.*", SearchOption.AllDirectories);
                lines.Add($"  [{sub}] {files.Length} 个文件:");
                foreach (var f in files.Take(10))
                    lines.Add($"    {Path.GetFileName(f)} ({new FileInfo(f).Length / 1024.0:F1} KB)");
                if (files.Length > 10) lines.Add($"    ... 共 {files.Length} 个");
            }
            DetailText.Text = string.Join(Environment.NewLine, lines);
        }
        catch (Exception ex) { DetailText.Text = $"扫描失败: {ex.Message}"; }
    }
}