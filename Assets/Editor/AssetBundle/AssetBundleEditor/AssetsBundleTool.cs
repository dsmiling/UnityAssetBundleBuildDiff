using UnityEngine;
using UnityEditor;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System;
using System.Xml;
using System.Text;

/// <summary>
/// unity5版本资源assetsbundle打包工具[强制更新的资源打包]
/// </summary>
public class AssetsBundleTool : EditorWindow
{
    const string notification = "1,platform.2,output dir.3,build.";
    static string m_RootDirectory = "";
    //static string m_RootUnforce = "";
    static string mLastVersion = "";
    static string mVersion = "";
    static string mLog = "";
    static TargetPlatform m_TargetPlatform = TargetPlatform.StandaloneWindow;
    static string m_Assets = "Assets/AssetData";

    static AssetsBundleTool window;
    static AssetBundleManifest mAssetBundleManifest;

    static List<string> allDeps = new List<string>();//存放被依赖的资源

    static Dictionary<string, AssstCaheData> mCacheAssetBund;
    static Dictionary<string, string> mPath2ABName;

    static StringBuilder mStringBuilder = new StringBuilder();

    public enum TargetPlatform : int
    {
        StandaloneWindow = (int)BuildTarget.StandaloneWindows64,
        Android = (int)BuildTarget.Android,
        IOS = (int)BuildTarget.iOS,
    }
    [MenuItem("Editor/资源打包/BuildAssetBundle &M")]
    static void CallBuildWindow()
    {
        window = EditorWindow.GetWindow<AssetsBundleTool>();
        window.maxSize = new Vector2(805f, 1000f);
        window.minSize = new Vector2(805f, 500f);
        window.titleContent = new GUIContent("资源打包工具");

        window.autoRepaintOnSceneChange = true;
        m_TargetPlatform = TargetPlatform.StandaloneWindow;

        if (string.IsNullOrEmpty(m_RootDirectory))
        {
            m_RootDirectory = Application.persistentDataPath + "/AssetBundle/" + m_TargetPlatform.ToString();//强制资源的输出路径
        }
        //m_RootUnforce = Application.dataPath + "/../../AssetBundle/unforce/" +m_TargetPlatform.ToString();//非强制资源的输出路径

        //XMLName = "UnforceStandaloneWindowXML.xml";
        GetSVNResversion();
    }
    static void GetSVNResversion()
    {
        mCacheAssetBund = new Dictionary<string, AssstCaheData>();

        var cachePath = Application.persistentDataPath + "/" + GetPlatformFloder() + "_AssetBundleCache.xml";
        string cachetext = string.Empty;
        if (File.Exists(cachePath))
        {
            using (FileStream fs = new FileStream(cachePath, FileMode.Open))
            {
                int fsLen = (int)fs.Length;
                byte[] heByte = new byte[fsLen];
                //int r = fs.Read(heByte, 0, heByte.Length);
                fs.Read(heByte, 0, heByte.Length);
                cachetext = System.Text.Encoding.UTF8.GetString(heByte);
            }
        }

        if (cachetext != string.Empty)
        {
            //  mCacheAssetBund = new Dictionary<string, AssstCaheData>();
            XmlDocument xml = new XmlDocument();
            xml.LoadXml(cachetext);
            XmlNode node = xml.SelectSingleNode("root");
            var list = xml.SelectNodes("root/asset");
            Debug.Log("AssetBundleCache " + list.Count);
            foreach (XmlNode cache in list)
            {
                AssstCaheData data = new AssstCaheData
                {
                    assetBundleName = cache.Attributes.GetNamedItem("name").Value,
                    mGuilds = new Dictionary<string, string>()
                };

                foreach (XmlNode item in cache.ChildNodes)
                {
                    string guid = item.Attributes.GetNamedItem("guid").Value;
                    string hash = item.Attributes.GetNamedItem("hash").Value;
                    data.mGuilds[guid] = hash;
                }
                mCacheAssetBund[data.assetBundleName] = data;
            }
        }
        var rvlPath = Application.persistentDataPath + "/ResVersion.xml";
        string text = string.Empty;
        if (File.Exists(rvlPath))
        {
            using (FileStream fs = new FileStream(rvlPath, FileMode.Open))
            {
                int fsLen = (int)fs.Length;
                byte[] heByte = new byte[fsLen];
                //int r = fs.Read(heByte, 0, heByte.Length);
                fs.Read(heByte, 0, heByte.Length);
                text = System.Text.Encoding.UTF8.GetString(heByte);
            }
        }

        if (text == string.Empty)
        {
            mLastVersion = "1.0.0";
            mVersion = "1.0.0";
        }
        else
        {
            XmlDocument xml = new XmlDocument();
            xml.LoadXml(text);
            XmlNode node = xml.SelectSingleNode("root");
            string version = node.Attributes["version"].Value;

            mLastVersion = version;
            mVersion = version;
            if (mCacheAssetBund != null)
            {
                var list = xml.SelectNodes("root/asset");
                Debug.Log("ResVersion " + list.Count);
                foreach (XmlNode cache in list)
                {
                    string name = cache.Attributes.GetNamedItem("name").Value;
                    string md5 = cache.Attributes.GetNamedItem("md5").Value;
                    string path = cache.Attributes.GetNamedItem("path").Value;
                    string size = cache.Attributes.GetNamedItem("size").Value;

                    AssstCaheData data;
                    if (mCacheAssetBund.TryGetValue(name, out data))
                    {
                        data.md5 = md5;
                        data.path = path;
                        data.size = size;
                    }
                    else
                    {
                        //    Debug.LogError("name " + name + " 没有缓存数据");
                        data.md5 = md5;
                        data.path = path;
                        data.size = size;
                    }
                    mCacheAssetBund[name] = data;

                    //string size = node.Attributes.GetNamedItem("size ").Value;
                    //string path = node.Attributes.GetNamedItem("path ").Value;
                }
            }
        }
    }

