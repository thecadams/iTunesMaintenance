#!/usr/local/bin/macruby
framework 'Cocoa'
framework 'ScriptingBridge'

load_bridge_support_file 'iTunes.bridgesupport'
$iTunes = SBApplication.applicationWithBundleIdentifier 'com.apple.iTunes'

class NSURL
	def getParentDir
		parentDir = Pointer.new "@"
		if not self.isFileURL or
			not self.getResourceValue parentDir,
				forKey:NSURLParentDirectoryURLKey, error:nil then
				puts "Problem getting parent directory of #{t.location}"
			exit 1
		end
		return parentDir.value.path
	end
end

class NSString
	def getParentDir
		return File.expand_path("..",self)
	end
	def fileExtension
		return self[/.[^.]+$/][1..-1]
	end
end

SinglesLocation = "~/Music/Singles".stringByExpandingTildeInPath
AlbumsLocation = "~/Music/Albums".stringByExpandingTildeInPath
iTunesLocation = "~/Music/iTunes".stringByExpandingTildeInPath
IgnoredLibraryExtensions = ["epub","pdf"]
AllowedFileExtensions = ["mp3", "m4a", "jpg", "jpeg", "gif", "bmp", "png", "txt", "pdf"]
MusicFileExtensions = ["mp3", "m4a", "aac"]
IgnoredFilenames = ["thumbs.db", "desktop.ini", ".ds_store"]
AlbumNameStartCharacter = "["

$allTracks = $iTunes.sources.objectWithName("Library").userPlaylists.objectWithName("Music").fileTracks
$tracksByLocation = Hash[$allTracks.collect {|t| [t.location.path, t]}]
$tracksByFolder = {}
$allTracks.each do |t| ($tracksByFolder[t.location.getParentDir] ||= []) << t end

# Enforce Library Rules.
# Don't allow files in library that can't be found
# Don't allow files in library outside the singles and albums location
rulesBroken = false
$allTracks.each do |t|
	if t.location == nil or not t.location.isFileURL then
		rulesBroken = true
		puts "Track whose location cannot be found: " + t.name + " by " + t.artist
		next
	end
	parentDir = t.location.getParentDir
	grandparentDir = parentDir == nil ? nil : parentDir.getParentDir
	extension = t.location.pathExtension.downcase

	if IgnoredLibraryExtensions.include? extension then next end
	if not parentDir == SinglesLocation and
		not grandparentDir == AlbumsLocation and
		not t.location.path.start_with? iTunesLocation then
		rulesBroken = true
		puts "Track in wrong path: " + t.location.path
	end
end

# Enforce Filesystem Rules.
# No files with banned extensions and no subfolders
# in either the singles folder or any album folder.
# Album folder must not contain any files.
# All folders in album folder must begin with "[".
def checkNoBannedExtensions(path)
	Dir.new(path).select do |i|
		f = File.join(path, i)
		not File.directory?(f) and
			not IgnoredFilenames.include? i.downcase and
			not AllowedFileExtensions.include? i.fileExtension.downcase
	end.each do |i|
		puts "File with banned extension: " + File.join(path, i)
		rulesBroken = true
	end
end

def checkNoSubfolders(path)
	files = Dir.new(path).select {|i| File.directory?(File.join(path, i)) and not i=="." and not i==".."}.each do |i|
		puts "Banned directory found: " + File.join(path, i)
		rulesBroken = true
	end
end

checkNoBannedExtensions SinglesLocation
checkNoSubfolders SinglesLocation
Dir.new(AlbumsLocation).select {|i| not i=="." and not i==".."}.each do |i|
	f = File.join(AlbumsLocation, i)
	if not File.directory?(f) then
		if IgnoredFilenames.include? i.downcase then next end
		puts "Banned file in the albums folder: " + f
		rulesBroken = true
	elsif not i.start_with? AlbumNameStartCharacter then
		puts "Album does not start with #{AlbumNameStartCharacter}: " + f
		rulesBroken = true
	else
		checkNoBannedExtensions f
		checkNoSubfolders f
	end
end

if rulesBroken == true then exit 1 end

# Delete Crap Tracks
# Put 1-star rated tracks in the Trash
$allTracks.select {|t| t.rating == 20}.each do |t|
	puts "Deleting crap track: #{t.location.path}"
	$allTracks.delete t
	$tracksByLocation.delete t.location
	$tracksByFolder[t.location.getParentDir].delete t
	loc = t.location
	t.delete
	loc.trashItemAtURL Pointer.new('@'), outResultingURL:nil, error:nil
end

# Add Missing Tracks
def addMissingTracks(path)
	missingTracks = Dir.new(path).select do |i|
		f = File.join(path, i)
		not File.directory?(f) and
		MusicFileExtensions.include? f.fileExtension.downcase and
		not $tracksByLocation.keys.include? f
	end.collect do |i|
		f = File.join(path, i)
		puts "Adding track: #{f}"
		NSURL.fileURLWithPath f
	end
	$iTunes.add missingTracks, to:nil
end

addMissingTracks(SinglesLocation)
Dir.new(AlbumsLocation).select {|i| not i=="." and not i==".." and File.directory?(File.join(AlbumsLocation, i))}
	.each {|d| addMissingTracks(File.join(AlbumsLocation, d))}

# Make Folder Playlists.
# One for each album, plus a bonus one for Singles
def makeFolderPlaylist(path, name)
	puts "Making playlist: #{name} for path: #{path}"
	playlist = ITunesUserPlaylist.alloc.initWithProperties ({"name"=>name})
	$iTunes.sources.objectWithName("Library").playlists << playlist
	$tracksByFolder[path]
		.sort_by {|t| [t.trackNumber, t.location.path]}
		.each {|t| t.duplicateTo(playlist)}
end

# Delete previous folder playlists
def playlistsToDelete
	$iTunes.sources.objectWithName("Library").playlists.each do |p|
		if p.name.start_with? AlbumNameStartCharacter or p.name == "Singles" then
			return true
		end
	end
	return false
end

while playlistsToDelete do
	$iTunes.sources.objectWithName("Library").playlists.select do |p|
		p.name.start_with? AlbumNameStartCharacter or p.name == "Singles"
	end.each do |p|
		#puts "Deleting playlist: #{p.name}"
		p.delete
	end
end
Dir.new(AlbumsLocation).select {|i| not i=="." and not i==".." and File.directory?(File.join(AlbumsLocation, i))}
	.each {|i| makeFolderPlaylist File.join(AlbumsLocation, i), i}
makeFolderPlaylist SinglesLocation, "Singles"

# Sync Connected iPods.
# The value below is defined in iTunes.h as iTunesESrcIPod
$iTunes.sources.select {|s| s.kind == 1800433508}.each do |s|
	puts "Updating #{s.name}"
	s.update
end