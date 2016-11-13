﻿using UnityEngine;
using System.Collections;
using JAB;

public class LoadScenes : MonoBehaviour
{
    public string sceneAssetBundle;
    public string sceneName;

    // Use this for initialization
    IEnumerator Start()
    {
        Initialize();

        // Load level.
        yield return StartCoroutine(InitializeLevelAsync(sceneName, true));
    }

    // Initialize the downloading url and AssetBundleManifest object.
    protected void Initialize()
    {
        // Don't destroy this gameObject as we depend on it to run the loading script.
        DontDestroyOnLoad(gameObject);

        // With this code, when in-editor or using a development builds: Always use the AssetBundle Server
        // (This is very dependent on the production workflow of the project. 
        // 	Another approach would be to make this configurable in the standalone player.)
#if DEVELOPMENT_BUILD || UNITY_EDITOR
        //AssetBundleManager.SetDevelopmentAssetBundleServer();
        AssetBundleManager.SetSourceAssetBundleURL(Utility.GetStreamingAssetsPath());
#else
		// Use the following code if AssetBundles are embedded in the project for example via StreamingAssets folder etc:
		AssetBundleManager.SetSourceAssetBundleURL(Application.dataPath + "/");
		// Or customize the URL based on your deployment or configuration
		//AssetBundleManager.SetSourceAssetBundleURL("http://www.MyWebsite/MyAssetBundles");
#endif
    }

    protected IEnumerator InitializeLevelAsync(string levelName, bool isAdditive)
    {
        // This is simply to get the elapsed time for this phase of AssetLoading.
        float startTime = Time.realtimeSinceStartup;

        bool newMethod = true;

        if (newMethod)
        {
            AssetBundleManager.LoadLevelAsync(sceneAssetBundle, levelName, isAdditive, (success) =>
            {
                // Calculate and display the elapsed time.
                float elapsedTime = Time.realtimeSinceStartup - startTime;
                Debug.Log("Finished loading scene " + levelName + " in " + elapsedTime + " seconds");
            });
        }
        else
        {
            // Load level from assetBundle.
            AssetBundleLoadOperation request = AssetBundleManager.LoadLevelAsync(sceneAssetBundle, levelName, isAdditive);
            if (request == null)
            {
                yield break;
            }
            yield return StartCoroutine(request);

            // Calculate and display the elapsed time.
            float elapsedTime = Time.realtimeSinceStartup - startTime;
            Debug.Log("Finished loading scene " + levelName + " in " + elapsedTime + " seconds");
        }
    }
}