# Recomp Hub

A Windows desktop app that tracks GTA 6 recompilation news and estimates PC compatibility using local hardware detection.

The compatibility tool works offline. With your permission, it detects your CPU, GPU, RAM, and drives. Your hardware details stay on your PC.

News and release status refresh from [gta6recomp.com](https://gta6recomp.com/) at startup and every five minutes. The local hardware catalog and project snapshot also use data from the site.

## Download

Open **Releases**, select **Recomp Hub v0.1.2**, and download `Recomp-Hub-v0.1.2.exe` from **Assets**. Run the executable on Windows.

## Build on Windows

The build uses the C# compiler included with the 64-bit edition of .NET Framework 4.x. Run this from the project directory:

```powershell
powershell.exe -NoProfile -File .\build.ps1
```

The executable is created at `dist\Recomp-Hub.exe`. It includes the hardware catalog, icon, and other app resources.

## Credits

Thanks to the [GTA6 Recomp team](https://gta6recomp.com/) for maintaining the site and sharing project updates.
