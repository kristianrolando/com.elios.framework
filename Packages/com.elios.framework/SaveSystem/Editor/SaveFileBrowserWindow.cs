using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Elios.Framework.SaveSystem.Editor
{
    // Read-only browser for whatever the save system actually wrote to disk. It talks to the
    // file system directly and never calls Save/SaveService/FileSaveStorage, so opening this
    // window cannot initialize or mutate runtime save state. SaveConstants is read only for
    // folder and extension names.
    public class SaveFileBrowserWindow : UnityEditor.EditorWindow
    {
        private const string WindowTitle = "Save File Browser";
        private const string MenuPath = "Tools/Save System/Save File Browser";

        // Mirrors the temp-file marker FileSaveStorage uses for interrupted writes. Kept local
        // so this window stays independent from the storage implementation.
        private const string TempFileMarker = ".tmp_";

        // Header FileSaveStorage prepends to encrypted payloads ("GSAVEC1\0").
        private const string EncryptedMagicText = "GSAVEC1";
        private const int EncryptedMagicLength = 8;

        private const float TreePanelWidth = 320f;
        private const float RowHeight = 18f;
        private const float IndentWidth = 14f;
        private const float FoldoutWidth = 14f;
        private const float RefreshInterval = 1f;
        private const float ToolbarButtonWidth = 90f;
        private const float RootToggleWidth = 180f;
        private const float PreviewWidthPadding = 48f;
        private const float MinPreviewHeight = 60f;

        private const int MaxScanDepth = 8;
        private const int MaxPreviewBytes = 512 * 1024;
        private const int MaxPreviewCharacters = 16000;

        private const float BytesPerKilobyte = 1024f;
        private const float BytesPerMegabyte = 1024f * 1024f;

        private static readonly Color SelectionColor = new Color(0.24f, 0.37f, 0.59f, 0.7f);
        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        [SerializeField] private bool _showFullPersistentData;
        [SerializeField] private bool _autoRefresh = true;
        [SerializeField] private Vector2 _treeScroll;
        [SerializeField] private Vector2 _detailScroll;
        [SerializeField] private string _selectedPath;
        [SerializeField] private List<string> _expandedPaths = new List<string>();

        private readonly Dictionary<string, SaveNode> _nodesByPath = new Dictionary<string, SaveNode>();
        private SaveNode _root;
        private long _treeSignature;
        private double _nextRefreshTime;

        private string _previewPath;
        private string _previewText = string.Empty;
        private string _previewNote = string.Empty;
        private DateTime _previewStamp;
        private long _previewSize;

        private GUIStyle _previewStyle;
        private GUIStyle _pathStyle;

        [MenuItem(MenuPath)]
        private static void Open()
        {
            SaveFileBrowserWindow window = GetWindow<SaveFileBrowserWindow>();
            window.titleContent = new GUIContent(WindowTitle);
            window.Show();
        }

        // ══════════════════════════════════════════════
        // Unity Lifecycle
        // ══════════════════════════════════════════════

        private void OnEnable()
        {
            RefreshTree();
        }

        private void OnFocus()
        {
            RefreshTree();
            Repaint();
        }

        private void Update()
        {
            if (!_autoRefresh)
                return;

            if (EditorApplication.timeSinceStartup < _nextRefreshTime)
                return;

            _nextRefreshTime = EditorApplication.timeSinceStartup + RefreshInterval;

            long previous = _treeSignature;
            RefreshTree();

            if (previous != _treeSignature)
                Repaint();
        }

        private void OnGUI()
        {
            EnsureStyles();

            DrawToolbar();
            DrawPlayModeWarning();

            EditorGUILayout.BeginHorizontal();
            DrawTreePanel();
            DrawDetailPanel();
            EditorGUILayout.EndHorizontal();

            DrawFooter();
        }

        // ══════════════════════════════════════════════
        // Main Logic
        // ══════════════════════════════════════════════

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            bool showFull = GUILayout.Toggle(_showFullPersistentData, "Whole persistentDataPath",
                EditorStyles.toolbarButton, GUILayout.Width(RootToggleWidth));

            if (showFull != _showFullPersistentData)
            {
                _showFullPersistentData = showFull;
                _selectedPath = null;
                RefreshTree();
            }

            _autoRefresh = GUILayout.Toggle(_autoRefresh, "Auto Refresh",
                EditorStyles.toolbarButton, GUILayout.Width(ToolbarButtonWidth));

            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(ToolbarButtonWidth)))
                RefreshTree();

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Reveal Root", EditorStyles.toolbarButton, GUILayout.Width(ToolbarButtonWidth)))
                RevealPath(GetRootPath());

            EditorGUILayout.EndHorizontal();
        }

        private void DrawPlayModeWarning()
        {
            if (!EditorApplication.isPlaying)
                return;

            EditorGUILayout.HelpBox(
                "Play Mode is running. The live save system holds its data in memory and may rewrite deleted files on its next flush.",
                MessageType.Warning);
        }

        private void DrawTreePanel()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(TreePanelWidth));
            _treeScroll = EditorGUILayout.BeginScrollView(_treeScroll, EditorStyles.helpBox);

            if (_root == null)
            {
                EditorGUILayout.LabelField("Folder does not exist yet.", EditorStyles.miniLabel);
                EditorGUILayout.LabelField("It is created on the first save.", EditorStyles.miniLabel);
            }
            else
            {
                DrawNode(_root, 0);
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawNode(SaveNode node, int depth)
        {
            Rect row = EditorGUILayout.GetControlRect(false, RowHeight);

            if (Event.current.type == EventType.Repaint && node.Path == _selectedPath)
                EditorGUI.DrawRect(row, SelectionColor);

            float offset = depth * IndentWidth;
            var foldoutRect = new Rect(row.x + offset, row.y, FoldoutWidth, row.height);
            var labelRect = new Rect(foldoutRect.xMax, row.y, row.width - offset - FoldoutWidth, row.height);

            bool isExpanded = false;
            if (node.IsFolder)
            {
                isExpanded = IsExpanded(node.Path);
                bool nextExpanded = EditorGUI.Foldout(foldoutRect, isExpanded, GUIContent.none);
                if (nextExpanded != isExpanded)
                {
                    SetExpanded(node.Path, nextExpanded);
                    isExpanded = nextExpanded;
                }
            }

            EditorGUI.LabelField(labelRect, BuildNodeLabel(node));

            if (Event.current.type == EventType.MouseDown && Event.current.button == 0 &&
                labelRect.Contains(Event.current.mousePosition))
            {
                Select(node.Path);
                Event.current.Use();
                Repaint();
            }

            if (!node.IsFolder || !isExpanded)
                return;

            for (int i = 0; i < node.Children.Count; i++)
                DrawNode(node.Children[i], depth + 1);
        }

        private void DrawDetailPanel()
        {
            EditorGUILayout.BeginVertical();
            _detailScroll = EditorGUILayout.BeginScrollView(_detailScroll, EditorStyles.helpBox);

            SaveNode node = GetSelectedNode();
            if (node == null)
            {
                EditorGUILayout.LabelField("Select a file or folder on the left.", EditorStyles.miniLabel);
            }
            else if (node.IsFolder)
            {
                DrawFolderDetail(node);
            }
            else
            {
                DrawFileDetail(node);
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawFolderDetail(SaveNode node)
        {
            EditorGUILayout.LabelField(node.Name, EditorStyles.boldLabel);
            EditorGUILayout.SelectableLabel(node.Path, _pathStyle, GUILayout.Height(RowHeight));
            EditorGUILayout.LabelField("Files", node.FileCount.ToString());
            EditorGUILayout.LabelField("Total Size", FormatSize(node.Size));

            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Reveal", GUILayout.Width(ToolbarButtonWidth)))
                RevealPath(node.Path);

            bool isRoot = _root != null && node.Path == _root.Path;
            using (new EditorGUI.DisabledScope(isRoot))
            {
                if (GUILayout.Button("Delete Folder", GUILayout.Width(ToolbarButtonWidth)))
                    ConfirmDeleteFolder(node);
            }

            EditorGUILayout.EndHorizontal();

            if (isRoot)
                EditorGUILayout.HelpBox("The root folder itself is kept. Use Delete All to empty it.", MessageType.Info);
        }

        private void DrawFileDetail(SaveNode node)
        {
            EditorGUILayout.LabelField(node.Name, EditorStyles.boldLabel);
            EditorGUILayout.SelectableLabel(node.Path, _pathStyle, GUILayout.Height(RowHeight));
            EditorGUILayout.LabelField("Size", FormatSize(node.Size));
            EditorGUILayout.LabelField("Modified", node.ModifiedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"));

            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Reveal", GUILayout.Width(ToolbarButtonWidth)))
                RevealPath(node.Path);

            if (GUILayout.Button("Delete File", GUILayout.Width(ToolbarButtonWidth)))
                ConfirmDeleteFile(node);

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
            EnsurePreview(node);

            if (!string.IsNullOrEmpty(_previewNote))
                EditorGUILayout.HelpBox(_previewNote, MessageType.Info);

            if (string.IsNullOrEmpty(_previewText))
                return;

            EditorGUILayout.LabelField("Content", EditorStyles.boldLabel);

            float width = Mathf.Max(position.width - TreePanelWidth - PreviewWidthPadding, PreviewWidthPadding);
            float height = Mathf.Max(_previewStyle.CalcHeight(new GUIContent(_previewText), width), MinPreviewHeight);
            EditorGUILayout.SelectableLabel(_previewText, _previewStyle, GUILayout.Height(height));
        }

        private void DrawFooter()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.LabelField("Root", EditorStyles.miniBoldLabel);
            EditorGUILayout.SelectableLabel(GetRootPath(), _pathStyle, GUILayout.Height(RowHeight));

            EditorGUILayout.BeginHorizontal();

            using (new EditorGUI.DisabledScope(_root == null))
            {
                if (GUILayout.Button("Cleanup .bak / .tmp"))
                    ConfirmCleanupBackupsAndTemp();

                if (GUILayout.Button("Delete All"))
                    ConfirmDeleteAll();
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        // ══════════════════════════════════════════════
        // Internal Helpers
        // ══════════════════════════════════════════════

        private string GetRootPath()
        {
            string persistent = Application.persistentDataPath;
            return _showFullPersistentData
                ? persistent
                : Path.Combine(persistent, SaveConstants.RootFolderName);
        }

        private void RefreshTree()
        {
            _nodesByPath.Clear();
            _treeSignature = 0;

            string root = GetRootPath();
            _root = Directory.Exists(root) ? BuildFolderNode(root, 0) : null;

            if (!string.IsNullOrEmpty(_selectedPath) && !_nodesByPath.ContainsKey(_selectedPath))
                _selectedPath = null;

            if (_root != null && !IsExpanded(_root.Path))
                SetExpanded(_root.Path, true);
        }

        private SaveNode BuildFolderNode(string path, int depth)
        {
            string name = Path.GetFileName(path);
            var node = new SaveNode
            {
                Path = path,
                Name = string.IsNullOrEmpty(name) ? path : name,
                IsFolder = true,
                Children = new List<SaveNode>()
            };

            _nodesByPath[path] = node;
            AccumulateSignature(path, 0, 0);

            if (depth >= MaxScanDepth)
                return node;

            try
            {
                string[] directories = Directory.GetDirectories(path);
                Array.Sort(directories, StringComparer.OrdinalIgnoreCase);
                foreach (string directory in directories)
                {
                    SaveNode child = BuildFolderNode(directory, depth + 1);
                    node.Children.Add(child);
                    node.FileCount += child.FileCount;
                    node.Size += child.Size;
                }

                string[] files = Directory.GetFiles(path);
                Array.Sort(files, StringComparer.OrdinalIgnoreCase);
                foreach (string file in files)
                {
                    SaveNode child = BuildFileNode(file);
                    node.Children.Add(child);
                    node.FileCount++;
                    node.Size += child.Size;
                }
            }
            catch (Exception e)
            {
                EditorDebug.Error($"[SaveFileBrowser] Could not read '{path}': {e.Message}");
            }

            return node;
        }

        private SaveNode BuildFileNode(string path)
        {
            var info = new FileInfo(path);
            var node = new SaveNode
            {
                Path = path,
                Name = info.Name,
                IsFolder = false,
                Size = info.Length,
                ModifiedUtc = info.LastWriteTimeUtc
            };

            _nodesByPath[path] = node;
            AccumulateSignature(path, node.Size, node.ModifiedUtc.Ticks);
            return node;
        }

        private void AccumulateSignature(string path, long size, long ticks)
        {
            unchecked
            {
                _treeSignature = _treeSignature * 31 + path.GetHashCode();
                _treeSignature = _treeSignature * 31 + size;
                _treeSignature = _treeSignature * 31 + ticks;
            }
        }

        private SaveNode GetSelectedNode()
        {
            if (string.IsNullOrEmpty(_selectedPath))
                return null;

            return _nodesByPath.TryGetValue(_selectedPath, out SaveNode node) ? node : null;
        }

        private void Select(string path)
        {
            _selectedPath = path;
            _previewPath = null;
        }

        private bool IsExpanded(string path) => _expandedPaths.Contains(path);

        private void SetExpanded(string path, bool expanded)
        {
            if (expanded)
            {
                if (!_expandedPaths.Contains(path))
                    _expandedPaths.Add(path);
                return;
            }

            _expandedPaths.Remove(path);
        }

        private static string BuildNodeLabel(SaveNode node)
        {
            return node.IsFolder
                ? $"{node.Name}   ({node.FileCount})"
                : $"{node.Name}   {FormatSize(node.Size)}";
        }

        private static string FormatSize(long bytes)
        {
            if (bytes >= BytesPerMegabyte)
                return (bytes / BytesPerMegabyte).ToString("0.00") + " MB";

            if (bytes >= BytesPerKilobyte)
                return (bytes / BytesPerKilobyte).ToString("0.0") + " KB";

            return bytes + " B";
        }

        private static void RevealPath(string path)
        {
            if (!File.Exists(path) && !Directory.Exists(path))
                return;

            EditorUtility.RevealInFinder(path);
        }

        private void EnsureStyles()
        {
            if (_previewStyle == null)
            {
                _previewStyle = new GUIStyle(EditorStyles.textArea) { wordWrap = true };
            }

            if (_pathStyle == null)
            {
                _pathStyle = new GUIStyle(EditorStyles.miniLabel) { wordWrap = false };
            }
        }

        // ══════════════════════════════════════════════
        // Preview
        // ══════════════════════════════════════════════

        private void EnsurePreview(SaveNode node)
        {
            bool isCurrent =
                _previewPath == node.Path &&
                _previewStamp == node.ModifiedUtc &&
                _previewSize == node.Size;

            if (isCurrent)
                return;

            _previewPath = node.Path;
            _previewStamp = node.ModifiedUtc;
            _previewSize = node.Size;
            _previewText = string.Empty;
            _previewNote = string.Empty;

            try
            {
                byte[] buffer;
                int read;

                using (var stream = new FileStream(node.Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    int length = (int)Math.Min(stream.Length, MaxPreviewBytes);
                    buffer = new byte[length];
                    read = stream.Read(buffer, 0, length);
                }

                if (HasEncryptedHeader(buffer, read))
                {
                    _previewNote = "Encrypted payload. This window never holds the decryption key, so the content cannot be shown.";
                    return;
                }

                _previewText = DecodeText(buffer, read);

                if (node.Size > MaxPreviewBytes)
                    _previewNote = $"Preview truncated to the first {FormatSize(MaxPreviewBytes)}.";

                if (_previewText.Length > MaxPreviewCharacters)
                {
                    _previewText = _previewText.Substring(0, MaxPreviewCharacters);
                    _previewNote = "Preview truncated: the file is too large to display in full.";
                }
            }
            catch (Exception e)
            {
                _previewText = string.Empty;
                _previewNote = "Could not read this file: " + e.Message;
            }
        }

        private static bool HasEncryptedHeader(byte[] buffer, int count)
        {
            if (count < EncryptedMagicLength)
                return false;

            for (int i = 0; i < EncryptedMagicText.Length; i++)
            {
                if (buffer[i] != (byte)EncryptedMagicText[i])
                    return false;
            }

            return buffer[EncryptedMagicText.Length] == 0;
        }

        private static string DecodeText(byte[] buffer, int count)
        {
            // Skip a UTF-8 BOM if the file was written with one.
            int offset = (count >= 3 && buffer[0] == 0xEF && buffer[1] == 0xBB && buffer[2] == 0xBF) ? 3 : 0;
            return Utf8NoBom.GetString(buffer, offset, count - offset);
        }

        // ══════════════════════════════════════════════
        // Deletion
        // ══════════════════════════════════════════════

        private void ConfirmDeleteFile(SaveNode node)
        {
            bool confirmed = EditorUtility.DisplayDialog(
                "Delete Save File",
                $"Permanently delete this file?\n\n{node.Name}  ({FormatSize(node.Size)})\n\n{node.Path}",
                "Delete", "Cancel");

            if (!confirmed)
                return;

            TryDeleteFile(node.Path);
            AfterDeletion(node.Path);
        }

        private void ConfirmDeleteFolder(SaveNode node)
        {
            bool confirmed = EditorUtility.DisplayDialog(
                "Delete Save Folder",
                $"Permanently delete this folder and everything inside it?\n\n{node.Name}\n{node.FileCount} file(s), {FormatSize(node.Size)}\n\n{node.Path}",
                "Delete", "Cancel");

            if (!confirmed)
                return;

            TryDeleteDirectory(node.Path);
            AfterDeletion(node.Path);
        }

        private void ConfirmDeleteAll()
        {
            if (_root == null)
                return;

            string rootPath = _root.Path;
            bool confirmed = EditorUtility.DisplayDialog(
                "Delete All Save Data",
                $"Delete every file and folder inside:\n\n{rootPath}\n\n{_root.FileCount} file(s), {FormatSize(_root.Size)}",
                "Continue", "Cancel");

            if (!confirmed)
                return;

            bool doubleConfirmed = EditorUtility.DisplayDialog(
                "Delete All Save Data",
                "This cannot be undone. Delete everything now?",
                "Delete Everything", "Cancel");

            if (!doubleConfirmed)
                return;

            try
            {
                foreach (string directory in Directory.GetDirectories(rootPath))
                    TryDeleteDirectory(directory);

                foreach (string file in Directory.GetFiles(rootPath))
                    TryDeleteFile(file);
            }
            catch (Exception e)
            {
                EditorDebug.Error($"[SaveFileBrowser] Delete All failed for '{rootPath}': {e.Message}");
            }

            AfterDeletion(rootPath);
        }

        private void ConfirmCleanupBackupsAndTemp()
        {
            if (_root == null)
                return;

            List<string> targets = CollectBackupAndTempFiles();
            if (targets.Count == 0)
            {
                EditorUtility.DisplayDialog("Cleanup", "No backup or temp files found.", "OK");
                return;
            }

            bool confirmed = EditorUtility.DisplayDialog(
                "Delete Backup and Temp Files",
                $"Permanently delete {targets.Count} backup/temp file(s)?\n\nSlot files ({SaveConstants.SlotFileExtension}) are kept.",
                "Delete", "Cancel");

            if (!confirmed)
                return;

            for (int i = 0; i < targets.Count; i++)
                TryDeleteFile(targets[i]);

            AfterDeletion(null);
        }

        private List<string> CollectBackupAndTempFiles()
        {
            var targets = new List<string>();
            CollectBackupAndTempFiles(_root, targets);
            return targets;
        }

        private static void CollectBackupAndTempFiles(SaveNode node, List<string> targets)
        {
            if (node == null)
                return;

            if (!node.IsFolder)
            {
                bool isBackup = node.Name.EndsWith(SaveConstants.BackupFileExtension, StringComparison.OrdinalIgnoreCase);
                bool isTemp = node.Name.Contains(TempFileMarker);

                if (isBackup || isTemp)
                    targets.Add(node.Path);

                return;
            }

            for (int i = 0; i < node.Children.Count; i++)
                CollectBackupAndTempFiles(node.Children[i], targets);
        }

        private static void TryDeleteFile(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch (Exception e)
            {
                EditorDebug.Error($"[SaveFileBrowser] Could not delete file '{path}': {e.Message}");
            }
        }

        private static void TryDeleteDirectory(string path)
        {
            try
            {
                if (Directory.Exists(path))
                    Directory.Delete(path, true);
            }
            catch (Exception e)
            {
                EditorDebug.Error($"[SaveFileBrowser] Could not delete folder '{path}': {e.Message}");
            }
        }

        private void AfterDeletion(string deletedPath)
        {
            if (!string.IsNullOrEmpty(deletedPath) && !string.IsNullOrEmpty(_selectedPath) &&
                _selectedPath.StartsWith(deletedPath, StringComparison.Ordinal))
            {
                _selectedPath = null;
            }

            _previewPath = null;
            RefreshTree();
            Repaint();
        }

        private sealed class SaveNode
        {
            public string Path;
            public string Name;
            public bool IsFolder;
            public long Size;
            public int FileCount;
            public DateTime ModifiedUtc;
            public List<SaveNode> Children;
        }
    }
}
