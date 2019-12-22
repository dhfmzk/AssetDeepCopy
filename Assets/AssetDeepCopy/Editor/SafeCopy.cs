using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;
using System.Linq;

namespace OrcaAssist {

    public class SafeCopy {
        private GameObject targetTemplate;
        private string texturePath;
        private string prefabPath;


		[MenuItem("Assets/Safe Copy", false, 702)]
        public static void AssetSafeCopy() {
            AssetSafeCopy(Selection.assetGUIDs);
        }

        [MenuItem("Assets/Safe Copy", true)]
        static bool CheckIfMainMethodIsValid() {
            return Selection.assetGUIDs
                .Select(e => AssetDatabase.GUIDToAssetPath(e))
                .All(path => {
                    FileAttributes attr = File.GetAttributes(@path);
                    return !attr.HasFlag(FileAttributes.Directory);
                });
        }

        private static void AssetSafeCopy(string[] assetGUIDs) {
            // Swing
            if (assetGUIDs == null)
                return;

            // Cloning
            var srcPaths    = assetGUIDs.Select(e => AssetDatabase.GUIDToAssetPath(e));
            var targetPaths = assetGUIDs.Select(e => AssetDatabase.GUIDToAssetPath(e)).Select(e => (e, GetFullPathWithSufix(e, " (Clone)")));

            CloneAssets(targetPaths);
            PostProcessingAssets(targetPaths);
        }
        
        public static void CloneAssets(IEnumerable<(string, string)> _clonePaths) {
            foreach (var pair in _clonePaths) {
                FileUtil.CopyFileOrDirectory(pair.Item1, pair.Item2);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void PostProcessingAssets(IEnumerable<(string, string)> _clonePaths) {
            var regex = new Regex(@"{[\w: \-]*, guid: [\w: \-]*((, [\w: \-]*)|)}");

            _clonePaths.Select(e => e.Item2).ToList()
                .ForEach(filePath =>{
                        var lines = File.ReadAllLines(@filePath);
                        var newLines = lines.ToList()
                            .Select(e => {
                                var line = e;
                                if (e.Contains("m_Script")) { return line; }

                                var results = regex.Matches(line);
                                results.Cast<Match>()
                                    .Select(result => result.Value).ToList()
                                    .ForEach(guid => {
                                        line = line.Replace(guid, "{fileID: 0}");
                                    });
                                return line;
                            });
                        File.WriteAllLines(@filePath, newLines);
                });
        }

        private static string GetFullPathWithSufix(string _path, string _sufix) {
            return Path.GetDirectoryName(_path) + "/" + Path.GetFileNameWithoutExtension(_path) + _sufix + Path.GetExtension(_path);
        }
    }
}