﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using ChaCustom;
using HarmonyLib;
using KKAPI.Maker;
using KKAPI.Utilities;
using Manager;
using UnityEngine;
using UnityEngine.UI;

namespace BrowserFolders.Hooks
{
    public class MakerCharaFolders : IFolderBrowser
    {
        private static Toggle _catToggle;
        private static CustomCharaFile _customCharaFile;
        private static FolderTreeView _folderTreeView;
        private static Toggle _loadCharaToggle;
        private static Toggle _saveCharaToggle;
        private static GameObject _saveFront;

        private static string _currentRelativeFolder;
        private static bool _refreshList;
        private static string _targetScene;

        public MakerCharaFolders()
        {
            _folderTreeView =
                new FolderTreeView(Utils.NormalizePath(UserData.Path), Utils.NormalizePath(UserData.Path));
            _folderTreeView.CurrentFolderChanged = OnFolderChanged;

            Harmony.CreateAndPatchAll(typeof(MakerCharaFolders));
            MakerCardSave.RegisterNewCardSavePathModifier(DirectoryPathModifier, null);
        }

        public void OnGui()
        {
            // Check the opened category

            if (_catToggle != null && _catToggle.isOn && _targetScene == Scene.Instance.AddSceneName)
                // Check opened tab
                if (_loadCharaToggle != null && _loadCharaToggle.isOn ||
                    _saveCharaToggle != null && _saveCharaToggle.isOn)
                    // Check if the character picture take screen is displayed
                    if (_saveFront == null || !_saveFront.activeSelf)
                    {
                        if (_refreshList)
                        {
                            OnFolderChanged();
                            _refreshList = false;
                        }

                        var screenRect = new Rect((int)(Screen.width * 0.004), (int)(Screen.height * 0.57f),
                            (int)(Screen.width * 0.125), (int)(Screen.height * 0.35));
                        IMGUIUtils.DrawSolidBox(screenRect);
                        GUILayout.Window(362, screenRect, TreeWindow, "Select character folder");
                        IMGUIUtils.EatInputInRect(screenRect);
                    }
        }

        private static string DirectoryPathModifier(string currentDirectoryPath)
        {
            return _folderTreeView != null ? _folderTreeView.CurrentFolder : currentDirectoryPath;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(CustomCharaFile), "Start")]
        internal static void InitHook(CustomCharaFile __instance)
        {
            Traverse.Create("ConvertChaFileScene").Method("Start").GetValue();
            var instance = CustomBase.Instance;
            _folderTreeView.DefaultPath = Path.Combine(Utils.NormalizePath(UserData.Path), instance.modeSex != 0 ? @"chara/female/" : "chara/male");
            _folderTreeView.CurrentFolder = _folderTreeView.DefaultPath;

            _customCharaFile = __instance;

            var gt = GameObject.Find("CustomScene/CustomRoot/FrontUIGroup/CustomUIGroup/CvsMenuTree/06_SystemTop");
            _loadCharaToggle = gt.transform.Find("tglLoadChara").GetComponent<Toggle>();
            _saveCharaToggle = gt.transform.Find("tglSaveChara").GetComponent<Toggle>();

            var mt = GameObject.Find("CustomScene/CustomRoot/FrontUIGroup/CustomUIGroup/CvsMainMenu/BaseTop/tglSystem");
            _catToggle = mt.GetComponent<Toggle>();

            _saveFront = GameObject.Find("CustomScene/CustomRoot/FrontUIGroup/CvsCaptureFront");

            _targetScene = Scene.Instance.AddSceneName;
        }

        [HarmonyTranspiler]
        [HarmonyPatch(typeof(CustomCharaFile), "Initialize")]
        internal static IEnumerable<CodeInstruction> InitializePatch(IEnumerable<CodeInstruction> instructions)
        {
            foreach (var instruction in instructions)
            {
                if (string.Equals(instruction.operand as string, "chara/female/", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(instruction.operand as string, "chara/male/", StringComparison.OrdinalIgnoreCase))
                {
                    //0x7E	ldsfld <field>	Push the value of the static field on the stack.
                    instruction.opcode = OpCodes.Ldsfld;
                    instruction.operand = typeof(MakerCharaFolders).GetField(nameof(_currentRelativeFolder),
                                              BindingFlags.NonPublic | BindingFlags.Static) ??
                                          throw new MissingMethodException("could not find GetCurrentRelativeFolder");
                }

                yield return instruction;
            }
        }

        private static void OnFolderChanged()
        {
            _currentRelativeFolder = _folderTreeView.CurrentRelativeFolder;

            if (_customCharaFile == null) return;

            var isLoad = _loadCharaToggle != null && _loadCharaToggle.isOn;
            var isSave = _saveCharaToggle != null && _saveCharaToggle.isOn;
            if (isLoad || isSave)
            {
                var ccfTrav = Traverse.Create(_customCharaFile);
                ccfTrav.Method("Initialize").GetValue();

                // Fix default cards being shown when refreshing in this way
                var lctrlTrav = ccfTrav.Field("listCtrl");
                if (isSave)
                {
                    var lst = lctrlTrav.Field("lstFileInfo").GetValue<List<CustomFileInfo>>();
                    foreach (var customFileInfo in lst.Where(x => x.category != 1)) customFileInfo.fic.Disvisible(true);
                }
                else
                {
                    lctrlTrav.Method("UpdateCategory").GetValue();
                }

                // Fix add info toggle breaking
                var tglField = lctrlTrav.Field("tglAddInfo");
                if (tglField.FieldExists())
                {
                    var tglInfo = tglField.GetValue<Toggle>();
                    tglInfo.onValueChanged.Invoke(tglInfo.isOn);
                }
            }
        }

        private static void TreeWindow(int id)
        {
            GUILayout.BeginVertical();
            {
                _folderTreeView.DrawDirectoryTree();

                GUILayout.BeginVertical(GUI.skin.box, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(false));
                {
                    if (GUILayout.Button("Refresh thumbnails"))
                        OnFolderChanged();

                    GUILayout.Space(1);

                    GUILayout.Label("Open in explorer...");
                    if (GUILayout.Button("Current folder"))
                        Utils.OpenDirInExplorer(_folderTreeView.CurrentFolder);
                    if (GUILayout.Button("Screenshot folder"))
                        Utils.OpenDirInExplorer(Path.Combine(Utils.NormalizePath(UserData.Path), "cap"));
                    if (GUILayout.Button("Main game folder"))
                        Utils.OpenDirInExplorer(Path.GetDirectoryName(Utils.NormalizePath(UserData.Path)));
                }
                GUILayout.EndVertical();
            }
            GUILayout.EndVertical();
        }
    }
}