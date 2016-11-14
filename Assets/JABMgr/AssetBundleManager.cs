using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using System.Collections.Generic;
using System;
using System.Collections;

namespace JAB
{
    /// <summary>
    /// Class takes care of loading assetBundle and its dependencies automatically, loading variants automatically.
    /// </summary>
    public class AssetBundleManager : MonoBehaviour
    {
        public enum LogMode { All, JustErrors };
        public enum LogType { Info, Warning, Error };

        #region 变量

        static private AssetBundleManager m_ABLoaderMgr;
        static private AssetBundleManager ABLoaderMgr
        {
            get
            {
                if (m_ABLoaderMgr == null)
                {
                    GameObject go = new GameObject("AssetBundleManager", typeof(AssetBundleManager));
                    DontDestroyOnLoad(go);
                    m_ABLoaderMgr = go.GetComponent<AssetBundleManager>();
                }
                return m_ABLoaderMgr;
            }
        }

        static LogMode m_LogMode = LogMode.All;
        static string m_BaseDownloadingURL = "";

        /// <summary>
        /// 设定要使用的变体名
        /// </summary>
        static string[] m_ActiveVariants = { };

        #region 根Manifest

        /// <summary>
        /// 根Manifest
        /// </summary>
        private static AssetBundleManifest m_AssetBundleManifest = null;

        /// <summary>
        /// <para>根Manifest</para>
        /// <para>AssetBundleManifest object which can be used to load the dependecies and check suitable assetBundle variants.</para>
        /// </summary>
        public static AssetBundleManifest AssetBundleManifestObject
        {
            set
            {
                m_AssetBundleManifest = value;
            }
        }

        #endregion 根Manifest

        /// <summary>
        /// 加载好缓存的资源
        /// </summary>
        static Dictionary<string, LoadedAssetBundle> m_LoadedAssetBundles = new Dictionary<string, LoadedAssetBundle>();
        
        /// <summary>
        /// 正在加载的资源
        /// </summary>
        static Dictionary<string, WWW> m_DownloadingWWWs = new Dictionary<string, WWW>();
        /// <summary>
        /// 加载错误，AssetBundle2ErrorStr
        /// </summary>
        static Dictionary<string, string> m_DownloadingErrors = new Dictionary<string, string>();
        /// <summary>
        /// 操作队列
        /// </summary>
        static List<AssetBundleLoadOperation> m_InProgressOperations = new List<AssetBundleLoadOperation>();
        /// <summary>
        /// 依赖表
        /// </summary>
        static Dictionary<string, string[]> m_Dependencies = new Dictionary<string, string[]>();

        public static LogMode logMode
        {
            get { return m_LogMode; }
            set { m_LogMode = value; }
        }

        /// <summary>
        /// The base downloading url which is used to generate the full downloading url with the assetBundle names.
        /// </summary>
        public static string BaseDownloadingURL
        {
            get { return m_BaseDownloadingURL; }
            set { m_BaseDownloadingURL = value; }
        }

        // Variants which is used to define the active variants.
        public static string[] ActiveVariants
        {
            get { return m_ActiveVariants; }
            set { m_ActiveVariants = value; }
        }

        private static void Log(LogType logType, string text)
        {
            if (logType == LogType.Error)
            {
                Debug.LogError("[AssetBundleManager] " + text);
            }
            else if (m_LogMode == LogMode.All)
            {
                Debug.Log("[AssetBundleManager] " + text);
            }
        }

        #endregion 变量

        #region 设置

        public static void SetSourceAssetBundleDirectory(string relativePath)
        {
            BaseDownloadingURL = GetStreamingAssetsPath() + relativePath;
        }

        public static void SetSourceAssetBundleURL(string absolutePath)
        {
            BaseDownloadingURL = absolutePath + Utility.GetPlatformName() + "/";
        }

