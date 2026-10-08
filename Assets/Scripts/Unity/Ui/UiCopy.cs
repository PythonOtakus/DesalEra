using DesalEra.Game;
using DesalEra.Structure;

namespace DesalEra.Unity.Ui
{
    /// <summary>
    /// Player-facing Chinese copy. Logic keys (piece.Name, ResourceKind) stay English;
    /// only what the HUD draws goes through here.
    /// </summary>
    public static class UiCopy
    {
        public static string Piece(string name)
        {
            switch (name)
            {
                case "Deck": return "甲板";
                case "Column": return "立柱";
                case "Pontoon": return "浮筒";
                case "Brace": return "斜撑";
                case "Still": return "淡化器";
                case "Roof": return "屋顶";
                case "Wall": return "墙";
                case "Stairs": return "楼梯";
                default: return name;
            }
        }

        public static string Facing(string english)
        {
            switch (english)
            {
                case "north": return "北";
                case "west": return "西";
                case "south": return "南";
                default: return "东";
            }
        }

        public static string Material(MaterialKind kind)
        {
            switch (kind)
            {
                case MaterialKind.Wood: return "木材";
                case MaterialKind.Steel: return "钢材";
                case MaterialKind.Concrete: return "混凝土";
                case MaterialKind.Plastic: return "塑料";
                default: return kind.ToString();
            }
        }

        public static string Resource(ResourceKind kind)
        {
            switch (kind)
            {
                case ResourceKind.Plank: return "木板";
                case ResourceKind.Scrap: return "废料";
                case ResourceKind.Metal: return "金属";
                case ResourceKind.Water: return "淡水";
                case ResourceKind.Food: return "食物";
                default: return kind.ToString();
            }
        }

        public static string ResourceShort(ResourceKind kind)
        {
            switch (kind)
            {
                case ResourceKind.Plank: return "板";
                case ResourceKind.Scrap: return "废";
                case ResourceKind.Metal: return "金";
                case ResourceKind.Water: return "水";
                case ResourceKind.Food: return "食";
                default: return "?";
            }
        }

        /// <summary>Maps core English refusal strings onto Chinese for the notice strip.</summary>
        public static string Notice(string message)
        {
            if (string.IsNullOrEmpty(message)) return message;

            switch (message)
            {
                case "In the water. Swim out to salvage, step back onto the deck to build.":
                    return "已入水。游出去打捞，回到甲板才能建造。";
                case "Back on deck.":
                    return "已回到甲板。";
                case "Cannot build from the water. Get back on the deck.":
                    return "水中无法建造，请先回到甲板。";
                case "That is the spawn deck.":
                    return "那是出生甲板，不能拆。";
                case "Cannot dismantle from the water. Get back on the deck.":
                    return "水中无法拆除，请先回到甲板。";
                case "No rations aboard.":
                    return "船上没有口粮了。";
                case "Ate a ration.":
                    return "吃掉了一份口粮。";
                case "no piece specified":
                    return "未选择构件。";
                case "outside the buildable area":
                    return "超出可建造范围。";
                case "that platform is already occupied":
                    return "该位置已被占用。";
                case "must connect to the existing structure":
                    return "必须与主体结构相邻相连。";
                case "not enough resources":
                    return "资源不足。";
                case "nothing to dismantle here":
                    return "这里没有可拆的东西。";
                case "that piece is already gone":
                    return "那件已经不在了。";
                case "invalid build level":
                    return "无效的建造层。";
                case "a roof must sit on top of columns":
                    return "屋顶必须架在立柱顶上（至少 1 层）。";
                case "pontoons only go at deck level":
                    return "浮筒只能放在甲板层。";
                case "a roof needs a column at every corner":
                    return "屋顶四个角都需要立柱支撑。";
                case "needs support below; build a column first":
                    return "下方没有支撑，请先立柱。";
                case "a raised beam needs support at both ends":
                    return "高处的梁两端都需要支撑。";
                case "a wall needs a column at both ends":
                    return "墙两端都需要立柱（上下都要连通）。";
                case "stairs need something to stand on at the bottom":
                    return "楼梯底端没有落脚点。";
                case "stairs need a column top to climb to":
                    return "楼梯顶端需要连到上一层的立柱顶。";
            }

            if (message.EndsWith(" pieces collapsed."))
            {
                string head = message.Substring(0, message.Length - " pieces collapsed.".Length);
                int split = head.LastIndexOf(". ");
                string prefix = split >= 0 ? Notice(head.Substring(0, split + 1)) : string.Empty;
                string count = split >= 0 ? head.Substring(split + 2) : head;
                return $"{prefix}{count} 个构件失去支撑或损毁，已坍塌。";
            }

            if (message.StartsWith("Facing "))
                return "朝向：" + Facing(message.Substring("Facing ".Length).TrimEnd('.')) + "。";

            if (message.StartsWith("Build level "))
                return "建造层：" + message.Substring("Build level ".Length).TrimEnd('.') + "。";

            if (message.StartsWith("Selected "))
                return "已选择 " + Piece(message.Substring("Selected ".Length).TrimEnd('.')) + "。";

            if (message.StartsWith("Built ") && message.Contains(" at "))
            {
                int at = message.IndexOf(" at ");
                string name = message.Substring("Built ".Length, at - "Built ".Length);
                string cell = message.Substring(at + 4).TrimEnd('.');
                return $"已建造{Piece(name)}（{cell}）。";
            }

            if (message.StartsWith("Dismantled at "))
                return "已拆除（" + message.Substring("Dismantled at ".Length).TrimEnd('.') + "）。";

            return message;
        }
    }
}
