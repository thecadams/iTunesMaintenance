using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using iTunesLib;
using Microsoft.VisualBasic.FileIO;

namespace iTunesMaint
{
    class Program
    {
        private const string SinglesLocation = @"C:\Users\christopher.adams\Music\Singles";
        private const string AlbumsLocation = @"C:\Users\christopher.adams\Music\Albums";
        private const string ItunesLocation = @"C:\Users\christopher.adams\Music\iTunes";
        private static readonly List<string> IgnoredLibraryExtensions = new List<string> { ".epub", ".pdf" };
        private static readonly List<string> AllowedFileExtensions = new List<string> { ".mp3", ".m4a", ".jpg", ".jpeg", ".gif", ".png", ".txt"};
        private static readonly List<string> MusicFileExtensions = new List<string> {".mp3", ".m4a", ".aac"};
        private static readonly List<string> IgnoredFilenames = new List<string> {"thumbs.db", "desktop.ini"};
        private const string AlbumNameStartCharacter = "[";

        static void Main()
        {
            var app = new iTunesApp();
            var allTracks = app.LibraryPlaylist.Tracks.OfType<IITFileOrCDTrack>().ToList();
            EnforceLibraryRules(allTracks);
            EnforceFilesystemRules();
            DeleteCrapTracks(allTracks);
            AddMissingTracks(app, allTracks);
            MakeFolderPlaylists(app);
            SyncConnectedIpods(app);
        }

        /// <summary>
        /// Don't allow files in library that can't be found
        /// Don't allow files in library outside the singles and albums location
        /// </summary>
        /// <param name="allTracks"></param>
        private static void EnforceLibraryRules(IEnumerable<IITFileOrCDTrack> allTracks)
        {
            var nullLocTracks = allTracks.Where(t => t.Location == null).ToList();
            if (nullLocTracks.Count > 0)
            {
                throw new InvalidOperationException("found some broken tracks");
            }
            foreach (var track in allTracks)
            {
                var ext = Path.GetExtension(track.Location.ToLower());
                if (IgnoredLibraryExtensions.Contains(ext))
                {
                    continue;
                }
                if (!track.Location.StartsWith(SinglesLocation + Path.DirectorySeparatorChar) &&
                    !track.Location.StartsWith(AlbumsLocation + Path.DirectorySeparatorChar) &&
                    !track.Location.StartsWith(ItunesLocation + Path.DirectorySeparatorChar))
                {
                    throw new InvalidOperationException("found a track from the wrong path");
                }
                var dir = Path.GetDirectoryName(track.Location);
                if (dir == null)
                {
                    throw new InvalidOperationException("no directory");
                }
                if (dir != SinglesLocation &&
                    !dir.StartsWith(ItunesLocation + Path.DirectorySeparatorChar) &&
                    !dir.StartsWith(AlbumsLocation + Path.DirectorySeparatorChar))
                {
                    throw new InvalidOperationException("found a track in the wrong path");
                }
                if (dir.StartsWith(AlbumsLocation + Path.DirectorySeparatorChar) &&
                    dir.Remove(0, (AlbumsLocation + Path.DirectorySeparatorChar).Length).Contains(Path.DirectorySeparatorChar))
                {
                    throw new InvalidOperationException("found a track in a subfolder of an album folder");
                }
            }
        }

        /// <summary>
        /// Enforce rules on singles and album folders
        /// </summary>
        private static void EnforceFilesystemRules()
        {
            CheckNoBannedExtensions(SinglesLocation);
            CheckNoSubfolders(SinglesLocation);
            // ReSharper disable LoopCanBeConvertedToQuery
            foreach (var file in Directory.GetFiles(AlbumsLocation))
            // ReSharper restore LoopCanBeConvertedToQuery
            {
                if (IgnoredFilenames.Contains(Path.GetFileName(file.ToLower())))
                {
                    continue;
                }
                throw new InvalidOperationException("found files in the root albums folder");
            }
            foreach (var dir in Directory.GetDirectories(AlbumsLocation))
            {
                var dirName = Path.GetFileName(dir);
                if (dirName == null)
                {
                    throw new InvalidOperationException("no directory");
                }
                // Album names must start with a square bracket
                if (!dirName.StartsWith(AlbumNameStartCharacter))
                {
                    throw new InvalidOperationException("found an invalid album folder name");
                }
                CheckNoBannedExtensions(dir);
                CheckNoSubfolders(dir);
            }
        }