        /// <summary>
        /// 设置AssetBundle服务器地址
        /// </summary>
        public static void SetDevelopmentAssetBundleServer()
        {
            TextAsset urlFile = Resources.Load("AssetBundleServerURL") as TextAsset;
            string url = (urlFile != null) ? urlFile.text.Trim() : null;
            if (url == null || url.Length == 0)
            {
                Debug.LogError("Development Server URL could not be found.");
            }
            else
            {
                AssetBundleManager.SetSourceAssetBundleURL(url);
            }
        }

        #endregion 设置

        #region 加载AssetBundle

        /// <summary>
        /// Load AssetBundle and its dependencies.
        /// </summary>
        /// <param name="assetBundleName"></param>
        /// <param name="isLoadingAssetBundleManifest">是否是加载Manifest</param>
        static protected void LoadAssetBundle(string assetBundleName, bool isLoadingAssetBundleManifest = false)
        {
            Log(LogType.Info, "Loading Asset Bundle " + (isLoadingAssetBundleManifest ? "Manifest: " : ": ") + assetBundleName);

            if (!isLoadingAssetBundleManifest)
            {
                if (m_AssetBundleManifest == null)
                {
                    Debug.LogError("Please initialize AssetBundleManifest by calling AssetBundleManager.Initialize()");
                    return;
                }
            }

            WWW www = null;
            // Check if the assetBundle has already been processed.
            bool isAlreadyProcessed = LoadAssetBundleInternal(assetBundleName, isLoadingAssetBundleManifest, out www);

            // Load dependencies.
            if (!isAlreadyProcessed && !isLoadingAssetBundleManifest)
            {
                LoadDependencies(assetBundleName);
            }
        }

        /// <summary>
        /// <para>开放给外界，可以预先加载AB，并且统计进度</para>
        /// <para>Where we actuall call WWW to download the assetBundle.</para>
        /// </summary>
        /// <param name="assetBundleName"></param>
        /// <param name="isLoadingAssetBundleManifest"></param>
        /// <returns></returns>
        static public bool LoadAssetBundleInternal(string assetBundleName, bool isLoadingAssetBundleManifest, out WWW www)
        {
            www = null;

            // Already loaded.
            LoadedAssetBundle bundle = null;
            m_LoadedAssetBundles.TryGetValue(assetBundleName, out bundle);
            if (bundle != null)
            {
                bundle.m_ReferencedCount++;
                return true;
            }

            // @TODO: Do we need to consider the referenced count of WWWs?
            // In the demo, we never have duplicate WWWs as we wait LoadAssetAsync()/LoadLevelAsync() to be finished before calling another LoadAssetAsync()/LoadLevelAsync().
            // But in the real case, users can call LoadAssetAsync()/LoadLevelAsync() several times then wait them to be finished which might have duplicate WWWs.
            if (m_DownloadingWWWs.ContainsKey(assetBundleName))
            {
                www = m_DownloadingWWWs[assetBundleName];
                return true;
            }

            string url = m_BaseDownloadingURL + assetBundleName;

            // For manifest assetbundle, always download it as we don't have hash for it.
            if (isLoadingAssetBundleManifest)
            {
                www = new WWW(url);
            }
            else
            {
                www = WWW.LoadFromCacheOrDownload(url, m_AssetBundleManifest.GetAssetBundleHash(assetBundleName), 0);
            }

            m_DownloadingWWWs.Add(assetBundleName, www);

            return false;
        }

        /// <summary>
        /// <para>加载依赖</para>
        /// <para>Where we get all the dependencies and load them all.</para>
        /// </summary>
        /// <param name="assetBundleName"></param>
        static protected void LoadDependencies(string assetBundleName)
        {
            if (m_AssetBundleManifest == null)
            {
                Debug.LogError("Please initialize AssetBundleManifest by calling AssetBundleManager.Initialize()");
                return;
            }

            // Get dependecies from the AssetBundleManifest object..
            string[] dependencies = m_AssetBundleManifest.GetAllDependencies(assetBundleName);
            if (dependencies.Length == 0)
            {
                return;
            }

            for (int i = 0; i < dependencies.Length; i++)
            {
                dependencies[i] = RemapVariantName(dependencies[i]);
            }

            // Record and load all dependencies.
            m_Dependencies.Add(assetBundleName, dependencies);
            WWW www = null;
            for (int i = 0; i < dependencies.Length; i++)
            {
                LoadAssetBundleInternal(dependencies[i], false, out www);
            }
        }

