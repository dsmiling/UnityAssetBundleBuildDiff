# UnityAssetBundleBuildDiff
Unity的AssetBundle模块差异化打包工具，主要目的用于节省AssetBundle打包时间
## 主要功能
* 独立的打包编辑窗口
* 检测冗余资源
* 差异化打包,不重复打包未修改资源,只处理新增,修改,删除资源,大大缩短了打包资源的时间
* 独立维护资源的依赖关系
* 资源加载Demo
## 版本信息
	Unity: 2017.1.x +

## 打开方式
	Unity编辑器下选择"Editor/资源打包/BuildAssetBundle"打开窗口，里面包含全部的操作方式。
	
## 界面说明
	Target Platform    选择打包平台
	LastVersion        最新资源版本,自增(只能增不能减)
	OutPut Directory   资源输出路径,默认Application.persistentDataPath,根据打包平台自动生成子目录
## 操作说明
	ReSetABName        生产需要打包资源的List,输出日志可查看所有资源
	CheckAsset         检查资源匹配,删除冗余资源
	CheckBuildDiff     资源差异化判断,输出需要打包资源
	BuildDiff          一键差异化打包
	BuildAll           一键全部打包,删除目标文件夹资源,重新打包

 ## 联系作者
	QQ：5713806
	邮件：5713806@qq.com
  