        /// <summary>
        /// Put 1-star rated tracks in the recycle bin
        /// </summary>
        /// <param name="allTracks"></param>
        private static void DeleteCrapTracks(ICollection<IITFileOrCDTrack> allTracks)
        {
            var tracksToDelete = new List<IITFileOrCDTrack>();
            foreach (var track in allTracks.Where(track => track.Rating == 20))
            {
                Console.WriteLine("Deleting crap track: " + track.Location);
                FileSystem.DeleteFile(track.Location, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
                track.Delete();
                tracksToDelete.Add(track);
            }
            foreach (var trackToDelete in tracksToDelete)
            {
                allTracks.Remove(trackToDelete);
            }
        }

        /// <summary>
        /// Make a playlist per album folder seen in library
        /// </summary>
        /// <param name="app"></param>
        private static void MakeFolderPlaylists(iTunesApp app)
        {
            // Delete all playlists starting with a square bracket
            foreach (var playlist in app.LibrarySource.Playlists.Cast<IITPlaylist>().ToList().Where(playlist => playlist.Name.StartsWith(AlbumNameStartCharacter) || playlist.Name == Path.GetFileName(SinglesLocation)))
            {
                playlist.Delete();
            }

            // Make new playlists for each album folder in library
            var allTracks = app.LibraryPlaylist.Tracks.OfType<IITFileOrCDTrack>().ToList(); // Include newly added tracks
            var folders = new Dictionary<string, List<IITFileOrCDTrack>>();
            foreach (var track in allTracks)
            {
                var ext = Path.GetExtension(track.Location.ToLower());
                var dir = Path.GetDirectoryName(track.Location);
                if (dir == null)
                {
                    throw new InvalidOperationException("no directory");
                }
                if (IgnoredLibraryExtensions.Contains(ext) ||
                    (
                        !dir.StartsWith(AlbumsLocation + Path.DirectorySeparatorChar) &&
                        dir != SinglesLocation
                    ))
                {
                    continue;
                }
                if (!folders.ContainsKey(dir))
                {
                    folders.Add(dir, new List<IITFileOrCDTrack>());
                }
                folders[dir].Add(track);
            }
            foreach (var folder in folders)
            {
                MakeFolderPlaylist(app, Path.GetFileName(folder.Key), folder.Value);
            }
        }

        private static void MakeFolderPlaylist(iTunesApp app, string name, IEnumerable<IITFileOrCDTrack> tracks)
        {
            var playlist = app.CreatePlaylist(name) as IITUserPlaylist;
            if (playlist == null)
            {
                throw new InvalidOperationException("no playlist found!");
            }
            foreach (var track in tracks.OrderBy(t=>t.TrackNumber).ThenBy(t=>t.Location))
            {
                var currentTrack = track as object;
                playlist.AddTrack(ref currentTrack);
            }
        }

        private static void AddMissingTracks(iTunesApp app, IEnumerable<IITFileOrCDTrack> allTracks)
        {
            var tracksByLocation = allTracks.ToDictionary(track => track.Location);
            AddMissingTracks(app, tracksByLocation, SinglesLocation);
            foreach (var dir in Directory.GetDirectories(AlbumsLocation))
            {
                AddMissingTracks(app, tracksByLocation, dir);
            }
        }

        private static void AddMissingTracks(iTunesApp app, IDictionary<string, IITFileOrCDTrack> tracksByLocation, string dir)
        {
            foreach (var file in Directory.GetFiles(dir))
            {
                if (!MusicFileExtensions.Contains(Path.GetExtension(file.ToLower())))
                {
                    continue;
                }
                if (!tracksByLocation.ContainsKey(file))
                {
                    Console.WriteLine("Adding track: " + file);
                    app.LibraryPlaylist.AddFile(file);
                }
            }
        }

        private static void CheckNoBannedExtensions(string dir)
        {
// ReSharper disable LoopCanBeConvertedToQuery
            foreach (var file in Directory.GetFiles(dir))
// ReSharper restore LoopCanBeConvertedToQuery
            {
                if (IgnoredFilenames.Contains(Path.GetFileName(file.ToLower())))
                {
                    continue;
                }
                if (!AllowedFileExtensions.Contains(Path.GetExtension(file.ToLower())))
                {
                    throw new InvalidOperationException("found a file with non-allowed extension");
                }
            }
        }

        private static void CheckNoSubfolders(string dir)
        {
            if (Directory.GetDirectories(dir).Count() > 0)
            {
                throw new InvalidOperationException("found a subfolder where it is not allowed");
            }
        }

        private static void SyncConnectedIpods(IiTunes app)
        {
            foreach (var ipodSource in app.Sources.OfType<IITIPodSource>())
            {
                ipodSource.UpdateIPod();
            }
        }
    }
}