using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using ExileCore;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.Elements;
using ExileCore.PoEMemory.Elements.InventoryElements;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared;
using ExileCore.Shared.Enums;
using ExileCore.Shared.Helpers;
using HarvestCraftExtractor;
using ImGuiNET;
using SharpDX;

namespace HarvestCraftExtractor
{
    public class Core : BaseSettingsPlugin<Settings>
    {
        private const string CRAFT_CLIPBOARD_EXPORT_FORMAT = "[@count] @name";
        private const string CRAFT_LEVEL_MATCH_GROUP_NAME = "ilvl";
        private const string CRAFT_MATCH_GROUP_NAME = "craft";

        private const string CRAFT_PATTERN = @"(?<" + CRAFT_MATCH_GROUP_NAME +
                                             @">([\w\x20\x25\x27\x2B\x2C\x2D\x2E])+(\w+\x2E?\x25?){1}) \((?<" +
                                             CRAFT_LEVEL_MATCH_GROUP_NAME + @">\d{1,3})\) \(crafted\)";

        private const string COROUTINE_NAME = "EXTRACT_HARVEST_CRAFTS";
        private Coroutine CoroutineWorker;
        private DateTime lastExtractTime;
        private readonly Stopwatch DebugTimer = new Stopwatch();
        private Random rng = new Random();

        public bool AreUITabsOpen => GameController.Game.IngameState.IngameUi.InventoryPanel.IsVisible &&
                                     GameController.Game.IngameState.IngameUi.StashElement.IsVisibleLocal;

        public override bool Initialise()
        {
            lastExtractTime = DateTime.Now;
            Input.RegisterKey(Settings.ExtractHarvestsKey);
            return base.Initialise();
        }

        public override void Render()
        {
            base.Render();

            if (CoroutineWorker != null)
            {
                if (CoroutineWorker.IsDone)
                {
                    FinishExtractHarvestCoroutine();
                    return;
                }

                if (!AreUITabsOpen)
                {
                    LogError("Stopping coroutine - inventory closed while checking crafts", 5F);
                    FinishExtractHarvestCoroutine();
                    return;
                }

                if (CoroutineWorker.Running && DebugTimer.ElapsedMilliseconds > 120000)
                {
                    LogError(
                        $"Stopped because worked for more than 2 minutes",
                        5F);
                    FinishExtractHarvestCoroutine();
                    return;
                }

                if (Input.IsKeyDown(Settings.ExtractHarvestsKey.Value) &&
                    (DateTime.Now - lastExtractTime).TotalMilliseconds > 500)
                {
                    LogMessage($"[{nameof(HarvestCraftExtractor)}] Stopping coroutine - user cancel.", 5F);
                    lastExtractTime = DateTime.Now;
                    FinishExtractHarvestCoroutine();
                    return;
                }
            }
            else
            {
                if (Input.IsKeyDown(Settings.ExtractHarvestsKey.Value) &&
                    (DateTime.Now - lastExtractTime).TotalMilliseconds > 500 && AreUITabsOpen)
                {
                    lastExtractTime = DateTime.Now;
                    CoroutineWorker = new Coroutine(ExtractHarvestsCoroutine(), this, COROUTINE_NAME);
                    ExileCore.Core.ParallelRunner.Run(CoroutineWorker);
                }
            }
        }

