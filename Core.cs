using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using ExileCore;
using ExileCore.Shared;
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

        private const string FILE_CRAFT_CLASSIFICATION = "CraftClassification.txt";
        private const string FILE_CRAFT_TEMPLATE = "CraftListTemplate.txt";
        private const string FILE_CRAFT_EXPORT = "ExportedCrafts.txt";
        private const string COROUTINE_NAME = "EXTRACT_HARVEST_CRAFTS";

        private string PATH_FILE_CRAFT_CLASSIFICATION =>
            DirectoryFullName + Path.DirectorySeparatorChar + FILE_CRAFT_CLASSIFICATION;

        private string PATH_FILE_CRAFT_TEMPLATE =>
            DirectoryFullName + Path.DirectorySeparatorChar + FILE_CRAFT_TEMPLATE;

        private string PATH_FILE_CRAFT_EXPORT => DirectoryFullName + Path.DirectorySeparatorChar + FILE_CRAFT_EXPORT;

        private Coroutine CoroutineWorker;
        private DateTime lastExtractTime;
        private readonly Stopwatch DebugTimer = new Stopwatch();
        private Random rng = new Random();
        private IDictionary<string, string> CraftsToTags;
        private IDictionary<string, string> TagsToCrafts;

        public bool AreUITabsOpen => GameController.Game.IngameState.IngameUi.InventoryPanel.IsVisible &&
                                     GameController.Game.IngameState.IngameUi.StashElement.IsVisibleLocal;

        public override bool Initialise()
        {
            lastExtractTime = DateTime.Now;
            EnsureFileCoherence();
            LoadCraftTags();
            LogMessage($"Loaded {(CraftsToTags != null ? CraftsToTags.Count.ToString() : "0")} tagged crafts.");
            Input.RegisterKey(Settings.ExtractHarvestsKey);
            return base.Initialise();
        }

        private void EnsureFileCoherence()
        {
            if (!File.Exists(PATH_FILE_CRAFT_CLASSIFICATION))
                File.WriteAllText(PATH_FILE_CRAFT_CLASSIFICATION, Helpers.FileContent.CraftClassification);
            if (!File.Exists(PATH_FILE_CRAFT_TEMPLATE))
                File.WriteAllText(PATH_FILE_CRAFT_TEMPLATE, Helpers.FileContent.CraftListTemplate);
        }

        private void LoadCraftTags()
        {
            const string CraftWithTagPattern = @".{3,}|.{16,}";

            if (!File.Exists(PATH_FILE_CRAFT_CLASSIFICATION))
            {
                CraftsToTags = null;
                TagsToCrafts = null;
            }

            CraftsToTags = new Dictionary<string, string>();
            TagsToCrafts = new Dictionary<string, string>();
            try
            {
                using (StreamReader sr = new StreamReader(PATH_FILE_CRAFT_CLASSIFICATION))
                {
                    string line = null;
                    while ((line = sr.ReadLine()) != null)
                    {
                        if (line.Length == 0 || line.StartsWith("#") || line.StartsWith("//"))
                            continue;
                        if (Regex.IsMatch(line, CraftWithTagPattern, RegexOptions.IgnoreCase))
                        {
                            var split = line.Split('|');
                            var tag = split[0];
                            var craft = split[1];
                            if (CraftsToTags.ContainsKey(craft))
                                LogError($"Duplicate craft in classification file: {craft}", 5F);
                            else
                                CraftsToTags.Add(craft, tag);
                            if (TagsToCrafts.ContainsKey(tag))
                                LogError($"Duplicate tag in classification file: {tag}", 5F);
                            else
                                TagsToCrafts.Add(tag, craft);
                        }
                    }
                }
            }
            catch (FileNotFoundException ex)
            {
                LogError(
                    $"Failed to read craft tag list file at {PATH_FILE_CRAFT_CLASSIFICATION}{Environment.NewLine}{ex.Message}",
                    3F);
                CraftsToTags = null;
                TagsToCrafts = null;
            }
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
            if (craftingBenches.Length == 0)
            {
                LogMessage("No crafting benches in current stash tab.");
                FinishExtractHarvestCoroutine();
                yield break;
            }

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
            yield return Input.SetCursorPositionSmooth(new Vector2(cursorPosPreMoving.X, cursorPosPreMoving.Y));
            Input.MouseMove();
            LogMessage($"Copied descriptions of {textList.Count} benches - now analyzing crafts.", 5F);
            var craftList = ExtractCrafts(textList);
            LogMessage($"Extracted {craftList.Count} diffrent crafts from benches - now exporting.", 5F);
            ExportCrafts(craftList);

            FinishExtractHarvestCoroutine();
        }

        private void ExportCrafts(IDictionary<string, int> craftList)
        {
            if (!File.Exists(PATH_FILE_CRAFT_CLASSIFICATION) || !File.Exists(PATH_FILE_CRAFT_TEMPLATE))
            {
                LogMessage($"Exists: {File.Exists(PATH_FILE_CRAFT_CLASSIFICATION)}, {PATH_FILE_CRAFT_CLASSIFICATION}",
                    6F);
                LogMessage($"Exists: {File.Exists(PATH_FILE_CRAFT_TEMPLATE)}, {PATH_FILE_CRAFT_TEMPLATE}", 6F);
                ExportCraftsToClipboard(craftList);
                return;
            }

            var template = LoadTemplate();
            if (string.IsNullOrWhiteSpace(template))
            {
                LogError($"Failed to read template file at {PATH_FILE_CRAFT_TEMPLATE}");
                return;
            }

            var classifiedCrafts = ClassifyCrafts(craftList);
            if (classifiedCrafts == null || classifiedCrafts.Count == 0)
            {
                LogError("Failed to export classified crafts to file.", 3F);
                return;
            }

            var listToExport = CreateExportList(template, classifiedCrafts);

            File.WriteAllText(PATH_FILE_CRAFT_EXPORT, listToExport);

            LogMessage($"Exported craft list to: {PATH_FILE_CRAFT_EXPORT}", 3F);
        }

        private string CreateExportList(string template, IDictionary<string, int> classifiedCrafts)
        {
            var sb = new StringBuilder();
            var sr = new StringReader(template);
            var tagPattern = @"\$[\w\x5F\x2D]+";
            string line = null;
            var unknownTags = new HashSet<string>();
            var replacedTags = new HashSet<string>();
            while ((line = sr.ReadLine()) != null)
            {
                var match = Regex.Match(line, tagPattern);
                if (!match.Success)
                {
                    sb.AppendLine(line);
                    continue;
                }

                var tagToReplace = line.Substring(match.Index + 1, match.Length - 1);
                var replaced = false;
                if (!TagsToCrafts.ContainsKey(tagToReplace))
                    unknownTags.Add(tagToReplace);
                else if (classifiedCrafts.TryGetValue(tagToReplace, out var replacement))
                {
                    line =
                        $"{line.Substring(0, match.Index)}{replacement.ToString().PadRight(2)}{line.Substring(match.Index + match.Length, line.Length - match.Index - match.Length)}";
                    replacedTags.Add(tagToReplace);
                    replaced = true;
                }

                if (replaced || !Settings.RemoveNonReplacedLines.Value)
                    sb.AppendLine(line);
            }

            if (unknownTags.Count > 0)
                LogError(
                    $"Found following unknown tags in template {PATH_FILE_CRAFT_TEMPLATE}. {Environment.NewLine}Add them in classification file {PATH_FILE_CRAFT_CLASSIFICATION}.{Environment.NewLine}{string.Join(Environment.NewLine, unknownTags)}",
                    10F);

            if (Settings.ListUnusedCrafts.Value)
            {
                ISet<string> missingInTemplateTags =
                    classifiedCrafts.Keys.Except(replacedTags).OrderBy(k => k).Distinct().ToHashSet();
                if (missingInTemplateTags.Count > 0)
                    LogMessage(
                        $"{Environment.NewLine}The following crafts you have are not used in the template. Consider adding them: {Environment.NewLine}{string.Join(Environment.NewLine, missingInTemplateTags)}{Environment.NewLine}",
                        10F);
            }

            return sb.ToString().Trim();
        }

        private string LoadTemplate()
        {
            if (!File.Exists(PATH_FILE_CRAFT_TEMPLATE))
                return null;

            try
            {
                return File.ReadAllText(PATH_FILE_CRAFT_TEMPLATE);
            }
            catch (FileNotFoundException ex)
            {
                LogError(
                    $"Failed to read craft list template at {PATH_FILE_CRAFT_TEMPLATE}{Environment.NewLine}{ex.Message}",
                    3F);
                return null;
            }
        }

        private IDictionary<string, int> ClassifyCrafts(IDictionary<string, int> craftList)
        {
            if (CraftsToTags == null || CraftsToTags.Count == 0)
                return null;

            var result = new Dictionary<string, int>();
            var failedToClassify = new List<string>();
            foreach (var craft in craftList)
            {
                if (CraftsToTags.TryGetValue(craft.Key, out var tag))
                    result.Add(tag, craft.Value);
                else
                    failedToClassify.Add(craft.Key);
            }

            if (failedToClassify.Count > 0)
                LogError(
                    $"Failed to classify some crafts. Add them with a tag to {PATH_FILE_CRAFT_CLASSIFICATION}.{Environment.NewLine}{string.Join(Environment.NewLine, failedToClassify)}",
                    10F);
            return result;
        }

        private void ExportCraftsToClipboard(IDictionary<string, int> craftList)
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
            bool ShouldIgnoreCraft(int ilvl) => ilvl >= Settings.MinCraftLevel.Value;

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
                    if (string.IsNullOrWhiteSpace(ilvl) && ShouldIgnoreCraft(int.Parse(ilvl)))
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