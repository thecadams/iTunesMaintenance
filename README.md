iTunesMaintenance
=================

iTunes maintenance utility. Versions available for Windows (iTunes COM) and Mac (MacRuby/ScriptingBridge).

**Please note:** This tool is public domain; feel free to use it, modify it, redistribute it and learn from it. Just don't pirate music - you filthy music pirate you. Go get Spotify, it's free!

## Overview

I created this utility to keep my iTunes library in check. I wanted to:

- Make sure every music file on my computer was in iTunes
- Automatically add any music on my computer to iTunes
- Use the folder structure I'd built up over years when I was in iTunes, but without overwriting any metadata
- Keep every music file on the computer in a few controlled locations (see below)
- Delete crap music from my library AND hard drive in one step.

I was doing all this manually and decided to find a better way. This tool replaces hours of tedium with a few seconds of watching a command prompt!

## Library Structure

Obviously I designed this utility for MY iTunes library, not yours. My iTunes library is structured as follows:

    ~/Music on Mac, or My Music on Windows
     |
     \- iTunes (the iTunes library)
     |
     \- Singles
     |   |
     |   \- [Artist] - Track Name.mp3
     |   |
     |   \- [Artist] - Another Track.mp3
     |   |
     |   \- ...
     |
     \- Albums
        |
        \- [Artist] - Album Name
        |   |
        |   \- track01.mp3
        |   |
        |   \- track02.mp3
        |   |
        |   \- ...
        |
        \- [Artist] - Album Name ...
            |
            \- ...

A few extra notes:

- In the singles and albums folders, only a few file extensions are allowed - for example, .m3u is banned. (The list of allowed extensions is below)
- Files only in the singles folder. No subfolders.
- No files allowed in the root albums folder.
- No subfolders allowed in any album folder.
- For albums with multiple discs, create multiple album folders: `[Artist] - Album Name Disc 1`, `[Artist] - Album Name Disc 2`, `[Artist] - Album Name Disc 3`…


## Functionality

1. Enforce library rules:
    a. No files which can't be located by iTunes (moved files etc.)
    b. No files outside the Singles, Albums, or iTunes folders (see above for structure)
    c. No files in the Singles folder with extensions other than `["mp3", "m4a", "jpg", "jpeg", "gif", "bmp", "png", "txt", "pdf"]`
    d. No subfolders in the Singles folder
    e. Step c and d above, but applied to each Albums folder
    f. No files in the root Albums folder
    g. No album names which do not start with `[` (due to `[Artist] - Album Name` convention)
2. Delete crap tracks: a crap track is any song marked with 1 star. If you hate a song while you're out and about, mark it as 1 star. Next sync it will automatically get deleted:
    a. Remove from iTunes library
    b. Move to Trash/Recycle Bin
3. Add missing tracks: look in Singles folder and in all Album folders
4. Make a playlist for each album folder. This is my favourite feature to demo; with folders as playlists, you can take advantage of your folder structure when you're using your iPod.
5. Make a playlist for the Singles folder. Again, this lets you take advantage of your folder structure even on your iPod.
6. Update any and all iPods that are currently connected.

**Note:** Folder playlists get sorted by track number (from the metadata) but because often the metadata is missing, it'll fall back to sorting by the filename. So if you've got a folder of MP3s named `01_track01.mp3, 01_track02.mp3...` you still get the nice folder ordering.

**Note 2:** Because step 6 above will update any connected iPods, you can also hook this script up with an automator action on Mac OS X.

## Installation

**WARNING! THIS CODE HAS SHARP EDGES! DO NOT RUN THIS CODE WITHOUT FIRST UNDERSTANDING IT!**

There's nothing to install, just download it, modify all the locations as required (or massage your library into my structure), and run. You might want to comment out stuff you're not interested in using - for example the library rules.

## Suggested improvements