    private bool isCompiling = false;
    void OnGUI()
    {
        if (EditorApplication.isCompiling)
        {
            isCompiling = true;
            ShowNotification(new GUIContent("正在编译 请等待..."));
            return;
        }
        if (isCompiling)
        {
            isCompiling = false;
            CallBuildWindow();
        }

        EditorGUILayout.HelpBox(notification, MessageType.Info, true);
        EditorGUILayout.BeginVertical();
        GUILayout.Space(10f);
        EditorGUILayout.BeginHorizontal();
        GUILayout.Space(10f);
        EditorGUILayout.LabelField("Target Platform:", GUILayout.Width(120f), GUILayout.Height(25));
        GUILayout.Space(10f);
        var targetPlatform = (TargetPlatform)EditorGUILayout.EnumPopup(m_TargetPlatform, GUILayout.Width(120f), GUILayout.Height(25));
        if (targetPlatform != m_TargetPlatform)
        {
            m_TargetPlatform = targetPlatform;
            GetSVNResversion();
            m_RootDirectory = Application.persistentDataPath + "/AssetBundle/" + m_TargetPlatform.ToString();//强制资源的输出路径
        }
        EditorGUILayout.EndHorizontal();
        GUILayout.Space(20f);
        EditorGUILayout.BeginHorizontal();
        GUILayout.Space(10f);
        EditorGUILayout.LabelField("LastVersion:", GUILayout.Width(80f), GUILayout.Height(20));
        GUILayout.Space(10f);
        EditorGUILayout.LabelField(mLastVersion, GUILayout.Width(80f), GUILayout.Height(20));
        EditorGUILayout.EndHorizontal();
        GUILayout.Space(20f);
        EditorGUILayout.BeginHorizontal();
        GUILayout.Space(10f);
        EditorGUILayout.LabelField("Version:", GUILayout.Width(80f), GUILayout.Height(20));
        GUILayout.Space(10f);
        EditorGUILayout.LabelField(mVersion, GUILayout.Width(80f), GUILayout.Height(20));
        if (mVersion == mLastVersion)
        {
            if (GUILayout.Button("add1", GUILayout.Width(60f), GUILayout.Height(25)))
            {
                AddVersion(1);
            }
            if (GUILayout.Button("add2", GUILayout.Width(60f), GUILayout.Height(25)))
            {
                AddVersion(2);
            }
            if (GUILayout.Button("add3", GUILayout.Width(60f), GUILayout.Height(25)))
            {
                AddVersion(3);
            }
        }

        EditorGUILayout.EndHorizontal();
        GUILayout.Space(10f);
        EditorGUILayout.BeginHorizontal();
        GUILayout.Space(10f);
        EditorGUILayout.LabelField("OutPut Directory *", GUILayout.Width(120f), GUILayout.Height(20));

        m_RootDirectory = EditorGUILayout.TextField(m_RootDirectory, GUILayout.Width(300f), GUILayout.Height(20));
        GUI.SetNextControlName("Browse");
        if (GUILayout.Button("Browse", GUILayout.Width(60f), GUILayout.Height(20)))
        {
            string path = EditorUtility.OpenFolderPanel("Browse", m_RootDirectory, "");
            m_RootDirectory = path.Length > 0 ? path : m_RootDirectory;
            GUI.FocusControl("Browse");
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
        EditorGUILayout.BeginVertical();
        GUILayout.Space(10f);
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("ResetABName", GUILayout.Width(150f), GUILayout.Height(25)))
        {
            var stopwatch = new System.Diagnostics.Stopwatch();
            stopwatch.Start(); // 开始监视代码运行时间

            ReSetABNameByPath();

            EditorUtility.DisplayDialog("Success", "set assetbundle name success!", "Yes");
            stopwatch.Stop(); // 停止监视
            TimeSpan timespan = stopwatch.Elapsed; // 获取当前实例测量得出的总时间
            string seconds = timespan.TotalSeconds.ToString(); // 总秒数
            Debug.Log("SetABName cost seconds " + seconds);
        }

        GUILayout.Space(50f);

        if (GUILayout.Button("CheckAsset", GUILayout.Width(150f), GUILayout.Height(25)))
        {
            var stopwatch = new System.Diagnostics.Stopwatch();
            stopwatch.Start(); // 开始监视代码运行时间

            AssetCheckClear();
            stopwatch.Stop(); // 停止监视
            TimeSpan timespan = stopwatch.Elapsed; // 获取当前实例测量得出的总时间
            string seconds = timespan.TotalSeconds.ToString(); // 总秒数
            Debug.Log("AssetCheck cost seconds " + seconds);
        }
        GUILayout.Space(50f);

        if (GUILayout.Button("CheckBuildDiff", GUILayout.Width(150f), GUILayout.Height(25)))
        {
            var stopwatch = new System.Diagnostics.Stopwatch();
            stopwatch.Start(); // 开始监视代码运行时间

            SetDiffBuilds();
            stopwatch.Stop(); // 停止监视
            TimeSpan timespan = stopwatch.Elapsed; // 获取当前实例测量得出的总时间
            string seconds = timespan.TotalSeconds.ToString(); // 总秒数
            Debug.Log("AssetCheck cost seconds " + seconds);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Build Diff", GUILayout.Width(120f), GUILayout.Height(25)))
        {
            var stopwatch = new System.Diagnostics.Stopwatch();
            stopwatch.Start(); // 开始监视代码运行时间

            ReSetABNameByPath();
            BuildDiff();

            stopwatch.Stop(); // 停止监视
            TimeSpan timespan = stopwatch.Elapsed; // 获取当前实例测量得出的总时间
            string seconds = timespan.TotalSeconds.ToString(); // 总秒数
            Debug.Log("BuildDiff cost seconds " + seconds);
        }
        GUILayout.Space(100f);

        if (GUILayout.Button("Build All", GUILayout.Width(120f), GUILayout.Height(25)))
        {
            var stopwatch = new System.Diagnostics.Stopwatch();
            stopwatch.Start(); // 开始监视代码运行时间

            ReSetABNameByPath();
            BuildAll();

            stopwatch.Stop(); // 停止监视
            TimeSpan timespan = stopwatch.Elapsed; // 获取当前实例测量得出的总时间
            string seconds = timespan.TotalSeconds.ToString(); // 总秒数
            Debug.Log("BuildDiff cost seconds " + seconds);
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.LabelField("OutPut Log:", GUILayout.Width(120f), GUILayout.Height(25));
        EditorGUILayout.LabelField(mLog, GUILayout.Width(300f), GUILayout.Height(60));
    }

    static void AddVersion(int index)
    {
        string[] lastArr = mLastVersion.Split('.');
        int num = int.Parse(lastArr[index - 1]) + 1;
        lastArr[index - 1] = num.ToString();

        mVersion = lastArr[0] + "." + lastArr[1] + "." + lastArr[2];
    }

    #region util

    static void ClearAll()
    {
        if (Directory.Exists(m_RootDirectory))
        {
            Directory.Delete(m_RootDirectory, true);
        }
    }

    /// <summary>
    /// 获取字节流文件的Md5码
    /// </summary>
    /// <param name="bytes">字节流</param>
    /// <returns></returns>
    static string GetMd5(byte[] bytes)
    {

        MD5 md5 = MD5.Create();
        byte[] mds = md5.ComputeHash(bytes);
        string md5Str = "";
        for (int i = 0; i < mds.Length; i++)
        {
            md5Str = md5Str + mds[i].ToString("X");
        }

        return md5Str;
    }

    /// <summary>
    /// 简单检查version格式
    /// </summary>
    /// <returns></returns>
    static bool CheckVersionFormat()
    {
        bool b = true;
        try
        {
            System.Version version = new System.Version(mVersion);
            Debug.LogError(version);
        }
        catch (Exception e)
        {
            Debug.LogError(e.ToString());
            mLog = "error:must set a assets version example 1.0.0";
            b = false;
        }

        return b;
    }
    static void GetAllFiles(string directory, List<string> paths)
    {
        if (!Directory.Exists(directory))
        {
            Debug.LogError("Directory Not Exist " + directory);
            return;
        }
        string[] files = Directory.GetFiles(directory);
        string[] directorys = Directory.GetDirectories(directory);

        for (int i = 0; i < files.Length; i++)
        {
            string path = files[i];
            if (string.IsNullOrEmpty(path))
            {
                continue;
            }

            string[] arr = null;
            if (path.Contains("\\"))
            {
                arr = path.Split('\\');
                path = arr[0] + "/" + arr[1];
            }
            //    Debug.Log(" path " + path);
            paths.Add(path);
        }

        for (int j = 0; j < directorys.Length; j++)
        {
            string path = directorys[j];
            if (string.IsNullOrEmpty(path))
            {
                continue;
            }

            string[] arr = null;
            if (path.Contains("\\"))
            {
                //Debug.Log(" path  " + path);
                arr = path.Split('\\');
                path = arr[0] + "/" + arr[1];
            }

            GetAllFiles(path, paths);
        }
    }
    private static string GetArg(string name)
    {
        var args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == name && args.Length > i + 1)
            {
                return args[i + 1];
            }
        }

        return string.Empty;
    }
    //辅助函数,获取当前打包目标平台
    static string GetPlatformFloder()
    {
        if (m_TargetPlatform == TargetPlatform.IOS)
        {
            return "IOS";
        }
        else if (m_TargetPlatform == TargetPlatform.Android)
        {
            return "Android";
        }
        else
        {
            return "StandaloneWindow";
        }

    }

