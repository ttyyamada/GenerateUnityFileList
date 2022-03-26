using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace GenerateFileList
{
    /// <summary>
    /// ファイルのリストを作成する拡張
    /// </summary>
    public class GenerateFileList : EditorWindow
    {
        private GenerateFileList _currentWindow;
        private DefaultAsset _searchFolder;
        private DefaultAsset _createTargetFolder;
        
        // 無視する拡張子のリスト
        private readonly List<string> _ignoreExtensionList = new List<string> {".meta", ".txt", ".DS_Store", ".cs"};
        
        // 名前のリスト
        private readonly List<string> _targetNameList = new List<string>();

        // サブフォルダもリストに含めるか
        private bool _isIncludeSubFolder = true;
        // アウトプットするファイル名
        private string _outputFileName = "FileNameList";
        // 名前空間名
        private string _outputNameSpaceName = "GenerateFileList";
        // クラス名
        private string _outputClassName = "FileNames";
        private string _codeTemplate;
        
        // 置き換える対象のリスト
        private static readonly string CodeTemplateFileName = "NameListsTemplate";
        private static readonly string NameSpaceReplaceTarget = "#NAMESPACE#";
        private static readonly string ClassNameReplaceTarget = "#CLASSNAME#";
        private static readonly string FileNameReplaceTarget = "#FILENAME#";
        private static readonly string FileNameValueReplaceTarget = "#filename#";

        private static readonly string FileListTemplate =
            "        public static readonly string #FILENAME# = \"#filename#\";";

        private string _nameLineTemplate;

        [MenuItem("Editor/GenerateFileNameList")]
        private static void CreateWindow()
        {
            GetWindow<GenerateFileList>(nameof(GenerateFileList));
        }

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(0, 0, _currentWindow.position.size.x, _currentWindow.position.size.y));
            if (_codeTemplate == null)
            {
                EditorGUILayout.HelpBox("コードテンプレートの読み込みエラー", MessageType.Error);
                GUILayout.EndArea();
                return;
            }
            _searchFolder = (DefaultAsset)EditorGUILayout.ObjectField("検索フォルダ", _searchFolder, typeof(DefaultAsset), false);
            if (_searchFolder == null)
            {
                EditorGUILayout.HelpBox("NameListを作成するフォルダを選択してください", MessageType.Info);
            }
            else
            {
                _outputFileName = EditorGUILayout.TextField("ファイル名(拡張子は無し)", _outputFileName);
                _outputNameSpaceName = EditorGUILayout.TextField("namaspace名", _outputNameSpaceName);
                _outputClassName = EditorGUILayout.TextField("クラス名", _outputClassName);
                _createTargetFolder = (DefaultAsset)EditorGUILayout.ObjectField("生成先フォルダ", _createTargetFolder, typeof(DefaultAsset), false);
                _isIncludeSubFolder = EditorGUILayout.ToggleLeft("サブフォルダを含める", _isIncludeSubFolder);
                if (GUILayout.Button("生成！"))
                {
                    CreateNameList();
                }
            }
            
            GUILayout.EndArea();
        }

        private void CreateNameList()
        {
            var searchFolder = AssetDatabase.GetAssetOrScenePath(_searchFolder);
            var dir = new DirectoryInfo(searchFolder);
            var searchOption = _isIncludeSubFolder ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            var info = dir.GetFiles("*", searchOption);
            _targetNameList.Clear();
            foreach (var file in info)
            {
                // 無視拡張子チェック
                if(IsContainsIgnoreExtension(file.Name)) continue;
                // プロジェクトのルートパス意外を削除
                var dataPath = Application.dataPath.Split('/');
                var assetPath = "";
                for (int i = 0; i < dataPath.Length - 1; i++)
                {
                    assetPath += dataPath[i] + "/";
                }
                var targetFile = file.FullName.Replace(assetPath, "").Replace($"{AssetDatabase.GetAssetOrScenePath(_searchFolder)}/", "");
                if(_targetNameList.Contains(targetFile)) continue;
                _targetNameList.Add(targetFile);
            }
            GenerateFileNameClass();
        }

        /// <summary>
        /// 無視拡張子リストに含まれている拡張子かを判定する
        /// </summary>
        private bool IsContainsIgnoreExtension(string fileName)
        {
            foreach (var ignoreExtension in _ignoreExtensionList)
            {
                if (Path.GetExtension(fileName).Equals(ignoreExtension))
                {
                    return true;
                }
            }

            return false;
        }

        private void OnEnable()
        {
            _currentWindow = this;
            _codeTemplate = Resources.Load<TextAsset>(CodeTemplateFileName).text;
        }

        /// <summary>
        /// ファイルリストの生成をする
        /// </summary>
        private void GenerateFileNameClass()
        {
            // コードテンプレートから文字を置き換える
            var generateClassText = ReplaceCodeTemplate();
            var listString = string.Empty;
            foreach (var targetName in _targetNameList)
            {
                // 拡張子を除くファイル名を取得
                var nameWithoutExtension = Path.GetFileNameWithoutExtension(targetName);
                
                // ファイルの空チェック
                if(string.IsNullOrEmpty(nameWithoutExtension) || string.IsNullOrWhiteSpace(nameWithoutExtension) || string.IsNullOrEmpty(Path.GetFileName(nameWithoutExtension))) continue;
                // 2行目から改行を入れる
                if (!string.IsNullOrEmpty(listString))
                {
                    listString += "\n";
                }
                // 変数名に使うものは空白を削除
                nameWithoutExtension = nameWithoutExtension.Replace(" ", "");
                var newText = FileListTemplate.Replace(FileNameReplaceTarget, nameWithoutExtension)
                    .Replace(FileNameValueReplaceTarget, targetName);
                listString += newText;
            }

            // リスト部分を置き換える
            generateClassText = generateClassText.Replace(FileListTemplate, listString);

            // ファイルを生成する
            File.WriteAllText($"{AssetDatabase.GetAssetOrScenePath(_createTargetFolder)}/{_outputFileName}.cs" , generateClassText);
            AssetDatabase.Refresh();
        }

        /// <summary>
        /// テンプレートからnamespaceとクラス名を置き換える
        /// </summary>
        private string ReplaceCodeTemplate()
        {
            var generateClassText = _codeTemplate;
            generateClassText = generateClassText.Replace(NameSpaceReplaceTarget, _outputNameSpaceName);
            generateClassText = generateClassText.Replace(ClassNameReplaceTarget, _outputClassName);

            return generateClassText;
        }
    }
}