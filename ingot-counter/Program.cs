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
            SortedDictionary<string, MyFixedPoint> ingotCounts = new SortedDictionary<string, MyFixedPoint>();

            List<string> lines = new List<string>();

            lines.Add("Current Inventory");

            GridTerminalSystem.GetBlocksOfType(blocks, block => block.IsSameConstructAs(Me) && block is IMyCargoContainer);

            foreach (var block in blocks)
            {
                List<MyInventoryItem> items = new List<MyInventoryItem>();
                IMyInventory inventory = block.GetInventory();
                inventory.GetItems(items);

                foreach (MyInventoryItem item in items)
                {
                    string itemName = item.Type.ToString();

                    if (itemName.Contains("Ingot"))
                    {
                        string metal = (string)item.Type.SubtypeId;

                        if (ingotCounts.ContainsKey(metal))
                        {

                            ingotCounts[metal] += item.Amount;
                        }
                        else
                        {
                            ingotCounts[metal] = item.Amount;
                        }
                    }

                }
            }

            foreach (var itemName in ingotCounts.Keys)
            {
                var parts = itemName.Split('/');

                if (parts.Length > 0)
                {
                    var amount = ToSI(ingotCounts[itemName]);
                    lines.Add($"{parts[parts.Length - 1]}: {amount}");
                }
            }

            string text = String.Join("\n", lines.ToArray());
            Show(text);
        }

        private void Show(string text)
        {
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
                (block as IMyTextSurfaceProvider).GetSurface(0).WriteText(text);
            }

            Echo(text);
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
