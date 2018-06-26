using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using UnityEngine;
using UnityEngine.UI;


public class AssetLoad : MonoBehaviour {

    public class VersionAssetData
    {
        public string name;
        public string md5;
        public int size;
        public string path;
    }
    public class AssetsBundleData
    {
        public string name;//资源名
        public int depIndex = 0;//被依赖索引计数器,卸载资源的时候，查找资源依赖的资源，依赖的会进行计数器-1，到0就就卸载这个依赖的资源
        public AssetBundle ab = null;//ab包
    }
    public delegate void LoadComplete(Object o, object obj);

    /// <summary>
    /// 根据资源名，得到md5码和路径
    /// </summary>
    private Dictionary<string, VersionAssetData> mNameMD5Dic = null;

    /// <summary>
    /// ab资源计数器集合
    /// </summary>
    private Dictionary<string, AssetsBundleData> mAssetsABDic = null;

    /// <summary>
    /// AssetBundleManifest 文件 记录ab的信息
    /// </summary>
    private IAssetBundleManifest mMainManifest = null;

    /// <summary>
    /// 本地资源目录路径
    /// </summary>
    private string mLocalResRootPath = ""; //Application.dataPath + "/AssetBundle/StandaloneWindow";

    /// <summary>
    /// 版本信息比对记录信息
    /// </summary>
    private string RVL = "ResVersion.xml";

    /// <summary>
    /// 资源维护依赖关系的xml
    /// </summary>
    private string Manifest = "Manifest.xml";

    public static AssetLoad Instance;
    // Use this for initialization

    void Awake () {
        Instance = this;

        mLocalResRootPath =  Application.persistentDataPath + "/AssetBundle/StandaloneWindow";
        mAssetsABDic = new Dictionary<string, AssetsBundleData>();
        StartCoroutine(SetMD5AssetsList());
    }
    private void Start()
    {
    }
    // Update is called once per frame
    void Update () {
		
	}

    IEnumerator SetMD5AssetsList()
    {
        mNameMD5Dic = new Dictionary<string, VersionAssetData>();
        string localPath = mLocalResRootPath + "/" + RVL;
#if UNITY_STANDALONE_WIN || UNITY_EDITOR
        localPath = mLocalResRootPath + "/" + RVL;
#elif UNITY_ANDROID
        localPath = mLocalResRootPath + "/" + RVL;
#elif UNITY_IPHONE
        localPath = mLocalResRootPath + "/" + RVL;
#endif
        //WWW localWWW = new WWW("File://" + localPath);

        if (!File.Exists(localPath))
        {
            Debug.LogError("SetMD5AssetsList file not exist:" + localPath);
        }
        else
        {
            using (FileStream fs = new FileStream(localPath, FileMode.Open)) {

            int fsLen = (int)fs.Length;
            byte[] heByte = new byte[fsLen];
            //int r = fs.Read(heByte, 0, heByte.Length);
            fs.Read(heByte, 0, heByte.Length);
            string text = System.Text.Encoding.UTF8.GetString(heByte);

            XmlDocument document = new XmlDocument();
            document.LoadXml(text);
            XmlNode root = document.SelectSingleNode("root");
            foreach (XmlNode _node in root.ChildNodes)
            {
                XmlElement node = _node as XmlElement;
                if (node == null) { continue; }
                if (node.Name.Equals("asset"))
                {
                    VersionAssetData loadAsset = new VersionAssetData();
                    loadAsset.name = node.GetAttribute("name");
                    loadAsset.md5 = node.GetAttribute("md5");
                    loadAsset.size = System.Convert.ToInt32(node.GetAttribute("size"));
                    loadAsset.path = node.GetAttribute("path");
                    if (!mNameMD5Dic.ContainsKey(loadAsset.name))
                    {
                        mNameMD5Dic.Add(loadAsset.name, loadAsset);
                    }
                }
            }
                Debug.Log("mNameMD5Dic Count " + mNameMD5Dic.Count);
            yield return StartCoroutine(WWWLoadMainAssets());
            }
        }
    }

    void LoadAssets(string path, LoadComplete cb, object obj = null)
    {
        if (mAssetsABDic.ContainsKey(path))
        {
            AssetsBundleData abd = mAssetsABDic[path];
            abd.depIndex += 1;
            UnityEngine.Object o = GetObj(abd.ab, path);
            if (cb != null)
            {
                cb.Invoke(o, obj);
            }
        }
        else
        {
            SynLoadAssets(path, false, obj, cb);
        }

    }

    /// <summary>
    /// 真正加载的地方
    /// </summary>
    /// <param name="path"></param>
    /// <param name="dp"></param>
    /// <param name="cb"></param>
    /// <param name="obj"></param>
    /// <returns></returns>

