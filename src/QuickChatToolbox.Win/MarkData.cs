using System;

namespace QuickChatToolboxWin
{
    /// <summary>标记列表（不分组，平铺显示）。</summary>
    public static class MarkData
    {
        public static readonly (int Id, string Name)[] Marks =
        {
            (40005, "请求治疗"),
            (40006, "请求转账"),
            (40007, "有 N 张手牌"),
            (40008, "准备治疗 XX"),
            (40009, "准备给 XX 转账"),
            (40012, "小心 XX"),
            (40013, "集火 XX"),
            (40014, "我来解决 XX"),
            (40017, "请求给牌"),
            (40020, "准备给 XX 发牌"),
        };
    }

    public sealed class TargetItem
    {
        public long Id;
        public string Nick;
        public string CharName;
        public bool IsSelf;
        public bool IsBot;
        public int CharType;
        public bool Alive = true;

        public override string ToString()
        {
            string name = !string.IsNullOrEmpty(CharName) ? CharName : (!string.IsNullOrEmpty(Nick) ? Nick : "(未命名)");
            return name + "（" + Id + "）" + (IsSelf ? " [我]" : "") + (IsBot ? " [Bot]" : "") + (Alive ? "" : "  [已死亡]");
        }
    }
}