    public static void StringBuilder(params object[] args)
    {
        for (int i = 0; i < args.Length; ++i)
        {
            mStringBuilder.Append(args[i]);
        }
    }
    //辅助函数,获取路径
    static string GetPathByFolder(string path, string folder, bool forward, bool containfolder)
    {
        var index = path.LastIndexOf(folder);
        if (forward)
        {
            if (!containfolder)
            {
                //获取folder前路径(不包含)
                return (index == -1) ? "" : path.Substring(0, index);
            }
            else
            {
                //获取folder前路径(包含)
                return (index == -1) ? "" : path.Substring(0, index + folder.Length);
            }
        }
        else
        {
            if (!containfolder)
            {
                //获取folder后路径(不包含)
                return (index == -1) ? "" : path.Substring(index + folder.Length, path.Length - index - folder.Length);
            }
            else
            {
                //获取folder后路径(不包含)
                return (index == -1) ? "" : path.Substring(index, path.Length - index);
            }
        }
    }
    //辅助函数,获取路径
    static string GetLastFolderPath(string path)
    {
        var index = path.LastIndexOf("/");
        //获取Asset下路径
        return (index == -1) ? "" : path.Substring(0, index);
    }
    //辅助函数,获取路径
    static string GetLastFolder(string path)
    {
        var index = path.LastIndexOf("/");
        //获取Asset下路径
        return (index == -1) ? "" : path.Substring(index + 1, path.Length - index - 1);
    }
    #endregion

