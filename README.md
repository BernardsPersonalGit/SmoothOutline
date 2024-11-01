# SmoothOutline
======================================================================================================================================
在Unity引擎中将模型（FBX格式）的硬边法线平滑化，将平滑化后的法线（经八面体压缩算法转换为Vector2）保存在模型UV中，并输出为新的FBX模型文件。

展示场景：Assets/Scenes/SampleScene.unity
主要代码：Assets/Editor/FbxMeshNormalProcessor.cs

Unity版本：2020.3.40f1c1
渲染管线：URP

必须依赖Package：com.autodesk.fbx

======================================================================================================================================
UnityエンジンでFBX模型のハードエッジをスムーズ化する。
その後に八面体マッピング(Octahedral mapping)でスムーズ化したエッジの法線(Vector3)をVector2に転換し、元模型ファイルの第二UVチャンネルに保存する。
最後には処理後の模型ファイルを新規FBX模型ファイルとして出力する。

サンプルシーン：Assets/Scenes/SampleScene.unity
メインコード：Assets/Editor/FbxMeshNormalProcessor.cs

Unityバージョン：2020.3.40f1c1
レンダーパイプライン：URP

必須Package：com.autodesk.fbx
