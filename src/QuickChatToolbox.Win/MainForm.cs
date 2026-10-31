using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace QuickChatToolboxWin
{
    public sealed class MainForm : Form
    {
        private readonly ComboBox _targets = new ComboBox();
        private readonly FlowLayoutPanel _marks = new FlowLayoutPanel();
        private readonly Label _status = new Label();
        private readonly Button _refresh = new Button();

        // 分享页
        private readonly TextBox _search = new TextBox();
        private readonly Button _refreshShare = new Button();
        private readonly ComboBox _cardCombo = new ComboBox();
        private readonly NumericUpDown _cardCount = new NumericUpDown();
        private readonly ComboBox _relicCombo = new ComboBox();
        private readonly NumericUpDown _skillCd = new NumericUpDown();
        private readonly ComboBox _taskCombo = new ComboBox();
        private readonly NumericUpDown _evtProgress = new NumericUpDown();
        private readonly ComboBox _eventCombo = new ComboBox();
        private readonly ComboBox _praiseCombo = new ComboBox();

        // 完整列表缓存（搜索过滤用）
        private readonly List<ComboItem> _cards = new List<ComboItem>();
        private readonly List<ComboItem> _relics = new List<ComboItem>();
        private readonly List<ComboItem> _tasks = new List<ComboItem>();
        private readonly List<ComboItem> _events = new List<ComboItem>();
        private readonly List<TargetItem> _alivePlayers = new List<TargetItem>();
        private readonly List<TargetItem> _deadPlayers = new List<TargetItem>();

        private const int RowW = 392;

        private sealed class ComboItem
        {
            public int Id;
            public string Name;
            public override string ToString()
            {
                return Id + "  " + Name;
            }
        }

        private DateTime _lastSend = DateTime.MinValue;

        public MainForm()
        {
            Text = "快捷短语工具箱 v0.5";
            StartPosition = FormStartPosition.Manual;
            Width = 420;
            Height = 680;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = true;
            ShowInTaskbar = true;
            BackColor = Color.FromArgb(248, 249, 251);
            Font = new Font("Microsoft YaHei UI", 9f);

            TabControl tabs = new TabControl { Dock = DockStyle.Fill };
            TabPage tpMarks = new TabPage("标记");
            TabPage tpShare = new TabPage("分享");
            tpMarks.BackColor = Color.FromArgb(248, 249, 251);
            tpShare.BackColor = Color.FromArgb(248, 249, 251);
            tabs.TabPages.Add(tpMarks);
            tabs.TabPages.Add(tpShare);

            BuildMarksTab(tpMarks);
            BuildShareTab(tpShare);

            Panel statusBar = new Panel { Dock = DockStyle.Bottom, Height = 30, BackColor = Color.FromArgb(232, 240, 250) };
            _status = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(8, 0, 8, 0),
                Text = "准备就绪",
            };
            statusBar.Controls.Add(_status);

            Controls.Add(tabs);
            Controls.Add(statusBar);

            BuildMarkButtons();
            Load += OnLoad;
        }

        private void BuildMarksTab(TabPage page)
        {
            Panel top = new Panel { Dock = DockStyle.Top, Height = 40 };
            Label lbl = new Label { Text = "目标：", Location = new Point(8, 12), AutoSize = true };
            _targets.DropDownStyle = ComboBoxStyle.DropDownList;
            _targets.Location = new Point(50, 8);
            _targets.Width = 290;
            _refresh.Text = "刷新";
            _refresh.Location = new Point(346, 7);
            _refresh.Width = 66;
            _refresh.Click += (s, e) => RefreshAll();
            top.Controls.AddRange(new Control[] { lbl, _targets, _refresh });

            Label hint = new Label
            {
                Dock = DockStyle.Top,
                Height = 20,
                Text = "  点击下方条目发送",
                ForeColor = Color.Gray,
                TextAlign = ContentAlignment.MiddleLeft,
            };

            _marks.Dock = DockStyle.Fill;
            _marks.FlowDirection = FlowDirection.TopDown;
            _marks.WrapContents = false;
            _marks.AutoScroll = true;
            _marks.Padding = new Padding(10, 6, 10, 6);

            page.Controls.Add(_marks);
            page.Controls.Add(hint);
            page.Controls.Add(top);
        }

        private void BuildShareTab(TabPage page)
        {
            FlowLayoutPanel flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                Padding = new Padding(8, 6, 8, 6),
            };

            // 搜索 + 刷新
            Panel searchRow = new Panel { Width = RowW, Height = 32, Margin = new Padding(0, 0, 0, 6) };
            _search.Location = new Point(2, 4);
            _search.Width = 280;
            _search.PlaceholderText = "搜索 id 或名称（过滤所有列表）";
            _search.TextChanged += (s, e) => ApplyFilter();
            _refreshShare.Text = "刷新";
            _refreshShare.Location = new Point(290, 3);
            _refreshShare.Width = 66;
            _refreshShare.Click += (s, e) => RefreshAll();
            searchRow.Controls.AddRange(new Control[] { _search, _refreshShare });
            flow.Controls.Add(searchRow);

            flow.Controls.Add(MakeRow("卡牌", _cardCombo, _cardCount, MakeButton("发送", () => SendShare("card"))));
            flow.Controls.Add(MakeRow("遗物", _relicCombo, null, MakeButton("发送", () => SendShare("relic"))));
            flow.Controls.Add(MakeRow("技能", null, _skillCd, MakeButton("发送", () => SendShare("skill"))));
            flow.Controls.Add(MakeRow("PVE任务", _taskCombo, null, MakeButton("发送", () => SendShare("task"))));
            flow.Controls.Add(MakeRow("PVE事件", _eventCombo, _evtProgress, MakeButton("发送", () => SendShare("pveevt"))));
            flow.Controls.Add(MakeRow("点赞", _praiseCombo, null, MakeButton("发送", () => SendShare("praise"))));

            page.Controls.Add(flow);
        }

        private Button MakeButton(string text, Action onClick)
        {
            Button b = new Button { Text = text, Width = 62, Height = 26 };
            b.Click += (s, e) => onClick();
            return b;
        }

        /// <summary>一行：Label + [数量/进度] + 主控件(下拉或数字) + 发送按钮，宽度自适应。</summary>
        private Panel MakeRow(string title, ComboBox mainCombo, Control extraNum, Button btn)
        {
            Panel row = new Panel { Width = RowW, Height = 34, Margin = new Padding(0, 2, 0, 2) };
            Label lbl = new Label { Text = title, Location = new Point(2, 9), Width = 56, AutoSize = false };
            row.Controls.Add(lbl);

            int btnX = RowW - 64;
            int right = btnX - 6;
            if (mainCombo != null)
            {
                mainCombo.DropDownStyle = ComboBoxStyle.DropDownList;
                int extraW = extraNum != null ? 84 : 0;
                mainCombo.Location = new Point(60, 5);
                mainCombo.Width = Math.Max(120, right - 60 - (extraNum != null ? extraW + 6 : 0));
                row.Controls.Add(mainCombo);
                if (extraNum != null)
                {
                    extraNum.Location = new Point(right - extraW, 5);
                    extraNum.Width = extraW;
                    row.Controls.Add(extraNum);
                }
            }
            else if (extraNum != null)
            {
                extraNum.Location = new Point(60, 5);
                extraNum.Width = 84;
                row.Controls.Add(extraNum);
            }
            btn.Location = new Point(btnX, 4);
            row.Controls.Add(btn);
            return row;
        }

        private void OnLoad(object sender, EventArgs e)
        {
            Ipc.Log("load begin");
            Rectangle wa = Screen.PrimaryScreen.WorkingArea;
            Location = new Point(wa.Right - Width - 24, wa.Top + 80);
            _cardCount.Minimum = 1;
            _cardCount.Maximum = int.MaxValue;
            _cardCount.Value = 1;
            _skillCd.Minimum = 0;
            _skillCd.Maximum = int.MaxValue;
            _skillCd.Value = 0;
            _evtProgress.Minimum = 0;
            _evtProgress.Maximum = 99;
            _evtProgress.Value = 1;
            CheckPing();
            RefreshAll();
            Ipc.Log("load done");
        }

        private void BuildMarkButtons()
        {
            _marks.Controls.Clear();
            foreach ((int Id, string Name) m in MarkData.Marks)
            {
                int markId = m.Id;
                Button btn = new Button
                {
                    Text = m.Id + "  " + m.Name,
                    Width = 370,
                    Height = 36,
                    TextAlign = ContentAlignment.MiddleLeft,
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.White,
                    Margin = new Padding(3, 3, 3, 5),
                };
                btn.FlatAppearance.BorderColor = Color.FromArgb(210, 216, 224);
                btn.Click += (s, e) => SendMark(markId);
                _marks.Controls.Add(btn);
            }
        }

        private void CheckPing()
        {
            (bool ok, string text) = Ipc.Send("ping", 1500);
            Ipc.Log("ping -> " + text);
            if (!ok)
            {
                SetStatus("插件未就绪：" + text);
            }
        }

        private void RefreshAll()
        {
            (bool rok, string rtext) = Ipc.Send("roominfo", 1500);
            Ipc.Log("roominfo -> " + rtext);
            (bool ok, string ptext) = Ipc.Send("players", 2000);
            Ipc.Log("players -> " + ptext);
            if (!ok)
            {
                _alivePlayers.Clear();
                _deadPlayers.Clear();
                SetStatus("刷新失败：" + ptext);
                return;
            }
            LoadPlayers();
            LoadCombo(_cards, "cards", "cards.txt");
            LoadCombo(_relics, "relics", "relics.txt");
            LoadCombo(_tasks, "tasks", "tasks.txt");
            LoadCombo(_events, "events", "events.txt");
            ApplyFilter();
            SetStatus("已刷新：玩家 " + (_alivePlayers.Count + _deadPlayers.Count) + "，卡牌 " + _cards.Count + "，遗物 " + _relics.Count + "，任务 " + _tasks.Count + "，事件 " + _events.Count + "  [" + rtext + "]");
        }

        private void LoadPlayers()
        {
            _alivePlayers.Clear();
            _deadPlayers.Clear();
            string raw = Ipc.ReadFile("players.txt");
            if (string.IsNullOrEmpty(raw))
            {
                return;
            }
            foreach (string line in raw.Split('\n'))
            {
                string s = line.Trim();
                if (s.Length == 0 || s.StartsWith("#"))
                {
                    continue;
                }
                string[] p = s.Split('\t');
                if (p.Length < 8)
                {
                    continue;
                }
                long id;
                if (!long.TryParse(p[0], out id))
                {
                    continue;
                }
                TargetItem t = new TargetItem { Id = id };
                int charType;
                if (p.Length >= 10)
                {
                    t.Nick = p[1];
                    t.CharName = p[2];
                    t.IsBot = p[5] == "1";
                    if (!int.TryParse(p[7], out charType))
                    {
                        continue;
                    }
                    t.CharType = charType;
                    t.IsSelf = p[8] == "1";
                    t.Alive = p[9] == "1";
                }
                else if (p.Length >= 9)
                {
                    t.Nick = p[1];
                    t.CharName = p[2];
                    t.IsBot = p[5] == "1";
                    if (!int.TryParse(p[7], out charType))
                    {
                        continue;
                    }
                    t.CharType = charType;
                    t.IsSelf = p[8] == "1";
                }
                else
                {
                    t.Nick = p[1];
                    t.IsBot = p[4] == "1";
                    if (!int.TryParse(p[6], out charType))
                    {
                        continue;
                    }
                    t.CharType = charType;
                    t.IsSelf = p[7] == "1";
                }
                if (t.Alive)
                {
                    _alivePlayers.Add(t);
                }
                else
                {
                    _deadPlayers.Add(t);
                }
            }
        }

        private void LoadCombo(List<ComboItem> target, string cmd, string file)
        {
            target.Clear();
            Ipc.Send(cmd, 2000);
            string raw = Ipc.ReadFile(file);
            if (string.IsNullOrEmpty(raw))
            {
                return;
            }
            foreach (string line in raw.Split('\n'))
            {
                string s = line.Trim();
                if (s.Length == 0 || s.StartsWith("#"))
                {
                    continue;
                }
                string[] p = s.Split('\t');
                int id;
                if (!int.TryParse(p[0], out id))
                {
                    continue;
                }
                target.Add(new ComboItem { Id = id, Name = p.Length > 1 ? p[1] : "" });
            }
        }

        private void ApplyFilter()
        {
            string kw = _search.Text.Trim();
            FillCombo(_cardCombo, _cards, kw);
            FillCombo(_relicCombo, _relics, kw);
            FillCombo(_taskCombo, _tasks, kw);
            FillCombo(_eventCombo, _events, kw);
            FillPlayers(_targets, kw);
            FillPlayers(_praiseCombo, kw);
        }

        private static bool Match(string id, string name, string kw)
        {
            if (string.IsNullOrEmpty(kw))
            {
                return true;
            }
            return id.Contains(kw) || (name != null && name.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private void FillCombo(ComboBox combo, List<ComboItem> source, string kw)
        {
            combo.Items.Clear();
            foreach (ComboItem it in source)
            {
                if (Match(it.Id.ToString(), it.Name, kw))
                {
                    combo.Items.Add(it);
                }
            }
            if (combo.Items.Count > 0)
            {
                combo.SelectedIndex = 0;
            }
        }

        private void FillPlayers(ComboBox combo, string kw)
        {
            combo.Items.Clear();
            foreach (TargetItem t in _alivePlayers)
            {
                if (Match(t.Id.ToString(), t.CharName, kw) || Match(t.Id.ToString(), t.Nick, kw))
                {
                    combo.Items.Add(t);
                }
            }
            if (_deadPlayers.Count > 0)
            {
                bool anyDead = false;
                foreach (TargetItem t in _deadPlayers)
                {
                    if (Match(t.Id.ToString(), t.CharName, kw) || Match(t.Id.ToString(), t.Nick, kw))
                    {
                        anyDead = true;
                        break;
                    }
                }
                if (anyDead)
                {
                    combo.Items.Add("──────── 已死亡 ────────");
                    foreach (TargetItem t in _deadPlayers)
                    {
                        if (Match(t.Id.ToString(), t.CharName, kw) || Match(t.Id.ToString(), t.Nick, kw))
                        {
                            combo.Items.Add(t);
                        }
                    }
                }
            }
            if (combo.Items.Count > 0)
            {
                combo.SelectedIndex = 0;
            }
        }

        private bool CheckCooldown()
        {
            if ((DateTime.Now - _lastSend).TotalMilliseconds < 400)
            {
                SetStatus("操作太快，稍等一下再发（防刷屏）");
                return false;
            }
            _lastSend = DateTime.Now;
            return true;
        }

        private void SendMark(int markId)
        {
            if (!CheckCooldown())
            {
                return;
            }
            if (!(_targets.SelectedItem is TargetItem t))
            {
                SetStatus("请先选择目标");
                return;
            }
            (bool ok, string text) = Ipc.Send("mark:" + markId + ":" + t.Id, 3000);
            Ipc.Log("mark " + markId + " -> " + t.Id + " : " + text);
            SetStatus((ok ? "已发送 " : "失败 ") + text);
        }

        private void SendShare(string kind)
        {
            if (!CheckCooldown())
            {
                return;
            }
            string cmd = null;
            switch (kind)
            {
                case "card":
                    if (_cardCombo.SelectedItem is ComboItem card)
                    {
                        cmd = "card:" + card.Id + ":" + (int)_cardCount.Value;
                    }
                    break;
                case "relic":
                    if (_relicCombo.SelectedItem is ComboItem relic)
                    {
                        cmd = "relic:" + relic.Id;
                    }
                    break;
                case "skill":
                    cmd = "skill:" + (int)_skillCd.Value;
                    break;
                case "task":
                    if (_taskCombo.SelectedItem is ComboItem task)
                    {
                        cmd = "task:" + task.Id;
                    }
                    break;
                case "pveevt":
                    if (_eventCombo.SelectedItem is ComboItem evt)
                    {
                        cmd = "pveevt:" + (int)_evtProgress.Value + ":" + evt.Id;
                    }
                    break;
                case "praise":
                    if (_praiseCombo.SelectedItem is TargetItem target)
                    {
                        cmd = "praise:" + target.Id;
                    }
                    break;
            }
            if (string.IsNullOrEmpty(cmd))
            {
                SetStatus("请先选择要发送的条目（PVE任务需要 PVE 房间才有数据）");
                return;
            }
            (bool ok, string text) = Ipc.Send(cmd, 3000);
            Ipc.Log(kind + " : " + text);
            SetStatus((ok ? "已发送 " : "失败 ") + text);
        }

        private void SetStatus(string text)
        {
            _status.Text = "  " + text;
        }
    }
}