    #region 命令行方法
    //命令行模式调用方法----打包所有
    static void BuildAssetsForCommandLine_IOS_All()
    {
        mVersion = GetArg("-assetsVersion");
        m_RootDirectory = GetArg("-outpath");
        m_TargetPlatform = TargetPlatform.IOS;
        ReSetABNameByPath();
        BuildAll();
    }

    //命令行模式调用方法
    static void BuildAssetsForCommandLine_IOS()
    {
        mVersion = GetArg("-assetsVersion");
        m_RootDirectory = GetArg("-outpath");
        m_TargetPlatform = TargetPlatform.IOS;
        GetSVNResversion();
        ReSetABNameByPath();
        BuildDiff();
    }

    //命令行模式调用方法--打包所有(安卓)
    static void BuildAssetsForCommandLine_Android_All()
    {
        mVersion = GetArg("-assetsVersion");
        m_RootDirectory = GetArg("-outpath");
        m_TargetPlatform = TargetPlatform.Android;
        ReSetABNameByPath();
        BuildAll();
    }

    //命令行模式调用方法
    static void BuildAssetsForCommandLine_Android()
    {
        mVersion = GetArg("-assetsVersion");
        m_RootDirectory = GetArg("-outpath");
        m_TargetPlatform = TargetPlatform.Android;
        GetSVNResversion();
        ReSetABNameByPath();
        BuildDiff();
    }

    #endregion

    #region 新的打包方式

    //------------------------------------------------------------------------------
    static IAssetBundleManifest mIManifest;
    private static Dictionary<string, CustomBuildItem> tempBuilds = new Dictionary<string, CustomBuildItem>();
    private static List<AssstCaheData> mClearList = new List<AssstCaheData>();
    private static List<AssetBundleBuild> mBuildsList = new List<AssetBundleBuild>();


    private struct AssstCaheData
    {
        public string assetBundleName;
        //这个是
        public string filepath;
        public string path;
        public string size;
        public string md5;

        public Dictionary<string, string> mGuilds;
    }

    //差异化打包
    private struct CustomBuildItem
    {
        //
        // 摘要:
        //     ///
        //     AssetBundle name.
        //     ///
        public string assetBundleName;
        //
        // 摘要:
        //     ///
        //     AssetBundle variant.
        //     ///
        public string assetBundleVariant;
        //
        // 摘要:
        //     ///
        //     Asset names which belong to the given AssetBundle.
        //     ///
        public List<string> assetNames;
        //
        // 摘要:
        //     ///
        //     Addressable name used to load an asset.
        //     ///
        public List<string> addressableNames;

        //
        // 摘要:
        //     ///
        //     Addressable name used to load an asset.
        //     ///
        public List<string> dependNames;

        public AssetBundleBuild GetAssetBundleBuild()
        {
            if (assetNames != null)
            {
                AssetBundleBuild build = new AssetBundleBuild
                {
                    assetBundleName = assetBundleName,
                    assetBundleVariant = assetBundleVariant,
                    assetNames = assetNames.ToArray()
                };
                if (addressableNames != null)
                {
                    build.addressableNames = addressableNames.ToArray();
                }

                return build;
            }
            else
            {
                return new AssetBundleBuild();
            }
        }
        /*
        public string GetAssetBundleHash() {

            string[] hashs = new string[assetNames.Count];
            foreach (var path in assetNames)
            {
                string hash = AssetDatabase.GetAssetDependencyHash(path).ToString();
            }
        }
        */
    }

    static void ReSetABNameByPath()
    {
        tempBuilds.Clear();
        mPath2ABName = new Dictionary<string, string>();
        BuildAssetSpawn(m_Assets);

        Debug.Log(" tempBuilds " + tempBuilds.Count);
    }
    static CustomBuildItem SetAsset(string abName, string path)
    {
        CustomBuildItem build;
        if (!tempBuilds.ContainsKey(abName))
        {
            build = new CustomBuildItem();
            build.assetBundleName = abName.ToLower();
            build.assetNames = new List<string>();
            build.assetNames.Add(path);
            tempBuilds[abName] = build;
        }
        else
        {
            build = tempBuilds[abName];
            if (!build.assetNames.Contains(path))
                build.assetNames.Add(path);
        }
        //把路径对应的ABName缓存起来
        mPath2ABName[path] = abName;

        return build;
    }
    static void BuildAssetSpawn(string assetPath)
    {
        if (!Directory.Exists(assetPath))
        {
            Debug.LogError(" assetPath Null : " + assetPath);
            return;
        }
        List<string> fileList = new List<string>();
        GetAllFiles(assetPath, fileList);

        for (int i = 0; i < fileList.Count; i++)
        {
            string path = fileList[i];
            if (path.Contains(".vscode") ||
                path.Contains(".cs") ||
                path.Contains(".shader") ||
                path.Contains("Local") ||
                path.Contains("Shader") ||
                path.Contains("SuperTextMesh") ||
                path.Contains(".meta") ||
                path.Contains(".DS_Store") ||
                path.Contains(".dll"))
            {
                continue;
            }
            var build = SetAsset(GetABNameFormat(path), path);
            var depends = AssetDatabase.GetDependencies(path);
            //var dpNames = new List<string>();
            foreach (string dp in depends)
            {
                if (dp.Contains(".cs") ||
                    dp.Contains(".mdb") ||
                    dp.Contains(".dll"))
                {
                    continue;
                }

                //如果依赖项本来就在要打包路径中,不继续检索
                if (fileList.Contains(dp))
                    continue;

                //因为是根据AB名进行载入，所以不能再加进去
                var dpABName = AssetDatabase.AssetPathToGUID(dp);

                //如果依赖项已被别的依赖,说明已经单独打包,不用继续走
                if (tempBuilds.ContainsKey(dpABName))
                    continue;

                SetAsset(dpABName, dp);
            }
        }

    }
    static int abNameIndex = 16;
    static string GetABNameFormat(string path)
    {
        return path.Substring(abNameIndex + 1, path.LastIndexOf(".") - abNameIndex - 1).ToLower();
    }

