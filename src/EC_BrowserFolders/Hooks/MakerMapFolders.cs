﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using BepInEx.Harmony;
using Pose;
using HarmonyLib;
using KKAPI.Maker;
using Manager;
using UnityEngine;
using UnityEngine.UI;
using BepInEx.Logging;

namespace BrowserFolders.Hooks.EC
{
    [BrowserType(BrowserType.MakerMap)]
    public class MakerMapFolders : IFolderBrowser
    {


        private static Map.MapLoadScene _mapLoadScene;



        private static FolderTreeView _folderTreeView;




        private static string _currentRelativeFolder;
        private static bool _refreshList;
        private static string _targetScene;

        public MakerMapFolders()
        {
            _folderTreeView = new FolderTreeView(Utils.NormalizePath(UserData.Path), Utils.NormalizePath(UserData.Path));
            _folderTreeView.CurrentFolderChanged = OnFolderChanged;

            HarmonyWrapper.PatchAll(typeof(MakerMapFolders));

        }

        private static string DirectoryPathModifier(string currentDirectoryPath)
        {
            return _folderTreeView != null ? _folderTreeView.CurrentFolder : currentDirectoryPath;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Map.MapLoadScene), "Awake")]
        internal static void InitHook(Map.MapLoadScene __instance)

        {

            _folderTreeView.DefaultPath = Path.Combine(Utils.NormalizePath(UserData.Path), "map/data");
            _folderTreeView.CurrentFolder = _folderTreeView.DefaultPath;

            _mapLoadScene = __instance;





            _targetScene = Scene.Instance.AddSceneName;
        }






        [HarmonyTranspiler]
            [HarmonyPatch(typeof(Map.MapLoadScene), "CreateList")]
            internal static IEnumerable<CodeInstruction> InitializePatch(IEnumerable<CodeInstruction> instructions)
            {


                foreach (var instruction in instructions)
                {
                    if (string.Equals(instruction.operand as string, "map/data", StringComparison.OrdinalIgnoreCase))

                    {
                        //0x7E	ldsfld <field>	Push the value of the static field on the stack.
                        instruction.opcode = OpCodes.Ldsfld;
                        instruction.operand = typeof(MakerMapFolders).GetField(nameof(_currentRelativeFolder), BindingFlags.NonPublic | BindingFlags.Static) ??
                                      throw new MissingMethodException("could not find GetCurrentRelativeFolder"); ;
                    }


                    yield return instruction;
                }
            }



        public void OnGui()
        {
            // Check the opened category
            if (_mapLoadScene != null && _targetScene == Scene.Instance.AddSceneName)
            {

                if (_refreshList)
                {
                    OnFolderChanged();
                    _refreshList = false;
                }

                var screenRect = new Rect((int)(Screen.width * 0.004), (int)(Screen.height * 0.55f), (int)(Screen.width * 0.125), (int)(Screen.height * 0.35));
                Utils.DrawSolidWindowBackground(screenRect);
                GUILayout.Window(362, screenRect, TreeWindow, "Select map folder");

            }

        }

        internal static Rect GetFullscreenBrowserRect()
        {
            return new Rect((int)(Screen.width * 0.015), (int)(Screen.height * 0.35f), (int)(Screen.width * 0.16), (int)(Screen.height * 0.4));
        }



        private static void OnFolderChanged()
        {
            _currentRelativeFolder = _folderTreeView.CurrentRelativeFolder;

            if (_mapLoadScene == null) return;




            var ccf = Traverse.Create(_mapLoadScene);


            ccf.Method("CreateList").GetValue();
            ccf.Method("RecreateScrollerList").GetValue();







            // private bool Initialize()



            // Fix add info toggle breaking




            // Fix add info toggle breaking


        }

        private static void TreeWindow(int id)
        {
            GUILayout.BeginVertical();
            {
                _folderTreeView.DrawDirectoryTree();
                Debug.Log(_folderTreeView);
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
