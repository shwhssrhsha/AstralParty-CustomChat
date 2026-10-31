using System;
using System.IO;
using System.Text;
using System.Threading;
using CesiumLoader.SDK.Logging;
using CesiumLoader.SDK.Scheduling;
using GameLogic;
using Tools;
using UI;

namespace QuickChatToolbox
{
    /// <summary>
    /// M1：IPC 链路 + players dump + mark 发送。
    /// 后台线程只看文件，所有游戏 API 都在主线程（UpdateService 每帧回调）执行。
    /// </summary>
    public static class ToolboxMod
    {
        private const string Tag = "QuickChatToolbox";
        private static readonly System.Collections.Concurrent.ConcurrentQueue<string> Queue = new System.Collections.Concurrent.ConcurrentQueue<string>();
        private static string _dir;
        private static bool _started;

        /// <summary>
        /// IPC 目录候选（必须与外部窗口 Ipc.ResolveDir 完全一致）：
        ///   1. 环境变量 QCT_IPC_DIR
        ///   2. %LOCALAPPDATA%\QuickChatToolbox\ipc（主路径，与游戏/窗口位置无关）
        ///   3. %TEMP%\AstralQuickChatToolbox\ipc（回退）
        /// </summary>
        private static string ResolveDir()
        {
            var list = new System.Collections.Generic.List<string>();
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
            return list[list.Count - 1];
        }

        public static void OnInit()
        {
            try
            {
                _dir = ResolveDir();
                Directory.CreateDirectory(_dir);
                SdkLog.Info(Tag, "init, dir=" + _dir);
                if (_started)
                {
                    return;
                }
                _started = true;
                UpdateService.SubscribeUpdate(OnFrame, null, Tag);
                try
                {
                    FairyGUI.Timers.inst.Add(0f, 0, delegate (object _) { OnFrame(); });
                    SdkLog.Info(Tag, "fairy timers hook ready");
                }
                catch (Exception tex)
                {
                    SdkLog.Warn(Tag, "fairy timers fail: " + tex.GetType().Name + ": " + tex.Message);
                }
                var th = new Thread(Loop) { IsBackground = true, Name = "QCT-IPC" };
                th.Start();
                SdkLog.Info(Tag, "ipc thread + update hook ready");
            }
            catch (Exception ex)
            {
                SdkLog.Warn(Tag, "init error " + ex.GetType().Name + ": " + ex.Message);
            }
        }

        public static void OnTick()
        {
            // M1 不需要；ModBase 的 tick 保留空实现
        }

        private static void Loop()
        {
            while (true)
            {
                try
                {
                    string path = Path.Combine(_dir, "cmd.txt");
                    if (File.Exists(path))
                    {
                        string text = File.ReadAllText(path, Encoding.UTF8);
                        try { File.Delete(path); } catch { }
                        text = text.Trim();
                        if (text.Length > 0)
                        {
                            Queue.Enqueue(text);
                        }
                    }
                }
                catch
                {
                }
                Thread.Sleep(50);
            }
        }

        private static void OnFrame()
        {
            try
            {
                while (Queue.TryDequeue(out string text))
                {
                    Execute(text);
                }
            }
            catch
            {
            }
        }

        private static void Execute(string text)
        {
            int sep = text.IndexOf('|');
            string seq = sep > 0 ? text.Substring(0, sep) : "?";
            string cmd = (sep > 0 ? text.Substring(sep + 1) : text).Trim();
            SdkLog.Info(Tag, "exec seq=" + seq + " cmd=" + cmd);
            try
            {
                if (cmd == "ping")
                {
                    Result(seq, "ok", "pong");
                    return;
                }
                if (cmd == "state")
                {
                    Result(seq, "ok", State());
                    return;
                }
                if (cmd == "players")
                {
                    Result(seq, "ok", DumpPlayers());
                    return;
                }
                if (cmd.StartsWith("mark:", StringComparison.Ordinal))
                {
                    Mark(seq, cmd.Substring(5));
                    return;
                }
                if (cmd == "cards")
                {
                    Result(seq, "ok", DumpCards());
                    return;
                }
                if (cmd == "relics")
                {
                    Result(seq, "ok", DumpRelics());
                    return;
                }
                if (cmd == "events")
                {
                    Result(seq, "ok", DumpEvents());
                    return;
                }
                if (cmd == "tasks")
                {
                    Result(seq, "ok", DumpTasks());
                    return;
                }
                if (cmd == "roominfo")
                {
                    Result(seq, "ok", RoomInfoDump());
                    return;
                }
                if (cmd.StartsWith("card:", StringComparison.Ordinal))
                {
                    CardShare(seq, cmd.Substring(5));
                    return;
                }
                if (cmd.StartsWith("relic:", StringComparison.Ordinal))
                {
                    RelicShare(seq, cmd.Substring(6));
                    return;
                }
                if (cmd.StartsWith("skill:", StringComparison.Ordinal))
                {
                    SkillShare(seq, cmd.Substring(6));
                    return;
                }
                if (cmd.StartsWith("task:", StringComparison.Ordinal))
                {
                    TaskShare(seq, cmd.Substring(5));
                    return;
                }
                if (cmd.StartsWith("pveevt:", StringComparison.Ordinal))
                {
                    PveEventShare(seq, cmd.Substring(7));
                    return;
                }
                if (cmd.StartsWith("praise:", StringComparison.Ordinal))
                {
                    Praise(seq, cmd.Substring(7));
                    return;
                }
                Result(seq, "err", "unknown cmd: " + cmd);
            }
            catch (Exception ex)
            {
                Result(seq, "err", ex.GetType().Name + ": " + ex.Message);
            }
        }