    static void SetDiffBuilds()
    {
        mBuildsList = new List<AssetBundleBuild>();
        mClearList = new List<AssstCaheData>();
        var isNeedPack = false;
        var cacheList = new List<string>();
        if (mCacheAssetBund != null)
        {
            cacheList = mCacheAssetBund.Keys.ToList();
        }

        foreach (var build in tempBuilds.Values)
        {
            isNeedPack = false;
            //差异化判断,如果没缓存表,就打包全部资源
            if (mCacheAssetBund != null)
            {
                var abName = build.assetBundleName;
                //如果缓存表存在AB包的key,开始检查缓存的与需要打包资源的差异
                if (mCacheAssetBund.ContainsKey(abName))
                {
                    //从缓存list中删除,如果删到最后list不为空,则说明资源需要删除
                    cacheList.Remove(abName);
                    var cache = mCacheAssetBund[abName];
                    //    Debug.Log(abName + "=> hash " + cache.md5);
                    //如何缓存的guid列表存在,则继续比较差异,如果不存在说明需要打包
                    if (cache.mGuilds != null)
                    {
                        var guids = cache.mGuilds.Keys.ToList();
                        //比对缓存的ab包和要打包的ab包的文件差异
                        foreach (var path in build.assetNames)
                        {
                            var guid = AssetDatabase.AssetPathToGUID(path);
                            var hash = AssetDatabase.GetAssetDependencyHash(path).ToString();
                            if (guids.Contains(guid))
                            {
                                guids.Remove(guid);
                                var cacheHash = cache.mGuilds[guid];
                                if (cacheHash != hash)
                                {
                                    Debug.LogError(abName + "hash 发生改变！old" + cacheHash + " cur " + hash);
                                    isNeedPack = true;
                                    break;
                                }
                            }
                            else
                            {
                                Debug.LogError(guid + "缓存不包括此guids！" + path);

                                isNeedPack = true;
                                break;
                            }
                        }
                        //如果取完build的path的keys发现还有缓存的,说明缓存的文件比要打包的文件多,需要重新打包
                        if (guids.Count > 0)
                        {
                            Debug.LogError("取完build的path的keys发现还有缓存的,需要重新打包==> 剩余guids Count" + guids.Count);

                            isNeedPack = true;
                        }
                    }
                    else
                    {
                        Debug.LogError("hashs 没有值直接需要打包");

                        isNeedPack = true;
                    }
                    if (isNeedPack)
                        mClearList.Add(cache);
                }
                else
                {
                    Debug.Log(abName + "=> 没有缓存需要重新打包 ");
                    isNeedPack = true;
                }
            }
            else
            {
                Debug.Log("CacheAssetBund 没有值需要重新打包 ");
                isNeedPack = true;
            }

            if (isNeedPack)
            {
                Debug.LogError("Is NeedPack item: " + build.assetBundleName + " count " + build.assetNames.Count);
                foreach (var path in build.assetNames)
                {
                    Debug.LogError("item Path: " + path);
                }
                mBuildsList.Add(build.GetAssetBundleBuild());
            }
            else
            {
                //    Debug.Log("Not NeedPack item: " + build.assetBundleName + " count " + build.assetNames.Count);
            }
        }
        if (cacheList.Count > 0)
        {
            foreach (var abName in cacheList)
            {
                var cache = mCacheAssetBund[abName];
                Debug.LogError(abName + "已经不需要,添加删除！");
                mClearList.Add(cache);
            }
        }
        Debug.Log(" 需要打包的数量 " + mBuildsList.Count);
    }
    static void SetAllBuilds()
    {
        mBuildsList = new List<AssetBundleBuild>();
        foreach (var build in tempBuilds.Values)
        {
            mBuildsList.Add(build.GetAssetBundleBuild());
        }
    }
    /// <summary>
    /// 检查资源匹配
    /// </summary>
    static void AssetCheckClear()
    {
        //如果缓存表不存在,则没什么好资源检查的,直接打包全部资源呗
        if (mCacheAssetBund == null)
            return;

        //这个是当前已被打包的资源
        List<string> fileList = new List<string>();
        GetAllFiles(m_RootDirectory, fileList);
        // var platm = GetPlatformFloder();
        //重新创建一个
        var isSearched = false;
        var realCache = new Dictionary<string, AssstCaheData>();
        //便利所有已有资源,并在cache类中添加真实路径,如果在配置表中没查到该资源,则直接删除

        Debug.LogError("mCacheAssetBund Count " + mCacheAssetBund.Count);
        foreach (var path in fileList)
        {
            if (path.Contains("ResVersion.xml") ||
                path.Contains(".mp4") ||
                path.Contains(".MP4"))
                continue;

            var name = GetLastFolder(path);
            //    Debug.Log(" Path :" + name);
            isSearched = false;

            foreach (var key in mCacheAssetBund.Keys)
            {
                var item = mCacheAssetBund[key];
                if (item.md5 == name)
                {
                    item.filepath = path;
                    isSearched = true;
                    realCache[key] = item;
                    //    Debug.LogError(" find md5" + item.md5);
                    break;
                }
            }
            //如果路径资源没在配置表中找到,说明是冗余文件,直接删除
            if (!isSearched)
            {
                Debug.LogError("警告 ! path " + path + " 没有在配置文件中,删除！");
                File.Delete(path);
            }
        }
        Debug.LogError("Checked mCacheAssetBund Count " + realCache.Count);

        mCacheAssetBund = realCache;
    }