        void Update()
        {
            // Collect all the finished WWWs.
            var keysToRemove = new List<string>();
            foreach (var keyValue in m_DownloadingWWWs)
            {
                WWW download = keyValue.Value;

                // If downloading fails.
                if (download.error != null)
                {
                    m_DownloadingErrors.Add(keyValue.Key, string.Format("Failed downloading bundle {0} from {1}: {2}", keyValue.Key, download.url, download.error));
                    keysToRemove.Add(keyValue.Key);
                    continue;
                }

                // If downloading succeeds.
                if (download.isDone)
                {
                    AssetBundle bundle = download.assetBundle;
                    if (bundle == null)
                    {
                        m_DownloadingErrors.Add(keyValue.Key, string.Format("{0} is not a valid asset bundle.", keyValue.Key));
                        keysToRemove.Add(keyValue.Key);
                        continue;
                    }

                    m_LoadedAssetBundles.Add(keyValue.Key, new LoadedAssetBundle(bundle));
                    keysToRemove.Add(keyValue.Key);
                }
            }

            // Remove the finished WWWs.
            foreach (var key in keysToRemove)
            {
                WWW download = m_DownloadingWWWs[key];
                m_DownloadingWWWs.Remove(key);
                download.Dispose();
            }

            // Update all in progress operations
            for (int i = 0; i < m_InProgressOperations.Count; )
            {
                if (!m_InProgressOperations[i].Update())
                {
                    m_InProgressOperations.RemoveAt(i);
                }
                else
                {
                    i++;
                }
            }
        }

        /// <summary>
        /// 异步加载，回调形式，不用关心过程
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="assetBundleName">包含变体</param>
        /// <param name="assetName"></param>
        /// <param name="callback"></param>
        static public void LoadAssetAsync<T>(string assetBundleName, string assetName, Action<T> callback) where T : UnityEngine.Object
        {
            ABLoaderMgr.StartCoroutine(IE_LoadAssetAsync<T>(LoadAssetAsync<T>(assetBundleName, assetName), callback, null));
        }

        /// <summary>
        /// 异步加载，返回Operation，可以自己控制多个加载的时序
        /// </summary>
        /// <param name="assetBundleName">包含变体</param>
        /// <param name="assetName"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        static public AssetBundleLoadAssetOperation LoadAssetAsync<T>(string assetBundleName, string assetName) where T : UnityEngine.Object
        {
            Log(LogType.Info, "Loading " + assetName + " from " + assetBundleName + " bundle");

            LoadManifest();

            assetBundleName = RemapVariantName(assetBundleName);
            LoadAssetBundle(assetBundleName);
            AssetBundleLoadAssetOperation operation = new AssetBundleLoadAssetOperation(assetBundleName, assetName, typeof(T));

            m_InProgressOperations.Add(operation);

            return operation;
        }

        /// <summary>
        /// 异步加载，回调形式，不用关心过程
        /// </summary>
        /// <param name="assetBundleName">包含变体</param>
        /// <param name="assetName"></param>
        /// <param name="callback"></param>
        static public void LoadLevelAsync(string assetBundleName, string levelName, bool isAdditive, Action<bool> callback)
        {
            ABLoaderMgr.StartCoroutine(IE_LoadAssetAsync<GameObject>(LoadLevelAsync(assetBundleName, levelName, isAdditive), null, callback));
        }

