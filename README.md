Exports source tree and removes all crappy files such as binary files and source control (bin, obj, dll, exe, tfs binding, svn, git, mercurial, Visual Studio files, Resharper, etc).
Useful to deliver sources of your projects.

Notables points of interest are:
- Possibility to filter files and folders that will be copied
- Can remove Tfs (Team Foundation Server) binding
- Can exclude generated files
- Can validate copied files with Md5 sum
- Can keep symbolic links
- Can replace files added as link in Visual Studio by "local" copy of files.
- Save your configuration in a simple XML file in order to reuse it later
- Search & Replace text (file name, folder name, file content)
- Remove some project from sln files

2 Ways of using it
- Graphical interface
- Console Application