    static bool BuildCondition()
    {
        //if (mLastVersion == mVersion)
        //{
        //    EditorUtility.DisplayDialog("Error", "资源版本号是否需要改变一下!", "Yes");
        //    return false;
        //}

        if (string.IsNullOrEmpty(m_RootDirectory))
        {
            EditorUtility.DisplayDialog("Error", "must browse a output directory first!", "Yes");
            mLog = "error:must browse a output directory first!";
            return false;
        }
        if (string.IsNullOrEmpty(mVersion))
        {
            EditorUtility.DisplayDialog("Error", "must set a assets version example 1.0.0!", "Yes");
            mLog = "error:must set a assets version example 1.0.0";
            return false;
        }
        if (!CheckVersionFormat())
        {
            EditorUtility.DisplayDialog("Error", "版本号格式不正确,请检查格式!", "Yes");
            mLog = "error:版本号格式不正确,请检查格式";
            return false;
        }
        return true;
    }
    /// <summary>
    /// 差异化打包
    /// </summary>
    static void BuildAll()
    {
        //如果不满足打包条件
        if (!BuildCondition())
            return;

        ClearAll();

        //不存在路径则创建
        if (!Directory.Exists(m_RootDirectory))
        {
            Directory.CreateDirectory(m_RootDirectory);
        }

        var stopwatch = new System.Diagnostics.Stopwatch();
        stopwatch.Start(); // 开始监视代码运行时间

        //这个是当前需要被打包的资源
        SetAllBuilds();

        stopwatch.Stop(); // 停止监视
        TimeSpan timespan = stopwatch.Elapsed; // 获取当前实例测量得出的总时间
        string seconds = timespan.TotalSeconds.ToString(); // 总秒数
        Debug.Log("===============SetAllBuilds Cost Seconds  =>" + seconds + "======================");

        stopwatch = new System.Diagnostics.Stopwatch();
        stopwatch.Start(); // 开始监视代码运行时间

        //生成资源依赖
        MakeIAssetBundleManifest();

        stopwatch.Stop(); // 停止监视
        timespan = stopwatch.Elapsed; // 获取当前实例测量得出的总时间
        seconds = timespan.TotalSeconds.ToString(); // 总秒数
        Debug.Log("===============MakeIAssetBundleManifest Cost Seconds  =>" + seconds + "======================");

        stopwatch = new System.Diagnostics.Stopwatch();
        stopwatch.Start(); // 开始监视代码运行时间

        //生成资源缓存
        MakeCacheData();

        stopwatch.Stop(); // 停止监视
        timespan = stopwatch.Elapsed; // 获取当前实例测量得出的总时间
        seconds = timespan.TotalSeconds.ToString(); // 总秒数
        Debug.Log("===============MakeCacheData Cost Seconds  =>" + seconds + "======================");

        stopwatch = new System.Diagnostics.Stopwatch();
        stopwatch.Start(); // 开始监视代码运行时间
        Debug.Log(" 开始打包所有资源 Count => " + mBuildsList.Count + " Platform" + m_TargetPlatform.ToString());
        mAssetBundleManifest = BuildPipeline.BuildAssetBundles(m_RootDirectory, mBuildsList.ToArray(), BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.ForceRebuildAssetBundle, (BuildTarget)((int)m_TargetPlatform));
        stopwatch.Stop(); // 停止监视
        timespan = stopwatch.Elapsed; // 获取当前实例测量得出的总时间
        seconds = timespan.TotalSeconds.ToString(); // 总秒数
        Debug.Log("===============BuildAssetBundles Cost Seconds  =>" + seconds + "======================");

        if (mAssetBundleManifest != null)
        {
            string[] arr = mAssetBundleManifest.GetAllAssetBundles();
            Debug.Log(" 打包完成！！完成数量 Count => " + arr.Length);

            //清理打包生产的依赖信息文件
            ClearDpFiles();
            //给打包的AB用MD5重命名
            NameMD5Files();

            mAssetBundleManifest = null;
        }
    }
    /// <summary>
    /// 差异化打包
    /// </summary>
    static void BuildDiff()
    {
        //如果不满足打包条件
        if (!BuildCondition())
            return;

        //不存在路径则创建
        if (!Directory.Exists(m_RootDirectory))
        {
            Directory.CreateDirectory(m_RootDirectory);
        }
        //=============================================
        var stopwatch = new System.Diagnostics.Stopwatch();
        stopwatch.Start(); // 开始监视代码运行时间

        //这个是当前已打包的资源检查,确保缓存的资源正确
        AssetCheckClear();

        stopwatch.Stop(); // 停止监视
        TimeSpan timespan = stopwatch.Elapsed; // 获取当前实例测量得出的总时间
        string seconds = timespan.TotalSeconds.ToString(); // 总秒数
        Debug.Log("===============AssetCheckClear Cost Seconds  =>" + seconds + "======================");
        //=============================================

        stopwatch = new System.Diagnostics.Stopwatch();
        stopwatch.Start(); // 开始监视代码运行时间

        //这个是当前需要被打包的资源,进行差异化检测
        SetDiffBuilds();

        stopwatch.Stop(); // 停止监视
        timespan = stopwatch.Elapsed; // 获取当前实例测量得出的总时间
        seconds = timespan.TotalSeconds.ToString(); // 总秒数
        Debug.Log("===============SetDiffBuilds Cost Seconds  =>" + seconds + "======================");

        //=============================================

        stopwatch = new System.Diagnostics.Stopwatch();
        stopwatch.Start(); // 开始监视代码运行时间
        Debug.LogError("开始删除 删除无用,冗余,改变的AB包 =>" + mClearList.Count);
        if (mClearList.Count > 0)
        {
            foreach (var item in mClearList)
            {
                if (File.Exists(item.filepath))
                {
                    Debug.LogError("删除==>" + item.filepath);
                    File.Delete(item.filepath);
                }
            }
        }
        Debug.LogError("删除结束");
        stopwatch.Stop(); // 停止监视
        timespan = stopwatch.Elapsed; // 获取当前实例测量得出的总时间
        seconds = timespan.TotalSeconds.ToString(); // 总秒数
        Debug.Log("===============ClearList Cost Seconds  =>" + seconds + "======================");
        //=============================================

        stopwatch = new System.Diagnostics.Stopwatch();
        stopwatch.Start(); // 开始监视代码运行时间

        //生成资源依赖
        MakeIAssetBundleManifest();

        stopwatch.Stop(); // 停止监视
        timespan = stopwatch.Elapsed; // 获取当前实例测量得出的总时间
        seconds = timespan.TotalSeconds.ToString(); // 总秒数
        Debug.Log("===============MakeIAssetBundleManifest Cost Seconds  =>" + seconds + "======================");
        //=============================================
        stopwatch = new System.Diagnostics.Stopwatch();
        stopwatch.Start(); // 开始监视代码运行时间

        //生成资源缓存
        MakeCacheData();

        stopwatch.Stop(); // 停止监视
        timespan = stopwatch.Elapsed; // 获取当前实例测量得出的总时间
        seconds = timespan.TotalSeconds.ToString(); // 总秒数
        Debug.Log("===============MakeCacheData Cost Seconds  =>" + seconds + "======================");
        //=============================================

        stopwatch = new System.Diagnostics.Stopwatch();
        stopwatch.Start(); // 开始监视代码运行时间
        Debug.Log(" 开始打包所有资源 Count => " + mBuildsList.Count);
        mAssetBundleManifest = BuildPipeline.BuildAssetBundles(m_RootDirectory, mBuildsList.ToArray(), BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.ForceRebuildAssetBundle, (BuildTarget)((int)m_TargetPlatform));
        stopwatch.Stop(); // 停止监视
        timespan = stopwatch.Elapsed; // 获取当前实例测量得出的总时间
        seconds = timespan.TotalSeconds.ToString(); // 总秒数
        Debug.Log("===============BuildAssetBundles Cost Seconds  =>" + seconds + "======================");
        //=============================================

        if (mAssetBundleManifest != null)
        {
            string[] arr = mAssetBundleManifest.GetAllAssetBundles();
            Debug.Log(" 打包完成！！完成数量 Count => " + arr.Length);

            //清理打包生产的依赖信息文件
            ClearDpFiles();
            mAssetBundleManifest = null;
        }
        else
        {
            Debug.Log(" mAssetBundleManifest 为空,没有需要打包的东西 ");
        }
        //给打包的AB用MD5重命名
        NameMD5Files();
    }

