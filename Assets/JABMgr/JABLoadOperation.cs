﻿using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;
using System;

namespace JAB
{
    /// <summary>
    /// <para>AssetBundle加载操作</para>
    /// <para>加载资源(Asset)的时候，等候AssetBundle的加载成功，再去AssetBundle里加载</para>
    /// </summary>
    public abstract class JABLoadOperation : IEnumerator
    {
        #region 系统

        public object Current
        {
            get
            {
                return null;
            }
        }

        public bool MoveNext()
        {
            return !IsDone();
        }

        public void Reset()
        {
        }

        #endregion 系统

        /// <summary>
        /// 等候m_Request构建好，返回值表示是否还需要更新
        /// </summary>
        /// <returns></returns>
        abstract public bool Update();

        abstract public bool IsDone();
    }

    public class JABLoadLevelOperation : JABLoadOperation
    {
        protected string m_AssetBundleName;
        protected string m_LevelName;
        protected bool m_IsAdditive;
        protected string m_DownloadingError;
        protected AsyncOperation m_Request;

        public JABLoadLevelOperation(string assetbundleName, string levelName, bool isAdditive)
        {
            m_AssetBundleName = assetbundleName;
            m_LevelName = levelName;
            m_IsAdditive = isAdditive;
        }

        public override bool Update()
        {
            if (m_Request != null)
            {
                return false;
            }

            LoadedAssetBundle bundle = JABMgr.GetLoadedAssetBundle(m_AssetBundleName, out m_DownloadingError);
            if (bundle != null)
            {
                if (m_IsAdditive)
                {
                    m_Request = SceneManager.LoadSceneAsync(m_LevelName, LoadSceneMode.Additive);
                }
                else
                {
                    m_Request = SceneManager.LoadSceneAsync(m_LevelName, LoadSceneMode.Single);
                }
                return false;
            }
            else
            {
                return true;
            }
        }

        public override bool IsDone()
        {
            // Return if meeting downloading error.
            // m_DownloadingError might come from the dependency downloading.
            if (m_Request == null && m_DownloadingError != null)
            {
                Debug.LogError(m_DownloadingError);
                return true;
            }

            return m_Request != null && m_Request.isDone;
        }
    }

    public class JABLoadAssetOperation : JABLoadOperation
    {
        protected string m_AssetBundleName;
        protected string m_AssetName;
        protected string m_DownloadingError;
        protected System.Type m_Type;
        protected AssetBundleRequest m_Request = null;

        public JABLoadAssetOperation(string bundleName, string assetName, System.Type type)
        {
            m_AssetBundleName = bundleName;
            m_AssetName = assetName;
            m_Type = type;
        }

        public T GetAsset<T>() where T : UnityEngine.Object
        {
            if (m_Request != null && m_Request.isDone)
            {
                return m_Request.asset as T;
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Returns true if more Update calls are required.
        /// </summary>
        /// <returns></returns>
        public override bool Update()
        {
            if (m_Request != null)
            {
                return false;
            }

            LoadedAssetBundle bundle = JABMgr.GetLoadedAssetBundle(m_AssetBundleName, out m_DownloadingError);
            if (bundle != null)
            {
                ///@TODO: When asset bundle download fails this throws an exception...
                m_Request = bundle.m_AssetBundle.LoadAssetAsync(m_AssetName, m_Type);
                return false;
            }
            else
            {
                return true;
            }
        }

        public override bool IsDone()
        {
            // Return if meeting downloading error.
            // m_DownloadingError might come from the dependency downloading.
            if (m_Request == null && m_DownloadingError != null)
            {
                Debug.LogError(m_DownloadingError);
                return true;
            }

            return m_Request != null && m_Request.isDone;
        }
    }

    public class JABLoadManifestOperation : JABLoadAssetOperation
    {
        public JABLoadManifestOperation(string bundleName, string assetName, System.Type type)
            : base(bundleName, assetName, type)
        {
        }

        public override bool Update()
        {
            base.Update();

            if (m_Request != null && m_Request.isDone)
            {
                JABMgr.AssetBundleManifestObject = GetAsset<AssetBundleManifest>();
                return false;
            }
            else
            {
                return true;
            }
        }
    }
}