using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace QuickChatToolboxWin
{
    /// <summary>
    /// 与游戏内 Cesium 插件通信：写 IPC 目录下的 cmd.txt，读 result.txt。
    /// </summary>
    public static class Ipc
    {
        public static readonly string Dir = ResolveDir();

        /// <summary>
        /// IPC 目录候选（必须与插件 ToolboxMod.ResolveDir 完全一致）：
        ///   1. 环境变量 QCT_IPC_DIR
        ///   2. %LOCALAPPDATA%\QuickChatToolbox\ipc（主路径，与窗口解压位置无关）
        ///   3. %TEMP%\AstralQuickChatToolbox\ipc（回退）
        /// </summary>
        private static string ResolveDir()
        {
            var list = new List<string>();
            try
            {
                string env = Environment.GetEnvironmentVariable("QCT_IPC_DIR");
                if (!string.IsNullOrEmpty(env))
                {
                    list.Add(env);
                }
            }
            catch
            {
            }
            try
            {
                string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                if (!string.IsNullOrEmpty(local))
                {
                    list.Add(Path.Combine(local, "QuickChatToolbox", "ipc"));
                }
            }
            catch
            {
            }
            list.Add(Path.Combine(Path.GetTempPath(), "AstralQuickChatToolbox", "ipc"));
            foreach (string d in list)
            {
                try
                {
                    Directory.CreateDirectory(d);
                    string probe = Path.Combine(d, ".probe");
                    File.WriteAllText(probe, "1");
                    File.Delete(probe);
                    return d;
                }
                catch
                {
                }
            }
            return list[0];
        }

        public static (bool Ok, string Text) Send(string cmd, int timeoutMs = 3000)
        {
            try
            {
                Directory.CreateDirectory(Dir);
                string seq = DateTime.Now.Ticks.ToString();
                string cmdPath = Path.Combine(Dir, "cmd.txt");
                string resPath = Path.Combine(Dir, "result.txt");
                try { File.Delete(resPath); } catch { }
                File.WriteAllText(cmdPath, seq + "|" + cmd, Encoding.UTF8);
                Stopwatch sw = Stopwatch.StartNew();
                while (sw.ElapsedMilliseconds < timeoutMs)
                {
                    if (File.Exists(resPath))
                    {
                        string text;
                        try { text = File.ReadAllText(resPath).Trim(); }
                        catch { text = null; }
                        if (!string.IsNullOrEmpty(text) && text.StartsWith(seq + "|", StringComparison.Ordinal))
                        {
                            return (true, text);
                        }
                    }
                    Thread.Sleep(30);
                }
                return (false, "超时：游戏内插件无响应（游戏没开或插件未加载）");
            }
            catch (Exception ex)
            {
                return (false, "IPC 失败：" + ex.GetType().Name + " " + ex.Message);
            }
        }

        public static string ReadPlayersFile()
        {
            return ReadFile("players.txt");
        }

        public static string ReadFile(string name)
        {
            try
            {
                string p = Path.Combine(Dir, name);
                if (File.Exists(p))
                {
                    return File.ReadAllText(p, Encoding.UTF8);
                }
            }
            catch
            {
            }
            return null;
        }

        public static void Log(string msg)
        {
            string line = DateTime.Now.ToString("HH:mm:ss.fff") + " " + msg + Environment.NewLine;
            try
            {
                Directory.CreateDirectory(Dir);
                string p = Path.Combine(Dir, "window.log");
                TrimIfLarge(p);
                File.AppendAllText(p, line, Encoding.UTF8);
            }
            catch
            {
            }
            try
            {
                string p2 = Path.Combine(AppContext.BaseDirectory, "window.log");
                TrimIfLarge(p2);
                File.AppendAllText(p2, line, Encoding.UTF8);
            }
            catch
            {
            }
        }

        private static void TrimIfLarge(string path)
        {
            try
            {
                FileInfo fi = new FileInfo(path);
                if (fi.Exists && fi.Length > 512 * 1024)
                {
                    File.Delete(path);
                }
            }
            catch
            {
            }
        }
    }
}