    /// <summary>
    /// 差异化缓存xml
    /// </summary>
    static void MakeCacheData()
    {
        mStringBuilder.Remove(0, mStringBuilder.Length);

        StringBuilder("<root version='", mVersion, "'>");
        var count = tempBuilds.Count;
        Debug.Log(" MakeCacheData  count " + count);
        foreach (var key in tempBuilds.Keys)
        {
            var build = tempBuilds[key];

            StringBuilder("<asset name='", build.assetBundleName.ToLower(), "'>");

            foreach (var abName in build.assetNames)
            {
                var guid = AssetDatabase.AssetPathToGUID(abName);
                var hash = AssetDatabase.GetAssetDependencyHash(abName).ToString();

                StringBuilder("<sub guid='", guid, "' hash='", hash, "'/>");
            }
            StringBuilder("</asset>");

        }
        StringBuilder("</root>");

        XmlDocument xml = new XmlDocument();
        xml.LoadXml(mStringBuilder.ToString());
        xml.Save(Application.persistentDataPath + "/" + GetPlatformFloder() + "_AssetBundleCache.xml");
        //     xml.Save(m_RootDirectory + "/AssetBundleCache.xml");
    }
    /// <summary>
    /// 生成依赖关系的文件
    /// </summary>
    static void MakeIAssetBundleManifest()
    {
        //var path = m_Assets + "/Manifest.xml";
        //if (File.Exists(path)) {
        //    File.Delete(path);
        //}
        mIManifest = new IAssetBundleManifest();
        var dic = new Dictionary<string, string[]>();
        foreach (var abName in tempBuilds.Keys)
        {
            var build = tempBuilds[abName];
            var dpList = new List<string>();
            foreach (var path in build.assetNames)
            {
                var depends = AssetDatabase.GetDependencies(path);

                foreach (var dpPath in depends)
                {
                    if (dpPath.Contains(".cs") ||
                        dpPath.Contains(".mdb") ||
                        dpPath.Contains(".dll"))
                    {
                        continue;
                    }
                    if (path.Equals(dpPath))
                        continue;

                    if (mPath2ABName.ContainsKey(dpPath))
                    {
                        dpList.Add(mPath2ABName[dpPath]);
                    }
                    else
                    {
                        //   Debug.LogError("错误！！！ 该依赖资源没有加入打包 " + dpPath);
                    }
                }
            }
            dic[abName] = dpList.ToArray();
        }
        mIManifest.SetAsstDpNames(dic);
        mIManifest.Serializate(m_RootDirectory, "Manifest");
        // mIManifest.Serializate(Application.dataPath + "/AssetData", "Manifest");
        // SetAsset("Manifest", path);
    }

