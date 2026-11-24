# Recomp Hub

A Windows desktop app that tracks GTA 6 recompilation news and estimates PC compatibility using local hardware detection.

The compatibility tool works offline. It can detect your CPU, GPU, RAM and drives after you grant permission. Hardware details stay on your PC. News and release status refresh from [gta6recomp.com](https://gta6recomp.com/) at startup and every five minutes. The local hardware catalog and project snapshot use data from the same site.

## Build on Windows

The build uses the C# compiler included with the 64-bit edition of .NET Framework 4.x. Run this from the project directory:

```powershell
powershell.exe -NoProfile -File .\build.ps1
```

The executable is created at `dist\Recomp-Hub.exe`. It includes the catalog, icon and other app resources.

## Publish on GitHub

1. Create a public repository named `recomp-hub` and choose an open-source license for your code. This source package leaves the license choice to the project owner.
2. In the **Code** tab, use **Add file → Upload files** to upload the contents of the extracted source folder. Preserve the `src` and `resources` directories and include `.gitignore`.
3. Go to **Releases → Draft a new release**, create a tag such as `v0.1.2`, and attach the executable before publishing.

[Create a repository](https://docs.github.com/en/repositories/creating-and-managing-repositories/creating-a-new-repository) · [License a repository](https://docs.github.com/en/repositories/managing-your-repositorys-settings-and-features/customizing-your-repository/licensing-a-repository) · [Publish a release](https://docs.github.com/en/repositories/releasing-projects-on-github/managing-releases-in-a-repository)
