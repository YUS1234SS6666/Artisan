﻿using Artisan.RawInformation;
using Dalamud.Interface.Components;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using static Artisan.CraftingLogic.CurrentCraft;

namespace Artisan.MacroSystem
{
    internal class MacroUI
    {
        private static string _newMacroName = string.Empty;
        private static bool _keyboardFocus;
        private const string MacroNamePopupLabel = "Macro Name";
        private static Macro selectedMacro = new();
        private static int selectedActionIndex = -1;

        internal static void Draw()
        {
            ImGui.TextWrapped("此选项卡将允许您添加Artisan可以使用的宏，而不是它自己决定。");
            ImGui.Separator();
            ImGui.Spacing();
            if (ImGui.Button("从剪贴板导入宏"))
                OpenMacroNamePopup(MacroNameUse.FromClipboard);

            if (ImGui.Button("新建宏"))
                OpenMacroNamePopup(MacroNameUse.NewMacro);

            DrawMacroNamePopup(MacroNameUse.FromClipboard);
            DrawMacroNamePopup(MacroNameUse.NewMacro);

            if (Service.Configuration.UserMacros.Count > 0)
            {
                ImGui.BeginGroup();
                float longestName = 0;
                foreach (var macro in Service.Configuration.UserMacros)
                {
                    if (ImGui.CalcTextSize($"{macro.Name} (CP: {GetCPCost(macro)})").Length() > longestName)
                        longestName = ImGui.CalcTextSize($"{macro.Name} (CP: {GetCPCost(macro)})").Length();
                }

                longestName = Math.Max(150, longestName);
                ImGui.Text("宏列表");
                if (ImGui.BeginChild("##selector", new Vector2(longestName + 40, 0), true))
                {
                    foreach (Macro m in Service.Configuration.UserMacros)
                    {
                        uint cpCost = GetCPCost(m);
                        var selected = ImGui.Selectable($"{m.Name} (CP: {cpCost})###{m.ID}", m.ID == selectedMacro.ID);

                        if (selected)
                        {
                            selectedMacro = m;
                        }
                    }
                    ImGui.EndChild();
                }
                if (selectedMacro.ID != 0)
                {
                    ImGui.SameLine();
                    ImGui.BeginChild("###selectedMacro", new Vector2(0, 0), false);
                    ImGui.Text($"选择的宏: {selectedMacro.Name}");
                    if (ImGui.Button("删除宏（按住Ctrl）") && ImGui.GetIO().KeyCtrl)
                    {
                        Service.Configuration.UserMacros.Remove(selectedMacro);
                        if (Service.Configuration.SetMacro?.ID == selectedMacro.ID)
                            Service.Configuration.SetMacro = null;

                        Service.Configuration.Save();
                        selectedMacro = new();
                        selectedActionIndex = -1;
                    }
                    ImGui.Spacing();
                    ImGui.SameLine();
                    bool skipQuality = selectedMacro.MacroOptions.SkipQualityIfMet;
                    if (ImGui.Checkbox("如果达到100%，则跳过品质技能", ref skipQuality))
                    {
                        selectedMacro.MacroOptions.SkipQualityIfMet = skipQuality;
                        if (Service.Configuration.SetMacro?.ID == selectedMacro.ID)
                            Service.Configuration.SetMacro = selectedMacro;
                        Service.Configuration.Save();
                    }
                    ImGuiComponents.HelpMarker("一旦达到100%品质，宏将跳过所有与品质相关的操作，包括增益。");
                    ImGui.SameLine();
                    bool upgradeActions = selectedMacro.MacroOptions.UpgradeActions;
                    if (ImGui.Checkbox("升级技能", ref upgradeActions))
                    {
                        selectedMacro.MacroOptions.UpgradeActions = upgradeActions;
                        if (Service.Configuration.SetMacro?.ID == selectedMacro.ID)
                            Service.Configuration.SetMacro = selectedMacro;
                        Service.Configuration.Save();
                    }
                    ImGuiComponents.HelpMarker("如果您获得了高品质或最高品质的状态，并且您的宏处于提高品质或进展的步骤（不包括比尔格的祝福），那么它将根据原始操作增加的内容将技能升级为集中加工或集中制作。");

                    ImGui.Columns(2, "actionColumns", false);
                    if (ImGui.Button("插入新技能"))
                    {
                        if (selectedMacro.MacroActions.Count == 0)
                            selectedMacro.MacroActions.Add(Skills.BasicSynth);
                        else
                            selectedMacro.MacroActions.Insert(selectedActionIndex + 1, Skills.BasicSynth);

                        Service.Configuration.Save();
                    }
                    ImGui.TextWrapped("宏步骤");
                    ImGui.Indent();
                    for (int i = 0; i < selectedMacro.MacroActions.Count(); i++)
                    {
                        var selectedAction = ImGui.Selectable($"{i+1}. {GetActionName(selectedMacro.MacroActions[i])}###selectedAction{i}", i == selectedActionIndex);

                        if (selectedAction)
                            selectedActionIndex = i;
                    }
                    ImGui.Unindent();
                    if (selectedActionIndex != -1)
                    {
                        if (selectedActionIndex >= selectedMacro.MacroActions.Count)
                            return;

                        ImGui.NextColumn();
                        ImGui.Text($"选择技能: {GetActionName(selectedMacro.MacroActions[selectedActionIndex])}");
                        if (selectedActionIndex > 0)
                        {
                            ImGui.SameLine();
                            if (ImGuiComponents.IconButton(Dalamud.Interface.FontAwesomeIcon.ArrowLeft))
                            {
                                selectedActionIndex--;
                            }
                        }

                        if (selectedActionIndex < selectedMacro.MacroActions.Count - 1)
                        {
                            ImGui.SameLine();
                            if (ImGuiComponents.IconButton(Dalamud.Interface.FontAwesomeIcon.ArrowRight))
                            {
                                selectedActionIndex++;
                            }
                        }


                        if (ImGui.Button("删除技能（按住Ctrl）") && ImGui.GetIO().KeyCtrl)
                        {
                            selectedMacro.MacroActions.RemoveAt(selectedActionIndex);
                            Service.Configuration.Save();

                            if (selectedActionIndex == selectedMacro.MacroActions.Count)
                                selectedActionIndex--;
                        }

                        if (ImGui.BeginCombo("###ReplaceAction", "替换技能"))
                        {
                            foreach(var constant in typeof(Skills).GetFields().OrderBy(x => GetActionName((uint)x.GetValue(null))))
                            {
                                if (ImGui.Selectable($"{GetActionName((uint)constant.GetValue(null))}"))
                                {
                                    selectedMacro.MacroActions[selectedActionIndex] = (uint)constant.GetValue(null);
                                    if (Service.Configuration.SetMacro?.ID == selectedMacro.ID)
                                        Service.Configuration.SetMacro = selectedMacro;

                                    Service.Configuration.Save();
                                }
                            }

                            ImGui.EndCombo();
                        }

                        ImGui.Text("技能排序");
                        if (selectedActionIndex > 0)
                        {
                            ImGui.SameLine();
                            if (ImGuiComponents.IconButton(Dalamud.Interface.FontAwesomeIcon.ArrowUp))
                            {
                                selectedMacro.MacroActions.Reverse(selectedActionIndex -1, 2);
                                selectedActionIndex--;
                                if (Service.Configuration.SetMacro?.ID == selectedMacro.ID)
                                    Service.Configuration.SetMacro = selectedMacro;

                                Service.Configuration.Save();
                            }
                        }

                        if (selectedActionIndex < selectedMacro.MacroActions.Count - 1)
                        {
                            ImGui.SameLine();
                            if (selectedActionIndex == 0)
                            {
                                ImGui.Dummy(new Vector2(22));
                                ImGui.SameLine();
                            }

                            if (ImGuiComponents.IconButton(Dalamud.Interface.FontAwesomeIcon.ArrowDown))
                            {
                                selectedMacro.MacroActions.Reverse(selectedActionIndex, 2);
                                selectedActionIndex++;
                                if (Service.Configuration.SetMacro?.ID == selectedMacro.ID)
                                    Service.Configuration.SetMacro = selectedMacro;

                                Service.Configuration.Save();
                            }
                        }

                    }
                    ImGui.Columns(1);
                    ImGui.EndChild();
                }
                else
                {
                    selectedActionIndex = -1;
                }

                ImGui.EndGroup();
            }
            else
            {
                selectedMacro = new();
                selectedActionIndex = -1;
            }
        }