        /// <summary>
        /// Load level from the given assetBundle.
        /// </summary>
        /// <param name="assetBundleName">包含变体</param>
        /// <param name="levelName"></param>
        /// <param name="isAdditive"></param>
        /// <returns></returns>
        static public AssetBundleLoadOperation LoadLevelAsync(string assetBundleName, string levelName, bool isAdditive)
        {
            Log(LogType.Info, "Loading " + levelName + " from " + assetBundleName + " bundle");

            LoadManifest();

            assetBundleName = RemapVariantName(assetBundleName);
            LoadAssetBundle(assetBundleName);
            AssetBundleLoadOperation operation = new AssetBundleLoadLevelOperation(assetBundleName, levelName, isAdditive);

            m_InProgressOperations.Add(operation);

            return operation;
        }

        static private IEnumerator IE_LoadAssetAsync<T>(AssetBundleLoadOperation request, Action<T> assetCallback = null, Action<bool> levelCallBack = null) where T : UnityEngine.Object
        {
            if (request == null)
            {
                if (assetCallback != null)
                {
                    assetCallback(null);
                }
                if (levelCallBack != null)
                {
                    levelCallBack(false);
                }
                yield break;
            }
            yield return ABLoaderMgr.StartCoroutine(request);
            if (assetCallback != null)
            {
                assetCallback((request as AssetBundleLoadAssetOperation).GetAsset<T>());
            }
            if (levelCallBack != null)
            {
                levelCallBack(true);
            }
        }

        static public AssetBundleLoadManifestOperation LoadManifest()
        {
            if (m_AssetBundleManifest != null)
            {
                return null;
            }

            if (ABLoaderMgr == null)
            {
                m_ABLoaderMgr = ABLoaderMgr;
            }

            string manifestAssetBundleName = Utility.GetPlatformName();
            LoadAssetBundle(manifestAssetBundleName, true);
            AssetBundleLoadManifestOperation operation = new AssetBundleLoadManifestOperation(manifestAssetBundleName, "AssetBundleManifest", typeof(AssetBundleManifest));
            m_InProgressOperations.Add(operation);
            
            return operation;
        }

        #endregion 加载AssetBundle

        #region 卸载AssetBundle

        /// <summary>
        /// Unload assetbundle and its dependencies.
        /// </summary>
        /// <param name="assetBundleName"></param>
        static public void UnloadAssetBundle(string assetBundleName)
        {
            //Debug.Log(m_LoadedAssetBundles.Count + " assetbundle(s) in memory before unloading " + assetBundleName);

            UnloadAssetBundleInternal(assetBundleName);
            UnloadDependencies(assetBundleName);

            //Debug.Log(m_LoadedAssetBundles.Count + " assetbundle(s) in memory after unloading " + assetBundleName);
        }

        static protected void UnloadDependencies(string assetBundleName)
        {
            string[] dependencies = null;
            if (!m_Dependencies.TryGetValue(assetBundleName, out dependencies))
            {
                return;
            }

            // Loop dependencies.
            foreach (var dependency in dependencies)
            {
                UnloadAssetBundleInternal(dependency);
            }

            m_Dependencies.Remove(assetBundleName);
        }

        static protected void UnloadAssetBundleInternal(string assetBundleName)
        {
            string error;
            LoadedAssetBundle bundle = GetLoadedAssetBundle(assetBundleName, out error);
            if (bundle == null)
            {
                return;
            }

            if (--bundle.m_ReferencedCount == 0)
            {
                bundle.m_AssetBundle.Unload(false);
                m_LoadedAssetBundles.Remove(assetBundleName);

                Log(LogType.Info, assetBundleName + " has been unloaded successfully");
            }
        }

        #endregion 卸载AssetBundle

        #region 辅助