        private static string State()
        {
            GameLogicManager gm = SimpleSingletonProvider<GameLogicManager>.inst;
            int players = 0;
            try { players = gm.battle.PlayerDatas.Count; } catch { }
            bool inRoom = false;
            try { inRoom = gm.room.IsInRoom; } catch { }
            BattlePlayerData self = null;
            try { self = gm.battle.GetSelfPlayerData(); } catch { }
            return "inRoom=" + (inRoom ? 1 : 0) + " players=" + players + " self=" + (self == null ? "null" : self.player.Id.ToString());
        }

        private static string DumpPlayers()
        {
            GameLogicManager gm = SimpleSingletonProvider<GameLogicManager>.inst;
            BattlePlayerData self = gm.battle.GetSelfPlayerData();
            long selfId = self == null ? 0L : self.player.Id;
            var sb = new StringBuilder();
            sb.AppendLine("#id\tnick\tcharName\tslot\tteam\tisBot\toffline\tcharType\tself\talive");
            int n = 0;
            foreach (BattlePlayerData p in gm.battle.PlayerDatas)
            {
                if (p == null || p.player == null)
                {
                    continue;
                }
                string nick = "";
                try { nick = p.player.GetNick(); } catch { }
                string charName = "";
                try
                {
                    int cid = p.player.characterConfig != null ? p.player.characterConfig.Id : 0;
                    charName = CharacterHandle.GetCharacterName(cid, p.characterType);
                    if (p.characterType == CharacterType.Monster && p.player.Hero != null)
                    {
                        charName = charName + " (" + p.player.Hero.MonsterIndex + ")";
                    }
                }
                catch
                {
                }
                bool alive;
                try
                {
                    // Character 是 UnityEngine 对象（Unit : MonoBehaviour），没有 IsDead()；
                    // 怪物死亡后模型会被销毁，引用变成 Unity 的"假 null"。
                    if (p.characterType == CharacterType.Monster)
                    {
                        alive = p.CharacterInst != null;
                    }
                    else
                    {
                        alive = true;
                    }
                }
                catch
                {
                    alive = true;
                }
                sb.Append(p.player.Id).Append('\t')
                  .Append(nick).Append('\t')
                  .Append(charName).Append('\t')
                  .Append(p.player.Slot).Append('\t')
                  .Append(p.player.TeamId).Append('\t')
                  .Append(p.player.IsBot ? 1 : 0).Append('\t')
                  .Append(p.player.OffLine ? 1 : 0).Append('\t')
                  .Append((int)p.characterType).Append('\t')
                  .Append(p.player.Id == selfId ? 1 : 0).Append('\t')
                  .Append(alive ? 1 : 0)
                  .AppendLine();
                n++;
            }
            File.WriteAllText(Path.Combine(_dir, "players.txt"), sb.ToString(), Encoding.UTF8);
            return "players " + n;
        }

        private static void Mark(string seq, string args)
        {
            string[] parts = args.Split(':');
            if (parts.Length < 2)
            {
                Result(seq, "err", "usage mark:<markId>:<playerId>");
                return;
            }
            int markId;
            long playerId;
            if (!int.TryParse(parts[0], out markId) || !long.TryParse(parts[1], out playerId))
            {
                Result(seq, "err", "bad number");
                return;
            }
            GameLogicManager gm = SimpleSingletonProvider<GameLogicManager>.inst;
            BattlePlayerData self = gm.battle.GetSelfPlayerData();
            if (self == null)
            {
                Result(seq, "err", "not in battle (self=null)");
                return;
            }
            BattlePlayerMarkMessage msg = new BattlePlayerMarkMessage(self, playerId, markId);
            gm.communicate.RequestSendMessageC2S(msg, markId, false);
            Result(seq, "ok", "mark " + markId + " -> " + playerId);
        }

