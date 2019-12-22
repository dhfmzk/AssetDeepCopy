using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;
using System.Linq;

namespace OrcaAssist {

    public class HardDeepCopy {
        private GameObject targetTemplate;
        private string texturePath;
        private string prefabPath;

        [SerializeField]
        private class DataScheme {
            public string pathAsId;
            public string srcGUID;
            public string dstGUID;
        }

        private class Database {
            public Dictionary<string, string> guidPairs             = new Dictionary<string, string>();
            public Dictionary<string, string> referenceFolders      = new Dictionary<string, string>();
            public Dictionary<string, string> referenceAssets       = new Dictionary<string, string>();
            public Dictionary<string, string> srcGuidLockupTable    = new Dictionary<string, string>();
            public Dictionary<string, string> dstGuidLockupTable    = new Dictionary<string, string>();

            public List<DataScheme> dataTable = new List<DataScheme>();

            public void JoinupLockupTable() {
                dataTable.Clear();
                dataTable.AddRange(
                    dstGuidLockupTable
                        .Select(e => new KeyValuePair<string, string>(e.Key, e.Value.Replace(" (Clone)", "")))
                        .Join(srcGuidLockupTable, o => o.Value, i => i.Value, (o, i) => new DataScheme() { pathAsId = o.Value, srcGUID = i.Key, dstGUID = o.Key })
                        .ToList()
                    );
            }

            public void Clear() {
                guidPairs.Clear();
                referenceFolders.Clear();
                referenceAssets.Clear();
                dataTable.Clear();
            }
        }

        private static Database database = new Database();


		[MenuItem("Assets/Hard Deep Copy", false, 702)]
        public static void DeepCopy() {
            DeepCopy(Selection.assetGUIDs);
        }

        [MenuItem("Assets/Hard Deep Copy", true)]
        static bool CheckIfMainMethodIsValid() {
            return Selection.assetGUIDs
                .Select(e => AssetDatabase.GUIDToAssetPath(e))
                .All(path => {
                    FileAttributes attr = File.GetAttributes(@path);
                    return attr.HasFlag(FileAttributes.Directory);
                });
        }

        private static void DeepCopy(string[] assetGUIDs) {
            // Swing
            if (assetGUIDs == null)
                return;

            database.Clear();

            // Cloning
            var srcPaths = assetGUIDs.Select(x => AssetDatabase.GUIDToAssetPath(x)).ToArray();
            var dstPaths = srcPaths.Select(x => x + " (Clone)").ToArray();
            AddPathsToDatabase(srcPaths, dstPaths);
            CloneAssetsInDatabase();

            GenerateLockupTable(srcPaths, dstPaths);

            PostProcessingAssets(dstPaths);
        }

        public static void AddPathsToDatabase(string[] srcPaths, string[] dstPaths) {
            for (var i = 0; i < srcPaths.Length; ++i) {
                if (AssetDatabase.IsValidFolder(srcPaths[i])) {
                    database.referenceFolders[srcPaths[i]] = dstPaths[i];
                }
                else {
                    database.referenceAssets[srcPaths[i]] = dstPaths[i];
                }
            }
        }
        
        public static void CloneAssetsInDatabase() {
            foreach (var pair in database.referenceFolders) {
                FileUtil.CopyFileOrDirectory(pair.Key, pair.Value);
            }

            foreach (var pair in database.referenceAssets) {
                FileUtil.CopyFileOrDirectory(pair.Key, pair.Value);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        public static void GenerateLockupTable(string[] srcPaths, string[] dstPaths) {
            database.srcGuidLockupTable = srcPaths.ToList()
                .Select(e => Directory.GetFiles(e, "*.*", SearchOption.AllDirectories))
                .SelectMany(e => e)
                .Where(e => !(Path.GetExtension(e) == ".meta"))
                .Where(e => !e.Split('/')[e.Split('/').Length - 1].StartsWith("."))
                .Distinct()
                .ToDictionary(e => AssetDatabase.AssetPathToGUID(e), e => e);

            database.dstGuidLockupTable = dstPaths.ToList()
                .Select(e => Directory.GetFiles(e, "*.*", SearchOption.AllDirectories))
                .SelectMany(e => e)
                .Where(e => !(Path.GetExtension(e) == ".meta"))
                .Where(e => !e.Split('/')[e.Split('/').Length - 1].StartsWith("."))
                .Distinct()
                .ToDictionary(e => AssetDatabase.AssetPathToGUID(e), e => e);

            database.JoinupLockupTable();
        }

        private static void PostProcessingAssets(string[] _targetPathArray) {
            var regex = new Regex(@"guid: [0-9a-z]*(,| })");

            Debug.LogError($"database size : {database.dataTable.Count()}");
            _targetPathArray.ToList()
                .ForEach(rootPath => {
                    Directory.GetFiles(rootPath, "*", SearchOption.AllDirectories)
                        .Where(e => !(Path.GetExtension(e) == ".meta"))
                        .Where(e => !e.Split('/')[e.Split('/').Length - 1].StartsWith("."))
                        .Distinct().ToList()
                        .ForEach(path => {
                            var lines = File.ReadAllLines(@path);
                            var newLines = lines.ToList()
                                .Select(e => {
                                    var line = e;
                                    var results = regex.Matches(line);
                                    results.Cast<Match>()
                                        .Select(result => result.Value.Replace("guid: ", "").Replace(",", "").Replace(" }", ""))
                                        .ToList()
                                        .ForEach(guid => {
                                            line = line.Replace(guid, database.dataTable.FirstOrDefault(data => data.srcGUID.Equals(guid))?.dstGUID ?? guid);
                                        });
                                    return line;
                                });
                            File.WriteAllLines(@path, newLines);
                        });
                });
        }

        private static string GetAssetPath(string _target) {
            var ret = string.Empty;
            if (_target.StartsWith(Application.dataPath)) {
                ret = "Assets" + _target.Substring(Application.dataPath.Length);
            }

            return ret;
        }

        private static string GetFullPath(string _target) {
            var ret = string.Empty;
            if (_target.StartsWith("Assets/")) {
                ret = Application.dataPath + _target.Substring("Assets".Length);
            }

            return ret;
        }
    }
}