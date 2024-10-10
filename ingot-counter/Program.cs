using Sandbox.Definitions;
using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Linq;
using VRage;
using VRage.Game;
using VRage.Game.ModAPI.Ingame;


namespace IngameScript
{
    partial class Program : MyGridProgram
    {
        // COPY AND PASTE START ON THIS LINE

        public static string[] categories = new string[] { "Component", "Ingot", "Ore" };
        public static string[] hiddenCategories = new string[] { "AmmoMagazine" };

        public static System.Text.RegularExpressions.Regex configRegex = new System.Text.RegularExpressions.Regex("([a-z]+): ?([^\\s]+( \\d+)?)");
        public static System.Text.RegularExpressions.Regex configRegexAllowSpace = new System.Text.RegularExpressions.Regex("([a-z]+): ?([ a-zA-Z0-9_-]+)");

        private SortedDictionary<string, SortedDictionary<string, MyFixedPoint>> counts = new SortedDictionary<string, SortedDictionary<string, MyFixedPoint>>();

        private List<string> debug = new List<string>();
        private List<string> lines = new List<string>();

        private List<IMyCargoContainer> containers = new List<IMyCargoContainer>();
        private List<IMyTerminalBlock> blocksToDrain = new List<IMyTerminalBlock>();

        private List<IMyTerminalBlock> textSurfaceBlocks = new List<IMyTerminalBlock>();

        private bool IsDebugMode = false;

        private int tick = 0;

        string _broadCastTag = "InventoryBroadcast";
        IMyBroadcastListener _myBroadcastListener;

        public Program()
        {
            _myBroadcastListener = IGC.RegisterBroadcastListener(_broadCastTag);
            _myBroadcastListener.SetMessageCallback(_broadCastTag);

            Runtime.UpdateFrequency = UpdateFrequency.Update100;
        }

        public void Main(string argument, UpdateType updateSource)
        {
            IsDebugMode = Me.CustomData.Contains("DEBUG!");

            containers.Clear();
            blocksToDrain.Clear();
            textSurfaceBlocks.Clear();

            GridTerminalSystem.GetBlocksOfType(containers, block => block.IsSameConstructAs(Me) && block is IMyCargoContainer);
            GridTerminalSystem.GetBlocksOfType(blocksToDrain, block => block.IsSameConstructAs(Me) && (block is IMyShipConnector || block is IMyRefinery || block is IMyAssembler));

            //debug.Add($"Tick {tick}");

            if (tick % 2 == 1)
            {
                SortItems(containers, blocksToDrain);

                Debug();
            }
            else
            {
                CountItems(containers);

                CraftItems(counts, blocksToDrain.Where(b => b is IMyAssembler && b.CustomData.Length > 5));

                RenderInventory(counts);

                HandleIncomingMessages(updateSource);

                Debug();
            }

            if (tick++ > 100)
            {
                tick = 0;
            }
        }

        private void SortItems(IEnumerable<IMyCargoContainer> containers, IEnumerable<IMyTerminalBlock> blocksToDrainOnly)
        {
            Dictionary<string, List<IMyCargoContainer>> preferredContainers = new Dictionary<string, List<IMyCargoContainer>>();
            foreach (var container in containers)
            {
                if (container.IsWorking && container.CustomData.Length > 5)
                {
                    foreach (var key in GetConfig("insert", container.CustomData))
                    {
                        if (!preferredContainers.ContainsKey(key))
                        {
                            preferredContainers[key] = new List<IMyCargoContainer>();
                        }
                        preferredContainers[key].Add(container);
                    }
                }
            }

            MoveItemsToTargets(containers, preferredContainers);

            if (tick % 10 == 0)
            {
                MoveItemsToTargets(blocksToDrainOnly, preferredContainers);
            }
        }