        private static void Result(string seq, string status, string detail)
        {
            try
            {
                string line = seq + "|" + status + "|" + detail + Environment.NewLine;
                File.WriteAllText(Path.Combine(_dir, "result.txt"), line, Encoding.UTF8);
            }
            catch
            {
            }
        }

        // ================= M3：分享类 =================

        private static bool Self(out GameLogicManager gm, out BattlePlayerData self, string seq)
        {
            gm = SimpleSingletonProvider<GameLogicManager>.inst;
            self = gm.battle.GetSelfPlayerData();
            if (self == null)
            {
                Result(seq, "err", "not in battle (self=null)");
                return false;
            }
            return true;
        }

        private static string DumpCards()
        {
            var sb = new StringBuilder();
            sb.AppendLine("#id\tname");
            int n = 0;
            try
            {
                foreach (CardInfoConfigure c in StaticConfigure.Card.Infos)
                {
                    string name = "";
                    try { name = c.NameID.GetLocal(UIStringType.Card); } catch { }
                    sb.Append(c.Id).Append('\t').Append(name).AppendLine();
                    n++;
                }
            }
            catch
            {
            }
            File.WriteAllText(Path.Combine(_dir, "cards.txt"), sb.ToString(), Encoding.UTF8);
            return "cards " + n;
        }

        private static string DumpRelics()
        {
            var sb = new StringBuilder();
            sb.AppendLine("#id\tname");
            int n = 0;
            try
            {
                foreach (RelicInfoConfigure r in StaticConfigure.Relic.Infos)
                {
                    string name = "";
                    try { name = r.NameID.GetLocal(UIStringType.Relic); } catch { }
                    sb.Append(r.Id).Append('\t').Append(name).AppendLine();
                    n++;
                }
            }
            catch
            {
            }
            File.WriteAllText(Path.Combine(_dir, "relics.txt"), sb.ToString(), Encoding.UTF8);
            return "relics " + n;
        }

        private static string DumpEvents()
        {
            var sb = new StringBuilder();
            sb.AppendLine("#id\tname");
            int n = 0;
            try
            {
                foreach (System.Collections.Generic.KeyValuePair<int, MapEventMapEventCardConfigure> kv in StaticConfigure.MapEvent.MapEventCardDict)
                {
                    string name = "";
                    try { name = kv.Value.NameID.GetLocal(UIStringType.MapEvent); } catch { }
                    sb.Append(kv.Key).Append('\t').Append(name).AppendLine();
                    n++;
                }
            }
            catch
            {
            }
            File.WriteAllText(Path.Combine(_dir, "events.txt"), sb.ToString(), Encoding.UTF8);
            return "events " + n;
        }

