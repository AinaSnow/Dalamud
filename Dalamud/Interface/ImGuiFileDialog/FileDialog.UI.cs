using System.Collections.Generic;
using System.IO;
using System.Numerics;

using ImGuiNET;

namespace Dalamud.Interface.ImGuiFileDialog
{
    /// <summary>
    /// A file or folder picker.
    /// </summary>
    public partial class FileDialog
    {
        private static Vector4 pathDecompColor = new(0.188f, 0.188f, 0.2f, 1f);
        private static Vector4 selectedTextColor = new(1.00000000000f, 0.33333333333f, 0.33333333333f, 1f);
        private static Vector4 dirTextColor = new(0.54509803922f, 0.91372549020f, 0.99215686275f, 1f);
        private static Vector4 codeTextColor = new(0.94509803922f, 0.98039215686f, 0.54901960784f, 1f);
        private static Vector4 miscTextColor = new(1.00000000000f, 0.47450980392f, 0.77647058824f, 1f);
        private static Vector4 imageTextColor = new(0.31372549020f, 0.98039215686f, 0.48235294118f, 1f);
        private static Vector4 standardTextColor = new(1f);

        private static Dictionary<string, IconColorItem> iconMap;

        /// <summary>
        /// Draws the dialog.
        /// </summary>
        /// <returns>Whether a selection or cancel action was performed.</returns>
        public bool Draw()
        {
            if (!this.visible) return false;

            var res = false;
            var name = this.title + "###" + this.id;

            bool windowVisible;
            this.isOk = false;
            this.wantsToQuit = false;

            this.ResetEvents();

            ImGui.SetNextWindowSize(new Vector2(800, 500), ImGuiCond.FirstUseEver);

            if (this.isModal && !this.okResultToConfirm)
            {
                ImGui.OpenPopup(name);
                windowVisible = ImGui.BeginPopupModal(name, ref this.visible, ImGuiWindowFlags.NoScrollbar);
            }
            else
            {
                windowVisible = ImGui.Begin(name, ref this.visible, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoNav);
            }

            bool wasClosed = false;
            if (windowVisible)
            {
                if (!this.visible)
                { // window closed
                    this.isOk = false;
                    wasClosed = true;
                }
                else
                {
                    if (this.selectedFilter.Empty() && (this.filters.Count > 0))
                    {
                        this.selectedFilter = this.filters[0];
                    }

                    if (this.files.Count == 0)
                    {
                        if (!string.IsNullOrEmpty(this.defaultFileName))
                        {
                            this.SetDefaultFileName();
                            this.SetSelectedFilterWithExt(this.defaultExtension);
                        }
                        else if (this.IsDirectoryMode())
                        {
                            this.SetDefaultFileName();
                        }

                        this.ScanDir(this.currentPath);
                    }

                    this.DrawHeader();
                    this.DrawContent();
                    res = this.DrawFooter();
                }

                if (this.isModal && !this.okResultToConfirm)
                {
                    ImGui.EndPopup();
                }
            }

            if (!this.isModal || this.okResultToConfirm)
            {
                ImGui.End();
            }

            return wasClosed || this.ConfirmOrOpenOverWriteFileDialogIfNeeded(res);
        }

        private static void AddToIconMap(string[] extensions, char icon, Vector4 color)
        {
            foreach (var ext in extensions)
            {
                iconMap[ext] = new IconColorItem
                {
                    Icon = icon,
                    Color = color,
                };
            }
        }

        private static IconColorItem GetIcon(string ext)
        {
            if (iconMap == null)
            {
                iconMap = new();
                AddToIconMap(new[] { "mp4", "gif", "mov", "avi" }, (char)FontAwesomeIcon.FileVideo, miscTextColor);
                AddToIconMap(new[] { "pdf" }, (char)FontAwesomeIcon.FilePdf, miscTextColor);
                AddToIconMap(new[] { "png", "jpg", "jpeg", "tiff" }, (char)FontAwesomeIcon.FileImage, imageTextColor);
                AddToIconMap(new[] { "cs", "json", "cpp", "h", "py", "xml", "yaml", "js", "html", "css", "ts", "java" }, (char)FontAwesomeIcon.FileCode, codeTextColor);
                AddToIconMap(new[] { "txt", "md" }, (char)FontAwesomeIcon.FileAlt, standardTextColor);
                AddToIconMap(new[] { "zip", "7z", "gz", "tar" }, (char)FontAwesomeIcon.FileArchive, miscTextColor);
                AddToIconMap(new[] { "mp3", "m4a", "ogg", "wav" }, (char)FontAwesomeIcon.FileAudio, miscTextColor);
                AddToIconMap(new[] { "csv" }, (char)FontAwesomeIcon.FileCsv, miscTextColor);
            }

            return iconMap.TryGetValue(ext.ToLower(), out var icon) ? icon : new IconColorItem
            {
                Icon = (char)FontAwesomeIcon.File,
                Color = standardTextColor,
            };
        }