    /// <summary>
    /// 打包完成后,复查一遍资源,把manifest文件干掉，把原本的总manifest文件干掉
    /// </summary>  
    static void ClearDpFiles()
    {
        //先把此次打包的所有AB取出
        var strArr = mAssetBundleManifest.GetAllAssetBundles();
        List<string> arr = new List<string>();
        arr.Add(GetPlatformFloder());
        arr.Add(GetPlatformFloder() + ".manifest");

        //做一遍路径
        for (int i = 0; i < strArr.Length; i++)
        {
            arr.Add(strArr[i] + ".manifest");
        }
        for (int i = 0; i < arr.Count; i++)
        {
            string name = arr[i];
            string oriPath = m_RootDirectory + "/" + name;

            if (!File.Exists(oriPath))
            {
                continue;
            }
            Debug.LogError(" 进度: " + (i + 1) + "/" + arr.Count + "删除文件  => " + oriPath);
            File.Delete(oriPath);
        }
        Debug.LogError(" ReCheckFiles 删除文件完成！！! => Delete Count " + arr.Count);
    }
    /// <summary>
    /// 以md5标识命名资源包
    /// </summary>
    static void NameMD5Files()
    {
        var strArr = mIManifest.GetAllAssetBundles();
        //添加依赖表的加密路径，妈蛋不要了，自己做依赖！
        //arr.Add(GetPlatformFloder());
        //arr.Add(GetPlatformFloder() + ".manifest");
        mStringBuilder.Remove(0, mStringBuilder.Length);

        StringBuilder("<root version='", mVersion, "'>");
        for (int i = 0; i < strArr.Length; i++)
        {
            string name = strArr[i];
            if (name == "a811bde74b26b53498b4f6d872b09b6d")
            {
                Debug.LogError(" 找到a811bde74b26b53498b4f6d872b09b6d ！！");
            }

            string oriPath = m_RootDirectory + "/" + strArr[i];

            //如果文件不存在
            if (!File.Exists(oriPath))
            {
                var lowName = name.ToLower();
                if (mCacheAssetBund.ContainsKey(lowName))
                {
                    var cache = mCacheAssetBund[lowName];
                    StringBuilder("<asset name='", lowName, "' md5='", cache.md5, "' size='", cache.size, "' path='", cache.path, "'/>");
                }
                else
                {
                    Debug.LogError("错误！！预重命名路径文件不存在=> " + oriPath);
                    continue;
                }
            }
            else
            {
                byte[] bytes = File.ReadAllBytes(oriPath);
                var hash = GetMd5(bytes);
                int size = bytes.Length / 1024;
                var index = name.LastIndexOf("/");
                //获取Asset下路径
                string path = (index == -1) ? "" : name.Substring(0, index);
                string newPath = (path == "") ? m_RootDirectory + "/" + hash : m_RootDirectory + "/" + path + "/" + hash;
                //        Debug.LogError("重命名 odl Name " + name + " new Path " + newPath);
                StringBuilder("<asset name='", name.ToLower(), "' md5='", hash, "' size='", size, "' path='", path, "'/>");

                File.Move(oriPath, newPath);
            }
        }
        string mf_path = m_RootDirectory + "/Manifest.xml";
        var mf_bytes = File.ReadAllBytes(mf_path);
        var mf_hash = GetMd5(mf_bytes);
        var mf_size = mf_bytes.Length / 1024;
        var mf_newpath = m_RootDirectory + "/" + mf_hash;
        StringBuilder("<asset name='", "Manifest", "' md5='", mf_hash, "' size='", mf_size, "' path='", "", "'/>");
        File.Move(mf_path, mf_newpath);
        StringBuilder("</root>");

        XmlDocument xml = new XmlDocument();
        xml.LoadXml(mStringBuilder.ToString());
        xml.Save(m_RootDirectory + "/ResVersion.xml");
        xml.Save(Application.persistentDataPath + "/ResVersion.xml");

    }
    #endregion

}
