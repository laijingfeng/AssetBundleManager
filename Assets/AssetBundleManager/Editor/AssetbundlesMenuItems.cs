﻿using UnityEditor;

namespace AssetBundles
{
    public class AssetBundlesMenuItems
    {
        const string kSimulationMode = "Tools/AssetBundles/Simulation Mode";

        [MenuItem(kSimulationMode)]
        public static void ToggleSimulationMode()
        {
            AssetBundleManager.SimulateAssetBundleInEditor = !AssetBundleManager.SimulateAssetBundleInEditor;
        }

        [MenuItem(kSimulationMode, true)]
        public static bool ToggleSimulationModeValidate()
        {
            Menu.SetChecked(kSimulationMode, AssetBundleManager.SimulateAssetBundleInEditor);
            return true;
        }

        [MenuItem("Tools/AssetBundles/Build AssetBundles")]
        static public void BuildAssetBundles()
        {
            BuildScript.BuildAssetBundles();
        }
    }
}