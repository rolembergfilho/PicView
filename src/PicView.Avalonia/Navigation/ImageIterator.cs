﻿using PicView.Avalonia.Models;
using PicView.Core.Config;
using PicView.Core.FileHandling;
using PicView.Core.Navigation;
using System.Diagnostics;
using PicView.Avalonia.Services;

namespace PicView.Avalonia.Navigation
{
    public class ImageIterator
    {
        public event FileSystemEventHandler? FileAdded;

        public event EventHandler<bool>? FileDeleted;

        public event FileSystemEventHandler? FileRenamed;

        public List<string> Pics { get; set; }

        public bool IsFileBeingRenamed { get; set; }

        public int Index;
        public FileInfo FileInfo;
        public bool Reverse;
        public PreLoader PreLoader { get; } = new();

        private static FileSystemWatcher? _watcher;
        private static bool _running;
        private readonly IPlatformSpecificService? _platformService;

        public ImageIterator(FileInfo fileInfo, IPlatformSpecificService? platformService)
        {
            ArgumentNullException.ThrowIfNull(fileInfo);

            FileInfo = fileInfo;

            _platformService = platformService;
            Pics = _platformService.GetFiles(fileInfo);
            Index = Pics.IndexOf(fileInfo.FullName);
#if DEBUG
            Debug.Assert(fileInfo.DirectoryName != null, "fileInfo.DirectoryName != null");
#endif
            _watcher ??= new FileSystemWatcher();
            _watcher.Path = fileInfo.DirectoryName;
            _watcher.EnableRaisingEvents = true;
            _watcher.Filter = "*.*";
            _watcher.IncludeSubdirectories = SettingsHelper.Settings.Sorting.IncludeSubDirectories;
            _watcher.Created += async (_, e) => await OnFileAdded(e).ConfigureAwait(false);
            _watcher.Deleted += (_, e) => OnFileDeleted(e);
            _watcher.Renamed += (_, e) => OnFileRenamed(e);
        }

        public async Task Preload(ImageService imageService)
        {
            await PreLoader.PreLoadAsync(Index, Pics.Count, Reverse, imageService, Pics).ConfigureAwait(false);
        }

        public async Task AddAsync(int index, ImageService imageService, ImageModel imageModel)
        {
            await PreLoader.AddAsync(index, imageService, Pics, imageModel).ConfigureAwait(false);
        }

        public int GetIteration(int index, NavigateTo navigateTo)
        {
            int next;
            switch (navigateTo)
            {
                case NavigateTo.Next:
                case NavigateTo.Previous:
                    var indexChange = navigateTo == NavigateTo.Next ? 1 : -1;
                    Reverse = navigateTo == NavigateTo.Previous;
                    if (SettingsHelper.Settings.UIProperties.Looping)
                    {
                        next = (index + indexChange + Pics.Count) % Pics.Count;
                    }
                    else
                    {
                        var newIndex = index + indexChange;
                        if (newIndex < 0)
                        {
                            return 0;
                        }
                        if (newIndex >= Pics.Count)
                        {
                            return Pics.Count - 1;
                        }
                        next = newIndex;
                    }

                    break;

                case NavigateTo.First:
                case NavigateTo.Last:
                    if (Pics.Count > PreLoader.MaxCount)
                        PreLoader.Clear();
                    next = navigateTo == NavigateTo.First ? 0 : Pics.Count - 1;
                    break;

                default: return -1;
            }
            return next;
        }

        private void OnFileRenamed(RenamedEventArgs e)
        {
            if (IsFileBeingRenamed)
            {
                return;
            }
            if (e.FullPath.IsSupported() == false)
            {
                if (Pics.Contains(e.OldFullPath))
                {
                    Pics.Remove(e.OldFullPath);
                }
                return;
            }
            if (_running) { return; }
            _running = true;

            var oldIndex = Pics.IndexOf(e.OldFullPath);
            var sameFile = Index == Pics.IndexOf(e.OldFullPath);

            var fileInfo = new FileInfo(e.FullPath);
            if (fileInfo.Exists == false) { return; }

            var newList = FileListHelper.RetrieveFiles(fileInfo).ToList();
            if (newList.Count == 0) { return; }

            if (fileInfo.Exists == false) { return; }

            Pics = newList;

            var index = Pics.IndexOf(e.FullPath);
            if (index < 0) { return; }

            if (fileInfo.Exists == false)
            {
                return;
            }

            PreLoader.Remove(index, Pics);

            _running = false;
            //FileHistoryNavigation.Rename(e.OldFullPath, e.FullPath);
            //await UpdateGalleryAsync(index, oldIndex, fileInfo, e.OldFullPath, e.FullPath, sameFile).ConfigureAwait(false);
            //await ImageInfo.UpdateValuesAsync(fileInfo).ConfigureAwait(false);
            FileRenamed?.Invoke(this, e);
        }

        private void OnFileDeleted(FileSystemEventArgs e)
        {
            if (IsFileBeingRenamed)
            {
                return;
            }
            if (e.FullPath.IsSupported() == false)
            {
                return;
            }

            if (Pics.Contains(e.FullPath) == false)
            {
                return;
            }

            if (_running) { return; }
            _running = true;
            var sameFile = Index == Pics.IndexOf(e.FullPath);
            if (!Pics.Remove(e.FullPath))
            {
                return;
            }
            Index--;

            PreLoader.Remove(Index, Pics);
            _running = false;

            //FileHistoryNavigation.Remove(e.FullPath);
            FileDeleted?.Invoke(this, sameFile);
        }

        private async Task OnFileAdded(FileSystemEventArgs e)
        {
            if (IsFileBeingRenamed)
            {
                return;
            }
            if (Pics.Contains(e.FullPath))
            {
                return;
            }
            if (e.FullPath.IsSupported() == false)
            {
                return;
            }
            var fileInfo = new FileInfo(e.FullPath);
            if (fileInfo.Exists == false) { return; }

            if (_running) { return; }
            _running = true;

            var newList = await Task.FromResult(_platformService.GetFiles(fileInfo));
            if (newList.Count == 0) { return; }
            if (newList.Count == Pics.Count) { return; }

            if (fileInfo.Exists == false) { return; }

            Pics = newList;

            _running = false;

            var index = Pics.IndexOf(e.FullPath);
            if (index < 0) { return; }

            var nextIndex = index + 1;
            if (index >= Pics.Count)
            {
                nextIndex = 0;
            }
            var prevIndex = index - 1;
            if (prevIndex < 0)
            {
                prevIndex = Pics.Count - 1;
            }
            if (PreLoader.Contains(index, Pics) || PreLoader.Contains(nextIndex, Pics) || PreLoader.Contains(prevIndex, Pics))
            {
                PreLoader.Clear();
            }
            FileAdded?.Invoke(this, e);
        }
    }
}