        private void DrawHeader()
        {
            this.DrawPathComposer();

            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 2);
            ImGui.Separator();
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 2);

            this.DrawSearchBar();
        }

        private void DrawPathComposer()
        {
            ImGui.PushFont(UiBuilder.IconFont);
            if (ImGui.Button($"{(this.pathInputActivated ? (char)FontAwesomeIcon.Times : (char)FontAwesomeIcon.Edit)}"))
            {
                this.pathInputActivated = !this.pathInputActivated;
            }

            ImGui.PopFont();

            ImGui.SameLine();

            if (this.pathDecomposition.Count > 0)
            {
                ImGui.SameLine();

                if (this.pathInputActivated)
                {
                    ImGui.PushItemWidth(ImGui.GetContentRegionAvail().X);
                    ImGui.InputText("##pathedit", ref this.pathInputBuffer, 255);
                    ImGui.PopItemWidth();
                }
                else
                {
                    for (var idx = 0; idx < this.pathDecomposition.Count; idx++)
                    {
                        if (idx > 0)
                        {
                            ImGui.SameLine();
                            ImGui.SetCursorPosX(ImGui.GetCursorPosX() - 3);
                        }

                        ImGui.PushID(idx);
                        ImGui.PushStyleColor(ImGuiCol.Button, pathDecompColor);
                        var click = ImGui.Button(this.pathDecomposition[idx]);
                        ImGui.PopStyleColor();
                        ImGui.PopID();

                        if (click)
                        {
                            this.currentPath = ComposeNewPath(this.pathDecomposition.GetRange(0, idx + 1));
                            this.pathClicked = true;
                            break;
                        }

                        if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
                        {
                            this.pathInputBuffer = ComposeNewPath(this.pathDecomposition.GetRange(0, idx + 1));
                            this.pathInputActivated = true;
                            break;
                        }
                    }
                }
            }
        }

        private void DrawSearchBar()
        {
            ImGui.PushFont(UiBuilder.IconFont);
            if (ImGui.Button($"{(char)FontAwesomeIcon.Home}"))
            {
                this.SetPath(".");
            }

            ImGui.PopFont();

            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Reset to current directory");
            }

            ImGui.SameLine();

            this.DrawDirectoryCreation();

            if (!this.createDirectoryMode)
            {
                ImGui.SameLine();
                ImGui.Text("Search :");
                ImGui.SameLine();
                ImGui.PushItemWidth(ImGui.GetContentRegionAvail().X);
                var edited = ImGui.InputText("##InputImGuiFileDialogSearchField", ref this.searchBuffer, 255);
                ImGui.PopItemWidth();
                if (edited)
                {
                    this.ApplyFilteringOnFileList();
                }
            }
        }

        private void DrawDirectoryCreation()
        {
            if (this.flags.HasFlag(ImGuiFileDialogFlags.DisableCreateDirectoryButton)) return;

            ImGui.PushFont(UiBuilder.IconFont);
            if (ImGui.Button($"{(char)FontAwesomeIcon.FolderPlus}"))
            {
                if (!this.createDirectoryMode)
                {
                    this.createDirectoryMode = true;
                    this.createDirectoryBuffer = string.Empty;
                }
            }

            ImGui.PopFont();

            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Create Directory");
            }

            if (this.createDirectoryMode)
            {
                ImGui.SameLine();
                ImGui.Text("New Directory Name");

                ImGui.SameLine();
                ImGui.PushItemWidth(ImGui.GetContentRegionAvail().X - 100f);
                ImGui.InputText("##DirectoryFileName", ref this.createDirectoryBuffer, 255);
                ImGui.PopItemWidth();

                ImGui.SameLine();

                if (ImGui.Button("Ok"))
                {
                    if (this.CreateDir(this.createDirectoryBuffer))
                    {
                        this.SetPath(Path.Combine(this.currentPath, this.createDirectoryBuffer));
                    }

                    this.createDirectoryMode = false;
                }

                ImGui.SameLine();

                if (ImGui.Button("Cancel"))
                {
                    this.createDirectoryMode = false;
                }
            }
        }

        private void DrawContent()
        {
            var size = ImGui.GetContentRegionAvail() - new Vector2(0, this.footerHeight);

            if (!this.flags.HasFlag(ImGuiFileDialogFlags.HideSideBar))
            {
                ImGui.BeginChild("##FileDialog_ColumnChild", size);
                ImGui.Columns(2, "##FileDialog_Columns");

                this.DrawSideBar(new Vector2(150, size.Y));

                ImGui.SetColumnWidth(0, 150);
                ImGui.NextColumn();

                this.DrawFileListView(size - new Vector2(160, 0));

                ImGui.Columns(1);
                ImGui.EndChild();
            }
            else
            {
                this.DrawFileListView(size);
            }
        }

        private void DrawSideBar(Vector2 size)
        {
            ImGui.BeginChild("##FileDialog_SideBar", size);

            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 5);

            foreach (var drive in this.drives)
            {
                ImGui.PushFont(UiBuilder.IconFont);
                if (ImGui.Selectable($"{drive.Icon}##{drive.Text}", drive.Text == this.selectedSideBar))
                {
                    this.SetPath(drive.Location);
                    this.selectedSideBar = drive.Text;
                }

                ImGui.PopFont();

                ImGui.SameLine(25);

                ImGui.Text(drive.Text);
            }

            foreach (var quick in this.quickAccess)
            {
                ImGui.PushFont(UiBuilder.IconFont);
                if (ImGui.Selectable($"{quick.Icon}##{quick.Text}", quick.Text == this.selectedSideBar))
                {
                    this.SetPath(quick.Location);
                    this.selectedSideBar = quick.Text;
                }

                ImGui.PopFont();

                ImGui.SameLine(25);

                ImGui.Text(quick.Text);
            }

            ImGui.EndChild();
        }

        private unsafe void DrawFileListView(Vector2 size)
        {
            ImGui.BeginChild("##FileDialog_FileList", size);

            var tableFlags = ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.RowBg | ImGuiTableFlags.Hideable | ImGuiTableFlags.ScrollY | ImGuiTableFlags.NoHostExtendX;
            if (ImGui.BeginTable("##FileTable", 4, tableFlags, size))
            {
                ImGui.TableSetupScrollFreeze(0, 1);

                var hideType = this.flags.HasFlag(ImGuiFileDialogFlags.HideColumnType);
                var hideSize = this.flags.HasFlag(ImGuiFileDialogFlags.HideColumnSize);
                var hideDate = this.flags.HasFlag(ImGuiFileDialogFlags.HideColumnDate);

                ImGui.TableSetupColumn(" File Name", ImGuiTableColumnFlags.WidthStretch, -1, 0);
                ImGui.TableSetupColumn("Type", ImGuiTableColumnFlags.WidthFixed | (hideType ? ImGuiTableColumnFlags.DefaultHide : ImGuiTableColumnFlags.None), -1, 1);
                ImGui.TableSetupColumn("Size", ImGuiTableColumnFlags.WidthFixed | (hideSize ? ImGuiTableColumnFlags.DefaultHide : ImGuiTableColumnFlags.None), -1, 2);
                ImGui.TableSetupColumn("Date", ImGuiTableColumnFlags.WidthFixed | (hideDate ? ImGuiTableColumnFlags.DefaultHide : ImGuiTableColumnFlags.None), -1, 3);

                ImGui.TableNextRow(ImGuiTableRowFlags.Headers);
                for (var column = 0; column < 4; column++)
                {
                    ImGui.TableSetColumnIndex(column);
                    var columnName = ImGui.TableGetColumnName(column);
                    ImGui.PushID(column);
                    ImGui.TableHeader(columnName);
                    ImGui.PopID();
                    if (ImGui.IsItemClicked())
                    {
                        if (column == 0)
                        {
                            this.SortFields(SortingField.FileName, true);
                        }
                        else if (column == 1)
                        {
                            this.SortFields(SortingField.Type, true);
                        }
                        else if (column == 2)
                        {
                            this.SortFields(SortingField.Size, true);
                        }
                        else
                        {
                            this.SortFields(SortingField.Date, true);
                        }
                    }
                }

                if (this.filteredFiles.Count > 0)
                {
                    ImGuiListClipperPtr clipper;
                    unsafe
                    {
                        clipper = new ImGuiListClipperPtr(ImGuiNative.ImGuiListClipper_ImGuiListClipper());
                    }

                    lock (this.filesLock)
                    {
                        clipper.Begin(this.filteredFiles.Count);
                        while (clipper.Step())
                        {
                            for (var i = clipper.DisplayStart; i < clipper.DisplayEnd; i++)
                            {
                                if (i < 0) continue;

                                var file = this.filteredFiles[i];
                                var selected = this.selectedFileNames.Contains(file.FileName);
                                var needToBreak = false;

                                var dir = file.Type == FileStructType.Directory;
                                var item = !dir ? GetIcon(file.Ext) : new IconColorItem
                                {
                                    Color = dirTextColor,
                                    Icon = (char)FontAwesomeIcon.Folder,
                                };

                                ImGui.PushStyleColor(ImGuiCol.Text, item.Color);
                                if (selected) ImGui.PushStyleColor(ImGuiCol.Text, selectedTextColor);

                                ImGui.TableNextRow();

                                if (ImGui.TableNextColumn())
                                {
                                    needToBreak = this.SelectableItem(file, selected, item.Icon);
                                }

                                if (ImGui.TableNextColumn())
                                {
                                    ImGui.Text(file.Ext);
                                }

                                if (ImGui.TableNextColumn())
                                {
                                    if (file.Type == FileStructType.File)
                                    {
                                        ImGui.Text(file.FormattedFileSize + " ");
                                    }
                                    else
                                    {
                                        ImGui.Text(" ");
                                    }
                                }

                                if (ImGui.TableNextColumn())
                                {
                                    var sz = ImGui.CalcTextSize(file.FileModifiedDate);
                                    ImGui.PushItemWidth(sz.X + 5);
                                    ImGui.Text(file.FileModifiedDate + " ");
                                    ImGui.PopItemWidth();
                                }

                                if (selected) ImGui.PopStyleColor();
                                ImGui.PopStyleColor();

                                if (needToBreak) break;
                            }
                        }

                        clipper.End();
                    }
                }

                if (this.pathInputActivated)
                {
                    if (ImGui.IsKeyReleased(ImGui.GetKeyIndex(ImGuiKey.Enter)))
                    {
                        if (Directory.Exists(this.pathInputBuffer)) this.SetPath(this.pathInputBuffer);
                        this.pathInputActivated = false;
                    }

                    if (ImGui.IsKeyReleased(ImGui.GetKeyIndex(ImGuiKey.Escape)))
                    {
                        this.pathInputActivated = false;
                    }
                }

                ImGui.EndTable();
            }

            if (this.pathClicked)
            {
                this.SetPath(this.currentPath);
            }

            ImGui.EndChild();
        }

        private bool SelectableItem(FileStruct file, bool selected, char icon)
        {
            var flags = ImGuiSelectableFlags.AllowDoubleClick | ImGuiSelectableFlags.SpanAllColumns;

            ImGui.PushFont(UiBuilder.IconFont);

            ImGui.Text($"{icon}");
            ImGui.PopFont();

            ImGui.SameLine(25f);

            if (ImGui.Selectable(file.FileName, selected, flags))
            {
                if (file.Type == FileStructType.Directory)
                {
                    if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                    {
                        this.pathClicked = this.SelectDirectory(file);
                        return true;
                    }
                    else if (this.IsDirectoryMode())
                    {
                        this.SelectFileName(file);
                    }
                }
                else
                {
                    this.SelectFileName(file);
                    if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                    {
                        this.wantsToQuit = true;
                        this.isOk = true;
                    }
                }
            }

            return false;
        }

        private bool SelectDirectory(FileStruct file)
        {
            var pathClick = false;

            if (file.FileName == "..")
            {
                if (this.pathDecomposition.Count > 1)
                {
                    this.currentPath = ComposeNewPath(this.pathDecomposition.GetRange(0, this.pathDecomposition.Count - 1));
                    pathClick = true;
                }
            }
            else
            {
                var newPath = Path.Combine(this.currentPath, file.FileName);

                if (Directory.Exists(newPath))
                {
                    this.currentPath = newPath;
                }

                pathClick = true;
            }

            return pathClick;
        }

        private void SelectFileName(FileStruct file)
        {
            if (ImGui.GetIO().KeyCtrl)
            {
                if (this.selectionCountMax == 0)
                { // infinite select
                    if (!this.selectedFileNames.Contains(file.FileName))
                    {
                        this.AddFileNameInSelection(file.FileName, true);
                    }
                    else
                    {
                        this.RemoveFileNameInSelection(file.FileName);
                    }
                }
                else
                {
                    if (this.selectedFileNames.Count < this.selectionCountMax)
                    {
                        if (!this.selectedFileNames.Contains(file.FileName))
                        {
                            this.AddFileNameInSelection(file.FileName, true);
                        }
                        else
                        {
                            this.RemoveFileNameInSelection(file.FileName);
                        }
                    }
                }
            }
            else if (ImGui.GetIO().KeyShift)
            {
                if (this.selectionCountMax != 1)
                { // can select a block
                    this.selectedFileNames.Clear();

                    var startMultiSelection = false;
                    var fileNameToSelect = file.FileName;
                    var savedLastSelectedFileName = string.Empty;

                    foreach (var f in this.filteredFiles)
                    {
                        // select top-to-bottom
                        if (f.FileName == this.lastSelectedFileName)
                        { // start (the previously selected one)
                            startMultiSelection = true;
                            this.AddFileNameInSelection(this.lastSelectedFileName, false);
                        }
                        else if (startMultiSelection)
                        {
                            if (this.selectionCountMax == 0)
                            {
                                this.AddFileNameInSelection(f.FileName, false);
                            }
                            else
                            {
                                if (this.selectedFileNames.Count < this.selectionCountMax)
                                {
                                    this.AddFileNameInSelection(f.FileName, false);
                                }
                                else
                                {
                                    startMultiSelection = false;
                                    if (!string.IsNullOrEmpty(savedLastSelectedFileName))
                                    {
                                        this.lastSelectedFileName = savedLastSelectedFileName;
                                    }

                                    break;
                                }
                            }
                        }

                        // select bottom-to-top
                        if (f.FileName == fileNameToSelect)
                        {
                            if (!startMultiSelection)
                            {
                                savedLastSelectedFileName = this.lastSelectedFileName;
                                this.lastSelectedFileName = fileNameToSelect;
                                fileNameToSelect = savedLastSelectedFileName;
                                startMultiSelection = true;
                                this.AddFileNameInSelection(this.lastSelectedFileName, false);
                            }
                            else
                            {
                                startMultiSelection = false;
                                if (!string.IsNullOrEmpty(savedLastSelectedFileName))
                                {
                                    this.lastSelectedFileName = savedLastSelectedFileName;
                                }

                                break;
                            }
                        }
                    }
                }
            }
            else
            {
                this.selectedFileNames.Clear();
                this.fileNameBuffer = string.Empty;
                this.AddFileNameInSelection(file.FileName, true);
            }
        }

        private void AddFileNameInSelection(string name, bool setLastSelection)
        {
            this.selectedFileNames.Add(name);
            if (this.selectedFileNames.Count == 1)
            {
                this.fileNameBuffer = name;
            }
            else
            {
                this.fileNameBuffer = $"{this.selectedFileNames.Count} files Selected";
            }

            if (setLastSelection)
            {
                this.lastSelectedFileName = name;
            }
        }

        private void RemoveFileNameInSelection(string name)
        {
            this.selectedFileNames.Remove(name);
            if (this.selectedFileNames.Count == 1)
            {
                this.fileNameBuffer = name;
            }
            else
            {
                this.fileNameBuffer = $"{this.selectedFileNames.Count} files Selected";
            }
        }

        private bool DrawFooter()
        {
            var posY = ImGui.GetCursorPosY();

            if (this.IsDirectoryMode())
            {
                ImGui.Text("Directory Path :");
            }
            else
            {
                ImGui.Text("File Name :");
            }

            ImGui.SameLine();

            var width = ImGui.GetContentRegionAvail().X - 100;
            if (this.filters.Count > 0)
            {
                width -= 150f;
            }

            var selectOnly = this.flags.HasFlag(ImGuiFileDialogFlags.SelectOnly);

            ImGui.PushItemWidth(width);
            if (selectOnly) ImGui.PushStyleVar(ImGuiStyleVar.Alpha, 0.5f);
            ImGui.InputText("##FileName", ref this.fileNameBuffer, 255, selectOnly ? ImGuiInputTextFlags.ReadOnly : ImGuiInputTextFlags.None);
            if (selectOnly) ImGui.PopStyleVar();
            ImGui.PopItemWidth();

            if (this.filters.Count > 0)
            {
                ImGui.SameLine();
                var needToApplyNewFilter = false;

                ImGui.PushItemWidth(150f);
                if (ImGui.BeginCombo("##Filters", this.selectedFilter.Filter, ImGuiComboFlags.None))
                {
                    var idx = 0;
                    foreach (var filter in this.filters)
                    {
                        var selected = filter.Filter == this.selectedFilter.Filter;
                        ImGui.PushID(idx++);
                        if (ImGui.Selectable(filter.Filter, selected))
                        {
                            this.selectedFilter = filter;
                            needToApplyNewFilter = true;
                        }

                        ImGui.PopID();
                    }

                    ImGui.EndCombo();
                }

                ImGui.PopItemWidth();

                if (needToApplyNewFilter)
                {
                    this.SetPath(this.currentPath);
                }
            }

            var res = false;

            ImGui.SameLine();

            var disableOk = string.IsNullOrEmpty(this.fileNameBuffer) || (selectOnly && !this.IsItemSelected());
            if (disableOk) ImGui.PushStyleVar(ImGuiStyleVar.Alpha, 0.5f);

            if (ImGui.Button("Ok") && !disableOk)
            {
                this.isOk = true;
                res = true;
            }

            if (disableOk) ImGui.PopStyleVar();

            ImGui.SameLine();

            if (ImGui.Button("Cancel"))
            {
                this.isOk = false;
                res = true;
            }

            this.footerHeight = ImGui.GetCursorPosY() - posY;

            if (this.wantsToQuit && this.isOk)
            {
                res = true;
            }

            return res;
        }

        private bool IsItemSelected()
        {
            if (this.selectedFileNames.Count > 0) return true;
            if (this.IsDirectoryMode()) return true; // current directory
            return false;
        }

        private bool ConfirmOrOpenOverWriteFileDialogIfNeeded(bool lastAction)
        {
            if (this.IsDirectoryMode()) return lastAction;
            if (!this.isOk && lastAction) return true; // no need to confirm anything, since it was cancelled

            var confirmOverwrite = this.flags.HasFlag(ImGuiFileDialogFlags.ConfirmOverwrite);

            if (this.isOk && lastAction && !confirmOverwrite) return true;

            if (this.okResultToConfirm || (this.isOk && lastAction && confirmOverwrite))
            { // if waiting on a confirmation, or need to start one
                if (this.isOk)
                {
                    if (!File.Exists(this.GetFilePathName()))
                    { // quit dialog, it doesn't exist anyway
                        return true;
                    }
                    else
                    { // already exists, open dialog to confirm overwrite
                        this.isOk = false;
                        this.okResultToConfirm = true;
                    }
                }

                var name = $"The file Already Exists !##{this.title}{this.id}OverWriteDialog";
                var res = false;
                var open = true;

                ImGui.OpenPopup(name);
                if (ImGui.BeginPopupModal(name, ref open, ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove))
                {
                    ImGui.Text("Would you like to Overwrite it ?");
                    if (ImGui.Button("Confirm"))
                    {
                        this.okResultToConfirm = false;
                        this.isOk = true;
                        res = true;
                        ImGui.CloseCurrentPopup();
                    }

                    ImGui.SameLine();
                    if (ImGui.Button("Cancel"))
                    {
                        this.okResultToConfirm = false;
                        this.isOk = false;
                        res = false;
                        ImGui.CloseCurrentPopup();
                    }

                    ImGui.EndPopup();
                }

                return res;
            }

            return false;
        }
    }
}