        /// <summary>
        /// <para>获得加载好的AssetBundle，依赖都加载好了才算好</para>
        /// <para>Get loaded AssetBundle, only return vaild object when all the dependencies are downloaded successfully.</para>
        /// </summary>
        /// <param name="assetBundleName"></param>
        /// <param name="error"></param>
        /// <returns></returns>
        static public LoadedAssetBundle GetLoadedAssetBundle(string assetBundleName, out string error)
        {
            if (m_DownloadingErrors.TryGetValue(assetBundleName, out error))
            {
                return null;
            }

            LoadedAssetBundle bundle = null;
            m_LoadedAssetBundles.TryGetValue(assetBundleName, out bundle);
            if (bundle == null)
            {
                return null;
            }

            // No dependencies are recorded, only the bundle itself is required.
            string[] dependencies = null;
            if (!m_Dependencies.TryGetValue(assetBundleName, out dependencies))
            {
                return bundle;
            }

            // Make sure all dependencies are loaded
            foreach (var dependency in dependencies)
            {
                if (m_DownloadingErrors.TryGetValue(assetBundleName, out error))
                {
                    return bundle;
                }

                // Wait all the dependent assetBundles being loaded.
                LoadedAssetBundle dependentBundle;
                m_LoadedAssetBundles.TryGetValue(dependency, out dependentBundle);
                if (dependentBundle == null)
                {
                    return null;
                }
            }

            return bundle;
        }

        /// <summary>
        /// bundle名检查变体
        /// </summary>
        /// <param name="assetBundleName"></param>
        /// <returns></returns>
        static protected string RemapVariantName(string assetBundleName)
        {
            string[] bundlesWithVariant = m_AssetBundleManifest.GetAllAssetBundlesWithVariant();
            string[] split = assetBundleName.Split('.');

            int bestFit = int.MaxValue;
            int bestFitIndex = -1;
            for (int i = 0; i < bundlesWithVariant.Length; i++)
            {
                //匹配bundle名
                string[] curSplit = bundlesWithVariant[i].Split('.');
                if (curSplit[0] != split[0])
                {
                    continue;
                }

                int found = System.Array.IndexOf(m_ActiveVariants, curSplit[1]);

                //如果指定的变体里没有，则使用第一个
                if (found == -1)
                {
                    found = int.MaxValue - 1;
                }

                if (found < bestFit)
                {
                    bestFit = found;
                    bestFitIndex = i;
                }
            }

            if (bestFit == int.MaxValue - 1)
            {
                Debug.LogWarning("Ambigious asset bundle variant chosen because there was no matching active variant: " + bundlesWithVariant[bestFitIndex]);
            }

            if (bestFitIndex != -1)
            {
                return bundlesWithVariant[bestFitIndex];
            }
            else
            {
                return assetBundleName;
            }
        }

        /// <summary>
        /// 后期移走
        /// </summary>
        /// <returns></returns>
        private static string GetStreamingAssetsPath()
        {
            if (Application.isEditor)
            {
                //Use the build output folder directly.
                return "file://" + System.Environment.CurrentDirectory.Replace("\\", "/");
            }
            else if (Application.isWebPlayer)
            {
                return System.IO.Path.GetDirectoryName(Application.absoluteURL).Replace("\\", "/") + "/StreamingAssets";
            }
            else if (Application.isMobilePlatform || Application.isConsolePlatform)
            {
                return Application.streamingAssetsPath;
            }
            else //For standalone player.
            {
                return "file://" + Application.streamingAssetsPath;
            }
        }

        #endregion 辅助
    }

    /// <summary>
    /// <para>Loaded assetBundle contains the references count which can be used to unload dependent assetBundles automatically.</para>
    /// <para>加载好的Bundle</para>
    /// </summary>
    public class LoadedAssetBundle
    {
        /// <summary>
        /// Bundle
        /// </summary>
        public AssetBundle m_AssetBundle;

        /// <summary>
        /// 引用计数
        /// </summary>
        public int m_ReferencedCount;

        public LoadedAssetBundle(AssetBundle assetBundle)
        {
            m_AssetBundle = assetBundle;
            m_ReferencedCount = 1;
        }
    }
}