        private IEnumerator ExtractHarvestsCoroutine()
        {
            DebugTimer.Restart();

            var visibleStash = GameController.IngameState.IngameUi.StashElement.VisibleStash;
            if (visibleStash == null || visibleStash.Address == 0x0)
            {
                LogError("Stopping coroutine - visible stash is null.", 3F);
                FinishExtractHarvestCoroutine();
                yield break;
            }

            var cursorPosPreMoving = Input.ForceMousePosition;
            var stashItems = visibleStash.VisibleInventoryItems.OrderBy(i => i.InventPosX).ThenBy(i => i.InventPosY);
            var craftingBenches = stashItems.Where(i =>
                i.Item != null && i.Item.Path.Equals("Metadata/Items/Harvest/HarvestCraftingBench",
                    StringComparison.Ordinal)).ToArray();
            Input.KeyDown(Keys.LControlKey);
            yield return new WaitTime(20);
            var textList = new List<string>();
            foreach (var bench in craftingBenches)
            {
                var pos = bench.GetClientRect().Center;
                yield return Input.SetCursorPositionSmooth(pos + Vector2.One * rng.NextFloat(-6F, 6F));
                yield return new WaitTime(30);
                yield return Input.KeyPress(Keys.C);
                yield return new WaitTime(30);
                yield return new WaitTime(20 + rng.Next(0, 20));
                var text = ImGui.GetClipboardText();
                textList.Add(text);
            }

            yield return new WaitTime(20);
            Input.KeyUp(Keys.LControlKey);
            LogMessage($"Copied descriptions of {textList.Count} benches - now analyzing crafts.", 5F);
            var craftList = ExtractCrafts(textList);
            LogMessage($"Extracted {craftList.Count} diffrent crafts from benches.", 5F);
            ExportToClipboard(craftList);
            yield return Input.SetCursorPositionSmooth(new Vector2(cursorPosPreMoving.X, cursorPosPreMoving.Y));
            Input.MouseMove();

            FinishExtractHarvestCoroutine();
        }

        private void ExportToClipboard(IDictionary<string, int> craftList)
        {
            var result = new StringBuilder();
            var parameters = new Dictionary<string, object>();
            var orderedCraftList = craftList.OrderBy(kvp => kvp.Key);
            foreach (var pair in orderedCraftList)
            {
                if (pair.Value == 0 || string.IsNullOrWhiteSpace(pair.Key))
                    continue;
                parameters.Clear();
                parameters.Add("@count", pair.Value.ToString().PadLeft(2, ' '));
                parameters.Add("@name", pair.Key);
                var line = parameters.Aggregate(CRAFT_CLIPBOARD_EXPORT_FORMAT,
                    (a, b) => a.Replace(b.Key, b.Value.ToString()));
                result.AppendLine(line);
            }

            var resultString = result.ToString();
            ImGui.SetClipboardText(resultString);
            LogMessage("Exported results to clipboard", 5F);
        }

        private IDictionary<string, int> ExtractCrafts(ICollection<string> benchDescriptions)
        {
            bool ShouldIgnoreCraft(string craft, int ilvl) => ilvl >= Settings.MinCraftLevel.Value;

            var result = new Dictionary<string, int>();
            if (benchDescriptions.Count == 0)
                return result;
            var regex = new Regex(CRAFT_PATTERN);
            foreach (var benchDescription in benchDescriptions)
            {
                var matches = regex.Matches(benchDescription);
                foreach (Match match in matches)
                {
                    var craft = match.Groups[CRAFT_MATCH_GROUP_NAME].Value;
                    var ilvl = match.Groups[CRAFT_LEVEL_MATCH_GROUP_NAME].Value;
                    if (string.IsNullOrWhiteSpace(ilvl) && ShouldIgnoreCraft(craft, int.Parse(ilvl)))
                        continue;
                    if (string.IsNullOrWhiteSpace(craft) || craft.Length < 18)
                        LogError($"Possible wrong craft parsing. Screenshot and send to author. Craft: {craft}", 10F);

                    if (result.ContainsKey(craft))
                        result[craft] = result[craft] + 1;
                    else
                        result.Add(craft, 1);
                }
            }

            return result;
        }

        private void FinishExtractHarvestCoroutine()
        {
            CoroutineWorker = ExileCore.Core.ParallelRunner.FindByName(COROUTINE_NAME);
            CoroutineWorker?.Done();
            CoroutineWorker = null;
            DebugTimer.Restart();
            DebugTimer.Stop();
            Input.KeyUp(Keys.LControlKey);
        }
    }
}