- Better iTunes integration:
    - Modify the script to show dialogs when run.
    - Embed the .rb script inside an AppleScript and put it in `~/Library/iTunes/Scripts`.
    - Show the AppleScript menu in iTunes, so the script becomes selectable from the menu.
- Optimise playlist deletion and recreation. Don't re-create a playlist which didn't change.
    - Originally I wanted to do this, but unless I've missed something the Windows iTunes COM library is limited when it comes to playlist manipulation - you can add tracks and delete entire playlists. So I implemented it that way and didn't really think about it during the MacRuby port. If you're finding it slow, minimise the iTunes window. It'll get a lot faster if iTunes doesn't have to repaint as it goes along.

## Footnote: the state of OS X scripting

**Note: This is just a whinge about OS X scripting, nothing to do with the tool.**

The Windows version of this tool, written first, took about 5 hours from start to finish. Porting it to Mac OS X took about 50 hours:

- First I wrote it in AppleScript. I got as far as making folder playlists and couldn't work out the right magic incarnation to make it not take copies of every array involved in the process.
- Then I found rb-appscript and started to port to that; soon after, I came across this gem: <http://appscript.sourceforge.net/status.html>
- Last was MacRuby, but simple things required learning everything about ScriptingBridge. Worst by far, but a typical example, was trying to create a playlist and add just one song to it: when created, the playlist object is just a proxy. Any function call on the playlist will cause an error. The playlist needs to be put into iTunes' playlist collection before its methods can be used. That was about 20 hours I'll never get back.

Apple have given us an awful mish-mash of automation choices:

- Automator: not up to the task of replacing a several-hundred-line C# program. It might've worked but it would've been one hell of a big automator action!
- AppleScript: unpredictable pass-by-value/pass-by-ref semantics meant the same code that ran in 2 seconds in .NET would hang my brand new Retina MacBook Pro when rewritten in AppleScript. Ugly nonsensical array-append syntax with no way to predict whether the entire array was being copied and then whether shallow or deep. No array map/select/reduce functions. I very quickly gave up; any language which doesn't do this stuff is not worth anyone's time in 2012.
- rb-appscript: Awesome! Found something that'll work nicely! Let's go learn Ruby-- oh, Apple obsoleted the API it was based on so it might work but might stop working soon. Thanks Apple. <http://appscript.sourceforge.net/status.html>
- Objective-C - either you need a bridge across to AppleScript land or send raw events. Haven't tried that one, it sounds like a lot of copying and pasting raw numbers into `#define`s.
- Some hybrid of `osascript` plus bash or ruby - I never tried, because the port would've involved starting and stopping AppleScript's runtime plus iTunes several thousand times per invocation.
- MacRuby and ScriptingBridge - MacRuby rocks! So good to be in Cocoa land, all classes at your fingertips, and Ruby - sublime. But ScriptingBridge has some weird limitations I spelled out above. Array objects such as playlists need to be added to their parent before you can operate on them - so much for avoiding UI flicker. Alloc/init are designed to model Objective-C semantics but don't actually reflect what goes on underneath at all; you generally get a useless proxy object. And apparently Apple events aren't strictly OO, so ScriptingBridge can't do everything some applications offer. Sometimes you have to twist your mental model to find the right API call that won't explode at runtime - for example, a Track might need to be told to add itself to a playlist.

Let's compare that to the Windows equivalent:

- iTunes COM API. COM works everywhere. Simply reference iTunes.exe in a C# project and IntelliSense works instantly. The API is missing stuff (such as ability to remove files from a playlist) but everything was achievable in about 5 hours of learning and coding with minimal Googling thanks to IntelliSense. Cheers Microsoft, shame on you Apple.

Apple: awesome automation infrastructure with Automator etc., but terrible platform choices. Please make ScriptingBridge better, it's almost where it needs to be, and thank the Gods that be for MacRuby, it's a match made in heaven!

Anyway we got there in the end on the Mac. I hope you get some use out of this tool - I use it every day and it makes me happy.