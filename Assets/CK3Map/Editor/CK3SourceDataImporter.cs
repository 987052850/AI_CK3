using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace CK3Map.Editor
{
    public sealed class CK3SourceDataImporter : EditorWindow
    {
        private const string DestinationRoot = "Assets/CK3Map/Data/Source";
        private const string ManifestPath = DestinationRoot + "/source_manifest.csv";

        private static readonly string[] SourceDirectories =
        {
            "game/map_data",
            "game/gfx/map",
            "game/gfx/FX",
            "game/gfx/models/tabletop",
            "game/gfx/models/detail_textures",
            "game/gfx/models/debug",
            "game/gfx/models/artifacts/books",
            "game/gfx/models/artifacts/containers/boxes",
            "game/gfx/models/artifacts/fp2",
            "game/gfx/models/artifacts/misc",
            "game/gfx/models/artifacts/objects",
            "game/gfx/models/artifacts/shields",
            "game/gfx/models/artifacts/weapons",
            "game/gfx/models/court/rooms/western/lights",
            "game/fonts",
            "game/common/landed_titles",
            "game/common/province_terrain",
            "game/common/defines/graphic",
            "game/common/defines/jomini",
            "game/common/culture",
            "game/common/religion",
            "game/history/titles",
            "game/history/provinces",
            "game/history/province_mapping",
            "game/history/characters",
            "game/localization/english",
            "jomini/gfx/FX",
            "jomini/gfx/editor_terrain",
            "jomini/gfx/tools",
            "jomini/gui/map_editor",
            "jomini/common/defines/jomini",
            "clausewitz/gfx/FX",
            "clausewitz/gfx/terrain",
            "clausewitz/gfx/editor_terrain"
        };

        private static readonly string[] SourceFiles =
        {
            // NJominiMap.WATERLEVEL and the complete world extents used by water/terrain.
            "game/common/defines/00_defines.txt",
            // map_table_style_western -> environment_western_table.txt.
            "game/gfx/portraits/environments/castle_interior_01_fire.dds"
        };

        [SerializeField]
        private string ck3GameRoot = string.Empty;

        [MenuItem("CK3 Map/原版数据导入器")]
        private static void OpenWindow()
        {
            CK3SourceDataImporter window = GetWindow<CK3SourceDataImporter>();
            window.titleContent = new GUIContent("CK3 原版数据");
            window.minSize = new Vector2(620.0f, 310.0f);
            window.Show();
        }

        private void OnEnable()
        {
            if (string.IsNullOrWhiteSpace(ck3GameRoot))
            {
                DirectoryInfo projectRoot = Directory.GetParent(Application.dataPath);
                ck3GameRoot = projectRoot != null && projectRoot.Parent != null
                    ? projectRoot.Parent.FullName
                    : string.Empty;
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("CK3 原版数据基线", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "该工具只复制 CK3 原始文件并生成来源清单，不转换未知格式，也不生成 Terrain、边界或河流 Mesh。",
                MessageType.Info);

            EditorGUILayout.Space(8.0f);
            ck3GameRoot = EditorGUILayout.TextField("CK3 游戏根目录", ck3GameRoot);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("选择目录", GUILayout.Width(100.0f)))
            {
                string selected = EditorUtility.OpenFolderPanel("选择 Crusader Kings III 根目录", ck3GameRoot, string.Empty);
                if (!string.IsNullOrEmpty(selected))
                {
                    ck3GameRoot = selected;
                }
            }

            if (GUILayout.Button("验证原版目录", GUILayout.Width(120.0f)))
            {
                ValidateAndReport();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(8.0f);
            EditorGUILayout.LabelField("Unity 目标目录", DestinationRoot);
            EditorGUILayout.LabelField("导入内容", "完整地图数据、FX 依赖、地图美术、历史/行政数据、英文名称与地图编辑器证据");

            GUILayout.FlexibleSpace();
            GUI.enabled = !EditorApplication.isPlayingOrWillChangePlaymode;
            if (GUILayout.Button("导入或同步 CK3 原版数据", GUILayout.Height(38.0f)))
            {
                ImportAll();
            }
            GUI.enabled = true;
        }

        private void ValidateAndReport()
        {
            if (!TryValidateSource(out string error))
            {
                EditorUtility.DisplayDialog("CK3 原版数据", error, "确定");
                return;
            }

            long totalBytes = 0;
            int totalFiles = 0;
            foreach (string relativeDirectory in SourceDirectories)
            {
                string sourceDirectory = Path.Combine(ck3GameRoot, ToPlatformPath(relativeDirectory));
                foreach (string file in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
                {
                    if (file.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                        continue;
                    totalFiles++;
                    totalBytes += new FileInfo(file).Length;
                }
            }

            foreach (string relativeFile in SourceFiles)
            {
                FileInfo info = new FileInfo(Path.Combine(ck3GameRoot, ToPlatformPath(relativeFile)));
                totalFiles++;
                totalBytes += info.Length;
            }

            EditorUtility.DisplayDialog(
                "CK3 原版数据",
                $"验证通过。\n文件数：{totalFiles:N0}\n总大小：{totalBytes / (1024.0 * 1024.0 * 1024.0):F2} GiB",
                "确定");
        }

        private void ImportAll()
        {
            if (!TryValidateSource(out string error))
            {
                EditorUtility.DisplayDialog("CK3 原版数据", error, "确定");
                return;
            }

            string destinationAbsoluteRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "CK3Map/Data/Source"));
            Directory.CreateDirectory(destinationAbsoluteRoot);

            // The tabletop mirror used to preserve Paradox .mesh/.anim/.asset files with
            // their native suffixes. Unity then attempted to deserialize them as Unity
            // assets. Rebuild this deterministic mirror so legacy files cannot survive a
            // synchronization after the storage-suffix fix below.
            ClearTabletopMirror(destinationAbsoluteRoot);

            List<string> sourceFiles = CollectSourceFiles();
            StringBuilder manifest = new StringBuilder();
            manifest.AppendLine("ck3_relative_path,unity_relative_path,size_bytes,last_write_utc");

            try
            {
                AssetDatabase.StartAssetEditing();
                for (int index = 0; index < sourceFiles.Count; index++)
                {
                    string sourceFile = sourceFiles[index];
                    string relative = NormalizePath(Path.GetRelativePath(ck3GameRoot, sourceFile));
                    string destinationRelative = GetDestinationRelativePath(relative);
                    string destinationFile = Path.Combine(destinationAbsoluteRoot, ToPlatformPath(destinationRelative));
                    Directory.CreateDirectory(Path.GetDirectoryName(destinationFile) ?? destinationAbsoluteRoot);

                    EditorUtility.DisplayProgressBar(
                        "导入 CK3 原版数据",
                        relative,
                        sourceFiles.Count == 0 ? 1.0f : (float)index / sourceFiles.Count);

                    File.Copy(sourceFile, destinationFile, true);
                    FileInfo info = new FileInfo(sourceFile);
                    string unityRelative = NormalizePath(DestinationRoot + "/" + destinationRelative);
                    manifest.Append(EscapeCsv(relative)).Append(',')
                        .Append(EscapeCsv(unityRelative)).Append(',')
                        .Append(info.Length).Append(',')
                        .Append(info.LastWriteTimeUtc.ToString("O")).AppendLine();
                }

                string manifestAbsolute = Path.Combine(destinationAbsoluteRoot, "source_manifest.csv");
                File.WriteAllText(manifestAbsolute, manifest.ToString(), new UTF8Encoding(false));
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("CK3 原版数据", "导入失败，详情请查看 Console。", "确定");
                return;
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                EditorUtility.ClearProgressBar();
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            }

            Debug.Log($"CK3 原版数据导入完成：{sourceFiles.Count} 个文件。来源清单：{ManifestPath}");
            EditorUtility.DisplayDialog("CK3 原版数据", $"导入完成：{sourceFiles.Count} 个文件。", "确定");
        }

        private bool TryValidateSource(out string error)
        {
            if (string.IsNullOrWhiteSpace(ck3GameRoot) || !Directory.Exists(ck3GameRoot))
            {
                error = "CK3 游戏根目录不存在。";
                return false;
            }

            foreach (string relativeDirectory in SourceDirectories)
            {
                string absolute = Path.Combine(ck3GameRoot, ToPlatformPath(relativeDirectory));
                if (!Directory.Exists(absolute))
                {
                    error = "缺少 CK3 原版目录：" + relativeDirectory;
                    return false;
                }
            }


            foreach (string relativeFile in SourceFiles)
            {
                string absolute = Path.Combine(ck3GameRoot, ToPlatformPath(relativeFile));
                if (!File.Exists(absolute))
                {
                    error = "缺少 CK3 原版文件：" + relativeFile;
                    return false;
                }
            }

            string defaultMap = Path.Combine(ck3GameRoot, ToPlatformPath("game/map_data/default.map"));
            string terrainShader = Path.Combine(ck3GameRoot, ToPlatformPath("game/gfx/FX/pdxterrain.shader"));
            if (!File.Exists(defaultMap) || !File.Exists(terrainShader))
            {
                error = "所选目录不是完整的 CK3 游戏根目录。";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private List<string> CollectSourceFiles()
        {
            List<string> files = new List<string>();
            foreach (string relativeDirectory in SourceDirectories)
            {
                string absolute = Path.Combine(ck3GameRoot, ToPlatformPath(relativeDirectory));
                foreach (string file in Directory.EnumerateFiles(absolute, "*", SearchOption.AllDirectories))
                {
                    // CK3 ships sidecar files named *.meta. Inside a Unity Assets folder
                    // that suffix belongs exclusively to Unity's asset database and cannot
                    // be mirrored as an ordinary source file.
                    if (!file.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                        files.Add(file);
                }
            }

            foreach (string relativeFile in SourceFiles)
            {
                files.Add(Path.Combine(ck3GameRoot, ToPlatformPath(relativeFile)));
            }

            files.Sort(StringComparer.OrdinalIgnoreCase);
            return files;
        }

        private static string NormalizePath(string path)
        {
            return path.Replace('\\', '/');
        }

        private static string GetDestinationRelativePath(string sourceRelativePath)
        {
            string normalized = NormalizePath(sourceRelativePath);
            if (normalized.StartsWith("game/gfx/models/tabletop/", StringComparison.OrdinalIgnoreCase) &&
                (normalized.EndsWith(".mesh", StringComparison.OrdinalIgnoreCase) ||
                 normalized.EndsWith(".anim", StringComparison.OrdinalIgnoreCase) ||
                 normalized.EndsWith(".asset", StringComparison.OrdinalIgnoreCase)))
            {
                // These are Clausewitz/Jomini resources, not Unity serialized assets.
                // The byte content stays exact; only the carrier suffix changes so Unity
                // imports it as source evidence instead of trying to deserialize it.
                return normalized + ".ck3source";
            }

            if (normalized.EndsWith(".shader", StringComparison.OrdinalIgnoreCase))
            {
                // Paradox .shader files are source evidence for the Clausewitz/Jomini shader
                // language, not Unity ShaderLab files. Preserve their exact bytes while keeping
                // Unity's Shader importer from compiling an incompatible language.
                return normalized + ".ck3source";
            }

            if (normalized.StartsWith("game/gfx/map/borders/", StringComparison.OrdinalIgnoreCase) &&
                normalized.EndsWith(".dds", StringComparison.OrdinalIgnoreCase))
            {
                // CK3 accepts BC1/BC3 border brushes such as 85x86. Unity's built-in DDS importer
                // rejects non-4x4 block-aligned dimensions, so preserve the original bytes as a
                // source file and let the dedicated Editor conversion stage decode them later.
                return normalized + ".ck3source";
            }

            return normalized;
        }

        private static void ClearTabletopMirror(string destinationAbsoluteRoot)
        {
            string tabletop = Path.GetFullPath(Path.Combine(
                destinationAbsoluteRoot,
                ToPlatformPath("game/gfx/models/tabletop")));
            string expectedPrefix = destinationAbsoluteRoot.TrimEnd(Path.DirectorySeparatorChar) +
                                    Path.DirectorySeparatorChar;
            if (!tabletop.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("拒绝清理 Source 根目录之外的 tabletop 镜像。");
            if (Directory.Exists(tabletop))
                Directory.Delete(tabletop, true);
            string unityMeta = tabletop + ".meta";
            if (File.Exists(unityMeta))
                File.Delete(unityMeta);
        }

        private static string ToPlatformPath(string path)
        {
            return path.Replace('/', Path.DirectorySeparatorChar);
        }

        private static string EscapeCsv(string value)
        {
            if (value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) < 0)
            {
                return value;
            }

            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
    }
}