        private static uint GetCPCost(Macro m)
        {
            uint output = 0;
            foreach (var act in m.MacroActions)
            {
                if (act >= 100000)
                {
                    output += LuminaSheets.CraftActions[act].Cost;
                }
                else
                {
                    output += LuminaSheets.ActionSheet[act].PrimaryCostValue;
                }
            }

            return output;
        }

        private static string GetActionName(uint action)
        {
            if (LuminaSheets.CraftActions.TryGetValue(action, out var act1))
            {
                return act1.Name.RawString;
            }
            else
            {
                LuminaSheets.ActionSheet.TryGetValue(action, out var act2);
                return act2.Name.RawString;
            }
        }

        private static void DrawMacroNamePopup(MacroNameUse use)
        {
            if (ImGui.BeginPopup($"{MacroNamePopupLabel}{use}"))
            {
                if (_keyboardFocus)
                {
                    ImGui.SetKeyboardFocusHere();
                    _keyboardFocus = false;
                }

                if (ImGui.InputText("宏名称##macroName", ref _newMacroName, 64, ImGuiInputTextFlags.EnterReturnsTrue)
                 && _newMacroName.Any())
                {
                    switch (use)
                    {
                        case MacroNameUse.NewMacro:
                            Macro newMacro = new();
                            newMacro.Name = _newMacroName;
                            newMacro.SetID();
                            newMacro.Save(true);
                            break;
                        case MacroNameUse.FromClipboard:
                            try
                            {
                                var text = ImGui.GetClipboardText();
                                ParseMacro(text, out Macro macro);
                                if (macro.ID != 0)
                                    if (macro.Save())
                                    {
                                        Service.ChatGui.Print($"{macro.Name} 已保存。");
                                    }
                                    else
                                    {
                                        Service.ChatGui.PrintError("无法保存宏。请检查您的剪贴板是否包含带有工匠技能的宏。");
                                    }
                                else
                                    Service.ChatGui.PrintError("无法解析剪贴板。请检查您的剪贴板是否包含带有工匠技能的宏。");
                            }
                            catch (Exception e)
                            {
                                Dalamud.Logging.PluginLog.Information($"Could not save new Macro from Clipboard:\n{e}");
                            }

                            break;
                    }

                    _newMacroName = string.Empty;
                    ImGui.CloseCurrentPopup();
                }

                ImGui.EndPopup();
            }
        }

