using UnityEngine;
using System.Collections;
using JAB;

public class LoadAssets : MonoBehaviour
{
    public const string AssetBundlesOutputPath = "/AssetBundles/";
    public string assetBundleName;
    public string assetName;

    // Use this for initialization
    IEnumerator Start()
    {
        yield return this.StartCoroutine(Initialize());

        // Load asset.
        yield return StartCoroutine(InstantiateGameObjectAsync(assetBundleName, assetName));
    }

    /// <summary>
    /// Initialize the downloading url and AssetBundleManifest object.
    /// </summary>
    /// <returns></returns>
    protected IEnumerator Initialize()
    {
        // Don't destroy this gameObject as we depend on it to run the loading script.
        DontDestroyOnLoad(gameObject);

        // With this code, when in-editor or using a development builds: Always use the AssetBundle Server
        // (This is very dependent on the production workflow of the project. 
        // 	Another approach would be to make this configurable in the standalone player.)
#if DEVELOPMENT_BUILD || UNITY_EDITOR
        //AssetBundleManager.SetDevelopmentAssetBundleServer();
        //AssetBundleManager.SetSourceAssetBundleDirectory("/AssetBundles/Android/");
        JABMgr.SetSourceAssetBundleURL(JABUtil.GetStreamingAssetsPath());
#else
		// Use the following code if AssetBundles are embedded in the project for example via StreamingAssets folder etc:
		AssetBundleManager.SetSourceAssetBundleURL(Application.streamingAssetsPath + "/");
		// Or customize the URL based on your deployment or configuration
		//AssetBundleManager.SetSourceAssetBundleURL("http://www.MyWebsite/MyAssetBundles");
#endif

        JABLoadManifestOperation request = JABMgr.LoadManifest();
        if (request == null)
        {
            yield break;
        }
        yield return this.StartCoroutine(request);
    }

    protected IEnumerator InstantiateGameObjectAsync(string assetBundleName, string assetName)
    {
        // This is simply to get the elapsed time for this phase of AssetLoading.
        float startTime = Time.realtimeSinceStartup;

        bool newMethod = true;

        if (newMethod)
        {
            JABMgr.LoadAssetAsync<GameObject>(assetBundleName, assetName, (obj) =>
            {
                if (obj != null)
                {
                    GameObject.Instantiate(obj);
                }

                // Calculate and display the elapsed time.
                float elapsedTime = Time.realtimeSinceStartup - startTime;
                Debug.Log(assetName + (obj == null ? " was not" : " was") + " loaded successfully in " + elapsedTime + " seconds");
            });
        }
        else
        {
            // Load asset from assetBundle.
            JABLoadAssetOperation request = JABMgr.LoadAssetAsync<GameObject>(assetBundleName, assetName);
            if (request == null)
            {
                yield break;
            }
            yield return StartCoroutine(request);

            // Get the asset.
            GameObject prefab = request.GetAsset<GameObject>();

            if (prefab != null)
            {
                GameObject.Instantiate(prefab);
            }

            // Calculate and display the elapsed time.
            float elapsedTime = Time.realtimeSinceStartup - startTime;
            Debug.Log(assetName + (prefab == null ? " was not" : " was") + " loaded successfully in " + elapsedTime + " seconds");
        }
    }
}