    void SynLoadAssets(string path, bool dp, object obj, LoadComplete cb)
    {
        if (mNameMD5Dic.ContainsKey(path))
        {
            VersionAssetData vad = mNameMD5Dic[path];
            string url = GetAssetsFilePath(vad.path, vad.md5);
            AssetBundle ab = null;
            if (!dp)
            {
                // 作为主资源加载的
                AssetsBundleData abd = null;
                if (mAssetsABDic.ContainsKey(path))//已经被加载过 计数器自增
                {
                    abd = mAssetsABDic[path];
                    abd.depIndex += 1;
                    ab = abd.ab;
                }
                else
                {
                    // 新加载的
                    ab = AssetBundle.LoadFromFile(url);
                    abd = new AssetsBundleData();
                    abd.name = path;
                    abd.depIndex = 1;
                    abd.ab = ab;

                    mAssetsABDic.Add(path, abd);
                }
                string[] dpAbs = mMainManifest.GetAllDependencies(path);//取这个资源的所有依赖资源
                #region
                if (dpAbs != null && dpAbs.Length == 0)//没有依赖 获取资源
                {
                    UnityEngine.Object o = GetObj(ab, path);
                    if (cb != null)
                    {
                        cb.Invoke(o, obj);
                    }
    
                }
                #endregion
                #region
                else
                {
                    //存在依赖资源
                    #region    
                    for (int i = 0; i < dpAbs.Length; i++)
                    {
                        if (mAssetsABDic.ContainsKey(dpAbs[i]))
                        {
                            // 依赖计数器中已经存在了 计数器+1
                            abd = mAssetsABDic[dpAbs[i]];
                            abd.depIndex += 1;
                        }
                        else
                        {
                            SynLoadAssets(dpAbs[i], true, obj,cb);
                        }
                    }
                    //依赖都加载完毕了 再看主资源
                    UnityEngine.Object o = GetObj(ab, path);

                    if (cb != null)
                    {
                        cb.Invoke(o, obj);
                    }
                    #endregion
                }
            }
            #endregion
            else
            {
                // 作为被依赖资源下载
                AssetsBundleData abd = null;
                if (mAssetsABDic.ContainsKey(path))
                {
                    abd = mAssetsABDic[path];
                    abd.depIndex += 1;
                }
                else//被依赖的 缓存
                {
                    ab = AssetBundle.LoadFromFile(url);
                    abd = new AssetsBundleData();
                    abd.name = path;
                    abd.depIndex = 1;
                    abd.ab = ab;

                    mAssetsABDic.Add(path, abd);
                }
            }
        }
        else
        {
            Debug.LogError("cannot find:" + path);
        }
    }
    /// <summary>
    /// 取出一个assets
    /// </summary>
    /// <param name="ab"></param>
    /// <param name="path"></param>
    /// <returns></returns>
    private UnityEngine.Object GetObj(AssetBundle ab, string path)
    {
        string name = GetABNameByPath(path);
        Debug.Log("name " + name);
       // return ab.LoadAsset<Sprite>(name);
        return ab.LoadAsset(name);

    }

    /// <summary>
    /// 加载各个平台的总记录的ab的 manifest信息 这是加载ab的前提
    /// </summary>
    /// <returns></returns>
    IEnumerator WWWLoadMainAssets()
    {
        VersionAssetData vad = mNameMD5Dic["Manifest"];

        string url = StringBuilder(GetPlantFormat(), "/", vad.md5);

        //只提供了测试加载,android不能通过File加载需要通过www方式
#if UNITY_IPHONE || UNITY_STANDALONE_WIN || UNITY_EDITOR
        if (!File.Exists(url))
        {
            Debug.LogError("Manifest file not exist:" + url);
        }
        else
        {
            var fs = File.OpenRead(url);

            int fsLen = (int)fs.Length;
            byte[] heByte = new byte[fsLen];
            //int r = fs.Read(heByte, 0, heByte.Length);
            fs.Read(heByte, 0, heByte.Length);
            string text = System.Text.Encoding.UTF8.GetString(heByte);
            mMainManifest = IAssetBundleManifest.DeSerializate(text);

            fs.Dispose();
        }

#endif
        yield return null;

        LoadAssets("prefab/cube", (o, obj) =>
        {
            Debug.Log(" 加载资源成功!!!");
            GameObject.Instantiate(o);
        });

    }

    /// <summary>
    /// 获取md5对应的真实名字
    /// </summary>
    /// <param name="path"></param>
    /// <param name="md5"></param>
    /// <returns></returns>
    private string GetAssetsFilePath(string path, string md5)
    {
        string url = string.IsNullOrEmpty(path)? StringBuilder(mLocalResRootPath, "/", md5) 
            : StringBuilder(mLocalResRootPath, "/", path, "/", md5);

        if (File.Exists(url))
        {
            return url;
        }
        else
        {
            path = string.IsNullOrEmpty(path)? "/": StringBuilder("/", path, "/");
            url = StringBuilder(GetPlantFormat(), "/", path, "/", md5);

            return url;
        }
    }
    private string GetPlantFormat() {
        var path = "";
#if UNITY_STANDALONE_WIN || UNITY_EDITOR
        path = StringBuilder(Application.persistentDataPath, "/AssetBundle/StandaloneWindow");
#elif UNITY_IPHONE
        mLocalResRootPath =  StringBuilder("File:///", Application.dataPath, "/Raw")
#elif UNITY_ANDROID

#endif
       return path;
    }

    private static System.Text.StringBuilder sb = new System.Text.StringBuilder();

    public static string StringBuilder(params object[] args)
    {
        sb.Remove(0, sb.Length);

        for (int i = 0; i < args.Length; ++i)
        {
            sb.Append(args[i]);
        }

        return sb.ToString();
    }

    /// <summary>
    /// 根据路径获取ab名字
    /// </summary>
    /// <param name="path"></param>
    /// <returns></returns>
    public static string GetABNameByPath(string path)
    {
        int index = path.LastIndexOf("/");
        string name = path.Substring(index + 1);
        return name;
    }

}
