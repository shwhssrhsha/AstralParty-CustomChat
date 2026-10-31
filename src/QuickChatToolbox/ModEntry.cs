using CesiumLoader.SDK.Mods;

namespace QuickChatToolbox
{
    /// <summary>
    /// Cesium 加载器入口：入口类型必须是 "&lt;dll文件名&gt;.ModEntry"，方法必须是 public static void Main()。
    /// </summary>
    public static class ModEntry
    {
        public static void Main()
        {
            ModBase.Run(ToolboxMod.OnInit, ToolboxMod.OnTick, tag: "QuickChatToolbox");
        }
    }
}
