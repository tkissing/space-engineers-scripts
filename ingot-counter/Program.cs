using Microsoft.Build.Framework.XamlTypes;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ObjectBuilders.Definitions;
using VRageMath;

namespace IngameScript
{
    partial class Program : MyGridProgram
    {
        // This file contains your actual script.
        //
        // You can either keep all your code here, or you can create separate
        // code files to make your program easier to navigate while coding.
        //
        // Go to:
        // https://github.com/malware-dev/MDK-SE/wiki/Quick-Introduction-to-Space-Engineers-Ingame-Scripts
        //
        // to learn more about ingame scripts.

        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update10;
        }

        public void Main(string argument, UpdateType updateSource)
        {
            List<IMyCargoContainer> blocks = new List<IMyCargoContainer>();

            SortedDictionary<string, SortedDictionary<string, MyFixedPoint>> counts = new SortedDictionary<string, SortedDictionary<string, MyFixedPoint>>();

            string[] categories = new string[] { "Ingot", "Component" };

            SortedDictionary<string, MyFixedPoint> ingotCounts = new SortedDictionary<string, MyFixedPoint>();

            GridTerminalSystem.GetBlocksOfType(blocks, block => block.IsSameConstructAs(Me) && block is IMyCargoContainer);

            foreach (var block in blocks)
            {
                List<MyInventoryItem> items = new List<MyInventoryItem>();
                IMyInventory inventory = block.GetInventory();
                inventory.GetItems(items);

                foreach (MyInventoryItem item in items)
                {
                    string itemName = item.Type.ToString();

                    foreach (string category in categories)
                    {
                        if (itemName.Contains($"_{category}/"))
                        {
                            if (!counts.ContainsKey(category))
                            {
                                counts[category] = new SortedDictionary<string, MyFixedPoint>();
                            }

                            string subtype = (string)item.Type.SubtypeId;

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

            int secondsPerCategory = (int)(60 / categories.Count());

            string categoryToShow = categories[(int)Math.Floor((double)(DateTime.Now.Second / secondsPerCategory))];

            var counterToShow = counts[categoryToShow];

            List<string> lines = new List<string>();

            lines.Add($"Current {categoryToShow} Inventory");

            foreach (var itemName in counterToShow.Keys)
            {
                var parts = itemName.Split('/');

                if (parts.Length > 0)
                {
                    var amount = ToSI(counterToShow[itemName]);
                    lines.Add($"{parts[parts.Length - 1]}: {amount}");
                }
            }

            Show(lines);
        }

        private void Show(List<string> lines)
        {
            float fontSize = 17.0f / lines.Count();

            string debug = $"DEBUG {lines.Count} {fontSize}";

            string text = String.Join("\n", lines.ToArray());

            var match = (new System.Text.RegularExpressions.Regex(" SHOW:([^ ]+)")).Match(Me.DisplayNameText);

            List<IMyTerminalBlock> blocks = new List<IMyTerminalBlock>();
            if (match.Success)
            {
                var search = match.Result("$1");
                GridTerminalSystem.GetBlocksOfType(blocks, block => block.IsSameConstructAs(Me) && block is IMyTextSurfaceProvider && block.DisplayNameText.Contains(search));
            }

            blocks.Add(Me);

            foreach (var block in blocks)
            {
                (block as IMyTextSurfaceProvider).GetSurface(0).FontSize = fontSize;
                (block as IMyTextSurfaceProvider).GetSurface(0).WriteText(text);
            }

            Echo($"{text}\n{debug}");
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
    }
}