        private static void ParseMacro(string text, out Macro macro)
        {
            macro = new();
            macro.Name = _newMacroName;
            using (System.IO.StringReader reader = new System.IO.StringReader(text))
            {
                string line = "";
                while ((line = reader.ReadLine()) != null)
                {
                    var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length < 2) continue;

                    if (parts[0].Equals("/ac", StringComparison.CurrentCultureIgnoreCase) || parts[0].Equals("/action", StringComparison.CurrentCultureIgnoreCase))
                    {
                        var builder = new StringBuilder();
                        for (int i = 1; i < parts.Length; i++)
                        {
                            if (parts[i].Contains("<")) continue;
                            builder.Append(parts[i]);
                            builder.Append(" ");
                        }
                        var action = builder.ToString().Trim();
                        action = action.Replace("\"", "");
                        if (string.IsNullOrEmpty(action)) continue;

                        if (LuminaSheets.CraftActions.Values.Any(x => x.Name.RawString.Equals(action, StringComparison.CurrentCultureIgnoreCase) && x.ClassJobCategory.Value.RowId != 0))
                        {
                            var act = LuminaSheets.CraftActions.Values.FirstOrDefault(x => x.Name.RawString.Equals(action, StringComparison.CurrentCultureIgnoreCase) && x.ClassJobCategory.Value.RowId != 0);
                            if (act == null)
                            {
                                Service.ChatGui.PrintError($"无法解析技能: {action}");
                            }
                            macro.MacroActions.Add(act.RowId);
                            continue;

                        }
                        else
                        {
                            var act = LuminaSheets.ActionSheet.Values.FirstOrDefault(x => x.Name.RawString.Equals(action, StringComparison.CurrentCultureIgnoreCase) && x.ClassJobCategory.Value.RowId != 0);
                            if (act == null)
                            {
                                Service.ChatGui.PrintError($"无法解析技能: {action}");
                            }
                            macro.MacroActions.Add(act.RowId);
                            continue;

                        }
                    }
                }
            }
            if (macro.MacroActions.Count > 0)
                macro.SetID();
        }

        private static void OpenMacroNamePopup(MacroNameUse use)
        {
            _newMacroName = string.Empty;
            _keyboardFocus = true;
            ImGui.OpenPopup($"{MacroNamePopupLabel}{use}");
        }

        internal enum MacroNameUse
        {
            SaveCurrent,
            NewMacro,
            DuplicateMacro,
            FromClipboard,
        }
    }
}