        private void MoveItemsToTargets(IEnumerable<IMyCubeBlock> containers, Dictionary<string, List<IMyCargoContainer>> preferredContainers)
        {
            foreach (var container in containers)
            {
                if (container.HasInventory)
                {
                    for (var i = 0; i < container.InventoryCount; i++)
                    {
                        List<MyInventoryItem> items = new List<MyInventoryItem>();
                        var inventory = container.GetInventory(i);
                        inventory.GetItems(items);

                        foreach (var item in items)
                        {
                            if ((container is IMyRefinery && IsOre(item)) || (container is IMyAssembler && IsIngot(item)))
                            {
                                continue;
                            }

                            var prefs = preferredContainers.Where(p => IsOfType(item, p.Key)).FirstOrDefault().Value ?? new List<IMyCargoContainer>();

                            if (!prefs.Any(p => p == container))
                            {
                                foreach (var pref in SortedPreferred(prefs, item))
                                {
                                    if (inventory.CanTransferItemTo(pref.GetInventory(), item.Type))
                                    {
                                        var amount = AmountToMove(item, pref.GetInventory());
                                        if (amount != MyFixedPoint.Zero && inventory.TransferItemTo(pref.GetInventory(), item))
                                        {
                                            debug.Add($"Moved {amount} {item.Type.SubtypeId} from {container.DisplayNameText} to {pref.DisplayNameText}");
                                            break;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        private void CountItems(IEnumerable<IMyCargoContainer> containers)
        {
            foreach (var category in categories.Concat(hiddenCategories))
            {
                counts[category] = new SortedDictionary<string, MyFixedPoint>();
            }

            foreach (var container in containers)
            {
                List<MyInventoryItem> items = new List<MyInventoryItem>();
                var inventory = container.GetInventory();
                inventory.GetItems(items);

                foreach (var item in items)
                {
                    foreach (var category in categories.Concat(hiddenCategories))
                    {
                        if (IsOfType(item, category))
                        {
                            var subtype = DisplayName(item);

                            if (counts[category].ContainsKey(subtype))
                            {

                                counts[category][subtype] += item.Amount;
                            }
                            else
                            {
                                counts[category][subtype] = item.Amount;
                            }
                        }
                    }
                }
            }
        }

        private void CraftItems(SortedDictionary<string, SortedDictionary<string, MyFixedPoint>> counts, IEnumerable<IMyTerminalBlock> assemblers)
        {
            foreach (IMyAssembler assembler in assemblers)
            {
                if (assembler.IsProducing || !assembler.IsWorking || !assembler.IsQueueEmpty)
                {
                    continue;
                }

                var desiredCounts = GetConfig("craft", assembler.CustomData);

                foreach (var desired in desiredCounts)
                {
                    try
                    {
                        var typeText = desired.Split(' ').First();
                        var c = long.Parse(desired.Split(' ').Last());

                        MyDefinitionId? bp = GetBlueprintFor(typeText);

                        if (bp.HasValue)
                        {

                            long cur = (long)GetCountFor(counts, typeText);

                            if (c > 0 && cur < c && assembler.CanUseBlueprint(bp.Value))
                            {
                                //debug.Add($"Crafting {typeText} on {assembler.DisplayNameText} to get to {c}");
                                assembler.AddQueueItem(bp.Value, 1d);
                            }
                        }
                        else
                        {
                            debug.Add($"Blueprint for {typeText} is currently not supported as auto-craft target");
                        }
                    }
                    catch (Exception e)
                    {
                        debug.Add(e.Message);
                    }
                }
            }

        }

        private void WriteCounter(List<string> lines, SortedDictionary<string, MyFixedPoint> counterToShow, string categoryName)
        {
            lines.Add($"{Me.CubeGrid.CustomName} {categoryName} Inventory");

            foreach (var itemName in counterToShow.Keys)
            {
                var label = itemName.Split('/').Last();
                var amount = ToSI(counterToShow[itemName]);
                lines.Add($"{label}: {amount}");
            }
        }

        private void RenderInventory(SortedDictionary<string, SortedDictionary<string, MyFixedPoint>> counts)
        {
            var categoriesWithItems = categories.Where(c => counts.ContainsKey(c) && counts[c].Count() > 0).ToArray();
            var secondsPerCategory = (int)(60 / categoriesWithItems.Length);
            var timeBasedCategory = categoriesWithItems[(int)Math.Floor((double)(DateTime.Now.Second / secondsPerCategory))];

            var displayNames = GetConfig("display", $"{Me.DisplayNameText} {Me.CustomData}");

            var projectToOtherGrids = tick % 10 == 0 && GetConfig("broadcast", Me.CustomData).FirstOrDefault() == "true";

            foreach (var dn in displayNames)
            {
                GridTerminalSystem.GetBlocksOfType(textSurfaceBlocks, block => block is IMyTextSurfaceProvider &&
                    block.IsSameConstructAs(Me) &&
                    (block.DisplayNameText.Contains(dn) || GetConfig("display", block.CustomData).FirstOrDefault() == dn));
            }

            foreach (var category in categories)
            {
                bool isDefaultCategory = category == timeBasedCategory;
                lines.Clear();

                WriteCounter(lines, counts[category], category);
                var txt = WriteTextOnMatchingBlocks(lines, category, isDefaultCategory);

                if (projectToOtherGrids)
                {
                    IGC.SendBroadcastMessage(_broadCastTag, $"{Me.CubeGrid.CustomName}|{category}|{isDefaultCategory}\n{txt}");
                }

                if (isDefaultCategory)
                {
                    WriteText(lines, new List<IMyTerminalBlock> { Me });
                    if (!IsDebugMode)
                    {
                        Echo(txt);
                    }
                }
            }
        }

        private void HandleIncomingMessages(UpdateType updateSource)
        {
            if ((updateSource & UpdateType.IGC) > 0)
            {
                while (_myBroadcastListener.HasPendingMessage)
                {
                    MyIGCMessage myIGCMessage = _myBroadcastListener.AcceptMessage();
                    if (myIGCMessage.Tag == _broadCastTag)
                    {
                        if (myIGCMessage.Data is string)
                        {
                            var lines = myIGCMessage.Data.ToString().Split('\n');

                            if (lines.Length > 1)
                            {
                                var config = lines.First().Split('|');

                                if (config.Length == 3)
                                {
                                    var sender = config[0];
                                    var category = config[1];
                                    var isDefaultCategory = config[2] == "True";

                                    debug.Add($"Parsed message from {sender} for {category}. default {isDefaultCategory}!");

                                    WriteTextOnMatchingBlocks(lines.Skip(1), category, isDefaultCategory, sender);
                                }
                            }
                        }
                    }
                }
            }
        }

        private void Debug()
        {
            if (IsDebugMode && debug.Count > 0)
            {
                Echo(String.Join("\n", debug));
            }
            debug.Clear();
        }

        private string WriteTextOnMatchingBlocks(IEnumerable<string> lines, string category, bool isDefaultCategory, string remoteGridName = null)
        {
            return WriteText(lines, textSurfaceBlocks.Where(b => DisplaysCategory(b, category, isDefaultCategory) &&
                GetConfig("grid", b.CustomData, true).FirstOrDefault() == remoteGridName?.Trim()
            ));
        }

        private string WriteText(IEnumerable<string> lines, IEnumerable<IMyTerminalBlock> blocks)
        {
            float fontSize = Math.Min(1.2f, 17.0f / lines.Count());
            string text = String.Join("\n", lines);

            foreach (var block in blocks)
            {
                (block as IMyTextSurfaceProvider).GetSurface(0).FontSize = fontSize;
                (block as IMyTextSurfaceProvider).GetSurface(0).WriteText(text);
            }

            return text;
        }

        private string HumanSubtypeToMachine(string subtype)
        {
            switch (subtype)
            {
                case "Assault":
                    return "MediumCalibreAmmo";
                case "Artillery":
                    return "LargeCalibreAmmo";
                case "Gatling":
                    return "NATO_25x184mm";
                case "Rocket":
                    return "Missile200mm";
            }
            return subtype;
        }

        private MyDefinitionId? GetBlueprintFor(string typeText)
        {
            string[] t = typeText.Split('/');
            string bp = "";

            if (t.Length == 2)
            {
                if (t[0] == "Component")
                {
                    switch (t[1])
                    {
                        case "SmallTube":
                        case "SolarCell":
                        case "SteelPlate":
                        case "Superconductor":
                        case "InteriorPlate":
                        case "MetalGrid":
                        case "BulletproofGlass":
                        case "Display":
                        case "LargeTube":
                        case "PowerCell":
                            bp = t[1];
                            break;
                        default:
                            bp = $"{t[1]}Component";
                            break;
                    }
                }
                else if (t[0] == "AmmoMagazine")
                {
                    switch (HumanSubtypeToMachine(t[1]))
                    {
                        case "LargeCalibreAmmo":
                            bp = "Position0120_LargeCalibreAmmo";
                            break;
                        case "MediumCalibreAmmo":
                            bp = "Position0110_MediumCalibreAmmo";
                            break;
                        case "NATO_25x184mm":
                            bp = "Position0080_NATO_25x184mmMagazine";
                            break;
                        case "Missile200mm":
                            bp = "Position0100_Missile200mm";
                            break;
                        case "LargeRailgunAmmo":
                            bp = "Position0140_LargeRailgunAmmo";
                            break;
                        case "SmallRailgunAmmo":
                            bp = "Position0130_SmallRailgunAmmo";
                            break;
                    }
                }
            }

            if (bp.Length > 0)
            {
                return MyDefinitionId.Parse($"MyObjectBuilder_BlueprintDefinition/{bp}");
            }

            return null;
        }

        private MyFixedPoint GetCountFor(SortedDictionary<string, SortedDictionary<string, MyFixedPoint>> counts, string typeText)
        {
            var t = typeText.Split('/');
            var subtype = HumanSubtypeToMachine(t[1]);

            return counts.ContainsKey(t[0]) && counts[t[0]].ContainsKey(subtype) ? counts[t[0]][subtype] : MyFixedPoint.Zero;
        }

        private IEnumerable<IMyCargoContainer> SortedPreferred(List<IMyCargoContainer> prefs, MyInventoryItem item)
        {
            var sorted = prefs.ToList();

            sorted.Sort((c1, c2) =>
            {
                var p1 = c1.CustomData.Contains($"prefer:{item.Type.SubtypeId}") ? 1 : 0;
                var p2 = c2.CustomData.Contains($"prefer:{item.Type.SubtypeId}") ? 1 : 0;

                if (p1 != p2) { return p2 - p1; }

                p1 = c1.GetInventory().GetItemAmount(item.Type).ToIntSafe();
                p2 = c2.GetInventory().GetItemAmount(item.Type).ToIntSafe();

                if (p1 != p2) { return p2 - p1; }

                return c1.DisplayNameText.CompareTo(c2.DisplayNameText);
            });

            return sorted;
        }

        private IEnumerable<string> GetConfig(string key, string text, bool allowSpace = false)
        {
            var captures = new List<string>();

            var matches = (allowSpace ? configRegexAllowSpace : configRegex).Matches(text);

            for (var i = 0; i < matches.Count; i++)
            {
                if (matches[i].Success && matches[i].Result("$1") == key)
                {
                    captures.Add(matches[i].Result("$2"));
                }
            }

            return captures;
        }

        private MyFixedPoint FreeVolume(IMyInventory inventory)
        {
            return inventory.MaxVolume - inventory.CurrentVolume;
        }

        private MyFixedPoint AmountToMove(MyInventoryItem item, IMyInventory to)
        {
            var fv = FreeVolume(to);
            var iv = item.Type.GetItemInfo().Volume;

            if ((float)fv > (iv * (float)item.Amount))
            {
                return item.Amount;
            }

            if ((float)fv > iv)
            {
                return fv;
            }

            return MyFixedPoint.Zero;
        }

        private bool DisplaysCategory(IMyTerminalBlock block, string category, bool isDefault)
        {
            var matches = GetConfig("category", block.CustomData);

            return matches.Count() > 0 ? matches.Any(m => m == category) : isDefault;
        }

        private string ToSI(MyFixedPoint a, string format = "f2")
        {
            double d = ((double)a);
            char[] incPrefixes = new[] { 'k', 'M', 'G', 'T', 'P', 'E', 'Z', 'Y' };
            char[] decPrefixes = new[] { 'm', '\u03bc', 'n', 'p', 'f', 'a', 'z', 'y' };

            int degree = (int)Math.Floor(Math.Log10(Math.Abs(d)) / 3);
            double scaled = d * Math.Pow(1000, -degree);

            char? prefix = null;
            switch (Math.Sign(degree))
            {
                case 1: prefix = incPrefixes[degree - 1]; break;
                case -1: prefix = decPrefixes[-degree - 1]; break;
            }

            return scaled.ToString(format) + prefix;
        }

        private bool IsIngot(MyInventoryItem item)
        {
            return IsOfType(item, "Ingot");
        }

        private bool IsOre(MyInventoryItem item)
        {
            return IsOfType(item, "Ore");
        }

        private bool IsOfType(MyInventoryItem item, string type)
        {
            return item.Type.ToString().Contains($"_{type}/");
        }

        private string DisplayName(MyInventoryItem item)
        {
            var name = item.Type.SubtypeId;

            if (name == "Stone" && IsIngot(item))
            {
                name = "Gravel";
            }

            return name;
        }

        // COPY AND PASTE END ON THIS LINE
    }
}