        private static string DumpTasks()
        {
            var sb = new StringBuilder();
            sb.AppendLine("#taskId\ttitle");
            int n = 0;
            string diag;
            try
            {
                GameLogicManager gm = SimpleSingletonProvider<GameLogicManager>.inst;
                RoomInfo info = gm.room?.curRoomInfo;
                if (info == null)
                {
                    diag = "roomInfo=null";
                }
                else
                {
                    diag = "mapType=" + (int)info.MapType + " mapId=" + info.MapId + " state=" + (int)info.State;
                    var dict = info.MapMissionDict;
                    if (dict == null)
                    {
                        diag += " dict=null";
                    }
                    else
                    {
                        diag += " dict=" + dict.Count;
                        foreach (var kv in dict)
                        {
                            string title = "";
                            try
                            {
                                var tds = kv.Value.targetsData;
                                if (tds != null && tds.Count > 0)
                                {
                                    title = tds[0].GetTitle();
                                }
                            }
                            catch
                            {
                            }
                            sb.Append(kv.Key).Append('\t').Append(title).AppendLine();
                            n++;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                diag = "exc " + ex.GetType().Name + ": " + ex.Message;
            }
            File.WriteAllText(Path.Combine(_dir, "tasks.txt"), sb.ToString(), Encoding.UTF8);
            return "tasks " + n + " (" + diag + ")";
        }

        private static string RoomInfoDump()
        {
            try
            {
                GameLogicManager gm = SimpleSingletonProvider<GameLogicManager>.inst;
                RoomInfo info = gm.room?.curRoomInfo;
                if (info == null)
                {
                    return "roomInfo=null";
                }
                var sb = new StringBuilder();
                sb.Append("mapType=").Append((int)info.MapType);
                sb.Append(" mapId=").Append(info.MapId);
                sb.Append(" state=").Append((int)info.State);
                sb.Append(" dict=").Append(info.MapMissionDict == null ? "null" : info.MapMissionDict.Count.ToString());
                sb.Append(" isPve=").Append(info.IsPVE() ? 1 : 0);
                return sb.ToString();
            }
            catch (Exception ex)
            {
                return "roominfo err " + ex.GetType().Name + ": " + ex.Message;
            }
        }

        private static void CardShare(string seq, string args)
        {
            string[] parts = args.Split(':');
            if (parts.Length < 2)
            {
                Result(seq, "err", "usage card:<cardId>:<count>");
                return;
            }
            int cardId;
            int count;
            if (!int.TryParse(parts[0], out cardId) || !int.TryParse(parts[1], out count))
            {
                Result(seq, "err", "bad number");
                return;
            }
            if (count < 1)
            {
                count = 1;
            }
            GameLogicManager gm;
            BattlePlayerData self;
            if (!Self(out gm, out self, seq))
            {
                return;
            }
            BattleCardMessage msg = new BattleCardMessage(self, cardId, count);
            int markId = BattleCardMessage.GetMarkIdByCardCount(count);
            gm.communicate.RequestSendMessageC2S(msg, markId, false);
            Result(seq, "ok", "card " + cardId + " x" + count + " markId=" + markId);
        }

        private static void RelicShare(string seq, string args)
        {
            int relicId;
            if (!int.TryParse(args.Trim(), out relicId))
            {
                Result(seq, "err", "usage relic:<relicId>");
                return;
            }
            GameLogicManager gm;
            BattlePlayerData self;
            if (!Self(out gm, out self, seq))
            {
                return;
            }
            BattleRelicMessage msg = new BattleRelicMessage(self, relicId);
            gm.communicate.RequestSendMessageC2S(msg, 40018, false);
            Result(seq, "ok", "relic " + relicId);
        }

        private static void SkillShare(string seq, string args)
        {
            int cd;
            if (!int.TryParse(args.Trim(), out cd))
            {
                Result(seq, "err", "usage skill:<cd>");
                return;
            }
            if (cd < 0)
            {
                cd = 0;
            }
            GameLogicManager gm;
            BattlePlayerData self;
            if (!Self(out gm, out self, seq))
            {
                return;
            }
            BattleSkillMessage msg = new BattleSkillMessage(self, cd);
            gm.communicate.RequestSendMessageC2S(msg, msg.CurMarkId, false);
            Result(seq, "ok", "skill cd=" + cd + " markId=" + msg.CurMarkId);
        }

        private static void TaskShare(string seq, string args)
        {
            int taskId;
            if (!int.TryParse(args.Trim(), out taskId))
            {
                Result(seq, "err", "usage task:<taskId>");
                return;
            }
            GameLogicManager gm;
            BattlePlayerData self;
            if (!Self(out gm, out self, seq))
            {
                return;
            }
            BattlePveTaskMessage msg = new BattlePveTaskMessage(self, taskId);
            gm.communicate.RequestSendMessageC2S(msg, 40015, false);
            Result(seq, "ok", "task " + taskId);
        }

        private static void PveEventShare(string seq, string args)
        {
            int sep = args.IndexOf(':');
            if (sep <= 0)
            {
                Result(seq, "err", "usage pveevt:<progress>:<eventCardIds>");
                return;
            }
            int progress;
            if (!int.TryParse(args.Substring(0, sep), out progress))
            {
                Result(seq, "err", "bad progress");
                return;
            }
            string eventCardIds = args.Substring(sep + 1).Trim();
            GameLogicManager gm;
            BattlePlayerData self;
            if (!Self(out gm, out self, seq))
            {
                return;
            }
            BattlePveEventMessage msg = new BattlePveEventMessage(self, 40016, progress, eventCardIds);
            gm.communicate.RequestSendMessageC2S(msg, 40016, false);
            Result(seq, "ok", "pveevt progress=" + progress + " cards=" + eventCardIds);
        }

        private static void Praise(string seq, string args)
        {
            long targetId;
            if (!long.TryParse(args.Trim(), out targetId))
            {
                Result(seq, "err", "usage praise:<playerId>");
                return;
            }
            GameLogicManager gm;
            BattlePlayerData self;
            if (!Self(out gm, out self, seq))
            {
                return;
            }
            var receivers = new System.Collections.Generic.List<long>();
            foreach (BattlePlayerData x in gm.battle.PlayerDatas)
            {
                if (x == null || x.player == null)
                {
                    continue;
                }
                if (x.characterType == CharacterType.Hero && !x.player.OffLine && !x.player.IsBot && x.player.Id != self.player.Id)
                {
                    receivers.Add(x.player.Id);
                }
            }
            if (receivers.Count == 0)
            {
                Result(seq, "err", "no receivers (only bots/self)");
                return;
            }
            BattlePraiseMessage msg = new BattlePraiseMessage(self, targetId);
            gm.communicate.RequestPlayerChatC2S(msg, receivers, false);
            Result(seq, "ok", "praise " + targetId + " -> " + receivers.Count + " receivers");
        }
    }
}
