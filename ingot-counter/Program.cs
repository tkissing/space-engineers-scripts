using Microsoft.Build.Framework.XamlTypes;
using Sandbox.Game;
using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Xml.Linq;
using VRage;
using VRage.Game.ModAPI.Ingame;
using VRage.ModAPI;


namespace IngameScript
{
    partial class Program : MyGridProgram
    {
        // COPY AND PASTE START ON THIS LINE

        public static string[] categories = new string[] { "Component", "Ingot", "Ore" };

        public static System.Text.RegularExpressions.Regex showRegex = new System.Text.RegularExpressions.Regex("display: ?([^\\s]+)");
        public static System.Text.RegularExpressions.Regex insertRegex = new System.Text.RegularExpressions.Regex("insert: ?([^\\s]+)");

        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update100;
        }

        public void Main(string argument, UpdateType updateSource)
        {
            List<string> lines = new List<string>();
            List<string> debug = new List<string>();

            List<IMyCargoContainer> containers = new List<IMyCargoContainer>();
            List<IMyShipConnector> connectors = new List<IMyShipConnector>();

            GridTerminalSystem.GetBlocksOfType(containers, block => block.IsSameConstructAs(Me) && block is IMyCargoContainer);
            GridTerminalSystem.GetBlocksOfType(connectors, block => block.IsSameConstructAs(Me) && block is IMyShipConnector);

            SortItems(containers, connectors, lines, debug);

            CountItems(containers, lines);

            bool doDebug = Me.CustomData.Contains("DEBUG!");

            Show(lines, doDebug);
            if (doDebug)
            {
                Debug(debug);
            }
        }

        private void SortItems(IEnumerable<IMyCargoContainer> containers, IEnumerable<IMyShipConnector> connectors, List<string> lines, List<string> debug)
        {
            Dictionary<string, List<IMyCargoContainer>> preferredContainers = new Dictionary<string, List<IMyCargoContainer>>();
            foreach (var container in containers)
            {
                if (container.IsWorking && container.CustomData.Length > 5 && FreeVolume(container) > 2)
                {
                    foreach (var key in GetFirstCaptures(insertRegex, container.CustomData))
                    {
                        if (!preferredContainers.ContainsKey(key))
                        {
                            preferredContainers[key] = new List<IMyCargoContainer>();
                        }
                        preferredContainers[key].Add(container);

                        debug.Add($"{container.DisplayNameText} gets {key}, free volume {FreeVolume(container)}");
                    }
                }
            }

            MoveItemsToTargets(containers, preferredContainers);
            MoveItemsToTargets(connectors, preferredContainers);
        }

        private void MoveItemsToTargets(IEnumerable<VRage.Game.ModAPI.Ingame.IMyEntity> containers, Dictionary<string, List<IMyCargoContainer>> preferredContainers)
        {
            List<MyInventoryItem> items = new List<MyInventoryItem>();

            foreach (var container in containers)
            {
                if (container.HasInventory)
                {
                    var inventory = container.GetInventory();
                    inventory.GetItems(items);

                    foreach (var item in items)
                    {
                        var itemName = item.Type.ToString();

                        var prefs = preferredContainers.Where(p => itemName.Contains($"_{p.Key}/")).FirstOrDefault().Value ?? new List<IMyCargoContainer>();

                        if (!prefs.Any(p => p == container))
                        {
                            foreach (var pref in SortedPreferred(prefs, item))
                            {
                                if (inventory.CanTransferItemTo(pref.GetInventory(), item.Type))
                                {
                                    if (inventory.TransferItemTo(pref.GetInventory(), item) || inventory.TransferItemTo(pref.GetInventory(), item, MyFixedPoint.SmallestPossibleValue))
                                    {
                                        break;
                                    }
                                }
                            }
                        }

                    }
                }
            }
        }

        private void CountItems(IEnumerable<IMyCargoContainer> containers, List<string> lines)
        {
            SortedDictionary<string, SortedDictionary<string, MyFixedPoint>> counts = new SortedDictionary<string, SortedDictionary<string, MyFixedPoint>>();

            foreach (var container in containers)
            {
                List<MyInventoryItem> items = new List<MyInventoryItem>();
                var inventory = container.GetInventory();
                inventory.GetItems(items);

                foreach (var item in items)
                {
                    var itemName = item.Type.ToString();

                    foreach (var category in categories)
                    {
                        if (itemName.Contains($"_{category}/"))
                        {
                            if (!counts.ContainsKey(category))
                            {
                                counts[category] = new SortedDictionary<string, MyFixedPoint>();
                            }

                            var subtype = item.Type.SubtypeId;

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

            if (categories.Sum(cat => counts[cat].Count()) > 12)
            {
                int secondsPerCategory = (int)(60 / categories.Count());
                var categoryToShow = categories[(int)Math.Floor((double)(DateTime.Now.Second / secondsPerCategory))];
                WriteCounter(lines, counts[categoryToShow], categoryToShow);
            }
            else
            {
                foreach (var cat in categories)
                {
                    WriteCounter(lines, counts[cat], cat);
                }
            }
        }

        private void WriteCounter(List<string> lines, SortedDictionary<string, MyFixedPoint> counterToShow, string categoryToShow)
        {
            lines.Add($"Current {categoryToShow} Inventory");

            foreach (var itemName in counterToShow.Keys)
            {
                var label = itemName.Split('/').Last();
                var amount = ToSI(counterToShow[itemName]);
                lines.Add($"{label}: {amount}");
            }
        }

        private void Show(List<string> lines, bool debug = false)
        {
            List<IMyTerminalBlock> blocks = new List<IMyTerminalBlock>();

            var matches = GetFirstCaptures(showRegex, $"{Me.DisplayNameText} {Me.CustomData}");

            foreach (var search in matches)
            {
                GridTerminalSystem.GetBlocksOfType(blocks, block => block.IsSameConstructAs(Me) && block is IMyTextSurfaceProvider && block.DisplayNameText.Contains(search));
            }

            if (!debug)
            {
                blocks.Add(Me);
            }

            WriteText(lines, blocks, !debug);
        }

        private void Debug(List<string> lines)
        {
            WriteText(lines, new IMyTerminalBlock[] { Me }, true); 
        }

        private void WriteText(List<string> lines, IEnumerable<IMyTerminalBlock> blocks, bool debug = false)
        {
            float fontSize = Math.Min(1.2f, 17.0f / lines.Count());
            string text = String.Join("\n", lines);

            foreach (var block in blocks)
            {
                (block as IMyTextSurfaceProvider).GetSurface(0).FontSize = fontSize;
                (block as IMyTextSurfaceProvider).GetSurface(0).WriteText(text);
            }

            if (debug)
            {
                Echo($"{text}");
            }
        }

        private MyFixedPoint FreeVolume(IMyCargoContainer cargo)
        {
            return cargo.GetInventory().MaxVolume - cargo.GetInventory().CurrentVolume;
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

        private IEnumerable<string> GetFirstCaptures(System.Text.RegularExpressions.Regex regex, string text)
        {
            var captures = new List<string>();

            var matches = regex.Matches(text);

            for (var i =0; i < matches.Count; i++)
            {
                var match = matches[i];
                if (match.Success)
                {
                    captures.Add(match.Result("$1"));
                }
            }

            return captures;
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

        // COPY AND PASTE END ON THIS LINE
    }
}
